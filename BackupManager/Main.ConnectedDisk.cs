// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="Main.ConnectedDisk.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

using BackupManager.Entities;
using BackupManager.Extensions;
using BackupManager.Properties;

namespace BackupManager;

internal sealed partial class Main
{
    private void CheckConnectedDiskAndCopyFilesAsync(bool deleteExtraFiles, bool copyFiles)
    {
        longRunningActionExecutingRightNow = true;
        DisableControlsForAsyncTasks();
        _ = CheckConnectedDisk(deleteExtraFiles);
        if (copyFiles) CopyFiles(true);
        ResetAllControls();
        longRunningActionExecutingRightNow = false;
    }

    private void CheckConnectedDiskAndCopyFilesRepeaterAsync(bool copyFiles)
    {
        longRunningActionExecutingRightNow = true;
        DisableControlsForAsyncTasks();
        var nextDiskMessage = Resources.Main_Please_insert_the_next_backup_disk_now;

        while (!ct.IsCancellationRequested)
        {
            var lastBackupDiskChecked = CheckConnectedDisk(true);

            if (lastBackupDiskChecked == null)
            {
                _ = MessageBox.Show(Resources.Main_BackupDiskError, Resources.Main_BackupDiskTitle, MessageBoxButtons.OK);
                continue;
            }
            if (copyFiles) CopyFiles(false);

            // send pushover high to change disk
            Utils.LogWithPushover(BackupAction.General, PushoverPriority.High,
                $"Backup disk {lastBackupDiskChecked.Name} checked. Please insert the next disk now");
            UpdateStatusLabel(nextDiskMessage);
            BackupDisk newDisk;

            do
            {
                WaitForNewDisk(nextDiskMessage);
                newDisk = SetupBackupDisk();
            } while (newDisk.Name == lastBackupDiskChecked.Name);
        }
    }

    private void WaitForNewDisk(string message)
    {
        Utils.TraceIn();
        UpdateStatusLabel(message);
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
        currentBackupDiskTextBox.Invoke(x => x.Text = disk.Name);
        backupDiskCapacityTextBox.Invoke(x => x.Text = disk.CapacityFormatted);
        backupDiskAvailableTextBox.Invoke(x => x.Text = disk.FreeFormatted);
        return returnValue;
    }

    /// <summary>
    ///     Returns a BackupDisk of the connected disk that's just been checked
    /// </summary>
    /// <param name="deleteExtraFiles"></param>
    /// <returns>null if there was an error</returns>
    private BackupDisk CheckConnectedDisk(bool deleteExtraFiles)
    {
        Utils.TraceIn();

        // Scans the connected backup disk and finds all its files
        // for each for found calculate the hash from the backup disk
        // find that hash in the backup data file
        // rebuilds the source filename from Directory+Path
        // checks the file still exists there
        // if it does compare the hash codes and update results
        // force a recalculation of both the hashes to check the files can both be read correctly
        var disk = SetupBackupDisk();
        var directoryToCheck = disk.BackupPath;
        Utils.LogWithPushover(BackupAction.CheckBackupDisk, $"Started checking backup disk {directoryToCheck}");
        UpdateStatusLabel($"Checking backup disk {directoryToCheck}");
        long readSpeed = 0, writeSpeed = 0;

        if (mediaBackup.Config.SpeedTestOnOff)
        {
            var diskTestSize = disk.Free > Utils.ConvertMBtoBytes(mediaBackup.Config.SpeedTestFileSize)
                ? Utils.ConvertMBtoBytes(mediaBackup.Config.SpeedTestFileSize)
                : disk.Free - Utils.BytesInOneKilobyte;
            UpdateStatusLabel(string.Format(Resources.Main_SpeedTesting, directoryToCheck));

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
        var filesToReset = mediaBackup.GetBackupFilesOnBackupDisk(disk.Name, true);

        foreach (var fileName in filesToReset)
        {
            fileName.ClearDiskChecked();
        }
        UpdateMediaFilesCountDisplay();
        UpdateStatusLabel(string.Format(Resources.Main_Scanning, directoryToCheck));
        var backupDiskFiles = Utils.GetFiles(directoryToCheck, "*", SearchOption.AllDirectories, FileAttributes.Hidden, ct);
        EnableProgressBar(0, backupDiskFiles.Length);

        for (var i = 0; i < backupDiskFiles.Length; i++)
        {
            var backupFileFullPath = backupDiskFiles[i];
            var backupFileIndexFolderRelativePath = backupFileFullPath[(directoryToCheck.Length + 1)..];
            UpdateStatusLabel(string.Format(Resources.Main_Scanning, directoryToCheck), i + 1);
            UpdateMediaFilesCountDisplay();

            if (mediaBackup.Contains(backupFileIndexFolderRelativePath))
            {
                var backupFile = mediaBackup.GetBackupFileFromHashKey(backupFileIndexFolderRelativePath);

                if (File.Exists(backupFile.FullPath))
                {
                    // sometimes we get the same file on multiple backup disks
                    // calling CheckContentHashes will switch it from one disk to another and they'll keep doing it
                    // so if it was last seen on another disk delete it from this one

                    if (disk.Name != backupFile.Disk && backupFile.Disk.HasValue())
                    {
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
                                Utils.FileDelete(backupFileFullPath);
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

                if (file != null && file.Length != 0 && file.BackupDiskNumber == 0)
                {
                    var destFileName = file.BackupDiskFullPath(disk.BackupPath);

                    Utils.LogWithPushover(BackupAction.CheckBackupDisk, PushoverPriority.Normal,
                        $"Renaming {backupFileFullPath} to {destFileName}");

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
                Utils.LogWithPushover(BackupAction.CheckBackupDisk,
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

        //TODO maybe check for root of a backup Disk being empty
        UpdateStatusLabel($"Deleting {directoryToCheck} empty folders");
        var directoriesDeleted = Utils.DeleteEmptyDirectories(directoryToCheck);

        foreach (var directory in directoriesDeleted)
        {
            Utils.Log(BackupAction.CheckBackupDisk, $"Deleted empty directory {directory}");
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
        mediaBackup.Save();
        UpdateStatusLabel(Resources.Main_Saved);

        if (!diskInfoMessageWasTheLastSent)
        {
            text = $"Name: {disk.Name}\nTotal: {disk.CapacityFormatted}\nFree: {disk.FreeFormatted}\nFiles: {disk.TotalFiles:n0}";
            Utils.LogWithPushover(BackupAction.CheckBackupDisk, text);
        }
        Utils.LogWithPushover(BackupAction.CheckBackupDisk, "Completed");
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
            Utils.LogWithPushover(BackupAction.General, PushoverPriority.High,
                $"Connect new backup drive to restore from {backupDisk}");

            var answer = MessageBox.Show(string.Format(Resources.Main_CorrectDiskPrompt, backupDisk),
                Resources.Main_CorrectDiskTitle, MessageBoxButtons.YesNo);

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

    private void SetupBackupDiskAsync()
    {
        longRunningActionExecutingRightNow = true;
        DisableControlsForAsyncTasks();
        var disk = SetupBackupDisk();
        _ = UpdateCurrentBackupDiskInfo(disk);
        ResetAllControls();
        longRunningActionExecutingRightNow = false;
    }

    /// <summary>
    ///     Waits for a valid backup disk to be inserted
    /// </summary>
    /// <returns></returns>
    private BackupDisk SetupBackupDisk()
    {
        var nextDiskMessage = Resources.Main_Please_insert_the_next_backup_disk_now;
        var disk = mediaBackup.GetBackupDisk(backupDiskTextBox.Text);

        while (disk == null)
        {
            WaitForNewDisk(nextDiskMessage);
            disk = mediaBackup.GetBackupDisk(backupDiskTextBox.Text);
        }

        if (!UpdateCurrentBackupDiskInfo(disk))
            _ = MessageBox.Show(Resources.Main_SetupBackupDisk_Can_t_find_a_valid_backup_share,
                Resources.Main_SetupBackupDisk_Backup_Disk, MessageBoxButtons.OK);
        return disk;
    }
}