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

using BackupManager.Entities;

namespace BackupManager;

internal sealed partial class Main
{
    private void CopyFiles(bool showCompletedMessage)
    {
        Utils.TraceIn();
        var disk = SetupBackupDisk();
        Utils.LogWithPushover(BackupAction.BackupFiles, "Started");
        UpdateStatusLabel("Copying");
        IEnumerable<BackupFile> filesToBackup = mediaBackup.GetBackupFilesWithDiskEmpty().OrderByDescending(static q => q.Length);
        var backupFiles = filesToBackup.ToArray();
        var sizeOfFiles = backupFiles.Sum(static x => x.Length);
        _ = Utils.GetDiskInfo(backupDiskTextBox.Text, out var availableSpace, out _);
        var remainingDiskSpace = availableSpace - Utils.ConvertMBtoBytes(mediaBackup.Config.BackupDiskMinimumFreeSpaceToLeave);
        var sizeOfCopy = remainingDiskSpace < sizeOfFiles ? remainingDiskSpace : sizeOfFiles;

        if (sizeOfCopy == 0)

            // This avoids any division by zero errors later
            sizeOfCopy = 1;

        // We use 100 as the max because the actual number of bytes could be far too large 
        EnableProgressBar(0, 100);
        CopyFilesLoop(backupFiles, sizeOfCopy, disk);
        UpdateMediaFilesCountDisplay();

        if (!UpdateCurrentBackupDiskInfo(disk))
        {
            Utils.LogWithPushover(BackupAction.BackupFiles, PushoverPriority.Emergency, $"Error updating info for backup disk {disk.Name}");
            return;
        }
        mediaBackup.Save();
        UpdateStatusLabel("Saved.");
        var filesStillNotOnBackupDisk = mediaBackup.GetBackupFilesWithDiskEmpty();
        var text = string.Empty;
        var stillNotOnBackupDisk = filesStillNotOnBackupDisk as BackupFile[] ?? filesStillNotOnBackupDisk.ToArray();

        if (stillNotOnBackupDisk.Any())
        {
            sizeOfFiles = stillNotOnBackupDisk.Sum(static p => p.Length);
            text = $"{stillNotOnBackupDisk.Length:n0} files still to backup at {Utils.FormatSize(sizeOfFiles)}.\n";
        }
        Utils.LogWithPushover(BackupAction.BackupFiles, text + $"{disk.FreeFormatted} free on backup disk");
        if (showCompletedMessage) Utils.LogWithPushover(BackupAction.BackupFiles, PushoverPriority.High, "Completed");
        Utils.TraceOut();
    }

    private void CopyFilesLoop(IEnumerable<BackupFile> backupFiles, long sizeOfCopy, BackupDisk disk)
    {
        var outOfDiskSpaceMessageSent = false;
        long copiedSoFar = 0;
        var fileCounter = 0;
        var files = backupFiles.ToList();
        var remainingSizeOfFilesToCopy = files.Sum(static x => x.Length);
        var totalFileCount = files.Count;
        var sizeOfFiles = files.Sum(static x => x.Length);

        // Start with a 30MB/s copy speed as a guess
        var lastCopySpeed = Utils.ConvertMBtoBytes(30);
        Utils.LogWithPushover(BackupAction.BackupFiles, $"{totalFileCount:n0} files to backup at {Utils.FormatSize(sizeOfFiles)}");

        foreach (var backupFile in files)
        {
            try
            {
                fileCounter++;
                UpdateStatusLabel("Copying", Convert.ToInt32(copiedSoFar * 100 / sizeOfCopy));
                UpdateMediaFilesCountDisplay();
                var destinationFileName = backupFile.BackupDiskFullPath(disk.BackupPath);

                // We use a temporary name for the copy first and then rename it after
                // This is in case the Backup is aborted during the copy
                // This file will be seen on the next scan and removed
                var destinationFileNameTemp = destinationFileName + ".copying";
                var sourceFileName = backupFile.FullPath;
                FileInfo sourceFileInfo = new(sourceFileName);
                var sourceFileSize = Utils.FormatSize(sourceFileInfo.Length);
                var copyTheFile = false;

                if (File.Exists(destinationFileName))
                {
                    // it could be that the source file hash changed after we read it (we read the hash, updated the master file and then copied it)
                    // in which case check the source hash again and then check the copied file 
                    // if the hash has changed we check the ModifiedTime. If its been modified at source and its newer then we delete from the backup
                    // disk and copy the new one
                    if (backupFile.CheckContentHashes(disk))
                    {
                        UpdateStatusLabel($"Skipping {Path.GetFileName(sourceFileName)}", Convert.ToInt32(copiedSoFar * 100 / sizeOfCopy));

                        Utils.LogWithPushover(BackupAction.BackupFiles,
                            $"[{fileCounter}/{totalFileCount}]\nSkipping copy of {sourceFileName} as it exists already.");
                    }
                    else
                    {
                        // check the modifiedTime and if its different copy it
                        var sourceLastWriteTime = backupFile.LastWriteTime;
                        var lastWriteTimeOfFileOnBackupDisk = Utils.GetFileLastWriteTime(destinationFileName);

                        if (sourceLastWriteTime == lastWriteTimeOfFileOnBackupDisk)
                        {
                            Utils.LogWithPushover(BackupAction.BackupFiles, PushoverPriority.High,
                                "There was an error with the hash codes on the source master folder and the backup disk.");
                        }
                        else
                        {
                            Utils.FileDelete(destinationFileName);
                            copyTheFile = true;
                        }
                    }
                }
                else
                    copyTheFile = true;

                if (copyTheFile)
                {
                    UpdateStatusLabel($"Copying {Path.GetFileName(sourceFileName)}", Convert.ToInt32(copiedSoFar * 100 / sizeOfCopy));
                    _ = Utils.GetDiskInfo(backupDiskTextBox.Text, out var availableSpace, out _);

                    if (availableSpace > Utils.ConvertMBtoBytes(mediaBackup.Config.BackupDiskMinimumFreeSpaceToLeave) + sourceFileInfo.Length)
                    {
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
                            formattedEndDateTime = ". Estimated finish by " + estimatedFinishDateTime.ToString("HH:mm");

                            // could be the following day
                            if (estimatedFinishDateTime.DayOfWeek != rightNow.DayOfWeek)
                                formattedEndDateTime = ". Estimated finish by tomorrow at " + estimatedFinishDateTime.ToString("HH:mm");
                            UpdateEstimatedFinish(estimatedFinishDateTime);
                        }

                        Utils.LogWithPushover(BackupAction.BackupFiles,
                            $"[{fileCounter}/{totalFileCount}] {Utils.FormatSize(availableSpace)} free.\nCopying {sourceFileName} at {sourceFileSize}{formattedEndDateTime}");
                        Utils.FileDelete(destinationFileNameTemp);
                        var sw = Stopwatch.StartNew();
                        _ = Utils.FileCopy(sourceFileName, destinationFileNameTemp);
                        sw.Stop();
                        var timeTaken = sw.Elapsed.TotalSeconds;

                        // We need to check this here in case Cancel was clicked during the copy of the file
                        if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();
                        Utils.FileMove(destinationFileNameTemp, destinationFileName);
                        Utils.Trace($"timeTaken {timeTaken}");
                        Utils.Trace($"sourceFileInfo.Length {sourceFileInfo.Length}");
                        lastCopySpeed = timeTaken > 0 ? Convert.ToInt64(sourceFileInfo.Length / timeTaken) : 0;
                        var copySpeed = lastCopySpeed > 0 ? Utils.FormatSpeed(lastCopySpeed) : "a very fast speed";
                        Utils.Trace($"Copy complete at {copySpeed}");

                        // Make sure its not readonly
                        Utils.ClearFileAttribute(destinationFileName, FileAttributes.ReadOnly);

                        // it could be that the source file hash changed after we read it (we read the hash, updated the master file and then copied it)
                        // in which case check the source hash again and then check the copied file 

                        if (!backupFile.CheckContentHashes(disk))

                            // There was an error with the hash codes of the source file anf the file on the backup disk
                        {
                            Utils.LogWithPushover(BackupAction.BackupFiles, PushoverPriority.High,
                                $"There was an error with the hash codes on the source and backup disk. Its likely the source file has changed since the last backup of {backupFile.FullPath} to {destinationFileName}");
                        }
                    }
                    else
                    {
                        if (!outOfDiskSpaceMessageSent)
                        {
                            Utils.LogWithPushover(BackupAction.BackupFiles,
                                $"[{fileCounter}/{totalFileCount}] {Utils.FormatSize(availableSpace)} free.\nSkipping {sourceFileName} as not enough free space");
                            outOfDiskSpaceMessageSent = true;
                        }
                    }
                }
                remainingSizeOfFilesToCopy -= backupFile.Length;
                copiedSoFar += backupFile.Length;
            }
            catch (FileNotFoundException)
            {
                Utils.LogWithPushover(BackupAction.BackupFiles, PushoverPriority.High,
                    $"{backupFile.FullPath} is not found. It's most likely been replaced since our scan.");
            }
            catch (IOException ex)
            {
                // Sometimes during a copy we get this if we lose the connection to the source NAS drive
                Utils.LogWithPushover(BackupAction.BackupFiles, PushoverPriority.Emergency, $"IOException during copy. Skipping file. Details {ex}");
            }
            UpdateCurrentBackupDiskInfo(disk);
            UpdateEstimatedFinish();
        }
    }

    private void CopyFilesAsync(bool showCompletedMessage)
    {
        longRunningActionExecutingRightNow = true;
        DisableControlsForAsyncTasks();
        CopyFiles(showCompletedMessage);
        ResetAllControls();
        longRunningActionExecutingRightNow = false;
    }
}