// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="Main.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

using BackupManager.Entities;
using BackupManager.Extensions;
using BackupManager.Properties;

namespace BackupManager;

internal sealed partial class Main : Form
{
    private static readonly object _lock = new();

    private int currentPercentComplete;

    private BlockingCollection<DirectoryScan> directoryScanBlockingCollection;

    private BlockingCollection<string> fileBlockingCollection;

    private int fileCounterForMultiThreadProcessing;

    private int reportedPercentComplete;

    private void FileSystemWatcher_OnError(object sender, ErrorEventArgs e)
    {
        var ex = e.GetException();
        Utils.LogWithPushover(BackupAction.General, PushoverPriority.High, $"Message: {ex.Message}");

        try
        {
            // the most common is DirectoryNotFound for a network path
            // wait a bit and then attempt restart the watcher
            Task.Delay(mediaBackup.Config.DirectoriesFileChangeWatcherRestartDelay * 1000, ct).Wait(ct);
            _ = mediaBackup.Watcher.Reset();
            _ = mediaBackup.Watcher.Start();
        }
        catch (Exception exc)
        {
            Utils.LogWithPushover(BackupAction.General, PushoverPriority.High, $"Unable to Reset FileSystemWatcher {exc}");
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
        tokenSource?.Dispose();
        tokenSource = new CancellationTokenSource();
        ct = tokenSource.Token;
        _ = TaskWrapperAsync(ScheduledBackupAsync);
        Utils.TraceOut();
    }

    private void BackupTimerButton_Click(object sender, EventArgs e)
    {
        Utils.TraceIn();
        mediaBackup.Config.ScheduledBackupOnOff = !mediaBackup.Config.ScheduledBackupOnOff;
        SetupDailyTrigger(mediaBackup.Config.ScheduledBackupOnOff);
        UpdateScheduledBackupButton();
        Utils.TraceOut();
    }

    private void CheckForOldBackupDisks_Click(object sender, EventArgs e)
    {
        Utils.TraceIn();
        CheckForOldBackupDisks();
        Utils.TraceOut();
    }

    private void UpdateMasterFilesButton_Click(object sender, EventArgs e)
    {
        Utils.TraceIn();

        if (!longRunningActionExecutingRightNow)
        {
            if (MessageBox.Show(Resources.Main_UpdateMasterFiles, Resources.Main_UpdateMasterFilesTitle,
                    MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                tokenSource?.Dispose();
                tokenSource = new CancellationTokenSource();
                ct = tokenSource.Token;
                _ = TaskWrapperAsync(ScanAllDirectoriesAsync);
            }
        }
        Utils.TraceOut();
    }

    private void CheckAllSymbolicLinksButton_Click(object sender, EventArgs e)
    {
        Utils.TraceIn();
        if (longRunningActionExecutingRightNow) return;

        if (MessageBox.Show(Resources.Main_RecreateAllSymbolicLinks, Resources.Main_SymbolicLinksTitle,
                MessageBoxButtons.YesNo) != DialogResult.Yes)
            return;

        tokenSource?.Dispose();
        tokenSource = new CancellationTokenSource();
        ct = tokenSource.Token;
        _ = TaskWrapperAsync(UpdateSymbolicLinksAsync);
    }

    private void CheckDiskAndDeleteButton_Click(object sender, EventArgs e)
    {
        Utils.TraceIn();

        if (MessageBox.Show(Resources.Main_AreYouSureDelete, Resources.Main_DeleteExtraTitle, MessageBoxButtons.YesNo) ==
            DialogResult.Yes)
            TaskWrapper(CheckConnectedDiskAndCopyFilesAsync, true, false);
        Utils.TraceOut();
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
            Utils.DiskSpeedTest(directory, Utils.ConvertMBtoBytes(mediaBackup.Config.SpeedTestFileSize),
                mediaBackup.Config.SpeedTestIterations, out var readSpeed, out var writeSpeed, ct);
            Utils.Log($"Testing {directory}, Read: {Utils.FormatSpeed(readSpeed)} Write: {Utils.FormatSpeed(writeSpeed)}");
        }

        foreach (var file in files)
        {
            Utils.Log($"{file.FullPath} : {file.Disk}");
            if (string.IsNullOrEmpty(file.Disk)) Utils.Log($"{file.FullPath} : not on a backup disk");
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
        if (MessageBox.Show(Resources.Main_RestoreFiles, Resources.Main_RestoreFilesTitle, MessageBoxButtons.YesNo) ==
            DialogResult.Yes)
        {
            if (directoriesComboBox.SelectedItem == null)
            {
                _ = MessageBox.Show(Resources.Main_RestoreFilesSelectDirectory, Resources.Main_RestoreFilesTitle,
                    MessageBoxButtons.OK);
                return;
            }
            var directory = directoriesComboBox.SelectedItem.ToString();

            if (restoreDirectoryComboBox.SelectedItem == null)
            {
                _ = MessageBox.Show(Resources.Main_RestoreFilesDirectoryToRestoreTo, Resources.Main_RestoreFilesTitle,
                    MessageBoxButtons.OK);
                return;
            }
            var targetDirectory = restoreDirectoryComboBox.SelectedItem.ToString();
            var files = mediaBackup.GetBackupFilesInDirectory(directory, false).Where(static p => p.Disk.HasValue());
            Utils.Log(BackupAction.Restore, $"Restoring files from directory {directory}");
            Utils.Log(BackupAction.Restore, $"Restoring files to target directory {targetDirectory}");
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
                        _ = MessageBox.Show(Resources.Main_CorrectDiskTitle, Resources.Main_RestoreFilesTitle,
                            MessageBoxButtons.OK);
                        return;
                    }

                    if (file.Disk != lastBackupDisk)
                    {
                        if (!mediaBackup.Config.DisksToSkipOnRestore.Contains(lastBackupDisk,
                                StringComparer.CurrentCultureIgnoreCase) && lastBackupDisk.HasValue())
                        {
                            mediaBackup.Config.DisksToSkipOnRestore.Add(lastBackupDisk);

                            // This is to save the backup disks we've completed so far
                            mediaBackup.Save();
                        }

                        // count the number of files on this disk
                        countOfFiles = backupFiles.Count(p => p.Disk == file.Disk);
                        fileCounter = 0;
                    }
                    fileCounter++;

                    // calculate the source path
                    // calculate the destination path
                    var sourceFileFullPath = Path.Combine(backupShare, file.Disk, Utils.GetIndexFolder(file.Directory),
                        file.RelativePath);

                    if (targetDirectory != null)
                    {
                        var targetFilePath = Path.Combine(targetDirectory, file.RelativePath);

                        if (File.Exists(targetFilePath))
                        {
                            Utils.LogWithPushover(BackupAction.Restore,
                                $"[{fileCounter}/{countOfFiles}] {targetFilePath} Already exists");
                        }
                        else
                        {
                            if (File.Exists(sourceFileFullPath))
                            {
                                Utils.LogWithPushover(BackupAction.Restore,
                                    $"[{fileCounter}/{countOfFiles}] Copying {sourceFileFullPath} as {targetFilePath}");
                                _ = Utils.FileCopy(sourceFileFullPath, targetFilePath);
                            }
                            else
                            {
                                Utils.LogWithPushover(BackupAction.Restore, PushoverPriority.High,
                                    $"[{fileCounter}/{countOfFiles}] {sourceFileFullPath} doesn't exist");
                            }
                        }

                        if (File.Exists(targetFilePath))
                        {
                            if (file.ContentsHash == Utils.GetShortMd5HashFromFile(targetFilePath))
                                file.Directory = targetDirectory;
                            else
                            {
                                Utils.LogWithPushover(BackupAction.Restore, PushoverPriority.High,
                                    $"ERROR: '{targetFilePath}' has a different Hashcode");
                            }
                        }
                    }
                }
                lastBackupDisk = file.Disk;
            }
            mediaBackup.Save();
        }
        Utils.TraceOut();
    }

    private void CheckBackupDeleteAndCopyButton_Click(object sender, EventArgs e)
    {
        Utils.TraceIn();

        if (MessageBox.Show(Resources.Main_AreYouSureDelete, Resources.Main_DeleteExtraTitle, MessageBoxButtons.YesNo) ==
            DialogResult.Yes)
            TaskWrapper(CheckConnectedDiskAndCopyFilesAsync, true, true);
        Utils.TraceOut();
    }

    private void ListMoviesWithMultipleFilesButton_Click(object sender, EventArgs e)
    {
        Utils.TraceIn();
        Utils.Log("Listing movies with multiple files in directory");
        Dictionary<string, BackupFile> allMovies = new();
        List<BackupFile> backupFilesWithDuplicates = new();

        foreach (var file in mediaBackup.BackupFiles)
        {
            var m = MoviesFilenameRegex().Match(file.FullPath);
            if (!m.Success) continue;

            var movieId = m.Groups[2].Value;

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

        Utils.LogWithPushover(BackupAction.General, PushoverPriority.Emergency, PushoverRetry.OneMinute, PushoverExpires.OneHour,
            "Emergency priority test");
        Utils.TraceOut();
    }

    private void ReportBackupDiskStatusButton_Click(object sender, EventArgs e)
    {
        Utils.TraceIn();
        IEnumerable<BackupDisk> disks = mediaBackup.BackupDisks.OrderBy(static p => p.Number);
        Utils.Log("Listing backup disk statuses");

        foreach (var disk in disks)
        {
            var lastChecked = string.Empty;

            if (disk.Checked.HasValue())
            {
                var d = DateTime.Parse(disk.Checked);
                lastChecked = d.ToString(Resources.DateTime_ddMMMyy);
            }

            var totalSizeOfFilesFromSumOfFiles =
                mediaBackup.GetBackupFilesOnBackupDisk(disk.Name, false).Sum(static p => p.Length);

            var deletedCount = mediaBackup.GetBackupFilesOnBackupDisk(disk.Name, true).Count() -
                               mediaBackup.GetBackupFilesOnBackupDisk(disk.Name, false).Count();
            var sizeFromDiskAnalysis = disk.Capacity - disk.Free;

            var difference = totalSizeOfFilesFromSumOfFiles > sizeFromDiskAnalysis
                ? 0
                : sizeFromDiskAnalysis - totalSizeOfFilesFromSumOfFiles;
            var percentageDiff = Math.Round(difference * 100 / (double)sizeFromDiskAnalysis, 0);
            var percentString = percentageDiff is < 1 and > -1 ? "-" : $"{percentageDiff}%";

            Utils.Log(
                $"{disk.Name,-11} Checked: {lastChecked,-9} Capacity: {disk.CapacityFormatted,-8} Used: {Utils.FormatSize(sizeFromDiskAnalysis),-7} Free: {disk.FreeFormatted,-7} Sum of files: {Utils.FormatSize(totalSizeOfFilesFromSumOfFiles),-8} DeletedFiles: {deletedCount,-3} Diff: {Utils.FormatSize(difference),-8} {percentString}");
        }
        var totalSizeFormatted = Utils.FormatSize(mediaBackup.BackupDisks.Sum(static p => p.Capacity));
        var totalFreeSpaceFormatted = Utils.FormatSize(mediaBackup.BackupDisks.Sum(static p => p.Free));

        Utils.Log(
            $"                         Total Capacity: {totalSizeFormatted,-22} Free: {totalFreeSpaceFormatted,-7} Sum of files: {Utils.FormatSize(mediaBackup.BackupFiles.Sum(static p => p.Length))}");
        Utils.TraceOut();
    }

    private void SpeedTestButton_Click(object sender, EventArgs e)
    {
        Utils.TraceIn();
        tokenSource?.Dispose();
        tokenSource = new CancellationTokenSource();
        ct = tokenSource.Token;
        _ = TaskWrapperAsync(SpeedTestAllDirectoriesAsync);
        Utils.TraceOut();
    }

    private void MonitoringButton_Click(object sender, EventArgs e)
    {
        Utils.TraceIn();

        if (mediaBackup.Config.MonitoringOnOff)
        {
            monitoringTimer.Stop();
            Utils.LogWithPushover(BackupAction.Monitoring, "Stopped");
            mediaBackup.Config.MonitoringOnOff = false;
        }
        else
        {
            Utils.LogWithPushover(BackupAction.Monitoring, "Started");
            MonitoringTimer_Tick(null, null);
            monitoringTimer.Interval = mediaBackup.Config.MonitoringInterval * 1000;
            monitoringTimer.Start();
            mediaBackup.Config.MonitoringOnOff = true;
        }
        UpdateMonitoringButton();
        Utils.TraceOut();
    }

    private void MonitoringTimer_Tick(object sender, EventArgs e)
    {
        if (!monitoringExecutingRightNow) TaskWrapper(monitoringAction);
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
                    Utils.LogWithPushover(BackupAction.Monitoring, PushoverPriority.Normal,
                        $"Stopping all '{toKill}' processes that match");
                    _ = Utils.KillProcesses(toKill);
                }
            }
            if (!monitor.ServiceToRestart.HasValue()) continue;

            Utils.LogWithPushover(BackupAction.Monitoring, PushoverPriority.Normal, $"Stopping '{monitor.ServiceToRestart}'");

            if (!Utils.StopService(monitor.ServiceToRestart, 5000))
            {
                Utils.LogWithPushover(BackupAction.Monitoring, PushoverPriority.High,
                    $"Failed to stop the service '{monitor.Name}'");
            }
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
        Utils.LogWithPushover(BackupAction.General, PushoverPriority.High, "BackupManager stopped");
        Utils.TraceOut();
        tokenSource = new CancellationTokenSource();
        ct = tokenSource.Token;
        Utils.BackupLogFile(ct);
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
                    Utils.LogWithPushover(BackupAction.Monitoring, PushoverPriority.Normal,
                        $"Stopping all '{toKill}' processes that match");
                    _ = Utils.KillProcesses(toKill);
                }
            }

            if (monitor.ServiceToRestart.HasValue())
            {
                Utils.LogWithPushover(BackupAction.Monitoring, PushoverPriority.Normal, $"Stopping '{monitor.ServiceToRestart}'");

                if (!Utils.StopService(monitor.ServiceToRestart, 5000))
                {
                    Utils.LogWithPushover(BackupAction.Monitoring, PushoverPriority.High,
                        $"Failed to stop the service '{monitor.Name}'");
                }
            }
        }
        Utils.TraceOut();
    }

    private void ListFilesWithDuplicateContentHashCodesButton_Click(object sender, EventArgs e)
    {
        Utils.TraceIn();
        Dictionary<string, BackupFile> allFilesUniqueContentsHash = new();
        List<BackupFile> backupFilesWithDuplicates = new();

        foreach (var f in mediaBackup.BackupFiles.Where(static f => f.FullPath.Contains("_Movies") || f.FullPath.Contains("_TV")))
        {
            if (allFilesUniqueContentsHash.TryGetValue(f.ContentsHash, out var originalFile))
            {
                backupFilesWithDuplicates.Add(f);
                if (!backupFilesWithDuplicates.Contains(originalFile)) backupFilesWithDuplicates.Add(originalFile);
            }
            else
                allFilesUniqueContentsHash.Add(f.ContentsHash, f);
        }

        foreach (var f in backupFilesWithDuplicates)
        {
            Utils.Log($"{f.FullPath} has a duplicate");
        }
        Utils.TraceOut();
    }

    private void CheckDeleteAndCopyAllBackupDisksButton_Click(object sender, EventArgs e)
    {
        Utils.TraceIn();

        // Check the current backup disk connected
        // when its finished prompt for another disk and wait

        if (MessageBox.Show(Resources.Main_AreYouSureDelete, Resources.Main_DeleteExtraTitle, MessageBoxButtons.YesNo) ==
            DialogResult.Yes)
        {
            tokenSource?.Dispose();
            tokenSource = new CancellationTokenSource();
            ct = tokenSource.Token;
            TaskWrapper(CheckConnectedDiskAndCopyFilesRepeaterAsync, true);
        }
        Utils.TraceOut();
    }

    private void CancelButton_Click(object sender, EventArgs e)
    {
        Utils.TraceIn();
        tokenSource.Cancel();
        toolStripStatusLabel.Text = Resources.Main_Cancelling;
        if (Utils.CopyProcess != null && !Utils.CopyProcess.HasExited) Utils.CopyProcess?.Kill();
        Utils.TraceOut();
    }

    private void ASyncTasksCleanUp()
    {
        Utils.TraceIn();
        if (Utils.CopyProcess != null && !Utils.CopyProcess.HasExited) Utils.CopyProcess?.Kill();
        ResetAllControls();
        UpdateMediaFilesCountDisplay();
        Utils.LogWithPushover(BackupAction.General, PushoverPriority.Normal, "Cancelled");
        longRunningActionExecutingRightNow = false;
        Utils.TraceOut();
    }

    private void CheckAllBackupDisksButton_Click(object sender, EventArgs e)
    {
        // Check the current backup disk connected
        // when its finished prompt for another disk and wait
        Utils.TraceIn();

        if (MessageBox.Show(Resources.Main_AreYouSureDelete, Resources.Main_DeleteExtraTitle, MessageBoxButtons.YesNo) ==
            DialogResult.Yes)
        {
            tokenSource?.Dispose();
            tokenSource = new CancellationTokenSource();
            ct = tokenSource.Token;
            TaskWrapper(CheckConnectedDiskAndCopyFilesRepeaterAsync, false);
        }
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
        tokenSource?.Dispose();
        tokenSource = new CancellationTokenSource();
        ct = tokenSource.Token;
        _ = TaskWrapperAsync(SetupBackupDiskAsync);
        Utils.TraceOut();
    }

    private void ListFilesMarkedAsDeletedButton_Click(object sender, EventArgs e)
    {
        Utils.TraceIn();
        var files = mediaBackup.GetBackupFilesMarkedAsDeleted(true);
        Utils.Log("Listing files marked as deleted");
        var backupFiles = files as BackupFile[] ?? files.ToArray();

        foreach (var file in backupFiles)
        {
            Utils.Log($"{file.FullPath} at {Utils.FormatSize(file.Length)} on {file.Disk}");
        }
        Utils.Log($"{backupFiles.Length} files at {Utils.FormatSize(backupFiles.Sum(static p => p.Length))}");
        Utils.TraceOut();
    }

    private void UpdateUI_Tick(object sender, EventArgs e)
    {
        timeToNextRunTextBox.Invoke(x =>
            x.Text = _trigger == null || !updateUITimer.Enabled
                ? string.Empty
                : _trigger.TimeToNextTrigger().ToString(Resources.DateTime_TimeFormat));
        directoriesToScanTextBox.Invoke(x => x.Text = mediaBackup.Watcher.DirectoriesToScan.Count.ToString());
        fileChangesDetectedTextBox.Invoke(x => x.Text = mediaBackup.Watcher.FileSystemChanges.Count.ToString());
        UpdateOldestBackupDisk();
    }

    private void FileSystemWatcher_ReadyToScan(object sender, FileSystemWatcherEventArgs e)
    {
        if (longRunningActionExecutingRightNow) return;

        longRunningActionExecutingRightNow = true;
        ReadyToScan(e, SearchOption.TopDirectoryOnly);
        longRunningActionExecutingRightNow = false;
    }

    private void ReadyToScan(FileSystemWatcherEventArgs e, SearchOption searchOption)
    {
        Utils.TraceIn($"e.Directories = {e.Directories.Length}");
        var toSave = false;

        if (!e.Directories.Any())
        {
            Utils.TraceOut();
            return;
        }

        for (var i = e.Directories.Length - 1; i >= 0; i--)
        {
            var directoryToScan = e.Directories[i];

            if (Utils.StringContainsFixedSpace(directoryToScan.Path))
            {
                Utils.LogWithPushover(BackupAction.ScanDirectory, PushoverPriority.High,
                    $"{directoryToScan.Path} contains a Fixed Space");
                Utils.TraceOut();
                return;
            }
            mediaBackup.ClearFlags();

            var fileCountInDirectoryBefore = mediaBackup.BackupFiles.Count(b =>
                b.FullPath.StartsWith(directoryToScan.Path, StringComparison.InvariantCultureIgnoreCase));

            if (ScanSingleDirectory(directoryToScan.Path, searchOption))
            {
                UpdateSymbolicLinkForDirectory(directoryToScan.Path);

                // instead of removing files that are no longer found in a directory we now flag them as deleted so we can report them later
                // unless they aren't on a backup disk in which case they are removed now 
                BackupFile[] filesToRemoveOrMarkDeleted;

                if (searchOption == SearchOption.TopDirectoryOnly)
                {
                    filesToRemoveOrMarkDeleted = mediaBackup.BackupFiles.Where(static b => !b.Flag)
                        .Where(b => b.FullPath.StartsWith(directoryToScan.Path, StringComparison.InvariantCultureIgnoreCase))
                        .Where(b => !b.FullPath.SubstringAfter(Utils.EnsurePathHasATerminatingSeparator(directoryToScan.Path),
                            StringComparison.CurrentCultureIgnoreCase).Contains('\\')).ToArray();
                }
                else
                {
                    filesToRemoveOrMarkDeleted = mediaBackup.BackupFiles.Where(static b => !b.Flag).Where(b =>
                        b.FullPath.StartsWith(directoryToScan.Path, StringComparison.InvariantCultureIgnoreCase)).ToArray();
                }
                RemoveOrDeleteFiles(filesToRemoveOrMarkDeleted, out var removedFilesCount, out var markedAsDeletedFilesCount);

                var fileCountAfter = mediaBackup.BackupFiles.Count(b =>
                    b.FullPath.StartsWith(directoryToScan.Path, StringComparison.InvariantCultureIgnoreCase));
                var filesNotOnBackupDiskCount = mediaBackup.GetBackupFilesWithDiskEmpty().Count();

                var text =
                    $"Directory scan completed. {fileCountInDirectoryBefore} files before and now {fileCountAfter} files. {markedAsDeletedFilesCount} marked as deleted and {removedFilesCount} removed. {filesNotOnBackupDiskCount} to backup.";
                Utils.Log(BackupAction.ScanDirectory, text);
                toSave = true;
            }
            else
            {
                var text = string.Format(Resources.Main_Directory_scan_skipped,
                    Utils.FormatTimeFromSeconds(mediaBackup.Config.DirectoriesScanTimer));
                Utils.LogWithPushover(BackupAction.ScanDirectory, text);
                mediaBackup.Watcher.DirectoriesToScan.Add(directoryToScan, ct);
            }
        }

        if (toSave)
        {
            mediaBackup.Save();
            UpdateStatusLabel(Resources.Main_Saved);
            UpdateUI_Tick(null, null);
            UpdateMediaFilesCountDisplay();
        }
        UpdateStatusLabel();
        Utils.TraceOut();
    }

    private void RemoveOrDeleteFiles(IReadOnlyList<BackupFile> files, out int removedFilesCount,
        out int markedAsDeletedFilesCount)
    {
        lock (_lock)
        {
            removedFilesCount = 0;
            markedAsDeletedFilesCount = 0;

            for (var j = files.Count - 1; j >= 0; j--)
            {
                var backupFile = files[j];

                if (string.IsNullOrEmpty(backupFile.Disk))
                {
                    Utils.Trace($"Removing {backupFile.FullPath}");
                    mediaBackup.RemoveFile(backupFile);
                    removedFilesCount++;
                }
                else
                {
                    Utils.Trace($"Marking {backupFile.FullPath} as Deleted");
                    backupFile.Deleted = true;
                    markedAsDeletedFilesCount++;
                }
            }
        }
    }

    private void Main_FormClosing(object sender, FormClosingEventArgs e)
    {
        if (mediaBackup.Watcher.DirectoriesToScan.Count <= 0 && mediaBackup.Watcher.FileSystemChanges.Count <= 0) return;

        // If file or directory changes were detected so save xml
        mediaBackup.Save();
    }

    private void CheckConnectedBackupDiskButton_Click(object sender, EventArgs e)
    {
        Utils.TraceIn();
        tokenSource?.Dispose();
        tokenSource = new CancellationTokenSource();
        ct = tokenSource.Token;
        TaskWrapper(CheckConnectedDiskAndCopyFilesAsync, false, false);
        Utils.TraceOut();
    }

    private void CopyFilesToBackupDiskButton_Click(object sender, EventArgs e)
    {
        Utils.TraceIn();
        TaskWrapper(CopyFilesAsync, true);
        Utils.TraceOut();
    }

    private void ListFilesNotOnBackupDiskButton_Click(object sender, EventArgs e)
    {
        Utils.TraceIn();

        IEnumerable<BackupFile> filesNotOnBackupDisk =
            mediaBackup.GetBackupFilesWithDiskEmpty().OrderByDescending(static p => p.Length);
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

        if (MessageBox.Show(Resources.Main_RecalculateAllHashes, Resources.Main_RecalculateAllHashesTitle,
                MessageBoxButtons.YesNo) == DialogResult.Yes)
        {
            foreach (var backupFile in mediaBackup.BackupFiles)
            {
                backupFile.UpdateContentsHash();
            }
            mediaBackup.Save();
        }
        Utils.TraceOut();
    }

    private void VersionCheckingButton_Click(object sender, EventArgs e)
    {
        Utils.TraceIn();
        mediaBackup.Config.MonitoringCheckLatestVersions = !mediaBackup.Config.MonitoringCheckLatestVersions;

        versionCheckingButton.Text = string.Format(Resources.Main_VersionCheckingButton_Click_,
            mediaBackup.Config.MonitoringCheckLatestVersions ? Resources.Main_ON : Resources.Main_OFF);
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

        var distinctDirectoriesScans = scans.Distinct().OrderBy(static s => s.Path).ToArray();
        var longestDirectoryLength = scans.Select(static d => d.Path.Length).Max();
        var headerLine1 = string.Empty.PadRight(longestDirectoryLength + 10);
        var headerLine2 = string.Empty.PadRight(longestDirectoryLength + 10);
        var totalLine = "Total time: " + string.Empty.PadRight(longestDirectoryLength - 4);
        var lapsedTimeLine = "Lapsed time: " + string.Empty.PadRight(longestDirectoryLength - 5);
        var totals = new TimeSpan[howMany];
        var lapsedTime = new TimeSpan[howMany];
        var previousId = string.Empty;

        // prepare the header lines
        foreach (var b in scans.OrderBy(static s => s.StartDateTime).Where(b => b.Id != previousId))
        {
            // build the line of scan dates like 30.11 01.12 etc
            headerLine1 += b.StartDateTime.ToString("dd.MM ");
            headerLine2 += b.StartDateTime.ToString("HH.mm ");
            previousId = b.Id;
        }
        var textLines = string.Empty;

        foreach (var directory in distinctDirectoriesScans)
        {
            var scansForDirectory = scans.Where(scan => scan.Path == directory.Path && scan.TypeOfScan == scanType).ToArray();
            var fileCount = mediaBackup.GetBackupFilesInDirectory(directory.Path, false).Count();
            textLines += $"{directory.Path.PadRight(longestDirectoryLength + 1)} {fileCount,6:n0}";

            for (var i = 0; i < scanIdsList.Length; i++)
            {
                var backup = scansForDirectory.FirstOrDefault(scan => scan.Id == scanIdsList[i]);

                if (backup == null)
                    textLines += "    --";
                else
                {
                    totals[i] += backup.ScanDuration;
                    textLines += $"{Utils.FormatTimeSpanMinutesOnly(backup.ScanDuration),6}";
                }
            }
            textLines += "\n";
        }

        for (var index = 0; index < scanIdsList.Length; index++)
        {
            var a = scanIdsList[index];
            var scanStartTime = DateTime.MaxValue;
            var scanEndTime = DateTime.MinValue;
            var directoryScans = mediaBackup.DirectoryScans.Where(s => s.Id == a && s.TypeOfScan == scanType);

            foreach (var directoryScan in directoryScans)
            {
                if (directoryScan.EndDateTime > scanEndTime) scanEndTime = directoryScan.EndDateTime;
                if (directoryScan.StartDateTime < scanStartTime) scanStartTime = directoryScan.StartDateTime;
            }
            lapsedTime[index] = scanEndTime - scanStartTime;
        }

        lapsedTimeLine = lapsedTime.Where(static aTotal => aTotal > TimeSpan.FromSeconds(0)).Aggregate(lapsedTimeLine,
            static (current, aTotal) => current + $"{Utils.FormatTimeSpanMinutesOnly(aTotal),6}");

        totalLine = totals.Where(static aTotal => aTotal > TimeSpan.FromSeconds(0)).Aggregate(totalLine,
            static (current, aTotal) => current + $"{Utils.FormatTimeSpanMinutesOnly(aTotal),6}");
        Utils.Log($"{scanType} report:");
        Utils.Log(headerLine1);
        Utils.Log(headerLine2);
        Utils.Log(textLines);
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
        Utils.TraceIn();

        if (scanDirectoryComboBox.SelectedIndex > -1)
        {
            if (!longRunningActionExecutingRightNow)
            {
                tokenSource?.Dispose();
                tokenSource = new CancellationTokenSource();
                ct = tokenSource.Token;
                _ = TaskWrapper(ScanDirectoryAsync, scanDirectoryComboBox.Text);
            }
        }
        Utils.TraceOut();
    }

    private void RootDirectoryChecks(Collection<string> directories)
    {
        var directoriesChecked = new HashSet<string>();

        foreach (var directory in directories)
        {
            UpdateStatusLabel(string.Format(Resources.Main_Scanning, directory));

            if (Directory.Exists(directory))
            {
                if (Utils.IsDirectoryWritable(directory))
                {
                    var rootPath = Utils.GetRootPath(directory);
                    if (directoriesChecked.Contains(rootPath)) continue;

                    RootDirectoryChecks(rootPath);
                    _ = directoriesChecked.Add(rootPath);
                }
                else
                    Utils.LogWithPushover(BackupAction.ScanDirectory, PushoverPriority.High, $"{directory} is not writable");
            }
            else
                Utils.LogWithPushover(BackupAction.ScanDirectory, PushoverPriority.High, $"{directory} doesn't exist");
        }
    }

    private void ProcessFilesButton_Click(object sender, EventArgs e)
    {
        Utils.TraceIn();

        if (!longRunningActionExecutingRightNow)
        {
            tokenSource?.Dispose();
            tokenSource = new CancellationTokenSource();
            ct = tokenSource.Token;
            _ = TaskWrapperAsync(ProcessFilesAsync);
        }
        Utils.TraceOut();
    }

    private void DirectoryScanReportLastRunOnlyButton_Click(object sender, EventArgs e)
    {
        DirectoryScanReport(DirectoryScanType.GetFiles, 1);
        DirectoryScanReport(DirectoryScanType.ProcessingFiles, 1);
    }

    private void DvProfile5CheckButton_Click(object sender, EventArgs e)
    {
        var files = mediaBackup.BackupFiles.Where(static f => f.FullPath.Contains("[DV]") && !f.Deleted)
            .OrderBy(static f => f.FullPath);

        foreach (var file in files)
        {
            if (File.Exists(file.FullPath))
            {
                Utils.Log(Utils.FileIsDolbyVisionProfile5(file.FullPath)
                    ? $"{file.FullPath} is DV Profile 5"
                    : $"{file.FullPath} is DV but not Profile 5");
            }
            else
            {
                Utils.Log($"{file.FullPath} not found");
                Utils.Log($"{Utils.StringContainsFixedSpace(file.FullPath)} for FixedSpace");
            }
        }
    }
}