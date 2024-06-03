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
    internal void CheckConnectedDiskAndCopyFilesAsync(bool deleteExtraFiles, bool copyFiles, CancellationToken ct)
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
                var dirsToScan = mediaBackup.Watcher.DirectoriesToScan.ToArray();
                ReadyToScan(new FileSystemWatcherEventArgs(dirsToScan), SearchOption.AllDirectories, true, ct);

                // Empty the DirectoriesToScan because we've processed all of them now
                // we do it here so if we get cancelled before this we leave the directories ready to scan for next time
                foreach (var a in dirsToScan)
                {
                    _ = mediaBackup.Watcher.DirectoriesToScan.Remove(a);
                }
                var lastBackupDiskChecked = CheckConnectedDisk(true, ct);

                if (lastBackupDiskChecked == null)
                {
                    _ = MessageBox.Show(Resources.BackupDiskError, Resources.BackupDisk, MessageBoxButtons.OK);
                    continue;
                }
                if (copyFiles) CopyFiles(false, ct);
                Utils.LogWithPushover(BackupAction.CopyFiles, PushoverPriority.High, $"Backup disk {lastBackupDiskChecked.Name} checked. Please insert the next disk now", true);
                UpdateStatusLabel(ct, nextDiskMessage);
                BackupDisk newDisk;

                do
                {
                    if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();
                    WaitForNewDisk(nextDiskMessage, ct);
                    newDisk = SetupBackupDisk(ct);
                } while (newDisk.Name == lastBackupDiskChecked.Name);
            }
            if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();
        }
        finally
        {
            Utils.TraceOut();
        }
    }

    /// <summary>
    ///     Waits 5 seconds
    /// </summary>
    /// <param name="message"></param>
    /// <param name="ct"></param>
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
    internal BackupDisk CheckConnectedDisk(bool deleteExtraFiles, CancellationToken ct)
    {
        Utils.TraceIn();

        // Scans the connected backup disk and finds all its files
        // for each for found calculate the hash from the backup disk
        // find that hash in the backup data file
        // rebuilds the source filename from Directory+Path
        // checks the file still exists there
        // if it does compare the hash codes and update results
        // force a recalculation of both the hashes to check the files can both be read correctly
        // edge cases are when a file has been moved from one source directory to another
        // or one backup disk to another
        var disk = SetupBackupDisk(ct);
        var directoryToCheck = disk.BackupPath;
        Utils.LogWithPushover(BackupAction.CheckBackupDisk, Resources.Started, false, true);
        Utils.LogWithPushover(BackupAction.CheckBackupDisk, string.Format(Resources.Checking, directoryToCheck));
        UpdateStatusLabel(ct, string.Format(Resources.Scanning, directoryToCheck));
        ConnectedDiskSpeedTest(disk, directoryToCheck, ct);
        var diskInfoMessageWasTheLastSent = true;

        // So we can cancel safely we use the BeingCheckedNow flag
        // Then if a cancel is requested we can put it how it was before we started scanning
        mediaBackup.ClearFlags();
        var filesPreviouslyOnThisBackupDisk = mediaBackup.GetBackupFilesOnBackupDisk(disk.Name, true).ToArray();

        // scenario 102 - file in our xml but not on disk anymore needs to show as deleted now
        foreach (var file in filesPreviouslyOnThisBackupDisk)
        {
            file.BeingCheckedNow = true;
        }
        UpdateMediaFilesCountDisplay();
        UpdateStatusLabel(ct, string.Format(Resources.Scanning, directoryToCheck));
        var backupDiskFiles = Utils.File.GetFiles(directoryToCheck, "*", SearchOption.AllDirectories, FileAttributes.Hidden, ct);
        EnableProgressBar(0, backupDiskFiles.Length);
        var reportedPercent = 0;

        for (var i = 0; i < backupDiskFiles.Length; i++)
        {
            if (ct.IsCancellationRequested) continue;

            var backupDiskFileFullPath = backupDiskFiles[i];
            var backupFileIndexFolderRelativePath = backupDiskFileFullPath[(directoryToCheck.Length + 1)..];
            UpdateStatusLabel(string.Format(Resources.Scanning, directoryToCheck), i + 1);
            UpdateMediaFilesCountDisplay();
            if (backupDiskFiles.Length > (Utils.InDebugBuild ? 2 : 100)) ConnectedDiskUpdatePercentComplete(i, ref reportedPercent, disk);
            Utils.Trace($"{backupDiskFileFullPath} has Index folder and relative path={backupFileIndexFolderRelativePath}");

            if (mediaBackup.Contains(backupFileIndexFolderRelativePath))
            {
                // scenario 103 or 105 on disk and in xml but could be different
                ConnectedDiskBackupDiskFileIsInTheHashtable(backupDiskFileFullPath, disk, ref diskInfoMessageWasTheLastSent, deleteExtraFiles, backupFileIndexFolderRelativePath);
            }
            else
            {
                // scenario 104 on disk but not in xml
                ConnectedDiskBackupDiskFileIsNotInTheHashtable(backupDiskFileFullPath, disk, ref diskInfoMessageWasTheLastSent, deleteExtraFiles);
            }
        }

        if (ct.IsCancellationRequested)
        {
            foreach (var file in filesPreviouslyOnThisBackupDisk)
            {
                file.BeingCheckedNow = false;
            }
            ct.ThrowIfCancellationRequested();
        }
        ConnectedDiskDeleteEmptyDirectories(directoryToCheck, disk, ct);

        // This updates any remaining files that were on this disk to be empty and ready for copying again
        foreach (var file in filesPreviouslyOnThisBackupDisk.Where(static f => f.BeingCheckedNow).ToArray())
        {
            file.Disk = string.Empty;
            file.BeingCheckedNow = false;
        }
        disk.UpdateDiskChecked();

        if (!UpdateCurrentBackupDiskInfo(disk))
        {
            Utils.LogWithPushover(BackupAction.CheckBackupDisk, PushoverPriority.Emergency, string.Format(Resources.ErrorUpdatingInfoForBackupDisk, disk.Name));
            return null;
        }
        UpdateMediaFilesCountDisplay();
        RemoveOrDeleteFiles(mediaBackup.BackupFiles.Where(static b => b.Deleted && b.Disk.HasNoValue()).ToArray());
        mediaBackup.Save(ct);
        UpdateStatusLabel(Resources.Saved);

        if (!diskInfoMessageWasTheLastSent)
        {
            var text = $"Name: {disk.Name}\nTotal: {disk.CapacityFormatted}\nFree: {disk.FreeFormatted}\nFiles: {disk.TotalFiles:n0}";
            Utils.LogWithPushover(BackupAction.CheckBackupDisk, text);
        }
        Utils.LogWithPushover(BackupAction.CheckBackupDisk, Resources.Completed, true);
        return Utils.TraceOut(disk);
    }

    private void ConnectedDiskDeleteEmptyDirectories(string directoryToCheck, BackupDisk disk, CancellationToken ct)
    {
        UpdateStatusLabel(ct, string.Format(Resources.DeletingEmptyDirectories, directoryToCheck));
        if (!mediaBackup.GetBackupFilesOnBackupDisk(disk.Name, true).Any()) return;

        var directoriesDeleted = Utils.Directory.DeleteEmpty(directoryToCheck);

        foreach (var directory in directoriesDeleted)
        {
            Utils.Log(BackupAction.CheckBackupDisk, $"Deleted empty directory {directory}");
        }
    }

    private static void ConnectedDiskRemoveExtraFile(bool deleteExtraFiles, string backupDiskFileFullPath, BackupDisk disk)
    {
        // One of the checks above returned False to we will delete the file now
        // as it's an extra file on the backup disk
        if (deleteExtraFiles)
        {
            Utils.LogWithPushover(BackupAction.CheckBackupDisk, PushoverPriority.Normal, $"Extra file {backupDiskFileFullPath} on backup disk {disk.Name} now deleted");
            _ = Utils.File.Delete(backupDiskFileFullPath);
        }
        else
            Utils.LogWithPushover(BackupAction.CheckBackupDisk, $"Extra file {backupDiskFileFullPath} on backup disk {disk.Name}");
    }

    private void ConnectedDiskBackupDiskFileIsNotInTheHashtable(string backupDiskFileFullPath, BackupDisk disk, ref bool diskInfoMessageWasTheLastSent, bool deleteExtraFiles)
    {
        // The file on the backup disk isn't found in the directory anymore
        // it could be that we've renamed it in the directory
        // We could just let it get deleted off the backup disk and copied again next time
        // Alternatively, find it by the contents hashcode as that's (almost guaranteed unique)
        // and then rename it 
        // if we try to rename, and it exists at the destination already then we delete the file instead
        Utils.TraceIn();
        var hashToCheck = Utils.File.GetShortMd5Hash(backupDiskFileFullPath);
        var file = mediaBackup.GetBackupFileFromContentsHashcode(hashToCheck);

        if (file == null || file.Length == 0 || file.Disk.HasNoValue())
        {
            ConnectedDiskRemoveExtraFile(deleteExtraFiles, backupDiskFileFullPath, disk);
            diskInfoMessageWasTheLastSent = false;
            Utils.TraceOut();
            return;
        }
        var destFileName = file.BackupDiskFullPath(disk.BackupPath);
        Utils.LogWithPushover(BackupAction.CheckBackupDisk, $"Renaming {backupDiskFileFullPath} to {destFileName}");

        if (File.Exists(destFileName))
        {
            // check the hash of the destination file to check it's the same as what we would've renamed too
            if (Utils.File.GetShortMd5Hash(destFileName) == hashToCheck)
            {
                Utils.LogWithPushover(BackupAction.CheckBackupDisk, PushoverPriority.Normal, string.Format(Resources.FileExistsAlreadySoDeleting, backupDiskFileFullPath));
                _ = Utils.File.Delete(backupDiskFileFullPath);
            }
        }
        else
            _ = Utils.File.Move(backupDiskFileFullPath, destFileName);

        // This forces a hash check on the source and backup disk files
        if (file.CheckContentHashes(disk))
        {
            // file is checked so flag it as such
            file.BeingCheckedNow = false;
            Utils.TraceOut();
            return;
        }
        Utils.LogWithPushover(BackupAction.CheckBackupDisk, PushoverPriority.High, string.Format(Resources.HashCodesError, file.FullPath));
        diskInfoMessageWasTheLastSent = false;
        Utils.TraceOut();
    }

    /// <summary>
    /// </summary>
    /// <param name="backupDiskFileFullPath"></param>
    /// <param name="disk"></param>
    /// <param name="diskInfoMessageWasTheLastSent"></param>
    /// <param name="deleteExtraFiles"></param>
    /// <param name="hashKey"></param>
    /// <returns></returns>
    private void ConnectedDiskBackupDiskFileIsInTheHashtable(string backupDiskFileFullPath, BackupDisk disk, ref bool diskInfoMessageWasTheLastSent, bool deleteExtraFiles, string hashKey)
    {
        Utils.TraceIn();
        var backupFile = mediaBackup.GetBackupFileFromHashKey(hashKey);
        var backupFileSourceDiskFullPath = backupFile.FullPath;

        if (File.Exists(backupFileSourceDiskFullPath))
        {
            // sometimes we get the same file on multiple backup disks
            // calling CheckContentHashes will switch it from one disk to another, and they'll keep doing it
            // so if it was last seen on another disk delete it from this one
            if (disk.Name != backupFile.Disk && backupFile.Disk.HasValue())
            {
                if (backupFile.Disk.HasValue()) Utils.Log($"{backupFileSourceDiskFullPath} was on {backupFile.Disk} but now found on {disk.Name}");

                // we will fall through from here to the delete further down and remove the file
            }
            else
            {
                // This forces a hash check on the source and backup disk files
                if (backupFile.CheckContentHashes(disk))
                {
                    // file is checked so flag it as such
                    backupFile.BeingCheckedNow = false;
                    Utils.TraceOut();
                    return;
                }
                Utils.Log("Checking LastWriteTime on files as the hash codes are different");

                // check the LastWriteTime and if its different delete the file from the backup disk so that it will be copied again
                // We Update the LastWriteTime from disk in case it's not been scanned since it changed
                backupFile.UpdateLastWriteTime();
                var sourceLastWriteTime = backupFile.LastWriteTime;
                var lastWriteTimeOfFileOnBackupDisk = Utils.File.GetLastWriteTime(backupDiskFileFullPath);
                Utils.Trace($"{backupFile.FileName} xml LastWriteTime={sourceLastWriteTime}. On disk is {lastWriteTimeOfFileOnBackupDisk}");

                if (sourceLastWriteTime == lastWriteTimeOfFileOnBackupDisk)
                {
                    Utils.LogWithPushover(BackupAction.CheckBackupDisk, PushoverPriority.High, string.Format(Resources.HashCodesError, backupFile.FullPath));
                    diskInfoMessageWasTheLastSent = false;
                }
                else
                {
                    if (deleteExtraFiles)
                    {
                        Utils.LogWithPushover(BackupAction.CheckBackupDisk, PushoverPriority.High, $"Deleting {backupDiskFileFullPath}.");
                        _ = Utils.File.Delete(backupDiskFileFullPath);
                    }
                    else
                        Utils.LogWithPushover(BackupAction.CheckBackupDisk, PushoverPriority.High, $"Would be deleting {backupDiskFileFullPath}. ");
                }
                Utils.TraceOut();
                return;
            }
        }
        else
        {
            // Backup doesn't exist in the directories anymore
            // so delete it
            mediaBackup.RemoveFile(backupFile);
        }
        ConnectedDiskRemoveExtraFile(deleteExtraFiles, backupDiskFileFullPath, disk);
        diskInfoMessageWasTheLastSent = false;
        Utils.TraceOut();
    }

    private void ConnectedDiskUpdatePercentComplete(int i, ref int reportedPercent, BackupDisk disk)
    {
        var currentPercent = i * 100 / toolStripProgressBar.Maximum;
        if (currentPercent % 10 != 0 || currentPercent <= reportedPercent) return;

        reportedPercent = currentPercent;

        if (UpdateCurrentBackupDiskInfo(disk))
            Utils.LogWithPushover(BackupAction.CheckBackupDisk, string.Format(Resources.ProcessingPercentage, currentPercent));
        else
            Utils.LogWithPushover(BackupAction.CheckBackupDisk, PushoverPriority.Emergency, string.Format(Resources.ErrorUpdatingInfoForBackupDisk, disk.Name));
    }

    private void ConnectedDiskSpeedTest(BackupDisk disk, string directoryToCheck, CancellationToken ct)
    {
        long readSpeed = 0, writeSpeed = 0;

        if (config.SpeedTestOnOff)
        {
            var diskTestSize = disk.Free > Utils.ConvertMBtoBytes(config.SpeedTestFileSize) ? Utils.ConvertMBtoBytes(config.SpeedTestFileSize) : disk.Free - Utils.BYTES_IN_ONE_KILOBYTE;
            UpdateStatusLabel(ct, string.Format(Resources.SpeedTesting, directoryToCheck));
            Utils.DiskSpeedTest(directoryToCheck, diskTestSize, config.SpeedTestIterations, out readSpeed, out writeSpeed, ct);
            disk.UpdateSpeeds(readSpeed, writeSpeed);
        }
        var text = string.Format(Resources.ConnectedDiskInfo, disk.Name, disk.CapacityFormatted, disk.FreeFormatted, Utils.FormatSpeed(readSpeed), Utils.FormatSpeed(writeSpeed));
        Utils.LogWithPushover(BackupAction.CheckBackupDisk, text);
        if (disk.Free < Utils.ConvertMBtoBytes(config.BackupDiskMinimumCriticalSpace)) Utils.LogWithPushover(BackupAction.CheckBackupDisk, PushoverPriority.High, string.Format(Resources.PrepareNewBackupDisk, disk.FreeFormatted));
    }

    internal bool EnsureConnectedBackupDisk(string backupDisk)
    {
        Utils.TraceIn();

        // checks the specified backup disk is connected already and returns if it is
        //if it's not it prompts the user to insert correct disk and waits
        // user clicks 'Yes' inserted and then returns
        if (!BackupDisk.CheckForValidBackupShare(backupDiskTextBox.Text)) return false;

        var currentConnectedBackupDiskName = BackupDisk.GetBackupDirectoryName(backupDiskTextBox.Text);

        while (!currentConnectedBackupDiskName.EqualsIgnoreCase(backupDisk))
        {
            Utils.LogWithPushover(BackupAction.Restore, PushoverPriority.High, $"Connect new backup drive to restore from {backupDisk}");
            var answer = MessageBox.Show(string.Format(Resources.CorrectDiskPrompt, backupDisk), Resources.CorrectDiskTitle, MessageBoxButtons.YesNo);

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
        if (!UpdateCurrentBackupDiskInfo(disk)) _ = MessageBox.Show(Resources.NoValidBackupShare, Resources.BackupDisk, MessageBoxButtons.OK);
        return Utils.TraceOut(disk);
    }
}