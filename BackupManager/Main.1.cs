using System;
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

        var foldersToCheck = mediaBackup.Config.SymbolicLinks.Select(a => Path.Combine(a.RootFolder, Utils.RemoveRegexGroupsFromString(a.RelativePath)))
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
            Utils.RegexPathFixer(m, ref path, ref pathToTarget);

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

    private void StartFileSystemWatchers()
    {
        Utils.TraceIn();
        if (mediaBackup.Watcher == null || mediaBackup.Watcher.Directories.Length == 0) mediaBackup.Watcher = SetupWatcher();
        mediaBackup.Watcher.Start();
        Utils.TraceOut();
    }

    private FileSystemWatcher SetupWatcher()
    {
        Utils.TraceIn();

        var watcher = new FileSystemWatcher
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
        return Utils.TraceOut(watcher);
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
            if (!Utils.IsDirectoryWritable(masterFolder)) continue;

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