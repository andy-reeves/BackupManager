// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="Main.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

using BackupManager.Entities;
using BackupManager.Extensions;
using BackupManager.Properties;

using CsvHelper;
using CsvHelper.Configuration;

namespace BackupManager;

internal sealed partial class Main : Form
{
    private void FileSystemWatcher_OnError(object sender, ErrorEventArgs e)
    {
        var ex = e.GetException();
        Utils.LogWithPushover(BackupAction.Error, $"Message: {ex.Message}");

        try
        {
            // the most common is DirectoryNotFound for a network path
            // wait a bit and then attempt restart the watcher
            Utils.Wait(mediaBackup.Config.DirectoriesFileChangeWatcherRestartDelay);
            _ = mediaBackup.Watcher.Reset();
            _ = mediaBackup.Watcher.Start();
        }
        catch (Exception exc)
        {
            Utils.LogWithPushover(BackupAction.Error, $"Unable to Reset FileSystemWatcher {exc}");
            mediaBackup.Config.DirectoriesFileChangeWatcherOnOff = false;
            SetupFileWatchers();
        }
    }

    private void FileWatcherButton_Click(object sender, EventArgs e)
    {
        Utils.TraceIn();
        mediaBackup.Config.DirectoriesFileChangeWatcherOnOff = !mediaBackup.Config.DirectoriesFileChangeWatcherOnOff;
        SetupFileWatchers();
    }

    private void ScheduledBackupRunNowButton_Click(object sender, EventArgs e)
    {
        Utils.TraceIn();
        ResetTokenSource();
        _ = TaskWrapper(() => ScheduledBackupAsync(mainCt), mainCt);
        Utils.TraceOut();
    }

    private void BackupTimerButton_Click(object sender, EventArgs e)
    {
        Utils.TraceIn();
        mediaBackup.Config.ScheduledBackupOnOff = !mediaBackup.Config.ScheduledBackupOnOff;
        SetupDailyTrigger(mediaBackup.Config.ScheduledBackupOnOff, scheduledDateTimePicker.Value);
        UpdateScheduledBackupButton();
        Utils.TraceOut();
    }

    internal void CheckForOldBackupDisks_Click(object sender, EventArgs e)
    {
        Utils.TraceIn();
        CheckForOldBackupDisks();
        Utils.TraceOut();
    }

    internal void ScanAllDirectoriesButton_Click(object sender, EventArgs e)
    {
        Utils.TraceIn();
        ResetTokenSource();
        _ = TaskWrapper(() => ScanAllDirectoriesAsync(mainCt), mainCt);
        Utils.TraceOut();
    }

    private void CheckAllSymbolicLinksButton_Click(object sender, EventArgs e)
    {
        Utils.TraceIn();
        ResetTokenSource();
        _ = TaskWrapper(() => UpdateSymbolicLinksAsync(mainCt), mainCt);
        Utils.TraceOut();
    }

    private void CheckDiskAndDeleteButton_Click(object sender, EventArgs e)
    {
        Utils.TraceIn();
        ResetTokenSource();
        _ = TaskWrapper(() => CheckConnectedDiskAndCopyFilesAsync(true, false, mainCt), mainCt);
        Utils.TraceOut();
    }

    private void ResetTokenSource()
    {
        mainCancellationTokenSource?.Dispose();
        mainCancellationTokenSource = new CancellationTokenSource();
        mainCt = mainCancellationTokenSource.Token;
    }

    private void ListDirectoriesToScanButton_Click(object sender, EventArgs e)
    {
        Utils.TraceIn();
        Utils.Log("Listing FileSystemChanges detected");

        foreach (var fileSystemEntry in mediaBackup.Watcher.FileSystemChanges)
        {
            Utils.Log($"{fileSystemEntry.Path} changed at {fileSystemEntry.ModifiedDateTime}");
        }
        Utils.Log("Listing DirectoriesToScan queued");

        foreach (var fileSystemEntry in mediaBackup.Watcher.DirectoriesToScan)
        {
            Utils.Log($"{fileSystemEntry.Path} changed at {fileSystemEntry.ModifiedDateTime}");
        }
        Utils.TraceOut();
    }

    private void ListFilesOnBackupDiskButton_Click(object sender, EventArgs e)
    {
        Utils.TraceIn();

        if (listFilesComboBox.SelectedItem != null)
        {
            var selectedItemText = listFilesComboBox.SelectedItem.ToString();
            var files = mediaBackup.GetBackupFilesOnBackupDisk(selectedItemText, true);
            Utils.Log($"Listing files on backup disk {selectedItemText}");

            foreach (var file in files)
            {
                Utils.Log($"{file.FullPath}");
            }
        }
        Utils.TraceOut();
    }

    private void ListFilesInDirectoryButton_Click(object sender, EventArgs e)
    {
        Utils.TraceIn();

        if (listDirectoriesComboBox.SelectedItem != null)
        {
            var directory = listDirectoriesComboBox.SelectedItem.ToString();
            var files = mediaBackup.GetBackupFilesInDirectory(directory, false);
            Utils.Log($"Listing files in directory {directory}");

            if (mediaBackup.Config.SpeedTestOnOff)
            {
                Utils.DiskSpeedTest(directory, Utils.ConvertMBtoBytes(mediaBackup.Config.SpeedTestFileSize), mediaBackup.Config.SpeedTestIterations, out var readSpeed, out var writeSpeed, mainCt);
                Utils.Log($"Testing {directory}, Read: {Utils.FormatSpeed(readSpeed)} Write: {Utils.FormatSpeed(writeSpeed)}");
            }

            foreach (var file in files)
            {
                Utils.Log($"{file.FullPath} : {file.Disk}");
                if (file.Disk.HasNoValue()) Utils.Log($"{file.FullPath} : not on a backup disk");
            }
        }
        Utils.TraceOut();
    }

    private void RestoreFilesButton_Click(object sender, EventArgs e)
    {
        Utils.TraceIn();

        // loop through all the files looking for the directory specified in the top drop down and copy to the bottom drop down
        // for each file order by backup disk
        // prompt for the backup disk to be inserted
        // check we have it inserted
        // copy any files off this disk until we're all done to the new disk that we specified
        if (MessageBox.Show(Resources.RestoreFilesAreYouSure, Resources.RestoreFilesTitle, MessageBoxButtons.YesNo) == DialogResult.Yes)
        {
            if (directoriesComboBox.SelectedItem == null)
            {
                _ = MessageBox.Show(Resources.RestoreFilesSelectDirectory, Resources.RestoreFilesTitle, MessageBoxButtons.OK);
                return;
            }
            var directory = directoriesComboBox.SelectedItem.ToString();

            if (restoreDirectoryComboBox.SelectedItem == null)
            {
                _ = MessageBox.Show(Resources.RestoreFilesDirectoryToRestoreTo, Resources.RestoreFilesTitle, MessageBoxButtons.OK);
                return;
            }
            var targetDirectory = restoreDirectoryComboBox.SelectedItem.ToString();
            var files = mediaBackup.GetBackupFilesInDirectory(directory, false).Where(static p => p.Disk.HasValue());
            Utils.Log(BackupAction.Restore, string.Format(Resources.RestoringFilesFromDirectory, directory));
            Utils.Log(BackupAction.Restore, string.Format(Resources.RestoringFilesToTargetDirectory, targetDirectory));
            var backupShare = backupDiskTextBox.Text;
            var lastBackupDisk = string.Empty;
            var fileCounter = 0;
            var countOfFiles = 0;
            var backupFiles = files as BackupFile[] ?? files.ToArray();

            foreach (var file in backupFiles)
            {
                if (!mediaBackup.Config.DisksToSkipOnRestore.Contains(file.Disk, StringComparer.CurrentCultureIgnoreCase))
                {
                    //we need to check the correct disk is connected and prompt if not
                    if (!EnsureConnectedBackupDisk(file.Disk))
                    {
                        _ = MessageBox.Show(Resources.CorrectDiskTitle, Resources.RestoreFilesTitle, MessageBoxButtons.OK);
                        return;
                    }

                    if (file.Disk != lastBackupDisk)
                    {
                        if (!mediaBackup.Config.DisksToSkipOnRestore.Contains(lastBackupDisk, StringComparer.CurrentCultureIgnoreCase) && lastBackupDisk.HasValue())
                        {
                            mediaBackup.Config.DisksToSkipOnRestore.Add(lastBackupDisk);

                            // This is to save the backup disks we've completed so far
                            mediaBackup.Save(mainCt);
                        }

                        // count the number of files on this disk
                        countOfFiles = backupFiles.Count(p => p.Disk == file.Disk);
                        fileCounter = 0;
                    }
                    fileCounter++;

                    // calculate the source path
                    // calculate the destination path
                    var sourceFileFullPath = Path.Combine(backupShare, file.Disk, Utils.GetIndexFolder(file.Directory), file.RelativePath);

                    if (targetDirectory != null)
                    {
                        var targetFilePath = Path.Combine(targetDirectory, file.RelativePath);

                        if (Utils.File.Exists(targetFilePath))
                            Utils.LogWithPushover(BackupAction.Restore, $"[{fileCounter}/{countOfFiles}] {targetFilePath} Already exists");
                        else
                        {
                            if (Utils.File.Exists(sourceFileFullPath))
                            {
                                Utils.LogWithPushover(BackupAction.Restore, $"[{fileCounter}/{countOfFiles}] Copying {sourceFileFullPath} as {targetFilePath}");
                                _ = Utils.File.Copy(sourceFileFullPath, targetFilePath, mainCt);
                            }
                            else
                                Utils.LogWithPushover(BackupAction.Restore, $"[{fileCounter}/{countOfFiles}] {sourceFileFullPath} doesn't exist");
                        }

                        if (Utils.File.Exists(targetFilePath))
                        {
                            if (file.ContentsHash == Utils.File.GetShortMd5Hash(targetFilePath))
                                file.Directory = targetDirectory;
                            else
                                Utils.LogWithPushover(BackupAction.Restore, PushoverPriority.High, $"ERROR: '{targetFilePath}' has a different Hashcode");
                        }
                    }
                }
                lastBackupDisk = file.Disk;
            }
            mediaBackup.Save(mainCt);
        }
        Utils.TraceOut();
    }

    private void CheckBackupDeleteAndCopyButton_Click(object sender, EventArgs e)
    {
        Utils.TraceIn();
        ResetTokenSource();
        _ = TaskWrapper(() => CheckConnectedDiskAndCopyFilesAsync(true, true, mainCt), mainCt);
        Utils.TraceOut();
    }

    private void ListMoviesWithMultipleFilesButton_Click(object sender, EventArgs e)
    {
        Utils.TraceIn();
        Utils.Log("Listing files with multiple matching files");
        Dictionary<string, BackupFile> allMovies = [];
        List<BackupFile> backupFilesWithDuplicates = [];

        foreach (var file in mediaBackup.GetBackupFiles(false))
        {
            var m = Regex.Match(file.FullPath, config.DuplicateFilesRegex);
            if (!m.Success) continue;

            var movieId = m.Groups[1].Value;

            if (allMovies.TryGetValue(movieId, out var movie))
            {
                if (!backupFilesWithDuplicates.Contains(file)) backupFilesWithDuplicates.Add(file);
                if (!backupFilesWithDuplicates.Contains(movie)) backupFilesWithDuplicates.Add(movie);
            }
            else
                allMovies.Add(movieId, file);
        }
        var sortedArray = backupFilesWithDuplicates.OrderBy(static o => o.FileName).ToArray();

        foreach (var backupMovieDuplicate in sortedArray)
        {
            Utils.Log($"{backupMovieDuplicate.FullPath}");
        }
        Utils.TraceOut();
    }

    private void TestPushoverHighButton_Click(object sender, EventArgs e)
    {
        Utils.TraceIn();
        Utils.LogWithPushover(BackupAction.General, PushoverPriority.High, "High priority test");
        Utils.TraceOut();
    }

    private void TestPushoverNormalButton_Click(object sender, EventArgs e)
    {
        Utils.TraceIn();
        Utils.LogWithPushover(BackupAction.General, PushoverPriority.Normal, "Normal priority test\nLine 2\nLine 3");
        Utils.TraceOut();
    }

    private void TestPushoverEmergencyButton_Click(object sender, EventArgs e)
    {
        Utils.TraceIn();
        Utils.LogWithPushover(BackupAction.General, PushoverPriority.Emergency, PushoverRetry.OneMinute, PushoverExpires.OneHour, "Emergency priority test");
        Utils.TraceOut();
    }

    private void ListBackupDiskStatusByFreeSpaceButton_Click(object sender, EventArgs e)
    {
        Utils.TraceIn();
        ListBackupDiskStatus(UpdateBackupDisksForDeletedFiles().OrderBy(static d => d.TotalFree));
        Utils.TraceOut();
    }

    // ReSharper disable once ReturnTypeCanBeEnumerable.Local
    private Collection<BackupDisk> UpdateBackupDisksForDeletedFiles()
    {
        var disks = mediaBackup.BackupDisks;

        // Update the actual total possible free space if deleted files were removed
        foreach (var disk in disks)
        {
            var backupFilesOnBackupDiskNotIncludingDeleted = mediaBackup.GetBackupFilesOnBackupDisk(disk.Name, false).ToArray();
            var backupFilesOnBackupDisk = mediaBackup.GetBackupFilesOnBackupDisk(disk.Name, true).ToArray();
            disk.DeletedFilesCount = backupFilesOnBackupDisk.Length - backupFilesOnBackupDiskNotIncludingDeleted.Length;
            disk.FilesSize = backupFilesOnBackupDiskNotIncludingDeleted.Sum(static p => p.Length);
        }
        return disks;
    }

    private void ListBackupDiskStatusByDiskNumberButton_Click(object sender, EventArgs e)
    {
        Utils.TraceIn();
        ListBackupDiskStatus(UpdateBackupDisksForDeletedFiles().OrderBy(static d => d.Number));
        Utils.TraceOut();
    }

    private void ListBackupDiskStatus(IOrderedEnumerable<BackupDisk> disks)
    {
        Utils.Log("Listing backup disk statuses");
        Utils.Log("Name        Checked    Capacity   Used     Free    Files  Deleted   Del     Free+Deleted");

        foreach (var disk in disks)
        {
            var lastChecked = disk.CheckedTime.HasValue ? disk.CheckedTime.Value.ToString(Resources.DateTime_ddMMMyy) : string.Empty;

            Utils.Log($"{disk.Name,-12}{lastChecked,9}{disk.CapacityFormatted,9}{(disk.Capacity - disk.Free).SizeSuffix(),9}" + $"{disk.FreeFormatted,9}{disk.FilesSize.SizeSuffix(),9}{disk.DeletedFilesCount,5}" +
                      $"{disk.DeletedFilesSize.SizeSuffix(),12}{disk.TotalFree.SizeSuffix(),10}");
        }
        var totalSizeFormatted = mediaBackup.BackupDisks.Sum(static p => p.Capacity).SizeSuffix();
        var totalFreeSpaceFormatted = mediaBackup.BackupDisks.Sum(static p => p.Free).SizeSuffix();
        var totalFreePlusDeletedFormatted = mediaBackup.BackupDisks.Sum(static p => p.TotalFree).SizeSuffix();
        Utils.Log($"\n      Total Capacity: {totalSizeFormatted,8}     Free: {totalFreeSpaceFormatted,7}                   Free+Del: {totalFreePlusDeletedFormatted,7}");
    }

    private void SpeedTestButton_Click(object sender, EventArgs e)
    {
        Utils.TraceIn();
        ResetTokenSource();
        _ = TaskWrapper(() => SpeedTestAllDirectoriesAsync(mainCt), mainCt);
        Utils.TraceOut();
    }

    private void MonitoringButton_Click(object sender, EventArgs e)
    {
        Utils.TraceIn();

        if (mediaBackup.Config.MonitoringOnOff)
        {
            monitoringTimer.Stop();
            Utils.LogWithPushover(BackupAction.ApplicationMonitoring, "Stopped");
            mediaBackup.Config.MonitoringOnOff = false;
        }
        else
        {
            monitoringTimer.Interval = mediaBackup.Config.MonitoringInterval;

            if (mediaBackup.Config.MonitoringStartDelayOnOff)
                Utils.LogWithPushover(BackupAction.ApplicationMonitoring, $"Starting in {Utils.FormatTimeFromSeconds(mediaBackup.Config.MonitoringInterval / 1000)}");
            else
            {
                Utils.LogWithPushover(BackupAction.ApplicationMonitoring, Resources.Started, true);
                MonitoringTimer_Tick(null, null);
            }
            monitoringTimer.Start();
            mediaBackup.Config.MonitoringOnOff = true;
        }
        UpdateMonitoringButton();
        Utils.TraceOut();
    }

    private void MonitoringTimer_Tick(object sender, EventArgs e)
    {
        if (monitoringExecutingRightNow) return;

        _ = TaskWrapper(monitoringAction, false, monitoringCancellationTokenSource.Token);
    }

    private void KillProcessesButton_Click(object sender, EventArgs e)
    {
        Utils.TraceIn();

        foreach (var monitor in mediaBackup.Config.Monitors)
        {
            if (monitor.ProcessToKill.HasValue())
            {
                var processesToKill = monitor.ProcessToKill.Split(',');

                foreach (var toKill in processesToKill)
                {
                    Utils.LogWithPushover(BackupAction.ApplicationMonitoring, $"Stopping all '{toKill}' processes that match");
                    _ = Utils.KillProcesses(toKill);
                }
            }
            if (monitor.ServiceToRestart.HasNoValue()) continue;

            Utils.LogWithPushover(BackupAction.ApplicationMonitoring, $"Stopping '{monitor.ServiceToRestart}'");
            if (!Utils.StopService(monitor.ServiceToRestart, 5000)) Utils.LogWithPushover(BackupAction.ApplicationMonitoring, string.Format(Resources.FailedToStopTheService, monitor.Name));
        }
        Utils.TraceOut();
    }

    private void TestPushoverLowButton_Click(object sender, EventArgs e)
    {
        Utils.TraceIn();
        Utils.LogWithPushover(BackupAction.General, PushoverPriority.Low, "Low priority test\nLine 2\nLine 3");
        Utils.TraceOut();
    }

    private void Main_FormClosed(object sender, FormClosedEventArgs e)
    {
        Utils.TraceIn();
        Utils.LogWithPushover(BackupAction.General, PushoverPriority.High, "BackupManager stopped", true);
        ResetTokenSource();
        Utils.BackupLogFile(mainCt);
        Utils.TraceOut();
    }

    private void StopProcessButton_Click(object sender, EventArgs e)
    {
        Utils.TraceIn();

        if (processesComboBox.SelectedItem != null)
        {
            var monitorName = processesComboBox.SelectedItem.ToString();
            var monitor = mediaBackup.Config.Monitors.First(m => m.Name == monitorName);

            if (monitor.ProcessToKill.HasValue())
            {
                var processesToKill = monitor.ProcessToKill.Split(',');

                foreach (var toKill in processesToKill)
                {
                    Utils.LogWithPushover(BackupAction.ApplicationMonitoring, $"Stopping all '{toKill}' processes that match");
                    _ = Utils.KillProcesses(toKill);
                }
            }

            if (monitor.ServiceToRestart.HasValue())
            {
                Utils.LogWithPushover(BackupAction.ApplicationMonitoring, $"Stopping '{monitor.ServiceToRestart}'");
                if (!Utils.StopService(monitor.ServiceToRestart, 5000)) Utils.LogWithPushover(BackupAction.ApplicationMonitoring, PushoverPriority.High, string.Format(Resources.FailedToStopTheService, monitor.Name));
            }
        }
        Utils.TraceOut();
    }

    private void ListFilesWithDuplicateContentHashCodesButton_Click(object sender, EventArgs e)
    {
        try
        {
            Utils.TraceIn();
            Dictionary<string, BackupFile> allFilesUniqueContentsHash = [];
            List<BackupFile> backupFilesWithDuplicates = [];
            if (config.DuplicateContentHashCodesDiscoveryRegex.HasNoValue()) return;

            foreach (var backupFile in mediaBackup.BackupFiles.Where(file => !file.Deleted && Regex.Match(file.FullPath, config.DuplicateContentHashCodesDiscoveryRegex).Success))
            {
                if (allFilesUniqueContentsHash.TryGetValue(backupFile.ContentsHash, out var originalFile))
                {
                    backupFilesWithDuplicates.Add(backupFile);
                    if (!backupFilesWithDuplicates.Contains(originalFile)) backupFilesWithDuplicates.Add(originalFile);
                }
                else
                    allFilesUniqueContentsHash.Add(backupFile.ContentsHash, backupFile);
            }

            foreach (var duplicateFile in backupFilesWithDuplicates)
            {
                Utils.Log($"{duplicateFile.FullPath} has a duplicate");
            }
        }
        finally
        {
            Utils.TraceOut();
        }
    }

    private void CheckDeleteAndCopyAllBackupDisksButton_Click(object sender, EventArgs e)
    {
        Utils.TraceIn();
        ResetTokenSource();
        _ = TaskWrapper(() => CheckConnectedDiskAndCopyFilesRepeaterAsync(true, mainCt), mainCt);
        Utils.TraceOut();
    }

    private void CancelButton_Click(object sender, EventArgs e)
    {
        Utils.TraceIn();
        mainCancellationTokenSource.Cancel();
        toolStripStatusLabel.Text = Resources.Cancelling;
        Utils.Log(BackupAction.General, Resources.Cancelling);
        KillCopyProcess();
        Utils.TraceOut();
    }

    private static void KillCopyProcess()
    {
        Utils.TraceIn();
        if (Utils.CopyProcess is { HasExited: false }) Utils.CopyProcess?.Kill();
        Utils.TraceOut();
    }

    /// <summary>
    ///     Kills the CopyProcess and sends the Cancelled message. Resets all controls.
    /// </summary>
    private void ASyncTasksCleanUp()
    {
        try
        {
            Utils.TraceIn();
            KillCopyProcess();
            UpdateMediaFilesCountDisplay();
            ResetAllControls();
            Utils.LogWithPushover(BackupAction.General, PushoverPriority.High, Resources.Cancelled);
        }
        finally
        {
            Utils.TraceOut();
        }
    }

    private void CheckAllBackupDisksButton_Click(object sender, EventArgs e)
    {
        Utils.TraceIn();
        ResetTokenSource();
        _ = TaskWrapper(() => CheckConnectedDiskAndCopyFilesRepeaterAsync(false, mainCt), mainCt);
        Utils.TraceOut();
    }

    private void PushoverOnOffButton_Click(object sender, EventArgs e)
    {
        Utils.TraceIn();
        mediaBackup.Config.PushoverOnOff = !mediaBackup.Config.PushoverOnOff;
        UpdateSendingPushoverButton();
        Utils.TraceOut();
    }

    private void PushoverLowCheckBox_CheckedChanged(object sender, EventArgs e)
    {
        Utils.TraceIn();
        mediaBackup.Config.PushoverSendLowOnOff = pushoverLowCheckBox.Checked;
        Utils.TraceOut();
    }

    private void PushoverNormalCheckBox_CheckedChanged(object sender, EventArgs e)
    {
        Utils.TraceIn();
        mediaBackup.Config.PushoverSendNormalOnOff = pushoverNormalCheckBox.Checked;
        Utils.TraceOut();
    }

    private void PushoverHighCheckBox_CheckedChanged(object sender, EventArgs e)
    {
        Utils.TraceIn();
        mediaBackup.Config.PushoverSendHighOnOff = pushoverHighCheckBox.Checked;
        Utils.TraceOut();
    }

    private void PushoverEmergencyCheckBox_CheckedChanged(object sender, EventArgs e)
    {
        Utils.TraceIn();
        mediaBackup.Config.PushoverSendEmergencyOnOff = pushoverEmergencyCheckBox.Checked;
        Utils.TraceOut();
    }

    private void SpeedTestDisksButton_Click(object sender, EventArgs e)
    {
        Utils.TraceIn();
        mediaBackup.Config.SpeedTestOnOff = !mediaBackup.Config.SpeedTestOnOff;
        UpdateSpeedTestDisksButton();
        Utils.TraceOut();
    }

    private void RefreshBackupDiskButton_Click(object sender, EventArgs e)
    {
        Utils.TraceIn();
        ResetTokenSource();
        _ = TaskWrapper(() => SetupBackupDiskAsync(mainCt), mainCt);
        Utils.TraceOut();
    }

    private void ListFilesMarkedAsDeletedButton_Click(object sender, EventArgs e)
    {
        Utils.TraceIn();
        Utils.Log("Listing files marked as deleted");
        var backupFiles = mediaBackup.GetBackupFilesMarkedAsDeleted(true).ToArray();

        foreach (var file in backupFiles)
        {
            Utils.Log($"{file.FullPath} at {file.Length.SizeSuffix()} on {file.Disk}");
        }
        Utils.Log($"{backupFiles.Length} files at {backupFiles.Sum(static p => p.Length).SizeSuffix()}");
        Utils.Log("Listing files marked as deleted ordered by size on backup disk");

        foreach (var d in backupFiles.Select(static f => f.Disk).Distinct().ToDictionary(static disk => disk, disk => backupFiles.Where(f => f.Disk == disk).Sum(static f => f.Length)).OrderByDescending(static i => i.Value))
        {
            Utils.Log($"{d.Key,-10} has {d.Value.SizeSuffix(),-8}");
        }
        Utils.TraceOut();
    }

    private void UpdateUI_Tick(object sender, EventArgs e)
    {
        pushoverMessagesRemainingTextBox.TextWithInvoke(Utils.PushoverMessagesRemaining.ToString("n0"));
        timeToNextRunTextBox.TextWithInvoke(_trigger == null || !updateUITimer.Enabled ? string.Empty : _trigger.TimeToNextTrigger().ToString(Resources.DateTime_TimeFormat));
        directoriesToScanTextBox.TextWithInvoke(mediaBackup.Watcher.DirectoriesToScan.Count.ToString());
        fileChangesDetectedTextBox.TextWithInvoke(mediaBackup.Watcher.FileSystemChanges.Count.ToString());
        UpdateOldestBackupDisk();
    }

    private void FileSystemWatcher_ReadyToScan(object sender, FileSystemWatcherEventArgs e)
    {
        try
        {
            Utils.TraceIn();

            if (longRunningActionExecutingRightNow)
            {
                // If another long-running task is already executing then add these directories back to the list to be scanned later
                foreach (var dir in e.Directories)
                {
                    _ = mediaBackup.Watcher.DirectoriesToScan.AddOrUpdate(dir);
                }
                return;
            }
            ResetTokenSource();
            DisableControlsForAsyncTasks(mainCt);
            ReadyToScan(e, SearchOption.TopDirectoryOnly, true, mainCt);
            ResetAllControls();
        }
        finally
        {
            Utils.TraceOut();
        }
    }

    private void ReadyToScan(FileSystemWatcherEventArgs e, SearchOption searchOption, bool scanPathForVideoCodec, CancellationToken ct)
    {
        try
        {
            Utils.TraceIn($"e.Directories = {e.Directories.Length}");
            var toSave = false;
            if (e.Directories.Length == 0) return;

            for (var i = e.Directories.Length - 1; i >= 0; i--)
            {
                var directoryToScan = e.Directories[i];

                if (Utils.StringContainsFixedSpace(directoryToScan.Path))
                {
                    Utils.LogWithPushover(BackupAction.ScanDirectory, PushoverPriority.High, $"{directoryToScan.Path} contains a fixed space");
                    return;
                }
                mediaBackup.ClearFlags();
                var fileCountInDirectoryBefore = mediaBackup.BackupFiles.Count(b => b.FullPath.StartsWithIgnoreCase(directoryToScan.Path));

                if (ScanSingleDirectory(directoryToScan.Path, searchOption, scanPathForVideoCodec, ct))
                {
                    UpdateSymbolicLinkForDirectory(directoryToScan.Path);

                    // instead of removing files that are no longer found in a directory we now flag them as deleted so we can report them later
                    // unless they aren't on a backup disk in which case they are removed now
                    BackupFile[] filesToRemoveOrMarkDeleted;

                    if (searchOption == SearchOption.TopDirectoryOnly)
                    {
                        filesToRemoveOrMarkDeleted = mediaBackup.BackupFiles.Where(static b => !b.Flag).Where(b => b.FullPath.StartsWithIgnoreCase(directoryToScan.Path))
                            .Where(b => !b.FullPath.SubstringAfterIgnoreCase(Utils.EnsurePathHasATerminatingSeparator(directoryToScan.Path)).Contains('\\')).ToArray();
                    }
                    else
                        filesToRemoveOrMarkDeleted = mediaBackup.BackupFiles.Where(static b => !b.Flag).Where(b => b.FullPath.StartsWith(directoryToScan.Path, StringComparison.InvariantCultureIgnoreCase)).ToArray();
                    RemoveOrDeleteFiles(filesToRemoveOrMarkDeleted);
                    var fileCountAfter = mediaBackup.BackupFiles.Count(b => b.FullPath.StartsWithIgnoreCase(directoryToScan.Path));
                    var filesNotOnBackupDiskCount = mediaBackup.GetBackupFilesWithDiskEmpty().Count();
                    var text = $"Directory scan {directoryToScan.Path} completed. {fileCountInDirectoryBefore} files before and now {fileCountAfter} files. {filesNotOnBackupDiskCount} to backup.";
                    Utils.Log(BackupAction.ScanDirectory, text);
                    toSave = true;
                }
                else
                {
                    var text = string.Format(Resources.DirectoryScanSkipped, Utils.FormatTimeFromSeconds(mediaBackup.Config.DirectoriesScanTimer / 1000));
                    Utils.LogWithPushover(BackupAction.ScanDirectory, text, true);
                    _ = mediaBackup.Watcher.DirectoriesToScan.AddOrUpdate(directoryToScan);
                }
            }

            if (toSave)
            {
                //   mediaBackup.Save(ct);
                UpdateStatusLabel(ct, Resources.Saved);
                UpdateUI_Tick(null, null);
                UpdateMediaFilesCountDisplay();
            }
            UpdateStatusLabel(ct);
        }
        finally
        {
            Utils.TraceOut();
        }
    }

    private void RemoveOrDeleteFiles(BackupFile[] files)
    {
        lock (_lock)
        {
            Utils.TraceIn();

            for (var j = files.Length - 1; j >= 0; j--)
            {
                var backupFile = files[j];

                if (backupFile.Disk.HasNoValue())
                {
                    Utils.Trace($"Removing {backupFile.FullPath}");
                    mediaBackup.RemoveFile(backupFile);
                }
                else
                {
                    Utils.Trace($"Marking {backupFile.FullPath} as Deleted");
                    backupFile.Deleted = true;
                }
            }
            Utils.TraceOut();
        }
    }

    private void Main_FormClosing(object sender, FormClosingEventArgs e)
    {
        ResetTokenSource();
        mediaBackup.Save(mainCt);
    }

    private void CheckConnectedBackupDiskButton_Click(object sender, EventArgs e)
    {
        Utils.TraceIn();
        ResetTokenSource();
        _ = TaskWrapper(() => CheckConnectedDiskAndCopyFilesAsync(false, false, mainCt), mainCt);
        Utils.TraceOut();
    }

    private void CopyFilesToBackupDiskButton_Click(object sender, EventArgs e)
    {
        Utils.TraceIn();
        ResetTokenSource();
        _ = TaskWrapper(() => CopyFilesAsync(true, mainCt), mainCt);
        Utils.TraceOut();
    }

    private void ListFilesNotOnBackupDiskButton_Click(object sender, EventArgs e)
    {
        Utils.TraceIn();
        IEnumerable<BackupFile> filesNotOnBackupDisk = mediaBackup.GetBackupFilesWithDiskEmpty().OrderByDescending(static p => p.Length);
        Utils.Log("Listing files not on a backup disk");
        var notOnBackupDisk = filesNotOnBackupDisk.ToArray();

        foreach (var file in notOnBackupDisk)
        {
            Utils.Log($"{file.FullPath} at {file.Length.SizeSuffix()}");
        }
        Utils.Log($"{notOnBackupDisk.Length} files at {notOnBackupDisk.Sum(static p => p.Length).SizeSuffix()}");
        Utils.TraceOut();
    }

    private void RecalculateAllHashesButton_Click(object sender, EventArgs e)
    {
        Utils.TraceIn();

        if (MessageBox.Show(Resources.RecalculateAllHashes, Resources.RecalculateAllHashesTitle, MessageBoxButtons.YesNo) == DialogResult.Yes)
        {
            foreach (var backupFile in mediaBackup.BackupFiles)
            {
                backupFile.UpdateContentsHash();
            }
            mediaBackup.Save(mainCt);
        }
        Utils.TraceOut();
    }

    private void VersionCheckingButton_Click(object sender, EventArgs e)
    {
        Utils.TraceIn();
        mediaBackup.Config.MonitoringCheckLatestVersions = !mediaBackup.Config.MonitoringCheckLatestVersions;
        versionCheckingButton.Text = string.Format(Resources.VersionCheckingButton, mediaBackup.Config.MonitoringCheckLatestVersions ? Resources.ON : Resources.OFF);
        Utils.TraceOut();
    }

    private void DirectoriesScanReportButton_Click(object sender, EventArgs e)
    {
        DirectoryScanReport(DirectoryScanType.GetFiles, 10);
        DirectoryScanReport(DirectoryScanType.ProcessingFiles, 10);
    }

    private void DirectoryScanReport(DirectoryScanType scanType, int howMany)
    {
        Utils.TraceIn();
        var scanIdsList = mediaBackup.GetLastScans(scanType, howMany);
        var scans = mediaBackup.DirectoryScans.Where(s => scanIdsList.Contains(s.Id) && s.TypeOfScan == scanType).ToArray();
        if (scans.Length == 0) return;

        mediaBackup.DeleteScanReportsNotInList(scanIdsList);
        const int columnWidth = 8;
        var distinctDirectoriesScans = scans.Distinct().OrderBy(static s => s.Path).ToArray();
        var longestDirectoryLength = scans.Select(static d => d.Path.Length).Max();
        var headerLine1 = string.Empty.PadRight(longestDirectoryLength + columnWidth + 2);
        var headerLine2 = string.Empty.PadRight(longestDirectoryLength + columnWidth + 2);
        var totalLine = "Total time:" + string.Empty.PadRight(longestDirectoryLength + columnWidth - 9);
        var lapsedTimeLine = "Lapsed time:" + string.Empty.PadRight(longestDirectoryLength + columnWidth - 10);
        var fileCountsLine = "File count:" + string.Empty.PadRight(longestDirectoryLength + columnWidth - 9);
        var totals = new TimeSpan[howMany];
        var lapsedTime = new TimeSpan[howMany];
        var fileCounts = new int[howMany];
        var previousId = string.Empty;

        // prepare the header lines
        foreach (var b in scans.OrderBy(static s => s.StartDateTime).Where(b => b.Id != previousId))
        {
            // build the line of scan dates like 30.11 01.12 etc
            headerLine1 += b.StartDateTime.ToString("dd.MM").PadLeft(columnWidth);
            headerLine2 += b.StartDateTime.ToString("HH.mm").PadLeft(columnWidth);
            previousId = b.Id;
        }
        var textLines = string.Empty;

        foreach (var directory in distinctDirectoriesScans)
        {
            var scansForDirectory = scans.Where(scan => scan.Path == directory.Path && scan.TypeOfScan == scanType).ToArray();
            var fileCount = mediaBackup.GetBackupFilesInDirectory(directory.Path, false).Count();
            textLines += $"{directory.Path.PadRight(longestDirectoryLength + 1)} {fileCount,columnWidth:n0}";

            for (var i = 0; i < scanIdsList.Length; i++)
            {
                var backup = scansForDirectory.FirstOrDefault(scan => scan.Id == scanIdsList[i]);

                if (backup == null)
                    textLines += "--".PadLeft(columnWidth);
                else
                {
                    totals[i] += backup.ScanDuration;
                    textLines += $"{Utils.FormatTimeSpanMinutesOnly(backup.ScanDuration),columnWidth}";
                }
            }
            textLines += "\n";
        }

        for (var index = 0; index < scanIdsList.Length; index++)
        {
            lapsedTime[index] = DirectoryScan.LapsedTime(

                // ReSharper disable once AccessToModifiedClosure
                mediaBackup.DirectoryScans.Where(s => s.Id == scanIdsList[index] && s.TypeOfScan == scanType));
            fileCounts[index] = mediaBackup.DirectoryScans.Where(s => s.Id == scanIdsList[index] && s.TypeOfScan == scanType).Sum(static directoryScan => directoryScan.TotalFiles);
        }
        fileCountsLine = fileCounts.Where(static aTotal => aTotal > -1).Aggregate(fileCountsLine, static (current, aTotal) => current + $"{aTotal,columnWidth:n0}");
        lapsedTimeLine = lapsedTime.Where(static aTotal => aTotal > TimeSpan.FromSeconds(0)).Aggregate(lapsedTimeLine, static (current, aTotal) => current + $"{Utils.FormatTimeSpanMinutesOnly(aTotal),columnWidth}");
        totalLine = totals.Where(static aTotal => aTotal > TimeSpan.FromSeconds(0)).Aggregate(totalLine, static (current, aTotal) => current + $"{Utils.FormatTimeSpanMinutesOnly(aTotal),columnWidth}");
        Utils.Log($"{scanType} report:");
        Utils.Log(headerLine1);
        Utils.Log(headerLine2);
        Utils.Log(textLines);
        Utils.Log(fileCountsLine);
        Utils.Log(totalLine);
        Utils.Log(lapsedTimeLine);
        Utils.TraceOut();
    }

    /// <summary>
    ///     Opens the current log file in Notepad
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void OpenLogFileButton_Click(object sender, EventArgs e)
    {
        Utils.OpenLogFile();
    }

    private void ScanDirectoryButton_Click(object sender, EventArgs e)
    {
        try
        {
            Utils.TraceIn();
            if (scanDirectoryComboBox.SelectedIndex <= -1 || longRunningActionExecutingRightNow) return;

            ResetTokenSource();
            _ = TaskWrapper(() => ScanDirectoryAsync(scanDirectoryComboBox.Text, mainCt), mainCt);
        }
        finally
        {
            Utils.TraceOut();
        }
    }

    private void RootDirectoryChecks(Collection<string> directories, CancellationToken ct)
    {
        Utils.TraceIn();
        var directoriesChecked = new HashSet<string>();

        foreach (var directory in directories)
        {
            UpdateStatusLabel(ct, string.Format(Resources.Scanning, directory));

            if (Utils.Directory.Exists(directory))
            {
                if (Utils.Directory.IsWritable(directory))
                {
                    var rootPath = Utils.GetRootPath(directory);
                    if (directoriesChecked.Contains(rootPath)) continue;

                    RootDirectoryChecks(rootPath, ct);
                    _ = directoriesChecked.Add(rootPath);
                }
                else
                    Utils.LogWithPushover(BackupAction.ScanDirectory, PushoverPriority.High, $"{directory} is not writable");
            }
            else
                Utils.LogWithPushover(BackupAction.ScanDirectory, PushoverPriority.High, $"{directory} doesn't exist");
        }
        Utils.TraceOut();
    }

    private void ProcessFilesButton_Click(object sender, EventArgs e)
    {
        Utils.TraceIn();
        ResetTokenSource();
        _ = TaskWrapper(() => ProcessFilesAsync(mainCt), mainCt);
        Utils.TraceOut();
    }

    private void DirectoryScanReportLastRunOnlyButton_Click(object sender, EventArgs e)
    {
        Utils.TraceIn();
        DirectoryScanReport(DirectoryScanType.GetFiles, 1);
        DirectoryScanReport(DirectoryScanType.ProcessingFiles, 1);
        Utils.TraceOut();
    }

    private void DvProfile5CheckButton_Click(object sender, EventArgs e)
    {
        Utils.TraceIn();
        var files = mediaBackup.BackupFiles.Where(static f => f.FullPath.Contains("[DV]") && !f.Deleted).OrderBy(static f => f.FullPath).ToArray();

        if (files.Length > 0)
        {
            foreach (var file in files)
            {
                if (Utils.File.Exists(file.FullPath))
                    Utils.Log(Utils.File.IsDolbyVisionProfile5(file.FullPath) ? $"{file.FullPath} is DV Profile 5" : $"{file.FullPath} is DV but not Profile 5");
                else
                {
                    Utils.Log($"{file.FullPath} not found");
                    Utils.Log($"{Utils.StringContainsFixedSpace(file.FullPath)} for FixedSpace");
                }
            }
        }
        else
            Utils.Log("No DV files found");
        Utils.TraceOut();
    }

    private void CreateNewBackupDiskButton_Click(object sender, EventArgs e)
    {
        Utils.TraceIn();

        try
        {
            if (MessageBox.Show(Resources.PrepareNewBackupDiskAreYouSure, Resources.CreateNewDisk, MessageBoxButtons.YesNo) != DialogResult.Yes) return;

            // get the next unused disk number
            // rename the disk
            // turn all recycle bin
            // create the backup folder
            // create the share
            // scan the disk
            // save the disk info
            // redraw UI
            var directoryInfo = new DirectoryInfo(backupDiskTextBox.Text);

            if (directoryInfo.GetFileSystemInfos("*", SearchOption.TopDirectoryOnly).Where(static x => (x.Attributes & FileAttributes.Hidden) == 0).Any())
            {
                _ = MessageBox.Show(Resources.DiskNotEmpty, Resources.DiskNotEmptyTitle, MessageBoxButtons.OK);
                return;
            }
            var i = 1;

            while (DiskNumberFound(i))
            {
                i++;
            }
            var newDiskName = $"Backup {i}";

            //create the directory
            _ = Utils.Directory.CreateDirectory(Path.Combine(backupDiskTextBox.Text, newDiskName.ToLowerInvariant()));

            //rename the disk
            _ = new DriveInfo(backupDiskTextBox.Text) { VolumeLabel = newDiskName };

            // create the share
            const string shareName = "backup";
            var tempShare = Win32Share.GetNamedShare(shareName);
            if (tempShare != null) _ = tempShare.Delete();
            _ = Utils.ShareFolder(backupDiskTextBox.Text, shareName, string.Empty);
            var domain = Environment.UserDomainName;
            Utils.AddPermissions(shareName, domain, "Everyone");
            var disk = mediaBackup.GetBackupDisk(backupDiskTextBox.Text);
            _ = disk.Update(mediaBackup.BackupFiles);
            mediaBackup.Save(mainCt);
        }
        finally
        {
            Utils.TraceOut();
        }
    }

    private bool DiskNumberFound(int diskNumber)
    {
        return mediaBackup.BackupFiles.Any(f => f.BackupDiskNumber == diskNumber);
    }

    private void VideoFilesCheckNameButton_Click(object sender, EventArgs e)
    {
        var files = mediaBackup.BackupFiles.Where(static bf =>
        {
            ArgumentException.ThrowIfNullOrEmpty(bf.FullPath);
            ArgumentException.ThrowIfNullOrEmpty(bf.FullPath);
            return !bf.Deleted && (Utils.File.IsVideo(bf.FullPath) || Utils.File.IsSubtitles(bf.FullPath));
        }).ToArray();

        for (var index = 0; index < files.Length; index++)
        {
            var fullPath = files[index].FullPath;
            Utils.Log($"[{index}/{files.Length}] {fullPath}");
            if (!Utils.File.Exists(fullPath)) continue;

            _ = Utils.MediaHelper.CheckVideoFileAndRenameIfRequired(ref fullPath);
        }
    }

    private void H264FilesButton_Click(object sender, EventArgs e)
    {
        var files = mediaBackup.BackupFiles.Where(static file =>
        {
            ArgumentException.ThrowIfNullOrEmpty(file.FullPath);
            return Utils.File.IsVideo(file.FullPath) && !Utils.File.IsSpecialFeature(file.FullPath) && file.FullPath.Contains("_TV") && !file.FullPath.ContainsAny("[h265]", "[VP9]", "[VC1]") && !file.Deleted;
        }).ToArray();
        var totalSize = files.Sum(static file => file.Length);
        Utils.Log($"Total size of {files.Length} TV files is {totalSize.SizeSuffix()}");

        files = mediaBackup.BackupFiles.Where(static file =>
        {
            ArgumentException.ThrowIfNullOrEmpty(file.FullPath);
            return Utils.File.IsVideo(file.FullPath) && !Utils.File.IsSpecialFeature(file.FullPath) && file.FullPath.Contains("_Movies") && !file.FullPath.ContainsAny("[Remux-1080p]", "[h265]", "[VP9]", "[VC1]") && !file.Deleted;
        }).ToArray();
        totalSize = files.Sum(static file => file.Length);
        Utils.Log($"Total size of {files.Length} of Movie files is {totalSize.SizeSuffix()}");
    }

    private void ScanDirectoriesWithChangesButton_Click(object sender, EventArgs e)
    {
        try
        {
            Utils.TraceIn();
            if (longRunningActionExecutingRightNow) return;

            ResetTokenSource();
            _ = TaskWrapper(() => ScanDirectoriesWithChangesAsync(mainCt), mainCt);
        }
        finally
        {
            Utils.TraceOut();
        }
    }

    private void ScanDirectoriesWithChangesAsync(CancellationToken ct)
    {
        if (longRunningActionExecutingRightNow) return;

        DisableControlsForAsyncTasks(ct);
        var dirsToScan = mediaBackup.Watcher.DirectoriesToScan.ToArray();
        ReadyToScan(new FileSystemWatcherEventArgs(dirsToScan), SearchOption.AllDirectories, true, ct);

        // Empty the DirectoriesToScan because we've processed all of them now
        // we do it here so if we get cancelled before this we leave the directories ready to scan for next time
        foreach (var a in dirsToScan)
        {
            _ = mediaBackup.Watcher.DirectoriesToScan.Remove(a);
        }
        ResetAllControls();
        UpdateUI_Tick(null, null);
        UpdateMediaFilesCountDisplay();
    }

    private void ScanLastFilesButton_Click(object sender, EventArgs e)
    {
        try
        {
            Utils.TraceIn();
            if (longRunningActionExecutingRightNow) return;

            ResetTokenSource();
            _ = TaskWrapper(() => ScanLastFilesForChangesAsync(mainCt), mainCt);
        }
        finally
        {
            Utils.TraceOut();
        }
    }

    private void ScanLastFilesForChangesAsync(CancellationToken ct)
    {
        if (longRunningActionExecutingRightNow) return;

        var count = mediaBackup.Config.FilesToScanForChanges;
        DisableControlsForAsyncTasks(ct);
        Utils.LogWithPushover(BackupAction.General, $"Scanning {count} files for changed directories");
        ReadyToScan(new FileSystemWatcherEventArgs(mediaBackup.BackupFiles.OrderBy(static f => f.LastWriteTime).TakeLast(count).ToArray()), SearchOption.AllDirectories, true, ct);
        ResetAllControls();
        UpdateUI_Tick(null, null);
        UpdateMediaFilesCountDisplay();
    }

    private void CheckSubtitlesButton_Click(object sender, EventArgs e)
    {
        var files = mediaBackup.BackupFiles.Where(static f => f.Extension == ".srt");
        var backupFiles = files as BackupFile[] ?? files.ToArray();
        Utils.LogWithPushover(BackupAction.General, $" {backupFiles.Length} subtitle files");

        foreach (var file in backupFiles)
        {
            var subFile = new SubtitlesBackupFile(file.FullPath);
            if (subFile.Language != "en" && subFile.Language != "es") Utils.LogWithPushover(BackupAction.General, $" {file.FullPath} has no language");
            ExtendedBackupFileBase ext = subFile;
            if (file.FileName != ext.Title + subFile.SubtitlesExtension) Utils.LogWithPushover(BackupAction.General, $" {file.FullPath} name error");
            if (subFile.Forced) Utils.LogWithPushover(BackupAction.General, $" {file.FullPath} is Forced ");
        }
    }

    private void ExportAndRemoveSubtitlesButton_Click(object sender, EventArgs e)
    {
        var count = 0;
        var csvConfiguration = new CsvConfiguration(CultureInfo.InvariantCulture);
        IEnumerable<TdarrTranscodeCancelled> records;
        var testDataDirectory = Path.GetFullPath(Path.Combine(Utils.GetProjectPath(typeof(Main)), @"..\TestProject\TestData"));

        using (var reader = new StreamReader(Path.Combine(testDataDirectory, "Transcode_ Error_Cancelled.csv")))
        using (var csv = new CsvReader(reader, csvConfiguration))
        {
            records = csv.GetRecords<TdarrTranscodeCancelled>().ToArray();
        }

        foreach (var fullPath in from record in records select Path.GetFullPath(record.Id) into fullPath where File.Exists(fullPath) where !Utils.File.IsSpecialFeature(fullPath) where Utils.MediaHelper.HasSubtitles(fullPath) select fullPath)
        {
            Utils.Log($"{fullPath} is video with subtitles");
            if (!Utils.MediaHelper.ExtractSubtitleFiles(fullPath)) continue;

            var ext = Path.GetExtension(fullPath);
            var directoryName = Path.GetDirectoryName(fullPath);

            if (directoryName != null)
            {
                var newPath = Path.Combine(directoryName, ".new" + ext);
                if (!Utils.MediaHelper.RemoveSubtitlesFromFile(fullPath, newPath)) continue;

                _ = Utils.File.Move(fullPath, fullPath + ".original");
                _ = Utils.File.Move(newPath, fullPath);
            }
            count++;
            Utils.Log($"Count is {count}");
            Utils.Log($"Processed {fullPath}");
        }
        Utils.Log($"{count} files that were exported from Tdarr");
    }

    private void ExtractChaptersButton_Click(object sender, EventArgs e)
    {
        // export chapters and remove them
        const string ext = ".mp4";

        // find mp4 files that have chapters
        var backupFiles = mediaBackup.BackupFiles.Where(static f => f.Extension == ext && !f.FullPath.Contains("[h265]")).OrderBy(static q => q.FullPath).ToArray();
        var count = 0;

        foreach (var file in backupFiles)
        {
            if (!File.Exists(file.FullPath)) continue;

            var mediaFile = Utils.MediaHelper.ExtendedBackupFileBase(file.FullPath);
            if (mediaFile is not TvEpisodeBackupFile) continue;
            if (Utils.File.IsSpecialFeature(file.FullPath)) continue;

            _ = mediaFile.RefreshMediaInfo();
            if (!Utils.MediaHelper.HasChapters(file.FullPath)) continue;

            Utils.Log($"{file.FullPath} is TV episode with chapters");
            var fileFullPath = file.FullPath + ".chap";
            if (File.Exists(fileFullPath)) continue;
            if (!Utils.MediaHelper.ExtractChapters(file.FullPath, fileFullPath)) continue;

            var newPath = Path.Combine(file.Directory, ".new" + ext);
            if (!Utils.MediaHelper.RemoveChaptersFromFile(file.FullPath, newPath)) continue;

            _ = Utils.File.Move(file.FullPath, file.FullPath + ".original");
            _ = Utils.File.Move(newPath, file.FullPath);
            count++;
            Utils.Log($"Count is {count}");
        }
        Utils.Log($"{count} files that are not [h265] TV episodes with chapters");
    }

    private void RemoveMetadataButton_Click(object sender, EventArgs e)
    {
        var count = 0;
        var csvConfiguration = new CsvConfiguration(CultureInfo.InvariantCulture);
        IEnumerable<TdarrTranscodeCancelled> records;
        var testDataDirectory = Path.GetFullPath(Path.Combine(Utils.GetProjectPath(typeof(Main)), @"..\TestProject\TestData"));

        using (var reader = new StreamReader(Path.Combine(testDataDirectory, "Transcode_ Error_Cancelled.csv")))
        using (var csv = new CsvReader(reader, csvConfiguration))
        {
            records = csv.GetRecords<TdarrTranscodeCancelled>().ToArray();
        }

        foreach (var fullPath in from record in records select Path.GetFullPath(record.Id) into fullPath where File.Exists(fullPath) where !Utils.File.IsSpecialFeature(fullPath) where Utils.MediaHelper.HasChapters(fullPath) select fullPath)
        {
            if (!File.Exists(fullPath)) continue;

            var ext = Path.GetExtension(fullPath);
            var directoryName = Path.GetDirectoryName(fullPath);
            Utils.Log($"{fullPath} is video with chapters");

            if (directoryName != null)
            {
                var newPath = Path.Combine(directoryName, ".new" + ext);
                if (!Utils.MediaHelper.RemoveChaptersFromFile(fullPath, newPath)) continue;

                _ = Utils.File.Move(fullPath, fullPath + ".original2");
                _ = Utils.File.Move(newPath, fullPath);
            }
            count++;
            Utils.Log($"Count is {count}");
        }
        Utils.Log($"{count} files that are not [h265] video with chapters removed");

        foreach (var fullPath in from record in records select Path.GetFullPath(record.Id) into fullPath where File.Exists(fullPath) where !Utils.File.IsSpecialFeature(fullPath) where Utils.MediaHelper.HasMetadata(fullPath) select fullPath)
        {
            if (!File.Exists(fullPath)) continue;

            var ext = Path.GetExtension(fullPath);
            var directoryName = Path.GetDirectoryName(fullPath);
            Utils.Log($"{fullPath} is video with metadata");

            if (directoryName != null)
            {
                var newPath = Path.Combine(directoryName, ".new" + ext);
                if (!Utils.MediaHelper.RemoveMetadataFromFile(fullPath, newPath)) continue;

                _ = Utils.File.Move(fullPath, fullPath + ".original1");
                _ = Utils.File.Move(newPath, fullPath);
            }
            count++;
            Utils.Log($"Count is {count}");
        }
        Utils.Log($"{count} files that are not [h265] video with metadata removed");
    }
}