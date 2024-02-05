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
using BackupManager.Properties;

namespace BackupManager;

internal sealed partial class Main
{
    private void CopyFiles(bool showCompletedMessage, CancellationToken ct)
    {
        Utils.TraceIn();
        var disk = SetupBackupDisk(ct);
        UpdateStatusLabel(ct, string.Format(Resources.Copying, string.Empty));
        IEnumerable<BackupFile> filesToBackup = mediaBackup.GetBackupFilesWithDiskEmpty().OrderByDescending(static q => q.Length);
        var backupFiles = filesToBackup.ToArray();
        var sizeOfFiles = backupFiles.Sum(static x => x.Length);
        Utils.LogWithPushover(BackupAction.CopyFiles, Resources.Started, true, true);

        Utils.LogWithPushover(BackupAction.CopyFiles,
            string.Format(Resources.CopyFilesToBackup, backupFiles.Length, Utils.FormatSize(sizeOfFiles)), false, true);
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
            Utils.LogWithPushover(BackupAction.CopyFiles, PushoverPriority.Emergency,
                string.Format(Resources.ErrorUpdatingInfoForBackupDisk, disk.Name));
            return;
        }
        mediaBackup.Save(ct);
        UpdateStatusLabel(ct, Resources.Saved);
        var filesStillNotOnBackupDisk = mediaBackup.GetBackupFilesWithDiskEmpty();
        var text = string.Empty;
        var stillNotOnBackupDisk = filesStillNotOnBackupDisk as BackupFile[] ?? filesStillNotOnBackupDisk.ToArray();

        if (stillNotOnBackupDisk.Any())
        {
            text = string.Format(Resources.CopyFilesStillToCopy, stillNotOnBackupDisk.Length,
                Utils.FormatSize(stillNotOnBackupDisk.Sum(static p => p.Length)));
        }
        Utils.LogWithPushover(BackupAction.CopyFiles, text + string.Format(Resources.CopyFilesFreeOnBackupDisk, disk.FreeFormatted));
        if (showCompletedMessage) Utils.LogWithPushover(BackupAction.CopyFiles, Resources.Completed, true);
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
                FileInfo sourceFileInfo = new(sourceFileName);

                if (FileExistsInternal(sizeOfCopy, disk, backupFile, sourceFileName, copiedSoFar, counter, totalFileCount, ct))
                {
                    CopyFileInternal(sizeOfCopy, disk, sourceFileName, copiedSoFar, sourceFileInfo, ref outOfDiskSpaceMessageSent,
                        remainingSizeOfFilesToCopy, counter, totalFileCount, backupFile, ref lastCopySpeed, ct);
                }
                remainingSizeOfFilesToCopy -= backupFile.Length;
                copiedSoFar += backupFile.Length;
            }
            catch (FileNotFoundException)
            {
                Utils.LogWithPushover(BackupAction.CopyFiles, PushoverPriority.High, string.Format(Resources.FileNotFound2, backupFile.FullPath));
            }
            catch (IOException ex)
            {
                // Sometimes during a copy we get this if we lose the connection to the source NAS drive
                Utils.LogWithPushover(BackupAction.CopyFiles, PushoverPriority.Emergency, string.Format(Resources.FileIOExceptionDuringCopy, ex));
            }
            _ = UpdateCurrentBackupDiskInfo(disk);
            ClearEstimatedFinish();
        }
    }

    private bool FileExistsInternal(long sizeOfCopy, BackupDisk disk, BackupFile backupFile, string sourceFileName, long copiedSoFar,
        int fileCounter, int totalFileCount, CancellationToken ct)
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
            UpdateStatusLabel(ct, string.Format(Resources.Skipping, Path.GetFileName(sourceFileName)),
                Convert.ToInt32(copiedSoFar * 100 / sizeOfCopy));

            Utils.LogWithPushover(BackupAction.CopyFiles,
                string.Format(Resources.CopyFilesFileExists, fileCounter, totalFileCount, sourceFileName), true);
        }
        else
        {
            // check the modifiedTime and if its different copy it
            var sourceLastWriteTime = backupFile.LastWriteTime;
            var lastWriteTimeOfFileOnBackupDisk = Utils.GetFileLastWriteTime(destinationFileName);

            if (sourceLastWriteTime == lastWriteTimeOfFileOnBackupDisk)
                Utils.LogWithPushover(BackupAction.CopyFiles, PushoverPriority.High, Resources.HashCodesError2);
            else
            {
                Utils.FileDelete(destinationFileName);
                copyTheFile = true;
            }
        }
        return Utils.TraceOut(copyTheFile);
    }

    private void CopyFileInternal(long sizeOfCopy, BackupDisk disk, string sourceFileName, long copiedSoFar, FileInfo sourceFileInfo,
        ref bool outOfDiskSpaceMessageSent, long remainingSizeOfFilesToCopy, int fileCounter, int totalFileCount, BackupFile backupFile,
        ref long lastCopySpeed, CancellationToken ct)
    {
        Utils.TraceIn();
        if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();
        var destinationFileName = backupFile.BackupDiskFullPath(disk.BackupPath);
        var destinationFileNameTemp = destinationFileName + ".copying";
        _ = Utils.GetDiskInfo(backupDiskTextBox.Text, out var availableSpace, out _);

        if (availableSpace > Utils.ConvertMBtoBytes(mediaBackup.Config.BackupDiskMinimumFreeSpaceToLeave) + sourceFileInfo.Length)
        {
            UpdateMediaFilesCountDisplay();

            UpdateStatusLabel(ct, string.Format(Resources.Copying, Path.GetFileName(sourceFileName)),
                Convert.ToInt32(copiedSoFar * 100 / sizeOfCopy));
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

                formattedEndDateTime = Resources.EstimatedFinishBy + estimatedFinishDateTime.ToString(Resources.DateTime_HHmm) +
                                       $" in {Utils.FormatTimeFromSeconds(Convert.ToInt32(numberOfSecondsOfCopyRemaining))}";

                // could be the following day
                if (estimatedFinishDateTime.DayOfWeek != rightNow.DayOfWeek)
                {
                    formattedEndDateTime = Resources.EstimatedFinishByTomorrow + estimatedFinishDateTime.ToString(Resources.DateTime_HHmm) +
                                           $" in {Utils.FormatTimeFromSeconds(Convert.ToInt32(numberOfSecondsOfCopyRemaining))}";
                }
                UpdateEstimatedFinish(estimatedFinishDateTime);
            }
            var sourceFileSize = Utils.FormatSize(sourceFileInfo.Length);

            Utils.LogWithPushover(BackupAction.CopyFiles,
                string.Format(Resources.CopyFilesMainMessage, fileCounter, totalFileCount, Utils.FormatSize(availableSpace), sourceFileName,
                    sourceFileSize, formattedEndDateTime), false, true);
            Utils.FileDelete(destinationFileNameTemp);
            var sw = Stopwatch.StartNew();
            _ = Utils.FileCopy(sourceFileName, destinationFileNameTemp, ct);
            sw.Stop();
            var timeTaken = sw.Elapsed.TotalSeconds;

            // We need to check this here in case Cancel was clicked during the copy of the file
            if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();
            Utils.FileMove(destinationFileNameTemp, destinationFileName);
            Utils.Trace($"timeTaken {timeTaken}");
            Utils.Trace($"sourceFileInfo.Length {sourceFileInfo.Length}");
            lastCopySpeed = timeTaken > 0 ? Convert.ToInt64(sourceFileInfo.Length / timeTaken) : 0;
            var copySpeed = lastCopySpeed > 0 ? Utils.FormatSpeed(lastCopySpeed) : Resources.AVeryFastSpeed;
            Utils.Trace($"Copy complete at {copySpeed}");

            // Make sure it's not readonly
            Utils.ClearFileAttribute(destinationFileName, FileAttributes.ReadOnly);

            // it could be that the source file hash changed after we read it (we read the hash, updated the master file and
            // then copied it)
            // in which case check the source hash again and then check the copied file 

            if (!backupFile.CheckContentHashes(disk))

                // There was an error with the hash codes of the source file anf the file on the backup disk
                Utils.LogWithPushover(BackupAction.CopyFiles, PushoverPriority.High, string.Format(Resources.HashCodesError, backupFile.FullPath));
        }
        else
        {
            UpdateStatusLabel(ct, string.Format(Resources.Skipping, string.Empty), Convert.ToInt32(copiedSoFar * 100 / sizeOfCopy));
            if (outOfDiskSpaceMessageSent) return;

            var text = string.Format(Resources.CopyFileInternalSkipping, fileCounter, totalFileCount, Utils.FormatSize(availableSpace),
                sourceFileName);
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