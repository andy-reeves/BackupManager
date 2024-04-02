// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="Main.1.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

using BackupManager.Entities;
using BackupManager.Extensions;
using BackupManager.Properties;

namespace BackupManager;

internal sealed partial class Main
{
    private static DailyTrigger _trigger;

    private static readonly object _lock = new();

    /// <summary>
    ///     The main application config.xml
    /// </summary>
    private readonly Config config;

    private readonly MediaBackup mediaBackup;

    private readonly Action monitoringAction;

    /// <summary>
    ///     For the monitoring applications
    /// </summary>
    private readonly CancellationTokenSource monitoringCancellationTokenSource = new();

    private readonly Action scheduledBackupAction;

    private int currentPercentComplete;

    private BlockingCollection<DirectoryScan> directoryScanBlockingCollection;

    private BlockingCollection<string> fileBlockingCollection;

    private int fileCounterForMultiThreadProcessing;

    /// <summary>
    ///     Any long-running action sets this to TRUE to stop the scheduledBackup timer from being able to start
    /// </summary>
    private bool longRunningActionExecutingRightNow;

    // Always create a new one before running a long-running task
    private CancellationTokenSource mainCancellationTokenSource;

    private CancellationToken mainCt;

    /// <summary>
    ///     When monitoring is executing prevent is executing again
    /// </summary>
    private bool monitoringExecutingRightNow;

    private int reportedPercentComplete;

    [SupportedOSPlatform("windows")]

    // ReSharper disable once FunctionComplexityOverflow
    internal Main()
    {
        try
        {
            InitializeComponent();
            TraceConfiguration.Register();

            if (Utils.InDebugBuild)
            {
                _ = Trace.Listeners.Add(new TextWriterTraceListener(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "BackupManager_Trace.log"), "myListener"));
            }
            var localMediaXml = Path.Combine(Application.StartupPath, "MediaBackup.xml");
            mediaBackup = MediaBackup.Load(File.Exists(localMediaXml) ? localMediaXml : ConfigurationManager.AppSettings.Get("MediaBackupXml"));
            config = mediaBackup.Config;
            Utils.Config = config;
            UpdateBackupDiskTextBoxFromConfig();
            if (Utils.IsRunningAsAdmin()) Text += Resources.AdminTitle;
            UpdateMediaFilesCountDisplay();
            Utils.LogHeader();
            Utils.LogWithPushover(BackupAction.General, Resources.BackupManagerStarted, false, true);
            config.LogParameters();
            var directoriesArray = config.DirectoriesToBackup.ToArray();
            listDirectoriesComboBox.Items.AddRange(directoriesArray.ToArray<object>());
            directoriesComboBox.Items.AddRange(directoriesArray.ToArray<object>());
            restoreDirectoryComboBox.Items.AddRange(directoriesArray.ToArray<object>());
            scanDirectoryComboBox.Items.AddRange(directoriesArray.ToArray<object>());

            foreach (var file in mediaBackup.BackupFiles.Where(static file => file.FullPath.Length > Utils.MAX_PATH))
            {
                Utils.Log(string.Format(Resources.PathIsLongerThan256Characters, file.FullPath));
            }

            foreach (var disk in mediaBackup.BackupDisks)
            {
                _ = listFilesComboBox.Items.Add(disk.Name);
            }
            pushoverLowCheckBox.Checked = config.PushoverSendLowOnOff;
            pushoverNormalCheckBox.Checked = config.PushoverSendNormalOnOff;
            pushoverHighCheckBox.Checked = config.PushoverSendHighOnOff;
            pushoverEmergencyCheckBox.Checked = config.PushoverSendEmergencyOnOff;

            foreach (var monitor in config.Monitors)
            {
                _ = processesComboBox.Items.Add(monitor.Name);
            }

            scheduledBackupAction = () =>
            {
                // If a long-running task is already executing then reset the trigger to try again in 1 minute
                if (longRunningActionExecutingRightNow)
                {
                    SetupDailyTrigger(mediaBackup.Config.ScheduledBackupOnOff, DateTime.Now.AddMinutes(1));
                    UpdateScheduledBackupButton();
                    return;
                }
                ResetTokenSource();
                _ = TaskWrapper(() => ScheduledBackupAsync(mainCt), mainCt);
            };
            monitoringAction = MonitorServices;
            scheduledDateTimePicker.Value = DateTime.Parse(config.ScheduledBackupStartTime);
            UpdateSendingPushoverButton();
            UpdateScheduledBackupButton();
            UpdateSpeedTestDisksButton();

            // we switch it off and force the button to be clicked to turn it on again
            config.MonitoringOnOff = !config.MonitoringOnOff;
            MonitoringButton_Click(null, null);
            _ = UpdateCurrentBackupDiskInfo(mediaBackup.GetBackupDisk(backupDiskTextBox.Text));

            if (config.ScheduledBackupRunOnStartup)
            {
                ResetTokenSource();
                _ = TaskWrapper(() => ScheduledBackupAsync(mainCt), mainCt);
            }
            SetupDailyTrigger(config.ScheduledBackupOnOff, scheduledDateTimePicker.Value);
            SetupFileWatchers();
            UpdateUI_Tick(null, null);

            // we switch it off and force the button to be clicked to turn it on again
            config.MonitoringCheckLatestVersions = !config.MonitoringCheckLatestVersions;
            VersionCheckingButton_Click(null, null);
            Utils.TraceOut();
        }
        catch (Exception ex)
        {
            _ = MessageBox.Show(string.Format(Resources.ExceptionOccured, ex));
            Environment.Exit(0);
        }
    }

    internal void UpdateBackupDiskTextBoxFromConfig()
    {
        backupDiskTextBox.Text = config.BackupDisk;
    }

    private void UpdateSymbolicLinksAsync(CancellationToken ct)
    {
        try
        {
            Utils.TraceIn();
            if (longRunningActionExecutingRightNow) return;

            DisableControlsForAsyncTasks(ct);
            UpdateSymbolicLinks(ct);
            ResetAllControls();
        }
        finally
        {
            Utils.TraceOut();
        }
    }

    private void UpdateSymbolicLinks(CancellationToken ct)
    {
        Utils.TraceIn();
        Utils.LogWithPushover(BackupAction.CheckingSymbolicLinks, Resources.Started, false, true);
        UpdateStatusLabel(ct, Resources.Started);
        HashSet<string> hashSet = new();

        // HashSet of parent paths that match the RegEx's from config
        foreach (var path in from backupFile in mediaBackup.BackupFiles
                 select mediaBackup.GetParentPath(backupFile.FullPath)
                 into directoryPath
                 where directoryPath != null
                 where config.SymbolicLinks.Select(a => Regex.Match(directoryPath, a.FileDiscoveryRegEx)).Any(static m => m.Success)
                 select directoryPath)
        {
            if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();
            _ = hashSet.Add(path);
        }
        UpdateStatusLabel(ct, "Checking for broken Symbolic Links");
        var directoriesToCheck = config.SymbolicLinks.Select(static a => Path.Combine(a.RootDirectory, Utils.RemoveRegexGroupsFromString(a.RelativePath))).Where(Directory.Exists).SelectMany(Directory.EnumerateDirectories).ToArray();

        // check the symbolic links root folders for any broken links
        EnableProgressBar(0, directoriesToCheck.Length);
        int percentCompleteCurrent;
        var percentCompleteReported = 0;

        for (var i = 0; i < directoriesToCheck.Length; i++)
        {
            if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();
            percentCompleteCurrent = i * 100 / toolStripProgressBar.Maximum;

            if (percentCompleteCurrent % 25 == 0 && percentCompleteCurrent > percentCompleteReported && directoriesToCheck.Length > 100)
            {
                percentCompleteReported = percentCompleteCurrent;
                Utils.LogWithPushover(BackupAction.CheckingSymbolicLinks, string.Format(Resources.CheckingBrokenLinks, percentCompleteCurrent));
            }
            var directoryToCheck = directoriesToCheck[i];
            UpdateStatusLabel(ct, string.Format(Resources.Checking, directoryToCheck), i);
            var linksDeleted = Utils.DeleteBrokenSymbolicLinks(directoryToCheck, true);

            foreach (var link in linksDeleted)
            {
                Utils.Log($"Symbolic link {link} deleted");
            }
        }
        Utils.LogWithPushover(BackupAction.CheckingSymbolicLinks, Resources.Completed);
        EnableProgressBar(0, 100);
        var counter = 0;
        percentCompleteReported = 0;

        foreach (var path in hashSet)
        {
            if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();
            counter++;
            percentCompleteCurrent = Convert.ToInt32(counter * 100 / hashSet.Count);
            UpdateStatusLabel(ct, string.Format(Resources.Checking, path), percentCompleteCurrent);

            if (percentCompleteCurrent % 25 == 0 && percentCompleteCurrent > percentCompleteReported && hashSet.Count > 100)
            {
                percentCompleteReported = percentCompleteCurrent;
                Utils.LogWithPushover(BackupAction.CheckingSymbolicLinks, PushoverPriority.Normal, string.Format(Resources.UpdatingPercentage, percentCompleteCurrent));
            }
            UpdateSymbolicLinkForDirectory(path);
        }
        Utils.LogWithPushover(BackupAction.CheckingSymbolicLinks, Resources.Completed, true);
        UpdateStatusLabel(ct, Resources.Completed);
        Utils.TraceOut();
    }

    private void UpdateSymbolicLinkForDirectory(string symbolicLinkPath)
    {
        Utils.TraceIn(symbolicLinkPath);

        if (!Directory.Exists(symbolicLinkPath))
        {
            Utils.TraceOut();
            return;
        }

        foreach (var a in config.SymbolicLinks)
        {
            var m = Regex.Match(symbolicLinkPath, a.FileDiscoveryRegEx);
            if (!m.Success) continue;

            var path = Path.Combine(a.RootDirectory, a.RelativePath);
            var pathToTarget = a.PathToTarget;
            Utils.RegexPathFixer(m, ref path, ref pathToTarget);

            if (pathToTarget == null)
            {
                Utils.TraceOut();
                return;
            }

            if (Directory.Exists(path) && Utils.Directory.IsEmpty(path))
            {
                Utils.Trace("Deleting link directory as its empty");
                _ = Utils.Directory.Delete(path, true);
            }
            if (path != null && Directory.Exists(path)) continue;

            Utils.Trace($"Creating new symbolic link at {path} with target {pathToTarget}");
            if (path != null) _ = Directory.CreateSymbolicLink(path, pathToTarget);
        }
        Utils.TraceOut();
    }

    private void UpdateMediaFilesCountDisplay()
    {
        try
        {
            var backupFilesWithoutDeleted = mediaBackup.GetBackupFiles(false).ToArray();
            var backupFilesWithDiskEmpty = mediaBackup.BackupFiles.Where(static p => (p.Disk.HasNoValue() || p.BeingCheckedNow) && !p.Deleted).ToArray();
            var backupFilesMarkedAsDeleted = mediaBackup.GetBackupFilesMarkedAsDeleted(false).ToArray();
            totalFilesTextBox.TextWithInvoke(backupFilesWithoutDeleted.Length.ToString("N0"));
            totalFilesSizeTextBox.TextWithInvoke(Utils.FormatSize(backupFilesWithoutDeleted.Sum(static y => y.Length)));
            notOnABackupDiskTextBox.TextWithInvoke(backupFilesWithDiskEmpty.Length.ToString("N0"));
            notOnABackupDiskSizeTextBox.TextWithInvoke(Utils.FormatSize(backupFilesWithDiskEmpty.Sum(static y => y.Length)));
            filesMarkedAsDeletedTextBox.TextWithInvoke(backupFilesMarkedAsDeleted.Length.ToString("N0"));
            filesMarkedAsDeletedSizeTextBox.TextWithInvoke(Utils.FormatSize(backupFilesMarkedAsDeleted.Sum(static y => y.Length)));
        }
        catch (InvalidOperationException)
        {
            // if collections are modified by checking the disk then we cant update the UI here
        }
    }

    private void StartFileSystemWatchers()
    {
        Utils.TraceIn();
        if (mediaBackup.Watcher.Directories.Length == 0) SetupWatcher();
        _ = mediaBackup.Watcher.Start();
        Utils.Trace($"mediaBackup.Watcher.Running = {mediaBackup.Watcher.Running}");
        Utils.TraceOut();
    }

    private void SetupWatcher()
    {
        Utils.TraceIn();
        mediaBackup.Watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName;

        // check directories are writable and only monitor those that are
        var writableDirectories = new List<string>();

        foreach (var directory in config.DirectoriesToBackup)
        {
            if (Utils.Directory.IsWritable(directory))
                writableDirectories.Add(directory);
            else
            {
                Utils.LogWithPushover(BackupAction.ApplicationMonitoring, PushoverPriority.High, string.Format(Resources.DirectoryIsNotWritable, directory));
            }
        }
        mediaBackup.Watcher.Directories = writableDirectories.ToArray();
        mediaBackup.Watcher.ProcessChangesInterval = config.DirectoriesProcessChangesTimer * 1000;
        mediaBackup.Watcher.ScanInterval = config.DirectoriesScanTimer * 1000;
        mediaBackup.Watcher.Filter = "*.*";
        mediaBackup.Watcher.RegexFilter = config.DirectoriesFilterRegEx;
        mediaBackup.Watcher.IncludeSubdirectories = true;
        mediaBackup.Watcher.ReadyToScan += FileSystemWatcher_ReadyToScan;
        mediaBackup.Watcher.Error += FileSystemWatcher_OnError;
        mediaBackup.Watcher.MinimumAgeBeforeScanEventRaised = config.DirectoriesMinimumAgeBeforeScanning;

        foreach (var item in mediaBackup.DirectoryChanges)
        {
            _ = mediaBackup.Watcher.FileSystemChanges.Add(new FileSystemEntry(item.Path, item.ModifiedDateTime));
        }

        foreach (var item in mediaBackup.DirectoriesToScan)
        {
            _ = mediaBackup.Watcher.DirectoriesToScan.Add(new FileSystemEntry(item.Path, item.ModifiedDateTime));
        }
        Utils.TraceOut();
    }

    private void CheckForOldBackupDisks()
    {
        Utils.TraceIn();
        var numberOfDays = config.BackupDiskDaysToReportSinceFilesChecked;
        var files = mediaBackup.BackupFiles.Where(p => p.DiskChecked.HasValue() && DateTime.Parse(p.DiskChecked).AddDays(numberOfDays) < DateTime.Today);
        var backupFiles = files as BackupFile[] ?? files.ToArray();
        var disks = backupFiles.GroupBy(static p => p.Disk).Select(static p => p.First());

        foreach (var disk in disks)
        {
            Utils.LogWithPushover(BackupAction.CheckBackupDisk, string.Format(Resources.CheckForOldBackupDisks, numberOfDays, disk.Disk));
        }

        Utils.Log(BackupAction.General,
            !backupFiles.Any() ? string.Format(Resources.AllFilesCheckedInLastNDays, config.BackupDiskDaysToReportSinceFilesChecked) : string.Format(Resources.ListingFilesNotCheckedInDays, config.BackupDiskDaysToReportSinceFilesChecked));

        foreach (var file in backupFiles)
        {
            var days = DateTime.Today.Subtract(DateTime.Parse(file.DiskChecked)).Days;
            Utils.Log(BackupAction.General, string.Format(Resources.ListingFileNotCheckedInDaysOnDisk, file.FullPath, days, file.Disk));
        }
        Utils.TraceOut();
    }

    private void UpdateSendingPushoverButton()
    {
        Utils.TraceIn();
        pushoverOnOffButton.Text = string.Format(Resources.SendingPushoverButton, config.PushoverOnOff ? Resources.ON : Resources.OFF);
        Utils.TraceOut();
    }

    private void SetupFileWatchers()
    {
        Utils.TraceIn();
        fileWatcherButton.Text = string.Format(Resources.FileWatchersButton, config.DirectoriesFileChangeWatcherOnOff ? Resources.ON : Resources.OFF);

        if (config.DirectoriesFileChangeWatcherOnOff)
            StartFileSystemWatchers();
        else
            StopFileSystemWatchers();
    }

    private void StopFileSystemWatchers()
    {
        _ = mediaBackup.Watcher.Stop();
    }

    private void UpdateMonitoringButton()
    {
        Utils.TraceIn();
        monitoringButton.TextWithInvoke(config.MonitoringOnOff ? string.Format(Resources.MonitoringButton, Resources.ON) : string.Format(Resources.MonitoringButton, Resources.OFF));
        Utils.TraceOut();
    }

    private void UpdateSpeedTestDisksButton()
    {
        Utils.TraceIn();
        speedTestDisksButton.TextWithInvoke(string.Format(Resources.SpeedTestDisksButton, config.SpeedTestOnOff ? Resources.ON : Resources.OFF));
        Utils.TraceOut();
    }

    private void UpdateScheduledBackupButton()
    {
        Utils.TraceIn();
        scheduledBackupTimerButton.TextWithInvoke(string.Format(Resources.UpdateScheduledBackupButton, config.ScheduledBackupOnOff ? Resources.ON : Resources.OFF));
        Utils.TraceOut();
    }

    private void SpeedTestAllDirectoriesAsync(CancellationToken ct)
    {
        try
        {
            Utils.TraceIn();
            if (longRunningActionExecutingRightNow) return;

            DisableControlsForAsyncTasks(ct);
            Utils.LogWithPushover(BackupAction.SpeedTest, Resources.Started, false, true);
            EnableProgressBar(0, config.DirectoriesToBackup.Count);

            for (var i = 0; i < config.DirectoriesToBackup.Count; i++)
            {
                var directory = config.DirectoriesToBackup[i];
                UpdateStatusLabel(ct, string.Format(Resources.SpeedTesting, directory), i + 1);
                if (!Utils.Directory.IsWritable(directory)) continue;

                Utils.DiskSpeedTest(directory, Utils.ConvertMBtoBytes(config.SpeedTestFileSize), config.SpeedTestIterations, out var readSpeed, out var writeSpeed, ct);
                Utils.Log($"testing {directory}, Read: {Utils.FormatSpeed(readSpeed)} Write: {Utils.FormatSpeed(writeSpeed)}");
            }
            Utils.LogWithPushover(BackupAction.SpeedTest, Resources.Completed, true);
            ResetAllControls();
        }
        finally
        {
            Utils.TraceOut();
        }
    }

    private void ClearEstimatedFinish()
    {
        estimatedFinishTimeTextBox.TextWithInvoke(string.Empty);
    }

    private void UpdateEstimatedFinish(DateTime estimatedFinishDateTime)
    {
        estimatedFinishTimeTextBox.TextWithInvoke(estimatedFinishDateTime.ToString(Resources.DateTime_HHmm));
    }

    private void DisableControlsForAsyncTasks(CancellationToken ct)
    {
        if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();
        longRunningActionExecutingRightNow = true;

        foreach (var c in Controls.Cast<Control>().Where(static c => c is not StatusStrip))
        {
            c.Invoke(static x => x.Enabled = false);
        }

        // ReSharper disable BadListLineBreaks
        var controlsToEnable = new Control[]
        {
            cancelButton, testPushoverEmergencyButton, testPushoverHighButton, testPushoverNormalButton, testPushoverLowButton, listFilesInDirectoryButton, listFilesNotCheckedInXXButton, listFilesNotOnBackupDiskButton,
            listFilesOnBackupDiskButton, listFilesWithDuplicateContentHashcodesButton, listMoviesWithMultipleFilesButton, processesGroupBox, pushoverGroupBox, listFilesGroupBox, listBackupDiskStatusByDiskNumberButton,
            listFilesInDirectoryGroupBox, monitoringButton, listBackupDiskStatusByFreeSpaceButton, listFilesOnBackupDiskGroupBox, openLogFileButton, allBackupDisksGroupBox
        };

        // ReSharper restore BadListLineBreaks

        foreach (var control in controlsToEnable)
        {
            control.Invoke(static x => x.Enabled = true);
        }
        checkAllBackupDisksButton.Invoke(static x => x.Enabled = false);
        checkDeleteAndCopyAllBackupDisksButton.Invoke(static x => x.Enabled = false);
    }

    /// <summary>
    ///     Re-enables all the form controls after an async task
    /// </summary>
    private void ResetAllControls()
    {
        longRunningActionExecutingRightNow = false;
        if (!IsHandleCreated || IsDisposed) return;

        foreach (Control c in Controls)
        {
            c.Invoke(static x => x.Enabled = true);
        }
        checkAllBackupDisksButton.Invoke(static x => x.Enabled = true);
        checkDeleteAndCopyAllBackupDisksButton.Invoke(static x => x.Enabled = true);
        cancelButton.Invoke(static x => x.Enabled = false);
        statusStrip.Invoke(_ => toolStripProgressBar.Visible = false);
        statusStrip.Invoke(_ => toolStripStatusLabel.Text = string.Empty);
        ClearEstimatedFinish();
    }

    private void UpdateStatusLabel(CancellationToken ct, string text = "", int value = 0)
    {
        Utils.TraceIn(value);
        if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();
        UpdateStatusLabel(text, value);
        Utils.TraceOut();
    }

    private void UpdateStatusLabel(string text = "", int value = 0)
    {
        text = text.Trim();
        var textToUse = string.Empty;

        if (value > 0)
        {
            var progress = 0;
            if (toolStripProgressBar.Maximum > 0) progress = value * 100 / toolStripProgressBar.Maximum;

            progress = progress switch
            {
                0 => 1,
                > 99 => 99,
                _ => progress
            };
            textToUse = $"{text} - {progress}%";
        }
        else
        {
            if (text != string.Empty && !text.EndsWithIgnoreCase(".")) textToUse = text + " ...";
        }
        UpdateProgressBar(value);
        if (toolStripStatusLabel.Text != textToUse) statusStrip.Invoke(_ => toolStripStatusLabel.Text = textToUse);
    }

    private void EnableProgressBar(int minimum, int maximum)
    {
        if (maximum < minimum) return;

        statusStrip.Invoke(_ => toolStripProgressBar.Minimum = minimum);
        statusStrip.Invoke(_ => toolStripProgressBar.Maximum = maximum);
        statusStrip.Invoke(_ => toolStripProgressBar.Visible = true);
        statusStrip.Invoke(_ => toolStripProgressBar.Value = minimum);
    }

    private void UpdateProgressBar(int value)
    {
        if (value > 0)
        {
            if (value >= toolStripProgressBar.Maximum) value = toolStripProgressBar.Maximum - 1;
            if (toolStripProgressBar.Value != value) statusStrip.Invoke(_ => toolStripProgressBar.Value = value);
            if (!toolStripProgressBar.Visible) statusStrip.Invoke(_ => toolStripProgressBar.Visible = true);
        }
        else
        {
            if (toolStripProgressBar.Visible) statusStrip.Invoke(_ => toolStripProgressBar.Visible = false);
        }
    }
}