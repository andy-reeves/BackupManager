// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="Main.1.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

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

internal sealed partial class Main
{
    private readonly MediaBackup mediaBackup;

    private readonly Action monitoringAction;

    private readonly Action scheduledBackupAction;

    private CancellationToken ct;

    /// <summary>
    ///     Any long-running action sets this to TRUE to stop the scheduledBackup timer from being able to start
    /// </summary>
    private bool longRunningActionExecutingRightNow;

    private CancellationTokenSource tokenSource;

    private DailyTrigger trigger;

    [SupportedOSPlatform("windows")]
    internal Main()
    {
        try
        {
            InitializeComponent();
            TraceConfiguration.Register();
#if DEBUG
            Trace.Listeners.Add(new TextWriterTraceListener(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "BackupManager_Trace.log"), "myListener"));

            // ReSharper disable StringLiteralTypo
            // ReSharper disable CommentTypo
            // backupDiskTextBox.Text = @"\\nas1\assets1\_Test\BackupDisks\backup 1001 parent";
            backupDiskTextBox.Text = Path.Combine(@"\\", Environment.MachineName, "backup");

            // ReSharper restore CommentTypo
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
            var directoriesArray = mediaBackup.Config.Directories.ToArray();
            listDirectoriesComboBox.Items.AddRange(directoriesArray.ToArray<object>());
            directoriesComboBox.Items.AddRange(directoriesArray.ToArray<object>());
            restoreDirectoryComboBox.Items.AddRange(directoriesArray.ToArray<object>());

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
        foreach (var path in from backupFile in mediaBackup.BackupFiles
                 select mediaBackup.GetParentPath(backupFile.FullPath)
                 into directoryPath
                 where directoryPath != null
                 where mediaBackup.Config.SymbolicLinks.Select(a => Regex.Match(directoryPath, a.FileDiscoveryRegEx)).Any(static m => m.Success)
                 select directoryPath)
        {
            hashSet.Add(path);
        }
        UpdateStatusLabel("Checking for broken Symbolic Links");

        var directoriesToCheck = mediaBackup.Config.SymbolicLinks
            .Select(static a => Path.Combine(a.RootDirectory, Utils.RemoveRegexGroupsFromString(a.RelativePath))).Where(Directory.Exists)
            .SelectMany(Directory.EnumerateDirectories).ToList();

        // check the symbolic links root folders for any broken links
        EnableProgressBar(0, directoriesToCheck.Count);

        for (var i = 0; i < directoriesToCheck.Count; i++)
        {
            var directoryToCheck = directoriesToCheck[i];
            UpdateStatusLabel(string.Format(Resources.Main_Checking, directoryToCheck), i);
            var linksDeleted = Utils.DeleteBrokenSymbolicLinks(directoryToCheck, true);

            foreach (var link in linksDeleted)
            {
                Utils.Log($"Symbolic link {link} deleted");
            }
        }
        EnableProgressBar(0, 100);
        var fileCounter = 0;

        foreach (var path in hashSet)
        {
            fileCounter++;
            UpdateStatusLabel(string.Format(Resources.Main_Checking, path), Convert.ToInt32(fileCounter * 100 / hashSet.Count));
            UpdateSymbolicLinkForDirectory(path);
        }
        UpdateStatusLabel(Resources.Main_Completed);
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

        foreach (var a in mediaBackup.Config.SymbolicLinks)
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
        totalFilesSizeTextBox.Invoke(x => x.Text = Utils.FormatSize(mediaBackup.GetBackupFilesNotMarkedAsDeleted().Sum(static y => y.Length)));
        notOnABackupDiskTextBox.Invoke(x => x.Text = mediaBackup.GetBackupFilesWithDiskEmpty().Count().ToString("N0"));
        notOnABackupDiskSizeTextBox.Invoke(x => x.Text = Utils.FormatSize(mediaBackup.GetBackupFilesWithDiskEmpty().Sum(static y => y.Length)));
        filesMarkedAsDeletedTextBox.Invoke(x => x.Text = mediaBackup.GetBackupFilesMarkedAsDeleted().Count().ToString("N0"));
        filesMarkedAsDeletedSizeTextBox.Invoke(x => x.Text = Utils.FormatSize(mediaBackup.GetBackupFilesMarkedAsDeleted().Sum(static y => y.Length)));
    }

    private void StartFileSystemWatchers()
    {
        Utils.TraceIn();
        if (mediaBackup.Watcher.Directories.Length == 0) SetupWatcher();
        mediaBackup.Watcher.Start();
        Utils.Trace($"mediaBackup.Watcher.Running = {mediaBackup.Watcher.Running}");
        Utils.TraceOut();
    }

    private void SetupWatcher()
    {
        Utils.TraceIn();
        var watcher = mediaBackup.Watcher;
        watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName;
        watcher.Directories = mediaBackup.Config.Directories.ToArray();
        watcher.ProcessChangesInterval = mediaBackup.Config.DirectoriesProcessChangesTimer;
        watcher.ScanInterval = mediaBackup.Config.DirectoriesScanTimer;
        watcher.Filter = "*.*";

        watcher.RegexFilter =
            mediaBackup.Config.DirectoriesFilterRegEx; // @".*(?<!\.tmp)$"; match all files except *.tmp (.*(?<!\.tmp)|.*\\_Backup\\.*)$
        watcher.IncludeSubdirectories = true;
        watcher.ReadyToScan += FileSystemWatcher_ReadyToScan;
        watcher.Error += FileSystemWatcher_OnError;
        watcher.MinimumAgeBeforeScanEventRaised = mediaBackup.Config.DirectoriesMinimumAgeBeforeScanning;

        foreach (var item in mediaBackup.DirectoryChanges)
        {
            watcher.FileSystemChanges.Add(new FileSystemEntry(item.Path, item.ModifiedDateTime), ct);
        }

        foreach (var item in mediaBackup.DirectoriesToScan)
        {
            watcher.DirectoriesToScan.Add(new FileSystemEntry(item.Path, item.ModifiedDateTime), ct);
        }
        Utils.TraceOut();
    }

    private void CheckForOldBackupDisks()
    {
        Utils.TraceIn();
        var numberOfDays = mediaBackup.Config.BackupDiskDaysToReportSinceFilesChecked;

        var files = mediaBackup.BackupFiles.Where(p =>
            p.DiskChecked.HasValue() && DateTime.Parse(p.DiskChecked).AddDays(numberOfDays) < DateTime.Today);
        var backupFiles = files as BackupFile[] ?? files.ToArray();
        var disks = backupFiles.GroupBy(static p => p.Disk).Select(static p => p.First());

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

        pushoverOnOffButton.Text = string.Format(Resources.Main_SendingPushoverButton,
            mediaBackup.Config.PushoverOnOff ? Resources.Main_ON : Resources.Main_OFF);
        Utils.TraceOut();
    }

    private void SetupFileWatchers()
    {
        Utils.TraceIn();

        fileWatcherButton.Text = string.Format(Resources.Main_FileWatchersButton,
            mediaBackup.Config.DirectoriesFileChangeWatchersOnOff ? Resources.Main_ON : Resources.Main_OFF);

        if (mediaBackup.Config.DirectoriesFileChangeWatchersOnOff)
            StartFileSystemWatchers();
        else
            StopFileSystemWatchers();
    }

    private void StopFileSystemWatchers()
    {
        mediaBackup.Watcher.Stop();
    }

    private void UpdateMonitoringButton()
    {
        Utils.TraceIn();

        monitoringButton.Text = mediaBackup.Config.MonitoringOnOff
            ? string.Format(Resources.Main_MonitoringButton, Resources.Main_ON)
            : string.Format(Resources.Main_MonitoringButton, Resources.Main_OFF);
        Utils.TraceOut();
    }

    private void UpdateSpeedTestDisksButton()
    {
        Utils.TraceIn();

        speedTestDisksButton.Text = string.Format(Resources.Main_SpeedTestDisksButton,
            mediaBackup.Config.SpeedTestOnOff ? Resources.Main_ON : Resources.Main_OFF);
        Utils.TraceOut();
    }

    private void UpdateScheduledBackupButton()
    {
        Utils.TraceIn();

        scheduledBackupTimerButton.Text = string.Format(Resources.Main_UpdateScheduledBackupButton,
            mediaBackup.Config.ScheduledBackupOnOff ? Resources.Main_ON : Resources.Main_OFF);
        Utils.TraceOut();
    }

    private void SpeedTestAllDirectoriesAsync()
    {
        longRunningActionExecutingRightNow = true;
        DisableControlsForAsyncTasks();
        Utils.Log("Speed testing all directories");
        EnableProgressBar(0, mediaBackup.Config.Directories.Count);

        for (var i = 0; i < mediaBackup.Config.Directories.Count; i++)
        {
            var directory = mediaBackup.Config.Directories[i];
            UpdateStatusLabel($"Speed testing {directory}", i + 1);
            if (!Utils.IsDirectoryWritable(directory)) continue;

            Utils.DiskSpeedTest(directory, Utils.ConvertMBtoBytes(mediaBackup.Config.SpeedTestFileSize), mediaBackup.Config.SpeedTestIterations,
                out var readSpeed, out var writeSpeed, ct);
            Utils.Log($"testing {directory}, Read: {Utils.FormatSpeed(readSpeed)} Write: {Utils.FormatSpeed(writeSpeed)}");
        }
        ResetAllControls();
        longRunningActionExecutingRightNow = false;
    }

    private void UpdateEstimatedFinish()
    {
        estimatedFinishTimeTextBox.Invoke(static x => x.Text = string.Empty);
    }

    private void UpdateEstimatedFinish(DateTime estimatedFinishDateTime)
    {
        estimatedFinishTimeTextBox.Invoke(x => x.Text = estimatedFinishDateTime.ToString(Resources.DateTime_HHmm));
    }

    private void DisableControlsForAsyncTasks()
    {
        if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();

        foreach (var c in Controls.Cast<Control>().Where(static c => c is not StatusStrip))
        {
            c.Invoke(static x => x.Enabled = false);
        }

        // ReSharper disable BadListLineBreaks
        var controlsToEnable = new Control[]
        {
            cancelButton, testPushoverEmergencyButton, testPushoverHighButton, testPushoverNormalButton, testPushoverLowButton,
            listFilesInDirectoryButton, listFilesNotCheckedInXXButton, listFilesNotOnBackupDiskButton, listFilesOnBackupDiskButton,
            listFilesWithDuplicateContentHashcodesButton, listMoviesWithMultipleFilesButton, processesGroupBox, pushoverGroupBox,
            listFilesGroupBox, allBackupDisksGroupBox, reportBackupDiskStatusButton, listFilesInDirectoryGroupBox, checkAllBackupDisksButton,
            checkDeleteAndCopyAllBackupDisksButton, monitoringButton, reportBackupDiskStatusButton, listFilesInDirectoryGroupBox,
            listFilesOnBackupDiskGroupBox
        };

        // ReSharper restore BadListLineBreaks

        foreach (var control in controlsToEnable)
        {
            control.Invoke(static x => x.Enabled = true);
        }
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
            c.Invoke(static x => x.Enabled = true);
        }
        checkAllBackupDisksButton.Invoke(static x => x.Enabled = true);
        checkDeleteAndCopyAllBackupDisksButton.Invoke(static x => x.Enabled = true);
        cancelButton.Invoke(static x => x.Enabled = false);
        statusStrip.Invoke(_ => toolStripProgressBar.Visible = false);
        statusStrip.Invoke(_ => toolStripStatusLabel.Text = string.Empty);
    }

    private void UpdateStatusLabel(string text = "", int value = 0)
    {
        Utils.TraceIn(value);
        if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();
        text = text.Trim();
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
            if (text != string.Empty && !text.EndsWith(".", StringComparison.Ordinal)) textToUse = text + " ...";
        }
        UpdateProgressBar(value);
        statusStrip.Invoke(_ => toolStripStatusLabel.Text = textToUse);
        if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();
        Utils.TraceOut();
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