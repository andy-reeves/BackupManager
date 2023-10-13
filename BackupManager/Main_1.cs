using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
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

partial class Main
{
    private readonly MediaBackup mediaBackup;

    private readonly Action monitoringAction;

    private readonly Action scheduledBackupAction;

    private CancellationToken ct;

    /// <summary>
    ///     Any long-running action sets this to TRUE to stop the scheduledBackup timer from being able to start
    /// </summary>
    private bool longRunningActionExecutingRightNow;

    private CancellationTokenSource tokenSource = new();

    private DailyTrigger trigger;

    [SupportedOSPlatform("windows")]
    public Main()
    {
        try
        {
            InitializeComponent();
            TraceConfiguration.Register();
#if DEBUG
            Trace.Listeners.Add(
                new TextWriterTraceListener(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "BackupManager_Trace.log"),
                    "myListener"));

            // ReSharper disable StringLiteralTypo
            backupDiskTextBox.Text = @"\\nas1\assets1\_Test\BackupDisks\backup 1001 parent";

            //backupDiskTextBox.Text = Path.Combine(@"\\", Environment.MachineName, "backup");

            // ReSharper restore StringLiteralTypo
#else
            backupDiskTextBox.Text = Path.Combine(@"\\", Environment.MachineName, "backup");
#endif
            var mediaBackupXml = ConfigurationManager.AppSettings.Get("MediaBackupXml");
            var localMediaXml = Path.Combine(Application.StartupPath, "MediaBackup.xml");
            mediaBackup = MediaBackup.Load(File.Exists(localMediaXml) ? localMediaXml : mediaBackupXml);
            if (Utils.IsRunningAsAdmin()) Text += Resources.Main_AdminTitle;
            UpdateMediaFilesCountDisplay();
            Utils.Config = mediaBackup.Config;
            Utils.LogWithPushover(BackupAction.General, "BackupManager started");

            // Log the parameters after setting the Pushover keys in the Utils class
            mediaBackup.Config.LogParameters();
            var masterFoldersArray = mediaBackup.Config.MasterFolders.ToArray();
            listMasterFoldersComboBox.Items.AddRange(masterFoldersArray.ToArray<object>());
            masterFoldersComboBox.Items.AddRange(masterFoldersArray.ToArray<object>());
            restoreMasterFolderComboBox.Items.AddRange(masterFoldersArray.ToArray<object>());

            foreach (var disk in mediaBackup.BackupDisks)
            {
                listFilesComboBox.Items.Add(disk.Name);
            }
            pushoverLowCheckBox.Checked = mediaBackup.Config.PushoverSendLowOnOff;
            pushoverNormalCheckBox.Checked = mediaBackup.Config.PushoverSendNormalOnOff;
            pushoverHighCheckBox.Checked = mediaBackup.Config.PushoverSendHighOnOff;
            pushoverEmergencyCheckBox.Checked = mediaBackup.Config.PushoverSendEmergencyOnOff;

            foreach (var monitor in mediaBackup.Config.Monitors)
            {
                processesComboBox.Items.Add(monitor.Name);
            }
            scheduledBackupAction = () => { TaskWrapper(ScheduledBackupAsync); };
            monitoringAction = MonitorServices;
            scheduledDateTimePicker.Value = DateTime.Parse(mediaBackup.Config.ScheduledBackupStartTime);
            UpdateSendingPushoverButton();
            UpdateMonitoringButton();
            UpdateScheduledBackupButton();
            UpdateSpeedTestDisksButton();

            if (mediaBackup.Config.MonitoringOnOff)
            {
                // we switch it off and force the button to be clicked to turn it on again
                mediaBackup.Config.MonitoringOnOff = false;
#if !DEBUG
                MonitoringButton_Click(null, null);
#endif
            }
            UpdateCurrentBackupDiskInfo(mediaBackup.GetBackupDisk(backupDiskTextBox.Text));

            if (mediaBackup.Config.ScheduledBackupRunOnStartup)
            {
#if !DEBUG
                TaskWrapper(ScheduledBackupAsync);
#endif
            }
            SetupDailyTrigger(mediaBackup.Config.ScheduledBackupOnOff);
            SetupFileWatchers();
            UpdateUI_Tick(null, null);
            Utils.TraceOut();
        }
        catch (Exception ex)
        {
            MessageBox.Show(string.Format(Resources.Main_ExceptionOccured, ex));
            Environment.Exit(0);
        }
    }

    public sealed override string Text
    {
        get => base.Text;

        set => base.Text = value;
    }

    private void UpdateSymbolicLinksAsync()
    {
        longRunningActionExecutingRightNow = true;
        DisableControlsForAsyncTasks();
        UpdateSymbolicLinks();
        ResetAllControls();
        longRunningActionExecutingRightNow = false;
    }

    private void UpdateSymbolicLinks()
    {
        Utils.TraceIn();
        UpdateStatusLabel("Started");
        HashSet<string> hashSet = new();

        // HashSet of parent paths that match the RegEx's from config
        foreach (var backupFile in mediaBackup.BackupFiles)
        {
            var folderPath = mediaBackup.GetParentFolder(backupFile.FullPath);
            if (folderPath == null) continue;

            if (mediaBackup.Config.SymbolicLinks.Select(a => Regex.Match(folderPath, a.FileDiscoveryRegEx)).Any(m => m.Success)) hashSet.Add(folderPath);
        }
        UpdateStatusLabel("Checking for broken Symbolic Links");

        var foldersToCheck = mediaBackup.Config.SymbolicLinks.Select(a => Path.Combine(a.RootFolder, Utils.RemoveRegExGroupsFromString(a.RelativePath)))
            .Where(Directory.Exists).SelectMany(Directory.EnumerateDirectories).ToList();

        // check the symbolic links root folders for any broken links
        EnableProgressBar(0, foldersToCheck.Count);

        for (var i = 0; i < foldersToCheck.Count; i++)
        {
            var folderToCheck = foldersToCheck[i];
            UpdateStatusLabel($"Checking {folderToCheck}", i);
            Utils.DeleteBrokenSymbolicLinks(folderToCheck, true);
        }
        EnableProgressBar(0, 100);
        var fileCounter = 0;

        foreach (var folderBackupFile in hashSet)
        {
            fileCounter++;
            UpdateStatusLabel($"Checking {folderBackupFile}", Convert.ToInt32(fileCounter * 100 / hashSet.Count));
            UpdateSymbolicLinkForFolder(folderBackupFile);
        }
        UpdateStatusLabel("Completed.");
        Utils.TraceOut();
    }

    private void UpdateSymbolicLinkForFolder(string folderPath)
    {
        Utils.TraceIn(folderPath);

        if (!Directory.Exists(folderPath))
        {
            Utils.TraceOut();
            return;
        }

        foreach (var a in mediaBackup.Config.SymbolicLinks)
        {
            var m = Regex.Match(folderPath, a.FileDiscoveryRegEx);
            if (!m.Success) continue;

            var path = Path.Combine(a.RootFolder, a.RelativePath);
            var pathToTarget = a.PathToTarget;
            Utils.RegExPathFixer(m, ref path, ref pathToTarget);

            if (pathToTarget == null)
            {
                Utils.TraceOut();
                return;
            }

            if (Directory.Exists(path) && Utils.IsDirectoryEmpty(path))
            {
                Utils.Trace("Deleting link directory as its empty");
                Directory.Delete(path, true);
            }
            if (path != null && Directory.Exists(path)) continue;

            Utils.Trace($"Creating new symbolic link at {path} with target {pathToTarget}");
            if (path != null) Directory.CreateSymbolicLink(path, pathToTarget);
        }
        Utils.TraceOut();
    }

    private void UpdateMediaFilesCountDisplay()
    {
        totalFilesTextBox.Invoke(x => x.Text = mediaBackup.GetBackupFilesNotMarkedAsDeleted().Count().ToString("N0"));
        totalFilesSizeTextBox.Invoke(x => x.Text = Utils.FormatSize(mediaBackup.GetBackupFilesNotMarkedAsDeleted().Sum(y => y.Length)));
        notOnABackupDiskTextBox.Invoke(x => x.Text = mediaBackup.GetBackupFilesWithDiskEmpty().Count().ToString("N0"));
        notOnABackupDiskSizeTextBox.Invoke(x => x.Text = Utils.FormatSize(mediaBackup.GetBackupFilesWithDiskEmpty().Sum(y => y.Length)));
        filesMarkedAsDeletedTextBox.Invoke(x => x.Text = mediaBackup.GetBackupFilesMarkedAsDeleted().Count().ToString("N0"));
        filesMarkedAsDeletedSizeTextBox.Invoke(x => x.Text = Utils.FormatSize(mediaBackup.GetBackupFilesMarkedAsDeleted().Sum(y => y.Length)));
    }

    private void SetupDailyTrigger(bool addTrigger)
    {
        Utils.TraceIn();
        updateUITimer.Enabled = true; // because we want to update the folder tracking every 1 min or so anyway

        if (addTrigger)
        {
            trigger = new DailyTrigger(scheduledDateTimePicker.Value);
            trigger.OnTimeTriggered += scheduledBackupAction;
            Utils.Trace("SetupDailyTrigger OnTimeTriggered added");
            UpdateUI_Tick(null, null);
        }
        else
        {
            if (trigger != null)
            {
                trigger.OnTimeTriggered -= scheduledBackupAction;
                Utils.Trace("SetupDailyTrigger OnTimeTriggered removed");
            }
            timeToNextRunTextBox.Text = string.Empty;
        }
        Utils.TraceOut();
    }

    private void StartFileSystemWatchers()
    {
        Utils.TraceIn();

        if (mediaBackup.Watcher == null || mediaBackup.Watcher.Directories.Length == 0)
        {
            mediaBackup.Watcher = new FileSystemWatcher
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
                Directories = mediaBackup.Config.MasterFolders.ToArray(),
                ProcessChangesInterval = mediaBackup.Config.MasterFoldersProcessChangesTimer,
                ScanInterval = mediaBackup.Config.MasterFoldersScanTimer,
                Filter = "*.*",
                IncludeSubdirectories = true
            };
            mediaBackup.Watcher.ReadyToScan += FileSystemWatcher_ReadyToScan;
            mediaBackup.Watcher.Error += FileSystemWatcher_OnError;
            mediaBackup.Watcher.MinimumAgeBeforeScanEventRaised = mediaBackup.Config.MasterFolderScanMinimumAgeBeforeScanning;

            foreach (var item in mediaBackup.FileOrFolderChanges)
            {
                mediaBackup.Watcher.FileSystemChanges.Add(new FileSystemEntry(item.Path, item.ModifiedDateTime), ct);
            }

            foreach (var item in mediaBackup.FoldersToScan)
            {
                mediaBackup.Watcher.DirectoriesToScan.Add(new FileSystemEntry(item.Path, item.ModifiedDateTime), ct);
            }
        }
        mediaBackup.Watcher.Start();
        Utils.TraceOut();
    }

    private void ScheduledBackup()
    {
        Utils.TraceIn();

        try
        {
            // check the service monitor is running
            // Take a copy of the current count of files we backup up last time
            // Then ScanFolders
            // If the new file count is less than x% lower then abort
            // This happens if the server running the backup cannot connect to the nas devices sometimes
            // It'll then delete everything off the connected backup disk as it doesn't think they're needed so this will prevent that

            if (mediaBackup.Config.MonitoringOnOff)
                Utils.LogWithPushover(BackupAction.General, $"Service monitoring is running every {mediaBackup.Config.MonitoringInterval} seconds");
            else
                Utils.LogWithPushover(BackupAction.General, PushoverPriority.High, "Service monitoring is not running");
            long oldFileCount = mediaBackup.BackupFiles.Count;
            var doFullBackup = false;
            _ = DateTime.TryParse(mediaBackup.MasterFoldersLastFullScan, out var backupFileDate);
            if (backupFileDate.AddDays(mediaBackup.Config.MasterFoldersDaysBetweenFullScan) < DateTime.Now) doFullBackup = true;

            // Update the master files if we've not been monitoring folders directly
            if (!mediaBackup.Config.MasterFoldersFileChangeWatchersOnOff || doFullBackup)
            {
                ScanFolders();
                UpdateSymbolicLinks();
            }

            if (mediaBackup.Config.BackupDiskDifferenceInFileCountAllowedPercentage != 0)
            {
                var minimumFileCountAllowed = oldFileCount - oldFileCount * mediaBackup.Config.BackupDiskDifferenceInFileCountAllowedPercentage / 100;
                long newFileCount = mediaBackup.BackupFiles.Count;

                if (newFileCount < minimumFileCountAllowed)
                    throw new Exception("ERROR: The count of files to backup is too low. Check connections to nas drives");
            }

            // checks for backup disks not verified in > xx days
            CheckForOldBackupDisks();

            // Check the connected backup disk (removing any extra files we don't need)
            _ = CheckConnectedDisk(true);

            // Copy any files that need a backup
            CopyFiles(true);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Utils.LogWithPushover(BackupAction.General, PushoverPriority.Emergency, $"Exception occurred {ex}");
        }
        Utils.TraceOut();
    }

    private void ScheduledBackupAsync()
    {
        // This is so if the timer goes off to start and we're doing something else it's skipped
        if (longRunningActionExecutingRightNow) return;

        longRunningActionExecutingRightNow = true;
        DisableControlsForAsyncTasks();
        ScheduledBackup();

        // reset the daily trigger
        SetupDailyTrigger(mediaBackup.Config.ScheduledBackupOnOff);
        Utils.Trace($"TriggerHour={trigger.TriggerHour}");
        ResetAllControls();
        longRunningActionExecutingRightNow = false;
    }

    private void CheckForOldBackupDisks()
    {
        Utils.TraceIn();
        var numberOfDays = mediaBackup.Config.BackupDiskDaysToReportSinceFilesChecked;
        var files = mediaBackup.BackupFiles.Where(p => p.DiskChecked.HasValue() && DateTime.Parse(p.DiskChecked).AddDays(numberOfDays) < DateTime.Today);
        var backupFiles = files as BackupFile[] ?? files.ToArray();
        var disks = backupFiles.GroupBy(p => p.Disk).Select(p => p.First());

        foreach (var disk in disks)
        {
            Utils.LogWithPushover(BackupAction.CheckBackupDisk, $"Backup disks not checked in {numberOfDays} days - {disk.Disk}");
        }

        Utils.Log(BackupAction.General,
            !backupFiles.Any()
                ? $"All files checked in last {mediaBackup.Config.BackupDiskDaysToReportSinceFilesChecked} days"
                : $"Listing files not checked in {mediaBackup.Config.BackupDiskDaysToReportSinceFilesChecked} days");

        foreach (var file in backupFiles)
        {
            var days = DateTime.Today.Subtract(DateTime.Parse(file.DiskChecked)).Days;
            Utils.Log(BackupAction.General, $"{file.FullPath} - not checked in {days} day(s) on disk {file.Disk}");
        }
        Utils.TraceOut();
    }

    private void UpdateSendingPushoverButton()
    {
        Utils.TraceIn();
        pushoverOnOffButton.Text = mediaBackup.Config.PushoverOnOff ? "Sending = ON" : "Sending = OFF";
        Utils.TraceOut();
    }

    private void SetupFileWatchers()
    {
        Utils.TraceIn();

        if (mediaBackup.Config.MasterFoldersFileChangeWatchersOnOff)
        {
            fileWatcherButton.Text = Resources.Main_SetupFileWatchersOn;
            StartFileSystemWatchers();
        }
        else
        {
            fileWatcherButton.Text = Resources.Main_SetupFileWatchersOff;
            StopFileSystemWatchers();
        }
    }

    private void StopFileSystemWatchers()
    {
        mediaBackup.Watcher.Stop();
    }

    private void UpdateMonitoringButton()
    {
        Utils.TraceIn();
        monitoringButton.Text = mediaBackup.Config.MonitoringOnOff ? "Monitoring = ON" : "Monitoring = OFF";
        Utils.TraceOut();
    }

    private void UpdateSpeedTestDisksButton()
    {
        Utils.TraceIn();
        speedTestDisksButton.Text = mediaBackup.Config.SpeedTestOnOff ? "Speed Test Disks = ON" : "Speed Test Disks = OFF";
        Utils.TraceOut();
    }

    private void UpdateScheduledBackupButton()
    {
        Utils.TraceIn();
        scheduledBackupTimerButton.Text = mediaBackup.Config.ScheduledBackupOnOff ? "Backup = ON" : "Backup = OFF";
        Utils.TraceOut();
    }

    [SupportedOSPlatform("windows")]
    private void MonitorServices()
    {
        foreach (var monitor in mediaBackup.Config.Monitors)
        {
            var result = monitor.Port > 0 ? Utils.ConnectionExists(monitor.Url, monitor.Port) : Utils.UrlExists(monitor.Url, monitor.Timeout * 1000);
            if (result) continue;

            // The monitor is down
            monitor.UpdateFailures(DateTime.Now);
            if (monitor.FailureRetryExceeded) continue;

            var s = monitor.Failures.Count > 1 ? "s" : string.Empty;
            var text = $"'{monitor.Name}' is down. {monitor.Failures.Count} failure{s} in the last {Utils.FormatTimeFromSeconds(monitor.FailureTimePeriod)}.";
            Utils.LogWithPushover(BackupAction.Monitoring, PushoverPriority.High, text);

            if (monitor.ProcessToKill.HasValue())
            {
                var processesToKill = monitor.ProcessToKill.Split(',');

                foreach (var toKill in processesToKill)
                {
                    Utils.LogWithPushover(BackupAction.Monitoring, PushoverPriority.Normal, $"Stopping all '{toKill}' processes that match");
                    _ = Utils.KillProcesses(toKill);
                }
            }

            if (monitor.ApplicationToStart.HasValue())
            {
                text = $"Starting {monitor.ApplicationToStart}";
                Utils.LogWithPushover(BackupAction.Monitoring, PushoverPriority.Normal, text);
                var processToStart = Environment.ExpandEnvironmentVariables(monitor.ApplicationToStart);

                if (File.Exists(processToStart))
                {
                    var newProcess = Process.Start(processToStart, monitor.ApplicationToStartArguments);

                    if (newProcess == null)
                        Utils.LogWithPushover(BackupAction.Monitoring, PushoverPriority.High, $"Failed to start the new process '{monitor.Name}'");
                    else
                        Utils.LogWithPushover(BackupAction.Monitoring, PushoverPriority.Normal, $"'{monitor.Name}' started");
                }
                else
                {
                    Utils.LogWithPushover(BackupAction.Monitoring, PushoverPriority.High,
                        $"Failed to start the new process '{monitor.Name}' as its not found at {monitor.ApplicationToStart} (expanded to {processToStart})");
                }
            }
            if (!monitor.ServiceToRestart.HasValue()) continue;

            text = $"Restarting '{monitor.ServiceToRestart}'";
            Utils.LogWithPushover(BackupAction.Monitoring, PushoverPriority.Normal, text);
            result = Utils.RestartService(monitor.ServiceToRestart, monitor.Timeout * 1000);

            if (result)
                Utils.LogWithPushover(BackupAction.Monitoring, PushoverPriority.Normal, $"'{monitor.Name}' started");
            else
                Utils.LogWithPushover(BackupAction.Monitoring, PushoverPriority.High, $"Failed to restart the service '{monitor.Name}'");
        }
    }

    private void SpeedTestAllMasterFoldersAsync()
    {
        longRunningActionExecutingRightNow = true;
        DisableControlsForAsyncTasks();
        Utils.Log("Speed testing all master folders");
        EnableProgressBar(0, mediaBackup.Config.MasterFolders.Count);

        for (var i = 0; i < mediaBackup.Config.MasterFolders.Count; i++)
        {
            var masterFolder = mediaBackup.Config.MasterFolders[i];
            UpdateStatusLabel($"Speed testing {masterFolder}", i + 1);
            if (!Utils.IsFolderWritable(masterFolder)) continue;

            Utils.DiskSpeedTest(masterFolder, Utils.ConvertMBtoBytes(mediaBackup.Config.SpeedTestFileSize), mediaBackup.Config.SpeedTestIterations,
                out var readSpeed, out var writeSpeed);
            Utils.Log($"testing {masterFolder}, Read: {Utils.FormatSpeed(readSpeed)} Write: {Utils.FormatSpeed(writeSpeed)}");
        }
        ResetAllControls();
        longRunningActionExecutingRightNow = false;
    }

    private void UpdateEstimatedFinish()
    {
        estimatedFinishTimeTextBox.Invoke(x => x.Text = string.Empty);
    }

    private void UpdateEstimatedFinish(DateTime estimatedFinishDateTime)
    {
        estimatedFinishTimeTextBox.Invoke(x => x.Text = estimatedFinishDateTime.ToString("HH:mm"));
    }

    public void TaskWrapper(Action methodName)
    {
        if (methodName is null) throw new ArgumentNullException(nameof(methodName));

        tokenSource = new CancellationTokenSource();
        ct = tokenSource.Token;

        Task.Run(methodName, ct).ContinueWith(u =>
        {
            if (u.Exception == null) return;

            Utils.Log("Exception occurred. Cancelling operation.");
            MessageBox.Show(string.Format(Resources.Main_TaskWrapperException, u.Exception));
            CancelButton_Click(null, null);
        }, default, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.FromCurrentSynchronizationContext());
    }

    public void TaskWrapper(Action<bool> methodName, bool param1)
    {
        if (methodName is null) throw new ArgumentNullException(nameof(methodName));

        tokenSource = new CancellationTokenSource();
        ct = tokenSource.Token;

        Task.Run(() => methodName(param1), ct).ContinueWith(u =>
        {
            if (u.Exception == null) return;

            Utils.Log("Exception occurred. Cancelling operation.");
            _ = MessageBox.Show(string.Format(Resources.Main_TaskWrapperException, u.Exception));
            CancelButton_Click(null, null);
        }, default, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.FromCurrentSynchronizationContext());
    }

    public void TaskWrapper(Action<bool, bool> methodName, bool param1, bool param2)
    {
        if (methodName is null) throw new ArgumentNullException(nameof(methodName));

        tokenSource = new CancellationTokenSource();
        ct = tokenSource.Token;

        Task.Run(() => methodName(param1, param2), ct).ContinueWith(u =>
        {
            if (u.Exception == null) return;

            Utils.Log("Exception occurred. Cancelling operation.");
            _ = MessageBox.Show(string.Format(Resources.Main_TaskWrapperException, u.Exception));
            CancelButton_Click(null, null);
        }, default, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.FromCurrentSynchronizationContext());
    }

    private void DisableControlsForAsyncTasks()
    {
        if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();

        foreach (Control c in Controls)
        {
            if (c is not StatusStrip) c.Invoke(x => x.Enabled = false);
        }
        cancelButton.Invoke(x => x.Enabled = true);
        testPushoverEmergencyButton.Invoke(x => x.Enabled = true);
        testPushoverHighButton.Invoke(x => x.Enabled = true);
        testPushoverNormalButton.Invoke(x => x.Enabled = true);
        testPushoverLowButton.Invoke(x => x.Enabled = true);
        listFilesInMasterFolderButton.Invoke(x => x.Enabled = true);
        listFilesNotCheckedInXXButton.Invoke(x => x.Enabled = true);
        listFilesNotOnBackupDiskButton.Invoke(x => x.Enabled = true);
        listFilesOnBackupDiskButton.Invoke(x => x.Enabled = true);
        listFilesWithDuplicateContentHashcodesButton.Invoke(x => x.Enabled = true);
        listMoviesWithMultipleFilesButton.Invoke(x => x.Enabled = true);
        processesGroupBox.Invoke(x => x.Enabled = true);
        pushoverGroupBox.Invoke(x => x.Enabled = true);
        listFilesGroupBox.Invoke(x => x.Enabled = true);
        allBackupDisksGroupBox.Invoke(x => x.Enabled = true);
        reportBackupDiskStatusButton.Invoke(x => x.Enabled = true);
        listFilesInMasterFolderGroupBox.Invoke(x => x.Enabled = true);
        checkAllBackupDisksButton.Invoke(x => x.Enabled = false);
        checkDeleteAndCopyAllBackupDisksButton.Invoke(x => x.Enabled = false);
        monitoringButton.Invoke(x => x.Enabled = true);
        reportBackupDiskStatusButton.Invoke(x => x.Enabled = true);
        listFilesInMasterFolderGroupBox.Invoke(x => x.Enabled = true);
        listFilesOnBackupDiskGroupBox.Invoke(x => x.Enabled = true);
        if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();
    }

    /// <summary>
    ///     Re-enables all the form controls (typically after Cancel was clicked or we've finished)
    /// </summary>
    private void ResetAllControls()
    {
        if (!IsHandleCreated || IsDisposed) return;

        foreach (Control c in Controls)
        {
            c.Invoke(x => x.Enabled = true);
        }
        checkAllBackupDisksButton.Invoke(x => x.Enabled = true);
        checkDeleteAndCopyAllBackupDisksButton.Invoke(x => x.Enabled = true);
        cancelButton.Invoke(x => x.Enabled = false);
        statusStrip.Invoke(_ => toolStripProgressBar.Visible = false);
        statusStrip.Invoke(_ => toolStripStatusLabel.Text = string.Empty);
    }

    /// <summary>
    ///     Returns True if the file was deleted
    /// </summary>
    /// <param name="filePath"></param>
    /// <returns></returns>
    private bool CheckForFilesToDelete(string filePath)
    {
        var filters = mediaBackup.Config.FilesToDelete
            .Select(filter => new { filter, replace = filter.Replace(".", @"\.").Replace("*", ".*").Replace("?", ".") }).Select(t => $"^{t.replace}$");
        var fileName = new FileInfo(filePath).Name;
        if (!filters.Any(pattern => Regex.IsMatch(fileName, pattern))) return false;

        Utils.LogWithPushover(BackupAction.ScanFolders, PushoverPriority.Normal, $"File matches RegEx and so will be deleted {filePath}");
        Utils.FileDelete(filePath);
        return true;
    }

    private void UpdateStatusLabel(string text, int value = 0)
    {
        if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();
        var textToUse = string.Empty;

        if (value > 0)
        {
            var progress = value * 100 / toolStripProgressBar.Maximum;

            progress = progress switch
            {
                0 => 1,
                > 99 => 99,
                _ => progress
            };
            textToUse = $"{text}     {progress}%";
        }
        else
        {
            if (!text.EndsWith("...") && !text.EndsWith(".")) textToUse = text + "...";
        }
        UpdateProgressBar(value);
        statusStrip.Invoke(_ => toolStripStatusLabel.Text = textToUse);
        if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();
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
            statusStrip.Invoke(_ => toolStripProgressBar.Visible = true);
            statusStrip.Invoke(_ => toolStripProgressBar.Value = value);
        }
        else
        {
            statusStrip.Invoke(_ => toolStripProgressBar.Value = toolStripProgressBar.Minimum);
            statusStrip.Invoke(_ => toolStripProgressBar.Visible = false);
        }
    }
}