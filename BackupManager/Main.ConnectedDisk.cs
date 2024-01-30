// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="Main.ConnectedDisk.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

using BackupManager.Entities;
using BackupManager.Extensions;
using BackupManager.Properties;

namespace BackupManager;

internal sealed partial class Main
{
    private void CheckConnectedDiskAndCopyFilesAsync(bool deleteExtraFiles, bool copyFiles, CancellationToken ct)
    {
        try
        {
            Utils.TraceIn();
            if (longRunningActionExecutingRightNow) return;

            DisableControlsForAsyncTasks(ct);
            _ = CheckConnectedDisk(deleteExtraFiles, ct);
            if (copyFiles) CopyFiles(true, ct);
            ResetAllControls();
        }
        finally
        {
            Utils.TraceOut();
        }
    }

    private void CheckConnectedDiskAndCopyFilesRepeaterAsync(bool copyFiles, CancellationToken ct)
    {
        try
        {
            Utils.TraceIn();
            if (longRunningActionExecutingRightNow) return;

            DisableControlsForAsyncTasks(ct);
            var nextDiskMessage = Resources.PleaseInsertTheNextBackupDiskNow;

            while (!ct.IsCancellationRequested)
            {
                var lastBackupDiskChecked = CheckConnectedDisk(true, ct);

                if (lastBackupDiskChecked == null)
                {
                    _ = MessageBox.Show(Resources.BackupDiskError, Resources.BackupDisk, MessageBoxButtons.OK);
                    continue;
                }
                if (copyFiles) CopyFiles(false, ct);

                Utils.LogWithPushover(BackupAction.CopyFiles, PushoverPriority.High,
                    $"Backup disk {lastBackupDiskChecked.Name} checked. Please insert the next disk now", true);
                UpdateStatusLabel(ct, nextDiskMessage);
                BackupDisk newDisk;

                do
                {
                    WaitForNewDisk(nextDiskMessage, ct);
                    newDisk = SetupBackupDisk(ct);
                } while (newDisk.Name == lastBackupDiskChecked.Name);
            }
        }
        finally
        {
            Utils.TraceOut();
        }
    }

    private void WaitForNewDisk(string message, CancellationToken ct)
    {
        Utils.TraceIn();
        UpdateStatusLabel(ct, message);
        Task.Delay(5000, ct).Wait(ct);
        Utils.TraceOut();
    }

    /// <summary>
    ///     Updates the disk info and redraws the UI
    /// </summary>
    /// <param name="disk"></param>
    /// <returns></returns>
    private bool UpdateCurrentBackupDiskInfo(BackupDisk disk)
    {
        if (disk == null) return false;

        var returnValue = disk.Update(mediaBackup.BackupFiles);
        currentBackupDiskTextBox.TextWithInvoke(disk.Name);
        backupDiskCapacityTextBox.TextWithInvoke(disk.CapacityFormatted);
        backupDiskAvailableTextBox.TextWithInvoke(disk.FreeFormatted);
        return returnValue;
    }

    /// <summary>
    ///     Returns a BackupDisk of the connected disk that's just been checked
    /// </summary>
    /// <param name="deleteExtraFiles"></param>
    /// <param name="ct"></param>
    /// <returns>null if there was an error</returns>
    [SuppressMessage("ReSharper", "MethodSupportsCancellation")]
    private BackupDisk CheckConnectedDisk(bool deleteExtraFiles, CancellationToken ct)
    {
        Utils.TraceIn();

        // Scans the connected backup disk and finds all its files
        // for each for found calculate the hash from the backup disk
        // find that hash in the backup data file
        // rebuilds the source filename from Directory+Path
        // checks the file still exists there
        // if it does compare the hash codes and update results
        // force a recalculation of both the hashes to check the files can both be read correctly
        var disk = SetupBackupDisk(ct);
        var directoryToCheck = disk.BackupPath;
        Utils.LogWithPushover(BackupAction.CheckBackupDisk, Resources.Started, false, true);
        Utils.LogWithPushover(BackupAction.CheckBackupDisk, $"Checking {directoryToCheck}");
        UpdateStatusLabel(ct, $"Checking backup disk {directoryToCheck}");
        long readSpeed = 0, writeSpeed = 0;

        if (mediaBackup.Config.SpeedTestOnOff)
        {
            var diskTestSize = disk.Free > Utils.ConvertMBtoBytes(mediaBackup.Config.SpeedTestFileSize)
                ? Utils.ConvertMBtoBytes(mediaBackup.Config.SpeedTestFileSize)
                : disk.Free - Utils.BYTES_IN_ONE_KILOBYTE;
            UpdateStatusLabel(ct, string.Format(Resources.SpeedTesting, directoryToCheck));

            Utils.DiskSpeedTest(directoryToCheck, diskTestSize, mediaBackup.Config.SpeedTestIterations, out readSpeed,
                out writeSpeed, ct);
            disk.UpdateSpeeds(readSpeed, writeSpeed);
        }

        var text = $"Name: {disk.Name}\nTotal: {disk.CapacityFormatted}\nFree: {disk.FreeFormatted}\n" +
                   $"Read: {Utils.FormatSpeed(readSpeed)}\nWrite: {Utils.FormatSpeed(writeSpeed)}";
        var diskInfoMessageWasTheLastSent = true;
        Utils.LogWithPushover(BackupAction.CheckBackupDisk, text);

        if (disk.Free < Utils.ConvertMBtoBytes(mediaBackup.Config.BackupDiskMinimumCriticalSpace))
        {
            Utils.LogWithPushover(BackupAction.CheckBackupDisk, PushoverPriority.High,
                $"{disk.FreeFormatted} free is very low. Prepare new backup disk");
        }

        // So we can cancel safely we only clear the disk.Name property and leave the DiskChecked value
        // Then if a cancel is requested we can put the disk.Name back how it was before we started scanning
        var filesToReset = mediaBackup.GetBackupFilesOnBackupDisk(disk.Name, true).ToArray();

        foreach (var file in filesToReset)
        {
            file.Disk = "-1"; // this is so we can reset them if we need to. DiskChecked is NOT cleared
        }
        UpdateMediaFilesCountDisplay();
        UpdateStatusLabel(ct, string.Format(Resources.Scanning, directoryToCheck));
        var backupDiskFiles = Utils.GetFiles(directoryToCheck, "*", SearchOption.AllDirectories, FileAttributes.Hidden, ct);
        EnableProgressBar(0, backupDiskFiles.Length);
        var reportedPercent = 0;

        for (var i = 0; i < backupDiskFiles.Length; i++)
        {
            if (ct.IsCancellationRequested) continue;

            var backupFileFullPath = backupDiskFiles[i];
            var backupFileIndexFolderRelativePath = backupFileFullPath[(directoryToCheck.Length + 1)..];
            UpdateStatusLabel(string.Format(Resources.Scanning, directoryToCheck), i + 1);
            UpdateMediaFilesCountDisplay();
            var currentPercent = i * 100 / toolStripProgressBar.Maximum;

            if (currentPercent % 10 == 0 && currentPercent > reportedPercent && backupDiskFiles.Length > 100)
            {
                reportedPercent = currentPercent;

                if (!UpdateCurrentBackupDiskInfo(disk))
                {
                    Utils.LogWithPushover(BackupAction.CheckBackupDisk, PushoverPriority.Emergency,
                        $"Error updating info for backup disk {disk.Name}");
                }
            }

            if (mediaBackup.Contains(backupFileIndexFolderRelativePath))
            {
                var backupFile = mediaBackup.GetBackupFileFromHashKey(backupFileIndexFolderRelativePath);

                if (File.Exists(backupFile.FullPath))
                {
                    // sometimes we get the same file on multiple backup disks
                    // calling CheckContentHashes will switch it from one disk to another and they'll keep doing it
                    // so if it was last seen on another disk delete it from this one
                    if (disk.Name != backupFile.Disk && backupFile.Disk.HasValue() && backupFile.Disk != "-1")
                    {
                        if (backupFile.Disk != "-1" && backupFile.Disk.HasValue())
                            Utils.Log($"{backupFile.FullPath} was on {backupFile.Disk} but now found on {disk.Name}");

                        // we will fall through from here to the delete further down and remove the file
                    }
                    else
                    {
                        // This forces a hash check on the source and backup disk files
                        Utils.Trace($"Checking hash for {backupFile.Hash}");
                        var returnValue = backupFile.CheckContentHashes(disk);

                        if (!returnValue)
                        {
                            // check the modifiedTime and if its different copy it
                            var sourceLastWriteTime = backupFile.LastWriteTime;
                            var lastWriteTimeOfFileOnBackupDisk = Utils.GetFileLastWriteTime(backupFileFullPath);

                            if (sourceLastWriteTime == lastWriteTimeOfFileOnBackupDisk)
                            {
                                // There was an error with the hash codes of the source file anf the file on the backup disk
                                Utils.LogWithPushover(BackupAction.CheckBackupDisk, PushoverPriority.High,
                                    $"There was an error with the hash codes on the source and backup disk. It's likely the source file has changed since the last backup of {backupFile.FullPath}. It could be that the source file or destination file are corrupted or in use by another process.");
                                diskInfoMessageWasTheLastSent = false;
                            }
                            else
                            {
                                Utils.LogWithPushover(BackupAction.CheckBackupDisk, PushoverPriority.High,
                                    $"Deleting {backupFile.FullPath}. ");
                                Utils.FileDelete(backupFileFullPath);
                            }
                        }
                        continue;
                    }
                }
                else
                {
                    // Backup doesn't exist in the directory anymore
                    // so delete it
                    mediaBackup.RemoveFile(backupFile);
                }
            }
            else
            {
                // The file on the backup disk isn't found in the directory anymore
                // it could be that we've renamed it in the directory
                // We could just let it get deleted off the backup disk and copied again next time
                // Alternatively, find it by the contents hashcode as that's (almost guaranteed unique)
                // and then rename it 
                // if we try to rename and it exists at the destination already then we delete the file instead
                var hashToCheck = Utils.GetShortMd5HashFromFile(backupFileFullPath);
                var file = mediaBackup.GetBackupFileFromContentsHashcode(hashToCheck);

                if (file != null && file.Length != 0 && file.Disk.HasValue())
                {
                    var destFileName = file.BackupDiskFullPath(disk.BackupPath);

                    // TODO put back after next full disk check Utils.LogWithPushover(BackupAction.CheckBackupDisk, $"Renaming {backupFileFullPath} to {destFileName}");
                    Utils.Log(BackupAction.CheckBackupDisk, $"Renaming {backupFileFullPath} to {destFileName}");

                    if (File.Exists(destFileName))
                    {
                        // check the hash of the destination file to check its the same as what we would've renamed too
                        if (Utils.GetShortMd5HashFromFile(destFileName) == hashToCheck)
                        {
                            Utils.LogWithPushover(BackupAction.CheckBackupDisk, PushoverPriority.Normal,
                                $"File exists already so deleting {backupFileFullPath} instead");
                            Utils.FileDelete(backupFileFullPath);
                        }
                    }
                    else
                        Utils.FileMove(backupFileFullPath, destFileName);

                    // This forces a hash check on the source and backup disk files
                    Utils.Trace($"Checking hash for {file.Hash}");
                    var returnValue = file.CheckContentHashes(disk);

                    if (returnValue == false)
                    {
                        // There was an error with the hash codes of the source file anf the file on the backup disk
                        Utils.LogWithPushover(BackupAction.CheckBackupDisk, PushoverPriority.High,
                            $"There was an error with the hash codes on the source and backup disk. It's likely the source file has changed since the last backup of {file.FullPath}. It could be that the source file or destination file are corrupted though.");
                        diskInfoMessageWasTheLastSent = false;
                    }
                    continue;
                }
            }

            // Extra file on a backup disk
            if (deleteExtraFiles)
            {
                Utils.LogWithPushover(BackupAction.CheckBackupDisk, PushoverPriority.High,
                    $"Extra file {backupFileFullPath} on backup disk {disk.Name} now deleted");
                Utils.FileDelete(backupFileFullPath);
                diskInfoMessageWasTheLastSent = false;
            }
            else
            {
                Utils.LogWithPushover(BackupAction.CheckBackupDisk,
                    $"Extra file {backupFileFullPath} on backup disk {disk.Name}");
                diskInfoMessageWasTheLastSent = false;
            }
        }

        if (!ct.IsCancellationRequested)
        {
            UpdateStatusLabel($"Deleting {directoryToCheck} empty folders");
            var filesOnThisDisk = mediaBackup.GetBackupFilesOnBackupDisk(disk.Name, true);

            if (filesOnThisDisk.Any())
            {
                var directoriesDeleted = Utils.DeleteEmptyDirectories(directoryToCheck);

                foreach (var directory in directoriesDeleted)
                {
                    Utils.Log(BackupAction.CheckBackupDisk, $"Deleted empty directory {directory}");
                }
            }

            // This updates any remaining files that were on this disk to be empty and ready for copying again
            var filesWithMinusOne = mediaBackup.GetBackupFilesOnBackupDisk("-1", true).ToArray();

            foreach (var file in filesWithMinusOne)
            {
                file.Disk = string.Empty;
            }
            disk.UpdateDiskChecked();

            if (!UpdateCurrentBackupDiskInfo(disk))
            {
                Utils.LogWithPushover(BackupAction.CheckBackupDisk, PushoverPriority.Emergency,
                    $"Error updating info for backup disk {disk.Name}");
                return null;
            }
            UpdateMediaFilesCountDisplay();
            var filesToRemoveOrMarkDeleted = mediaBackup.BackupFiles.Where(static b => b.Deleted && !b.Disk.HasValue()).ToArray();
            RemoveOrDeleteFiles(filesToRemoveOrMarkDeleted, out var removedFilesCount, out var deletedFilesCount);

            if (removedFilesCount > 0)
            {
                Utils.LogWithPushover(BackupAction.CheckBackupDisk, PushoverPriority.Normal,
                    $"{removedFilesCount} files removed completely");
            }

            if (deletedFilesCount > 0)
            {
                Utils.LogWithPushover(BackupAction.CheckBackupDisk, PushoverPriority.Normal,
                    $"{deletedFilesCount} files marked as deleted");
            }
            mediaBackup.Save(ct);
            UpdateStatusLabel(Resources.Saved);

            if (!diskInfoMessageWasTheLastSent)
            {
                text =
                    $"Name: {disk.Name}\nTotal: {disk.CapacityFormatted}\nFree: {disk.FreeFormatted}\nFiles: {disk.TotalFiles:n0}";
                Utils.LogWithPushover(BackupAction.CheckBackupDisk, text);
            }
            Utils.LogWithPushover(BackupAction.CheckBackupDisk, Resources.Completed, true);
        }
        else
        {
            // cancelling so reset the files that we cleared earlier
            foreach (var file in filesToReset)
            {
                // resetting to before we started
                file.Disk = disk.Name;
            }
            ct.ThrowIfCancellationRequested();
        }
        return Utils.TraceOut(disk);
    }

    private bool EnsureConnectedBackupDisk(string backupDisk)
    {
        Utils.TraceIn();

        // checks the specified backup disk is connected already and returns if it is
        //if its not it prompts the user to insert correct disk and waits
        // user clicks 'Yes' inserted and then returns
        if (!BackupDisk.CheckForValidBackupShare(backupDiskTextBox.Text)) return false;

        var currentConnectedBackupDiskName = BackupDisk.GetBackupDirectoryName(backupDiskTextBox.Text);

        while (currentConnectedBackupDiskName != backupDisk)
        {
            Utils.LogWithPushover(BackupAction.Restore, PushoverPriority.High,
                $"Connect new backup drive to restore from {backupDisk}");

            var answer = MessageBox.Show(string.Format(Resources.CorrectDiskPrompt, backupDisk), Resources.CorrectDiskTitle,
                MessageBoxButtons.YesNo);

            // ReSharper disable once SwitchStatementMissingSomeEnumCasesNoDefault
            switch (answer)
            {
                case DialogResult.No:
                    return false;
                case DialogResult.Yes:
                    currentConnectedBackupDiskName = BackupDisk.GetBackupDirectoryName(backupDiskTextBox.Text);
                    break;
            }
        }
        return Utils.TraceOut(true);
    }

    private void SetupBackupDiskAsync(CancellationToken ct)
    {
        Utils.TraceIn();
        DisableControlsForAsyncTasks(ct);
        var disk = SetupBackupDisk(ct);
        _ = UpdateCurrentBackupDiskInfo(disk);
        ResetAllControls();
        Utils.TraceOut();
    }

    /// <summary>
    ///     Waits for a valid backup disk to be inserted
    /// </summary>
    /// <returns></returns>
    private BackupDisk SetupBackupDisk(CancellationToken ct)
    {
        Utils.TraceIn();
        var nextDiskMessage = Resources.PleaseInsertTheNextBackupDiskNow;
        var disk = mediaBackup.GetBackupDisk(backupDiskTextBox.Text);

        while (disk == null)
        {
            WaitForNewDisk(nextDiskMessage, ct);
            disk = mediaBackup.GetBackupDisk(backupDiskTextBox.Text);
        }

        if (!UpdateCurrentBackupDiskInfo(disk))
            _ = MessageBox.Show(Resources.NoValidBackupShare, Resources.BackupDisk, MessageBoxButtons.OK);
        return Utils.TraceOut(disk);
    }
}