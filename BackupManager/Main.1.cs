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
    private Config config;

    private readonly Dictionary<string, HashSet<string>> episodesForASeason = new(); // key=showName:season, value=hashset of episodes

    private MediaBackup mediaBackup;

    private Action monitoringAction;

    /// <summary>
    ///     For the monitoring applications
    /// </summary>
    private readonly CancellationTokenSource monitoringCancellationTokenSource = new();

    private Action scheduledBackupAction;

    private readonly Dictionary<string, HashSet<string>> tvShowEditions = new(); // key=showName, value=hashset of editions

    private readonly Dictionary<string, HashSet<string>> tvShowSeasons = new(); // key=showName, value=hashset of seasons

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

    private Dictionary<string, MovieBackupFile> movieNames;

    private int reportedPercentComplete;

    // ReSharper disable once FunctionComplexityOverflow
    internal Main()
    {
        try
        {
            InitializeComponent();
            TraceConfiguration.Register();
            SetupDebugListeners();
            LoadXmlAndSetupConfig();
            UpdateBackupDiskTextBoxFromConfig();
            if (Utils.IsRunningAsAdmin()) Text += Resources.AdminTitle;
            UpdateMediaFilesCountDisplay();
            Utils.LogHeader();
            Utils.LogWithPushover(BackupAction.General, Resources.BackupManagerStarted, false, true);
            config.LogParameters();
            mediaBackup.CheckMovieAndTvForDuplicates();
            mediaBackup.CheckForDuplicateTvEpisodesGlobally();
            PopulateComboBoxesWithDirectories();
            AddTvShowsToCaches();
            AddMoviesToCaches();
            CheckDirectoriesForMaxPath();
            PopulateComboBoxesWithBackupDisks();
            SetupPushoverCheckBoxes();
            PopulateComboBoxesWithServices();
            SetupMonitoring();
            UpdateSendingPushoverButton();
            UpdateScheduledBackupButton();
            UpdateSpeedTestDisksButton();
            SetupMonitoringButton();
            _ = UpdateCurrentBackupDiskInfo(mediaBackup.GetBackupDisk(backupDiskTextBox.Text));
            RunScheduledBackupOnStartupIfRequired();
            SetupDailyTrigger(config.ScheduledBackupOnOff, scheduledDateTimePicker.Value);
            SetupFileWatchers();
            UpdateUI_Tick(null, null);
            VersionCheckingButton_Click(null, null);
            Utils.TraceOut();
        }
        catch (Exception ex)
        {
            _ = MessageBox.Show(string.Format(Resources.ExceptionOccured, ex));
            Environment.Exit(0);
        }
    }

    private void CheckDirectoriesForMaxPath()
    {
        foreach (var file in mediaBackup.BackupFiles.Where(static file => file.FullPath.Length > Utils.MAX_PATH))
        {
            Utils.LogWithPushover(BackupAction.Error, string.Format(Resources.PathTooLong, file.FullPath));
        }
    }

    private void LoadXmlAndSetupConfig()
    {
        var localMediaXml = Path.Combine(Application.StartupPath, "MediaBackup.xml");
        mediaBackup = MediaBackup.Load(File.Exists(localMediaXml) ? localMediaXml : ConfigurationManager.AppSettings.Get("MediaBackupXml"));
        config = mediaBackup.Config;
        Utils.Config = config;
    }

    private static void SetupDebugListeners()
    {
        if (!Utils.InDebugBuild) return;

        _ = Trace.Listeners.Add(new TextWriterTraceListener(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "BackupManager_Trace.log"),
            "myListener"));
    }

    private void RunScheduledBackupOnStartupIfRequired()
    {
        if (!config.ScheduledBackupRunOnStartup) return;

        ResetTokenSource();
        _ = TaskWrapper(() => ScheduledBackupAsync(mainCt), mainCt);
    }

    private void SetupMonitoringButton()
    {
        // we switch it off and force the button to be clicked to turn it on again
        config.MonitoringOnOff = !config.MonitoringOnOff;
        MonitoringButton_Click(null, null);
    }

    private void SetupPushoverCheckBoxes()
    {
        pushoverLowCheckBox.Checked = config.PushoverSendLowOnOff;
        pushoverNormalCheckBox.Checked = config.PushoverSendNormalOnOff;
        pushoverHighCheckBox.Checked = config.PushoverSendHighOnOff;
        pushoverEmergencyCheckBox.Checked = config.PushoverSendEmergencyOnOff;
    }

    private void SetupMonitoring()
    {
        scheduledBackupAction = () =>
        {
            ResetTokenSource();
            _ = TaskWrapper(() => ScheduledBackupAsync(mainCt), mainCt);
        };
        monitoringAction = MonitorServices;
        scheduledDateTimePicker.Value = DateTime.Parse(config.ScheduledBackupStartTime);

        // we switch it off and force the button to be clicked to turn it on again
        config.MonitoringCheckLatestVersions = !config.MonitoringCheckLatestVersions;
    }

    private void PopulateComboBoxesWithServices()
    {
        foreach (var monitor in config.Monitors)
        {
            _ = processesComboBox.Items.Add(monitor.Name);
        }
    }

    private void PopulateComboBoxesWithBackupDisks()
    {
        foreach (var disk in mediaBackup.BackupDisks)
        {
            _ = listFilesComboBox.Items.Add(disk.Name);
        }
    }

    private void PopulateComboBoxesWithDirectories()
    {
        var directoriesArray = config.DirectoriesToBackup.ToArray();
        listDirectoriesComboBox.Items.AddRange([.. directoriesArray]);
        directoriesComboBox.Items.AddRange([.. directoriesArray]);
        restoreDirectoryComboBox.Items.AddRange([.. directoriesArray]);
        scanDirectoryComboBox.Items.AddRange([.. directoriesArray]);
    }

    private void AddMoviesToCaches()
    {
        movieNames = new Dictionary<string, MovieBackupFile>();

        foreach (var fileFullPath in from file in mediaBackup.BackupFiles
                 let fileFullPath = file.FullPath
                 where !file.Deleted && Utils.File.IsMovieComedyOrConcert(fileFullPath) && !Utils.File.IsSpecialFeature(fileFullPath)
                 select fileFullPath)
        {
            if (Utils.MediaHelper.ExtendedBackupFileBase(fileFullPath) is not MovieBackupFile movie) continue;
            if (!movie.TmdbId.HasValue()) continue;

            var edition = movie.Edition == Edition.Unknown ? string.Empty : movie.Edition.ToEnumMember();
            movieNames.TryAdd($"{Convert.ToInt32(movie.TmdbId),0:D7} - {movie.Title} ({movie.ReleaseYear}) {edition}", movie);
        }
        movieComboBox.Items.AddRange([.. movieNames.OrderBy(static i => i.Value.Title).ToDictionary(static i => i.Key, static i => i.Value).Keys]);
    }

    private void AddTvShowsToCaches()
    {
        // use a dictionary so we can order by the .Values.Title later
        var tvShowNames = new Dictionary<string, TvEpisodeBackupFile>();

        foreach (var fileFullPath in from file in mediaBackup.BackupFiles
                 let fileFullPath = file.FullPath
                 where !file.Deleted && Utils.File.IsTv(fileFullPath) && !Utils.File.IsSpecialFeature(fileFullPath)
                 select fileFullPath)
        {
            if (Utils.MediaHelper.ExtendedBackupFileBase(fileFullPath) is not TvEpisodeBackupFile tvEp) continue;

            tvShowNames.TryAdd($"{Convert.ToInt32(tvEp.TvdbId),0:D6} - {tvEp.Title}", tvEp);
            if (!tvShowSeasons.ContainsKey(tvEp.Title)) tvShowSeasons.Add(tvEp.Title, []);
            tvShowSeasons[tvEp.Title].Add(tvEp.Season);
            var key = $"{tvEp.Title}:{tvEp.Season}";
            if (!episodesForASeason.ContainsKey(key)) episodesForASeason.Add(key, []);
            episodesForASeason[key].Add(tvEp.Episode);
            if (tvEp.Edition.HasNoValue()) continue;

            if (!tvShowEditions.ContainsKey(tvEp.Title)) tvShowEditions.Add(tvEp.Title, []);
            tvShowEditions[tvEp.Title].Add(tvEp.Edition);
        }
        tvShowComboBox.Items.AddRange([.. tvShowNames.OrderBy(static i => i.Value.Title).ToDictionary(static i => i.Key, static i => i.Value).Keys]);
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
        HashSet<string> hashSet = [];

        // HashSet of parent paths that match the RegEx's from config
        foreach (var path in from backupFile in mediaBackup.BackupFiles
                 select mediaBackup.GetParentPath(backupFile.FullPath)
                 into directoryPath
                 where directoryPath != null
                 where config.SymbolicLinks.Select(a => Regex.Match(directoryPath, a.FileDiscoveryRegEx)).Any(static m => m.Success)
                 select directoryPath)
        {
            ct.ThrowIfCancellationRequested();
            _ = hashSet.Add(path);
        }
        UpdateStatusLabel(ct, "Checking for broken Symbolic Links");

        var directoriesToCheck = config.SymbolicLinks.Select(static a => Path.Combine(a.RootDirectory, Utils.RemoveRegexGroupsFromString(a.RelativePath)))
            .Where(Directory.Exists).SelectMany(Directory.EnumerateDirectories).ToArray();

        // check the symbolic links root folders for any broken links
        EnableProgressBar(0, directoriesToCheck.Length);
        int percentCompleteCurrent;
        var percentCompleteReported = 0;

        for (var i = 0; i < directoriesToCheck.Length; i++)
        {
            ct.ThrowIfCancellationRequested();
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
                Utils.Log(BackupAction.CheckingSymbolicLinks, $"Symbolic link {link} deleted");
            }
        }
        Utils.LogWithPushover(BackupAction.CheckingSymbolicLinks, Resources.Completed);
        EnableProgressBar(0, 100);
        var counter = 0;
        percentCompleteReported = 0;

        foreach (var path in hashSet)
        {
            ct.ThrowIfCancellationRequested();
            counter++;
            percentCompleteCurrent = Convert.ToInt32(counter * 100 / hashSet.Count);
            UpdateStatusLabel(ct, string.Format(Resources.Checking, path), percentCompleteCurrent);

            if (percentCompleteCurrent % 25 == 0 && percentCompleteCurrent > percentCompleteReported && hashSet.Count > 100)
            {
                percentCompleteReported = percentCompleteCurrent;
                Utils.LogWithPushover(BackupAction.CheckingSymbolicLinks, string.Format(Resources.UpdatingPercentage, percentCompleteCurrent));
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
            if (path == null) continue;

            _ = Directory.CreateSymbolicLink(path, pathToTarget);
            Utils.LogWithPushover(BackupAction.ScanDirectory, $"Creating new symbolic link at {path} with target {pathToTarget}");
        }
        Utils.TraceOut();
    }

    private void UpdateMediaFilesCountDisplay()
    {
        Utils.TraceIn();

        try
        {
            var backupFilesWithoutDeleted = mediaBackup.GetBackupFiles(false).ToArray();
            var backupFilesWithDiskEmpty = mediaBackup.BackupFiles.Where(static p => (p.Disk.HasNoValue() || p.BeingCheckedNow) && !p.Deleted).ToArray();
            var backupFilesMarkedAsDeleted = mediaBackup.GetBackupFilesMarkedAsDeleted(false).ToArray();
            totalFilesTextBox.TextWithInvoke(backupFilesWithoutDeleted.Length.ToString("N0"));
            totalFilesSizeTextBox.TextWithInvoke(backupFilesWithoutDeleted.Sum(static y => y.Length).SizeSuffix());
            notOnABackupDiskTextBox.TextWithInvoke(backupFilesWithDiskEmpty.Length.ToString("N0"));
            notOnABackupDiskSizeTextBox.TextWithInvoke(backupFilesWithDiskEmpty.Sum(static y => y.Length).SizeSuffix());
            filesMarkedAsDeletedTextBox.TextWithInvoke(backupFilesMarkedAsDeleted.Length.ToString("N0"));
            filesMarkedAsDeletedSizeTextBox.TextWithInvoke(backupFilesMarkedAsDeleted.Sum(static y => y.Length).SizeSuffix());
        }
        catch (InvalidOperationException)
        {
            // if collections are modified by checking the disk then we cant update the UI here
        }
        finally
        {
            Utils.TraceOut();
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
        mediaBackup.Watcher.Directories = [.. config.DirectoriesToBackup];
        mediaBackup.Watcher.ProcessChangesInterval = config.DirectoriesProcessChangesTimer;
        mediaBackup.Watcher.ScanInterval = config.DirectoriesScanTimer;
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
            _ = mediaBackup.Watcher.DirectoriesToScan.AddOrUpdate(new FileSystemEntry(item.Path, item.ModifiedDateTime));
        }
        Utils.TraceOut();
    }

    private void CheckForOldBackupDisks()
    {
        Utils.TraceIn();
        var numberOfDays = config.BackupDiskDaysToReportSinceFilesChecked;
        var files = mediaBackup.BackupFiles.Where(p => p.DiskCheckedTime.HasValue && p.DiskCheckedTime.Value.AddDays(numberOfDays) < DateTime.Today);
        var backupFiles = files as BackupFile[] ?? files.ToArray();
        var disks = backupFiles.GroupBy(static p => p.Disk).Select(static p => p.First());

        foreach (var disk in disks)
        {
            Utils.LogWithPushover(BackupAction.CheckBackupDisk, string.Format(Resources.CheckForOldBackupDisks, numberOfDays, disk.Disk));
        }

        Utils.Log(BackupAction.General,
            !(backupFiles.Length > 0)
                ? string.Format(Resources.AllFilesCheckedInLastNDays, config.BackupDiskDaysToReportSinceFilesChecked)
                : string.Format(Resources.ListingFilesNotCheckedInDays, config.BackupDiskDaysToReportSinceFilesChecked));

        foreach (var file in backupFiles)
        {
            if (file.DiskCheckedTime == null) continue;

            var days = DateTime.Today.Subtract(file.DiskCheckedTime.Value).Days;
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

        try
        {
            fileWatcherButton.Text = string.Format(Resources.FileWatchersButton, config.DirectoriesFileChangeWatcherOnOff ? Resources.ON : Resources.OFF);

            if (config.DirectoriesFileChangeWatcherOnOff)
                StartFileSystemWatchers();
            else
                StopFileSystemWatchers();
        }
        catch (ArgumentException ex)
        {
            // This gets thrown if one of the monitored directories does not exist
            Utils.LogWithPushover(BackupAction.General, $"Exception when trying to start the monitored directories {ex}");
            fileWatcherButton.Text = string.Format(Resources.FileWatchersButton, Resources.OFF);
        }
    }

    private void StopFileSystemWatchers()
    {
        _ = mediaBackup.Watcher.Stop();
    }

    private void UpdateMonitoringButton()
    {
        Utils.TraceIn();

        monitoringButton.TextWithInvoke(config.MonitoringOnOff
            ? string.Format(Resources.MonitoringButton, Resources.ON)
            : string.Format(Resources.MonitoringButton, Resources.OFF));
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
            var disksAndFirstDirectories = Utils.GetDiskAndFirstDirectory(config.DirectoriesToBackup);
            EnableProgressBar(0, disksAndFirstDirectories.Length);

            for (var i = 0; i < disksAndFirstDirectories.Length; i++)
            {
                var directory = disksAndFirstDirectories[i];
                UpdateStatusLabel(ct, string.Format(Resources.SpeedTesting, directory), i + 1);
                if (!Utils.Directory.IsWritable(directory)) continue;

                Utils.DiskSpeedTest(directory, Utils.ConvertMBtoBytes(config.SpeedTestFileSize), config.SpeedTestIterations, out var readSpeed, out var writeSpeed, ct);
                Utils.LogWithPushover(BackupAction.SpeedTest, $"{directory}, Read: {Utils.FormatSpeed(readSpeed)} Write: {Utils.FormatSpeed(writeSpeed)}");
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

    /// <summary>
    ///     Disables controls that cant be enabled when long-running tasks are executing. Sets
    ///     longRunningActionExecutingRightNow = true
    /// </summary>
    /// <param name="ct"></param>
    private void DisableControlsForAsyncTasks(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        longRunningActionExecutingRightNow = true;

        foreach (var c in Controls.Cast<Control>().Where(static c => c is not StatusStrip))
        {
            c.Invoke(static x => x.Enabled = false);
        }

        // ReSharper disable BadListLineBreaks
        var controlsToEnable = new Control[]
        {
            cancelButton, testPushoverEmergencyButton, testPushoverHighButton, testPushoverNormalButton, testPushoverLowButton, listFilesInDirectoryButton,
            listFilesNotCheckedInXXButton, listFilesNotOnBackupDiskButton, listFilesOnBackupDiskButton, listFilesWithDuplicateContentHashcodesButton,
            listMoviesWithMultipleFilesButton, processesGroupBox, pushoverGroupBox, listFilesGroupBox, listBackupDiskStatusByDiskNumberButton,
            listFilesInDirectoryGroupBox, monitoringButton, listBackupDiskStatusByFreeSpaceButton, listFilesOnBackupDiskGroupBox, openLogFileButton,
            allBackupDisksGroupBox
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
        Utils.TraceIn();
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
        Utils.TraceOut();
    }

    private void UpdateStatusLabel(CancellationToken ct, string text = "", int value = 0)
    {
        Utils.TraceIn(value);
        ct.ThrowIfCancellationRequested();
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
            textToUse = $"{progress}% - {text}";
        }
        else
        {
            if (text != string.Empty && !text.EndsWithIgnoreCase(".")) textToUse = text + " ...";
        }
        UpdateProgressBar(value);
        textToUse = textToUse.Replace("&", "&&");
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