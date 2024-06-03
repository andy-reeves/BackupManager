// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="Main.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

using BackupManager.Entities;
using BackupManager.Extensions;
using BackupManager.Properties;

namespace BackupManager;

internal sealed partial class Main : Form
{
    private void FileSystemWatcher_OnError(object sender, ErrorEventArgs e)
    {
        var ex = e.GetException();
        Utils.LogWithPushover(BackupAction.Error, PushoverPriority.High, $"Message: {ex.Message}");

        try
        {
            // the most common is DirectoryNotFound for a network path
            // wait a bit and then attempt restart the watcher
            var ct = new CancellationToken();
            Task.Delay(mediaBackup.Config.DirectoriesFileChangeWatcherRestartDelay, ct).Wait(ct);
            _ = mediaBackup.Watcher.Reset();
            _ = mediaBackup.Watcher.Start();
        }
        catch (Exception exc)
        {
            Utils.LogWithPushover(BackupAction.Error, PushoverPriority.High, $"Unable to Reset FileSystemWatcher {exc}");
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

    private void CheckForOldBackupDisks_Click(object sender, EventArgs e)
    {
        Utils.TraceIn();
        CheckForOldBackupDisks();
        Utils.TraceOut();
    }

    private void ScanAllDirectoriesButton_Click(object sender, EventArgs e)
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
        Utils.TraceOut();
    }

    private void RestoreFilesButton_Click(object sender, EventArgs e)
    {
        Utils.TraceIn();

        // loop through all the files looking for the directory specified in the top drop down and copy to the bottom drop down 
        // for each file order by backup disk
        // prompt for the back up disk to be inserted 
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
                                Utils.LogWithPushover(BackupAction.Restore, PushoverPriority.High, $"[{fileCounter}/{countOfFiles}] {sourceFileFullPath} doesn't exist");
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
        Dictionary<string, BackupFile> allMovies = new();
        List<BackupFile> backupFilesWithDuplicates = new();

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
        ListBackupDiskStatus(mediaBackup.BackupDisks.OrderByDescending(static d => d.Free));
        Utils.TraceOut();
    }

    private void ListBackupDiskStatusByDiskNumberButton_Click(object sender, EventArgs e)
    {
        Utils.TraceIn();
        ListBackupDiskStatus(mediaBackup.BackupDisks.OrderBy(static d => d.Number));
        Utils.TraceOut();
    }

    private void ListBackupDiskStatus(IOrderedEnumerable<BackupDisk> disks)
    {
        Utils.Log("Listing backup disk statuses");
        Utils.Log("Name        Checked    Capacity   Used     Free    Files  Deleted   Diff      %");

        foreach (var disk in disks)
        {
            var lastChecked = string.Empty;

            if (disk.Checked.HasValue())
            {
                var d = DateTime.Parse(disk.Checked);
                lastChecked = d.ToString(Resources.DateTime_ddMMMyy);
            }
            var backupFilesOnBackupDiskNotIncludingDeleted = mediaBackup.GetBackupFilesOnBackupDisk(disk.Name, false).ToArray();
            var totalSizeOfFilesFromSumOfFiles = backupFilesOnBackupDiskNotIncludingDeleted.Sum(static p => p.Length);
            var deletedCount = mediaBackup.GetBackupFilesOnBackupDisk(disk.Name, true).Count() - backupFilesOnBackupDiskNotIncludingDeleted.Length;
            var sizeFromDiskAnalysis = disk.Capacity - disk.Free;
            var difference = totalSizeOfFilesFromSumOfFiles > sizeFromDiskAnalysis ? 0 : sizeFromDiskAnalysis - totalSizeOfFilesFromSumOfFiles;
            var percentageDiff = Math.Round(difference * 100 / (double)sizeFromDiskAnalysis, 0);
            var percentString = percentageDiff is < 1 and > -1 ? "-" : $"{percentageDiff}%";

            Utils.Log($"{disk.Name,-12}{lastChecked,9}{disk.CapacityFormatted,9}{Utils.FormatSize(sizeFromDiskAnalysis),9}" + $"{disk.FreeFormatted,9}{Utils.FormatSize(totalSizeOfFilesFromSumOfFiles),9}{deletedCount,5}" +
                      $"{Utils.FormatSize(difference),12}{percentString,5}");
        }
        var totalSizeFormatted = Utils.FormatSize(mediaBackup.BackupDisks.Sum(static p => p.Capacity));
        var totalFreeSpaceFormatted = Utils.FormatSize(mediaBackup.BackupDisks.Sum(static p => p.Free));
        Utils.Log($"\n      Total Capacity: {totalSizeFormatted,8}     Free: {totalFreeSpaceFormatted,7}");
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
                Utils.LogWithPushover(BackupAction.ApplicationMonitoring, $"Starting in {Utils.FormatTimeFromSeconds(mediaBackup.Config.MonitoringInterval)}");
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

    [SupportedOSPlatform("windows")]
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
                    Utils.LogWithPushover(BackupAction.ApplicationMonitoring, PushoverPriority.Normal, $"Stopping all '{toKill}' processes that match");
                    _ = Utils.KillProcesses(toKill);
                }
            }
            if (monitor.ServiceToRestart.HasNoValue()) continue;

            Utils.LogWithPushover(BackupAction.ApplicationMonitoring, PushoverPriority.Normal, $"Stopping '{monitor.ServiceToRestart}'");
            if (!Utils.StopService(monitor.ServiceToRestart, 5000)) Utils.LogWithPushover(BackupAction.ApplicationMonitoring, PushoverPriority.High, string.Format(Resources.FailedToStopTheService, monitor.Name));
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

    [SupportedOSPlatform("windows")]
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
                    Utils.LogWithPushover(BackupAction.ApplicationMonitoring, PushoverPriority.Normal, $"Stopping all '{toKill}' processes that match");
                    _ = Utils.KillProcesses(toKill);
                }
            }

            if (monitor.ServiceToRestart.HasValue())
            {
                Utils.LogWithPushover(BackupAction.ApplicationMonitoring, PushoverPriority.Normal, $"Stopping '{monitor.ServiceToRestart}'");
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
            Dictionary<string, BackupFile> allFilesUniqueContentsHash = new();
            List<BackupFile> backupFilesWithDuplicates = new();
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
            Utils.Log($"{file.FullPath} at {Utils.FormatSize(file.Length)} on {file.Disk}");
        }
        Utils.Log($"{backupFiles.Length} files at {Utils.FormatSize(backupFiles.Sum(static p => p.Length))}");
        Utils.Log("Listing files marked as deleted ordered by size on backup disk");

        foreach (var d in backupFiles.Select(static f => f.Disk).Distinct().ToDictionary(static disk => disk, disk => backupFiles.Where(f => f.Disk == disk).Sum(static f => f.Length)).OrderByDescending(static i => i.Value))
        {
            Utils.Log($"{d.Key,-10} has {Utils.FormatSize(d.Value),-8}");
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
            if (!e.Directories.Any()) return;

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
                    var text = string.Format(Resources.DirectoryScanSkipped, Utils.FormatTimeFromSeconds(mediaBackup.Config.DirectoriesScanTimer));
                    Utils.LogWithPushover(BackupAction.ScanDirectory, text, true);
                    _ = mediaBackup.Watcher.DirectoriesToScan.AddOrUpdate(directoryToScan);
                }
            }

            if (toSave)
            {
                mediaBackup.Save(ct);
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

    private void RemoveOrDeleteFiles(IReadOnlyList<BackupFile> files)
    {
        lock (_lock)
        {
            Utils.TraceIn();

            for (var j = files.Count - 1; j >= 0; j--)
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
        if (mediaBackup.Watcher.DirectoriesToScan.Count <= 0 && mediaBackup.Watcher.FileSystemChanges.Count <= 0) return;

        // If file or directory changes were detected so save xml
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
            Utils.Log($"{file.FullPath} at {Utils.FormatSize(file.Length)}");
        }
        Utils.Log($"{notOnBackupDisk.Length} files at {Utils.FormatSize(notOnBackupDisk.Sum(static p => p.Length))}");
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
        var scanIdsList = mediaBackup.GetLastScans(mediaBackup.DirectoryScans.ToArray(), scanType, howMany);
        var scans = mediaBackup.DirectoryScans.Where(s => scanIdsList.Contains(s.Id) && s.TypeOfScan == scanType).ToArray();
        if (scans.Length == 0) return;

        const int columnWidth = 7;
        var distinctDirectoriesScans = scans.Distinct().OrderBy(static s => s.Path).ToArray();
        var longestDirectoryLength = scans.Select(static d => d.Path.Length).Max();
        var headerLine1 = string.Empty.PadRight(longestDirectoryLength + columnWidth + 4);
        var headerLine2 = string.Empty.PadRight(longestDirectoryLength + columnWidth + 4);
        var totalLine = "Total time:" + string.Empty.PadRight(longestDirectoryLength - 2);
        var lapsedTimeLine = "Lapsed time:" + string.Empty.PadRight(longestDirectoryLength - columnWidth + 4);
        var fileCountsLine = "File count:" + string.Empty.PadRight(longestDirectoryLength - columnWidth + 5);
        var totals = new TimeSpan[howMany];
        var lapsedTime = new TimeSpan[howMany];
        var fileCounts = new int[howMany];
        var previousId = string.Empty;

        // prepare the header lines
        foreach (var b in scans.OrderBy(static s => s.StartDateTime).Where(b => b.Id != previousId))
        {
            // build the line of scan dates like 30.11 01.12 etc
            headerLine1 += b.StartDateTime.ToString("dd.MM  ");
            headerLine2 += b.StartDateTime.ToString("HH.mm  ");
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

        if (files.Any())
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

    [SupportedOSPlatform("windows")]
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

            Utils.MediaHelper.CheckVideoFileAndRenameIfRequired(ref fullPath);
        }
    }

    private void H264FilesButton_Click(object sender, EventArgs e)
    {
        var files = mediaBackup.BackupFiles.Where(static file =>
        {
            ArgumentException.ThrowIfNullOrEmpty(file.FullPath);
            return Utils.File.IsVideo(file.FullPath) && file.FullPath.Contains("_TV") && !file.FullPath.Contains("[h265");
        }).ToArray();
        var totalSize = files.Sum(static file => file.Length);
        Utils.Log($"Total size of {files.Length} TV files is {Utils.FormatSize(totalSize)}");

        files = mediaBackup.BackupFiles.Where(static file =>
        {
            ArgumentException.ThrowIfNullOrEmpty(file.FullPath);
            return Utils.File.IsVideo(file.FullPath) && file.FullPath.Contains("_Movies") && !file.FullPath.Contains("[h265]");
        }).ToArray();
        totalSize = files.Sum(static file => file.Length);
        Utils.Log($"Total size of {files.Length} of Movie files is {Utils.FormatSize(totalSize)}");
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
}