// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="Main.Copying.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

using BackupManager.Entities;
using BackupManager.Extensions;
using BackupManager.Properties;

namespace BackupManager;

internal sealed partial class Main
{
    internal void CopyFiles(bool showCompletedMessage, CancellationToken ct)
    {
        Utils.TraceIn();
        var disk = SetupBackupDisk(ct);
        UpdateStatusLabel(ct, string.Format(Resources.Copying, string.Empty));
        IEnumerable<BackupFile> filesToBackup = mediaBackup.GetBackupFilesWithDiskEmpty().OrderByDescending(static q => q.Length);
        var backupFiles = filesToBackup.ToArray();
        var sizeOfFiles = backupFiles.Sum(static x => x.Length);
        Utils.LogWithPushover(BackupAction.CopyFiles, Resources.Started, true, true);
        Utils.LogWithPushover(BackupAction.CopyFiles, string.Format(Resources.CopyFilesToBackup, backupFiles.Length, sizeOfFiles.SizeSuffix()), false, true);
        _ = Utils.GetDiskInfo(backupDiskTextBox.Text, out var availableSpace, out _);
        var remainingDiskSpace = availableSpace - Utils.ConvertMBtoBytes(mediaBackup.Config.BackupDiskMinimumFreeSpaceToLeave);
        var sizeOfCopy = remainingDiskSpace < sizeOfFiles ? remainingDiskSpace : sizeOfFiles;

        // This avoids any division by zero errors later
        if (sizeOfCopy == 0) sizeOfCopy = 1;

        // We use 100 as the max because the actual number of bytes could be far too large
        EnableProgressBar(0, 100);
        CopyFilesLoop(backupFiles, sizeOfCopy, disk, ct);
        UpdateMediaFilesCountDisplay();

        if (!UpdateCurrentBackupDiskInfo(disk))
        {
            Utils.LogWithPushover(BackupAction.CopyFiles, PushoverPriority.High, string.Format(Resources.ErrorUpdatingInfoForBackupDisk, disk.Name));
            return;
        }
        mediaBackup.Save(ct);
        UpdateStatusLabel(ct, Resources.Saved);
        var filesStillNotOnBackupDisk = mediaBackup.GetBackupFilesWithDiskEmpty();
        var text = string.Empty;
        var stillNotOnBackupDisk = filesStillNotOnBackupDisk as BackupFile[] ?? filesStillNotOnBackupDisk.ToArray();
        if (stillNotOnBackupDisk.Length > 0) text = string.Format(Resources.CopyFilesStillToCopy, stillNotOnBackupDisk.Length, stillNotOnBackupDisk.Sum(static p => p.Length).SizeSuffix());
        Utils.LogWithPushover(BackupAction.CopyFiles, text + string.Format(Resources.CopyFilesFreeOnBackupDisk, disk.FreeFormatted));
        if (showCompletedMessage) Utils.LogWithPushover(BackupAction.CopyFiles, Resources.Completed, true, true);
        Utils.TraceOut();
    }

    // ReSharper disable once FunctionComplexityOverflow
    private void CopyFilesLoop(IEnumerable<BackupFile> backupFiles, long sizeOfCopy, BackupDisk disk, CancellationToken ct)
    {
        if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();
        var outOfDiskSpaceMessageSent = false;
        long copiedSoFar = 0;
        var counter = 0;
        var files = backupFiles.ToArray();
        var remainingSizeOfFilesToCopy = files.Sum(static x => x.Length);
        var totalFileCount = files.Length;

        // Start with a 30MB/s copy speed as a guess
        var lastCopySpeed = Utils.ConvertMBtoBytes(30);

        // Available disk space is only checked at the start and after successfully copying a file
        // We don't check it for each file if the space is not changing
        _ = Utils.GetDiskInfo(backupDiskTextBox.Text, out var availableSpace, out _);

        foreach (var backupFile in files)
        {
            try
            {
                if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();
                counter++;

                // We use a temporary name for the copy first and then rename it after
                // This is in case the Backup is aborted during the copy
                // This file will be seen on the next scan and removed
                var sourceFileName = backupFile.FullPath;

                if (FileExistsInternal(sizeOfCopy, disk, backupFile, sourceFileName, copiedSoFar, counter, totalFileCount, ct))
                    CopyFileInternal(sizeOfCopy, disk, sourceFileName, ref copiedSoFar, ref outOfDiskSpaceMessageSent, ref remainingSizeOfFilesToCopy, counter, totalFileCount, backupFile, ref lastCopySpeed, ref availableSpace, ct);
            }
            catch (FileNotFoundException)
            {
                Utils.LogWithPushover(BackupAction.CopyFiles, PushoverPriority.High, string.Format(Resources.FileNotFound2, backupFile.FullPath));
            }
            catch (IOException ex)
            {
                // Sometimes during a copy we get this if we lose the connection to the source NAS drive
                Utils.LogWithPushover(BackupAction.CopyFiles, PushoverPriority.High, string.Format(Resources.FileIOExceptionDuringCopy, ex));
            }
        }
        ClearEstimatedFinish();
    }

    /// <summary>
    /// </summary>
    /// <param name="sizeOfCopy"></param>
    /// <param name="disk"></param>
    /// <param name="backupFile"></param>
    /// <param name="sourceFileName"></param>
    /// <param name="copiedSoFar"></param>
    /// <param name="fileCounter"></param>
    /// <param name="totalFileCount"></param>
    /// <param name="ct"></param>
    /// <returns>True if the file needs to be copied</returns>
    private bool FileExistsInternal(long sizeOfCopy, BackupDisk disk, BackupFile backupFile, string sourceFileName, long copiedSoFar, int fileCounter, int totalFileCount, CancellationToken ct)
    {
        Utils.TraceIn();
        var destinationFileName = backupFile.BackupDiskFullPath(disk.BackupPath);
        if (!File.Exists(destinationFileName)) return Utils.TraceOut(true);

        var copyTheFile = false;

        // it could be that the source file hash changed after we read it (we read the hash, updated the master file and then copied it)
        // in which case check the source hash again and then check the copied file
        // if the hash has changed we check the ModifiedTime. If it's been modified at source and its newer then we delete from the backup
        // disk and copy the new one
        if (backupFile.CheckContentHashes(disk))
        {
            UpdateStatusLabel(ct, string.Format(Resources.Skipping, Path.GetFileName(sourceFileName)), Convert.ToInt32(copiedSoFar * 100 / sizeOfCopy));
            Utils.LogWithPushover(BackupAction.CopyFiles, string.Format(Resources.CopyFilesFileExists, fileCounter, totalFileCount, sourceFileName), true);
        }
        else
        {
            // check the modifiedTime and if its different copy it
            var sourceLastWriteTime = backupFile.LastWriteTime;
            var lastWriteTimeOfFileOnBackupDisk = Utils.File.GetLastWriteTime(destinationFileName);

            if (sourceLastWriteTime == lastWriteTimeOfFileOnBackupDisk)
                Utils.LogWithPushover(BackupAction.CopyFiles, PushoverPriority.High, string.Format(Resources.HashCodesError, sourceFileName));
            else
            {
                _ = Utils.File.Delete(destinationFileName);
                copyTheFile = true;
            }
        }
        return Utils.TraceOut(copyTheFile);
    }

    private void CopyFileInternal(long sizeOfCopy, BackupDisk disk, string sourceFileName, ref long copiedSoFar, ref bool outOfDiskSpaceMessageSent, ref long remainingSizeOfFilesToCopy, int fileCounter, int totalFileCount, BackupFile backupFile,
        ref long lastCopySpeed, ref long availableSpace, CancellationToken ct)
    {
        Utils.TraceIn();
        if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();
        var destinationFileName = backupFile.BackupDiskFullPath(disk.BackupPath);
        var destinationFileNameTemp = destinationFileName + ".c";

        if (availableSpace > Utils.ConvertMBtoBytes(mediaBackup.Config.BackupDiskMinimumFreeSpaceToLeave) + backupFile.Length)
        {
            FileInfo sourceFileInfo = new(sourceFileName);
            UpdateMediaFilesCountDisplay();
            UpdateStatusLabel(ct, string.Format(Resources.Copying, Path.GetFileName(sourceFileName)), Convert.ToInt32(copiedSoFar * 100 / sizeOfCopy));
            outOfDiskSpaceMessageSent = false;
            var formattedEndDateTime = string.Empty;

            if (lastCopySpeed > 0)
            {
                // remaining size is the smallest of remaining disk size-critical space to leave free OR
                // size of remaining files to copy
                var remainingDiskSpace = availableSpace - Utils.ConvertMBtoBytes(mediaBackup.Config.BackupDiskMinimumFreeSpaceToLeave);
                var sizeOfCopyRemaining = remainingDiskSpace < remainingSizeOfFilesToCopy ? remainingDiskSpace : remainingSizeOfFilesToCopy;
                var numberOfSecondsOfCopyRemaining = sizeOfCopyRemaining / (double)lastCopySpeed;
                var rightNow = DateTime.Now;
                var estimatedFinishDateTime = rightNow.AddSeconds(numberOfSecondsOfCopyRemaining);
                formattedEndDateTime = Resources.EstimatedFinishBy + estimatedFinishDateTime.ToString(Resources.DateTime_HHmm) + $" in {Utils.FormatTimeFromSeconds(Convert.ToInt32(numberOfSecondsOfCopyRemaining))}";

                // could be the following day
                if (estimatedFinishDateTime.DayOfWeek != rightNow.DayOfWeek)
                    formattedEndDateTime = Resources.EstimatedFinishByTomorrow + estimatedFinishDateTime.ToString(Resources.DateTime_HHmm) + $" in {Utils.FormatTimeFromSeconds(Convert.ToInt32(numberOfSecondsOfCopyRemaining))}";
                UpdateEstimatedFinish(estimatedFinishDateTime);
            }
            var sourceFileSize = sourceFileInfo.Length.SizeSuffix();
            Utils.LogWithPushover(BackupAction.CopyFiles, string.Format(Resources.CopyFilesMainMessage, fileCounter, totalFileCount, availableSpace.SizeSuffix(), sourceFileName, sourceFileSize, formattedEndDateTime), false, true);
            _ = Utils.File.Delete(destinationFileNameTemp);
            var sw = Stopwatch.StartNew();
            _ = Utils.File.Copy(sourceFileName, destinationFileNameTemp, ct);
            sw.Stop();
            var timeTaken = sw.Elapsed.TotalSeconds;

            // We need to check this here in case Cancel was clicked during the copy of the file
            if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();
            _ = Utils.File.Move(destinationFileNameTemp, destinationFileName);
            Utils.Trace($"timeTaken {timeTaken}");
            Utils.Trace($"sourceFileInfo.Length {sourceFileInfo.Length}");
            lastCopySpeed = timeTaken > 0 ? Convert.ToInt64(sourceFileInfo.Length / timeTaken) : 0;
            var copySpeed = lastCopySpeed > 0 ? Utils.FormatSpeed(lastCopySpeed) : Resources.AVeryFastSpeed;
            Utils.Trace($"Copy complete at {copySpeed}");
            remainingSizeOfFilesToCopy -= backupFile.Length;
            copiedSoFar += backupFile.Length;

            // Update the available disk space after a file is copied
            _ = Utils.GetDiskInfo(backupDiskTextBox.Text, out availableSpace, out _);

            // Make sure it's not readonly
            Utils.File.ClearFileAttribute(destinationFileName, FileAttributes.ReadOnly);

            // it could be that the source file hash changed after we read it (we read the hash, updated the master file and
            // then copied it)
            // in which case check the source hash again and then check the copied file
            if (!backupFile.CheckContentHashes(disk)) Utils.LogWithPushover(BackupAction.CopyFiles, PushoverPriority.High, string.Format(Resources.HashCodesError, backupFile.FullPath));
        }
        else
        {
            UpdateStatusLabel(ct, $"[{fileCounter}/{totalFileCount}] Skipping due to low disk space", Convert.ToInt32(copiedSoFar * 100 / sizeOfCopy));
            if (outOfDiskSpaceMessageSent) return;

            var text = string.Format(Resources.CopyFileInternalSkipping, fileCounter, totalFileCount, availableSpace.SizeSuffix(), sourceFileName);
            Utils.LogWithPushover(BackupAction.CopyFiles, text, false, true);
            outOfDiskSpaceMessageSent = true;
        }
        Utils.TraceOut();
    }

    private void CopyFilesAsync(bool showCompletedMessage, CancellationToken ct)
    {
        try
        {
            Utils.TraceIn();
            if (longRunningActionExecutingRightNow) return;

            DisableControlsForAsyncTasks(ct);
            CopyFiles(showCompletedMessage, ct);
            ResetAllControls();
        }
        finally
        {
            Utils.TraceOut();
        }
    }
}