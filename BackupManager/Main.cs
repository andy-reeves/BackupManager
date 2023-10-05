// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="Main.cs" company="Andy Reeves">
//
//  </copyright>
//  --------------------------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using BackupManager.Entities;
using BackupManager.Extensions;
using BackupManager.Properties;
#if DEBUG
using System.Runtime.CompilerServices;
#endif

[assembly: AssemblyVersion("1.0.0.0")]
[assembly: NeutralResourcesLanguage("en-GB")]

#if DEBUG
[assembly: InternalsVisibleTo("TestProject")]
#endif

namespace BackupManager;

public partial class Main : Form
{
    /// <summary>
    ///     This is a Dictionary of files/folders where changes have been detected and the last time they changed
    /// </summary>
    private static readonly Dictionary<string, DateTime> FolderChanges = new();

    private readonly MediaBackup mediaBackup;

    private readonly Action monitoringAction;

    private readonly Action scheduledBackupAction;

    private readonly List<FileSystemWatcher> watcherList = new();

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

            backupDiskTextBox.Text = "\\\\nas1\\assets1\\_Test\\BackupDisks\\backup 1001 parent";

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

            Utils.Trace($"masterFoldersArray.Length = {masterFoldersArray.Length}");
            listMasterFoldersComboBox.Items.AddRange(masterFoldersArray);
            masterFoldersComboBox.Items.AddRange(masterFoldersArray);
            restoreMasterFolderComboBox.Items.AddRange(masterFoldersArray);

            foreach (var disk in mediaBackup.BackupDisks)
            {
                listFilesComboBox.Items.Add(disk.Name);
            }

            pushoverLowCheckBox.Checked = mediaBackup.Config.PushoverSendLowONOFF;
            pushoverNormalCheckBox.Checked = mediaBackup.Config.PushoverSendNormalONOFF;
            pushoverHighCheckBox.Checked = mediaBackup.Config.PushoverSendHighONOFF;
            pushoverEmergencyCheckBox.Checked = mediaBackup.Config.PushoverSendEmergencyONOFF;

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

            if (mediaBackup.Config.MonitoringONOFF)
            {
                // we switch it off and force the button to be clicked to turn it on again
                mediaBackup.Config.MonitoringONOFF = false;
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

            SetupDailyTrigger(mediaBackup.Config.ScheduledBackupONOFF);

            processFolderChangesTimer.Interval = mediaBackup.Config.MasterFoldersProcessChangesTimer * 1000;
            scanFoldersTimer.Interval = mediaBackup.Config.MasterFoldersScanTimer * 1000;

            SetupFileWatchers();

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

    private static void FileSystemWatcher_OnSomethingHappened(object sender, FileSystemEventArgs e)
    {
        Utils.Trace("FileSystemWatcher_OnSomethingHappened enter");
        Utils.Trace($"e.FullPath = {e.FullPath}");

        if (e.ChangeType is not WatcherChangeTypes.Changed and not WatcherChangeTypes.Deleted and not WatcherChangeTypes.Renamed)
        {
            Utils.Trace("FileSystemWatcher_OnSomethingHappened exit as not changed/deleted/renamed");
            return;
        }

        // add this changed folder/file to the list to potentially scan
        if (FolderChanges.ContainsKey(e.FullPath))
            FolderChanges[e.FullPath] = DateTime.Now;
        else
            FolderChanges.Add(e.FullPath, DateTime.Now);

        Utils.Trace("FileSystemWatcher_OnSomethingHappened exit");
    }

    private static void FileSystemWatcher_OnError(object sender, ErrorEventArgs e)
    {
        var ex = e.GetException();

        Utils.LogWithPushover(BackupAction.General, $"Message: {ex.Message}");
        Utils.LogWithPushover(BackupAction.General, $"Stacktrace: {ex.StackTrace}");
    }

    private void CheckDiskAndDeleteButton_Click(object sender, EventArgs e)
    {
        Utils.Trace("checkDiskAndDeleteButton_Click enter");

        if (MessageBox.Show(Resources.Main_AreYouSureDelete, Resources.Main_DeleteExtraTitle, MessageBoxButtons.YesNo) == DialogResult.Yes)
            TaskWrapper(CheckConnectedDiskAndCopyFilesAsync, true, false);

        Utils.Trace("checkDiskAndDeleteButton_Click exit");
    }

    private void BackupTimerButton_Click(object sender, EventArgs e)
    {
        Utils.Trace("timerButton_Click enter");

        mediaBackup.Config.ScheduledBackupONOFF = !mediaBackup.Config.ScheduledBackupONOFF;

        SetupDailyTrigger(mediaBackup.Config.ScheduledBackupONOFF);

        UpdateScheduledBackupButton();
        Utils.Trace("timerButton_Click exit");
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

    private void ScheduledBackupAsync()
    {
        // This is so if the timer goes off to start and we're doing something else it's skipped
        if (longRunningActionExecutingRightNow) return;

        longRunningActionExecutingRightNow = true;
        DisableControlsForAsyncTasks();

        ScheduledBackup();

        // reset the daily trigger
        SetupDailyTrigger(mediaBackup.Config.ScheduledBackupONOFF);

        Utils.Trace($"TriggerHour={trigger.TriggerHour}");

        ResetAllControls();
        longRunningActionExecutingRightNow = false;
    }

    private void ScheduledBackup()
    {
        Utils.Trace("ScheduledBackup enter");

        try
        {
            // check the service monitor is running
            // Take a copy of the current count of files we backup up last time
            // Then ScanFolders
            // If the new file count is less than x% lower then abort
            // This happens if the server running the backup cannot connect to the nas devices sometimes
            // It'll then delete everything off the connected backup disk as it doesn't think they're needed so this will prevent that

            if (mediaBackup.Config.MonitoringONOFF)
                Utils.LogWithPushover(BackupAction.General, $"Service monitoring is running every {mediaBackup.Config.MonitoringInterval} seconds");
            else
                Utils.LogWithPushover(BackupAction.General, PushoverPriority.High, "Service monitoring is not running");

            long oldFileCount = mediaBackup.BackupFiles.Count;

            var doFullBackup = false;
            _ = DateTime.TryParse(mediaBackup.MasterFoldersLastFullScan, out var backupFileDate);

            if (backupFileDate.AddDays(mediaBackup.Config.MasterFoldersDaysBetweenFullScan) < DateTime.Now) doFullBackup = true;

            // Update the master files if we've not been monitoring folders directly
            if (!mediaBackup.Config.MasterFoldersFileChangeWatchersONOFF || doFullBackup) _ = ScanFolders();

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
        catch (OperationCanceledException)
        {
        }

        catch (Exception ex)
        {
            Utils.LogWithPushover(BackupAction.General, PushoverPriority.Emergency, $"Exception occured {ex}");
        }

        Utils.Trace("ScheduledBackup exit");
    }

    private void ListFilesOnBackupDiskButton_Click(object sender, EventArgs e)
    {
        Utils.Trace("listFilesOnBackupDiskButton_Click enter");

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

        Utils.Trace("listFilesOnBackupDiskButton_Click exit");
    }

    private void ListFilesInMasterFolderButton_Click(object sender, EventArgs e)
    {
        Utils.Trace("listFilesInMasterFolderButton_Click enter");

        var masterFolder = listMasterFoldersComboBox.SelectedItem.ToString();

        var files = mediaBackup.GetBackupFilesInMasterFolder(masterFolder);

        Utils.Log($"Listing files in master folder {masterFolder}");

        if (mediaBackup.Config.SpeedTestONOFF)
        {
            Utils.DiskSpeedTest(masterFolder, Utils.ConvertMBtoBytes(mediaBackup.Config.SpeedTestFileSize), mediaBackup.Config.SpeedTestIterations,
                out var readSpeed, out var writeSpeed);
            Utils.Log($"Testing {masterFolder}, Read: {Utils.FormatSpeed(readSpeed)} Write: {Utils.FormatSpeed(writeSpeed)}");
        }

        foreach (var file in files)
        {
            Utils.Log($"{file.FullPath} : {file.Disk}");

            if (string.IsNullOrEmpty(file.Disk)) Utils.Log($"{file.FullPath} : not on a backup disk");
        }

        Utils.Trace("listFilesInMasterFolderButton_Click exit");
    }

    private void CheckForOldBackupDisks_Click(object sender, EventArgs e)
    {
        Utils.Trace("CheckForOldBackupDisks_Click enter");

        CheckForOldBackupDisks();

        Utils.Trace("CheckForOldBackupDisks_Click exit");
    }

    private void CheckForOldBackupDisks()
    {
        Utils.Trace("CheckForOldBackupDisks enter");

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

        Utils.Trace("CheckForOldBackupDisks exit");
    }

    private void RestoreFilesButton_Click(object sender, EventArgs e)
    {
        Utils.Trace("restoreFilesButton_Click enter");

        // loop through all the files looking for the master folder specified in the top drop down and copy to the bottom drop down 
        // for each file order by backup disk
        // prompt for the back up disk to be inserted 
        // check we have it inserted
        // copy any files off this disk until we're all done to the new disk that we specified
        if (MessageBox.Show(Resources.Main_RestoreFiles, Resources.Main_RestoreFilesTitle, MessageBoxButtons.YesNo) == DialogResult.Yes)
        {
            if (masterFoldersComboBox.SelectedItem == null)
            {
                _ = MessageBox.Show(Resources.Main_RestoreFilesSelectAMasterFolder, Resources.Main_RestoreFilesTitle, MessageBoxButtons.OK);
                return;
            }

            var masterFolder = masterFoldersComboBox.SelectedItem.ToString();

            if (restoreMasterFolderComboBox.SelectedItem == null)
            {
                _ = MessageBox.Show(Resources.Main_RestoreFilesMasterFolderToRestoreTo, Resources.Main_RestoreFilesTitle, MessageBoxButtons.OK);
                return;
            }

            var targetMasterFolder = restoreMasterFolderComboBox.SelectedItem.ToString();

            var files = mediaBackup.GetBackupFilesInMasterFolder(masterFolder).Where(p => p.Disk.HasValue());

            Utils.Log(BackupAction.Restore, $"Restoring files from master folder {masterFolder}");
            Utils.Log(BackupAction.Restore, $"Restoring files to target master folder {targetMasterFolder}");

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
                        _ = MessageBox.Show(Resources.Main_BackupDiskConnectCorrectDisk, Resources.Main_RestoreFilesTitle, MessageBoxButtons.OK);
                        return;
                    }

                    if (file.Disk != lastBackupDisk)
                    {
                        if (!mediaBackup.Config.DisksToSkipOnRestore.Contains(lastBackupDisk, StringComparer.CurrentCultureIgnoreCase) &&
                            lastBackupDisk.HasValue())
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
                    var sourceFileFullPath = Path.Combine(backupShare, file.Disk, file.IndexFolder, file.RelativePath);

                    Debug.Assert(targetMasterFolder != null, nameof(targetMasterFolder) + " != null");
                    var targetFilePath = Path.Combine(targetMasterFolder, file.IndexFolder, file.RelativePath);

                    if (File.Exists(targetFilePath))
                    {
                        Utils.LogWithPushover(BackupAction.Restore, $"[{fileCounter}/{countOfFiles}] {targetFilePath} Already exists");
                    }
                    else
                    {
                        if (File.Exists(sourceFileFullPath))
                        {
                            Utils.LogWithPushover(BackupAction.Restore, $"[{fileCounter}/{countOfFiles}] Copying {sourceFileFullPath} as {targetFilePath}");
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
                            file.MasterFolder = targetMasterFolder;
                        else
                            Utils.LogWithPushover(BackupAction.Restore, PushoverPriority.High, $"ERROR: '{targetFilePath}' has a different Hashcode");
                    }
                }

                lastBackupDisk = file.Disk;
            }

            mediaBackup.Save();
        }

        Utils.Trace("restoreFilesButton_Click exit");
    }

    private void CheckBackupDeleteAndCopyButton_Click(object sender, EventArgs e)
    {
        Utils.Trace("checkBackupDeleteAndCopyButton_Click enter");

        if (MessageBox.Show(Resources.Main_AreYouSureDelete, Resources.Main_DeleteExtraTitle, MessageBoxButtons.YesNo) == DialogResult.Yes)
            TaskWrapper(CheckConnectedDiskAndCopyFilesAsync, true, true);

        Utils.Trace("checkBackupDeleteAndCopyButton_Click exit");
    }

    private void ListMoviesWithMultipleFilesButton_Click(object sender, EventArgs e)
    {
        Utils.Trace("listMoviesWithMultipleFilesButton_Click enter");

        Utils.Log("Listing movies with multiple files in folder");

        Dictionary<string, BackupFile> allMovies = new();

        List<BackupFile> backupFilesWithDuplicates = new();

        foreach (var file in mediaBackup.BackupFiles)
        {
            const string pattern =
                @"^(?:.*\\_Movies(?:\s\(non-tmdb\))?\\(.*)\s({tmdb-\d{1,7}?})\s(?:{edition-(?:(?:[1-7][05]TH\sANNIVERSARY)|4K|BLURAY|CHRONOLOGICAL|COLLECTORS|(?:CRITERION|KL\sSTUDIO)\sCOLLECTION|DIAMOND|DVD|IMAX|REDUX|REMASTERED|RESTORED|SPECIAL|(?:THE\sCOMPLETE\s)?EXTENDED|THE\sGODFATHER\sCODA|(?:THE\sRICHARD\sDONNER|DIRECTORS|FINAL)\sCUT|THEATRICAL|ULTIMATE|UNCUT|UNRATED)}\s)?\[(?:DVD|SDTV|(?:WEB(?:Rip|DL)|Bluray|HDTV|Remux)-(?:48|72|108|216)0p)\](?:\[(?:(?:DV)?(?:(?:\s)?HDR10(?:Plus)?)?|PQ|3D)\])?\[(?:DTS(?:\sHD|-(?:X|ES|HD\s(?:M|HR)A))?|(?:TrueHD|EAC3)(?:\sAtmos)?|AC3|FLAC|PCM|MP3|A[AV]C|Opus)\s(?:[1-8]\.[01])\]\[(?:[hx]26[45]|MPEG[24]|HEVC|XviD|V(?:C1|P9)|AVC)\])\.(m(?:kv|p(?:4|e?g))|ts|avi)$";

            var m = Regex.Match(file.FullPath, pattern);

            if (!m.Success) continue;

            var movieId = m.Groups[2].Value;

            if (allMovies.TryGetValue(movieId, out var movie))
            {
                if (!backupFilesWithDuplicates.Contains(file)) backupFilesWithDuplicates.Add(file);

                if (!backupFilesWithDuplicates.Contains(movie)) backupFilesWithDuplicates.Add(movie);
            }
            else
            {
                allMovies.Add(movieId, file);
            }
        }

        var sortedList = backupFilesWithDuplicates.OrderBy(o => o.FileName).ToList();

        foreach (var backupMovieDuplicate in sortedList)
        {
            Utils.Log($"{backupMovieDuplicate.FullPath}");
        }

        Utils.Trace("listMoviesWithMultipleFilesButton_Click exit");
    }

    private void TestPushoverHighButton_Click(object sender, EventArgs e)
    {
        Utils.Trace("testPushoverHighButton_Click enter");

        Utils.LogWithPushover(BackupAction.General, PushoverPriority.High, "High priority test");

        Utils.Trace("testPushoverHighButton_Click exit");
    }

    private void TestPushoverNormalButton_Click(object sender, EventArgs e)
    {
        Utils.Trace("testPushoverNormalButton_Click enter");

        Utils.LogWithPushover(BackupAction.General, PushoverPriority.Normal, "Normal priority test\nLine 2\nLine 3");

        Utils.Trace("testPushoverNormalButton_Click exit");
    }

    private void TestPushoverEmergencyButton_Click(object sender, EventArgs e)
    {
        Utils.Trace("testPushoverEmergencyButton_Click enter");

        Utils.LogWithPushover(BackupAction.General, PushoverPriority.Emergency, PushoverRetry.OneMinute, PushoverExpires.OneHour, "Emergency priority test");

        Utils.Trace("testPushoverEmergencyButton_Click exit");
    }

    private void ReportBackupDiskStatusButton_Click(object sender, EventArgs e)
    {
        Utils.Trace("reportBackupDiskStatusButton_Click enter");

        IEnumerable<BackupDisk> disks = mediaBackup.BackupDisks.OrderBy(p => p.Number);

        Utils.Log("Listing backup disk statuses");

        long actualUsableSpace = 0;

        foreach (var disk in disks)
        {
            var lastChecked = string.Empty;

            if (disk.Checked.HasValue())
            {
                var d = DateTime.Parse(disk.Checked);
                lastChecked = d.ToString("dd-MMM-yy");
            }

            var totalSizeOfFilesFromSumOfFiles = mediaBackup.GetBackupFilesOnBackupDisk(disk.Name, false).Sum(p => p.Length);

            var deletedCount = mediaBackup.GetBackupFilesOnBackupDisk(disk.Name, true).Count() -
                               mediaBackup.GetBackupFilesOnBackupDisk(disk.Name, false).Count();

            var sizeFromDiskAnalysis = disk.Capacity - disk.Free;

            var difference = totalSizeOfFilesFromSumOfFiles > sizeFromDiskAnalysis ? 0 : sizeFromDiskAnalysis - totalSizeOfFilesFromSumOfFiles;
            double percentageDiff = difference * 100 / sizeFromDiskAnalysis;

            var percentString = percentageDiff is < 1 and > -1 ? "-" : $"{percentageDiff}%";

            Utils.Log(
                $"{disk.Name,-11}   Last check: {lastChecked,-9}   Capacity: {disk.CapacityFormatted,-7}   Used: {Utils.FormatSize(sizeFromDiskAnalysis),-8}   Free: {disk.FreeFormatted,-7}   Sum of files: {Utils.FormatSize(totalSizeOfFilesFromSumOfFiles),-7}  DeletedFiles: {deletedCount,-6} Diff: {Utils.FormatSize(difference),-9} {percentString}");

            if (disk.Free > Utils.ConvertMBtoBytes(mediaBackup.Config.BackupDiskMinimumFreeSpaceToLeave))
                actualUsableSpace += disk.Free - Utils.ConvertMBtoBytes(mediaBackup.Config.BackupDiskMinimumFreeSpaceToLeave);
        }

        var totalSizeFormatted = Utils.FormatSize(mediaBackup.BackupDisks.Sum(p => p.Capacity));
        var totalFreeSpaceFormatted = Utils.FormatSize(mediaBackup.BackupDisks.Sum(p => p.Free));

        Utils.Log(
            $"Total Capacity: {totalSizeFormatted} Free: {totalFreeSpaceFormatted}  UsableSpace: {Utils.FormatSize(actualUsableSpace)} Sum of files: {Utils.FormatSize(mediaBackup.BackupFiles.Sum(p => p.Length))}");

        Utils.Trace("reportBackupDiskStatusButton_Click exit");
    }

    private void SpeedTestButton_Click(object sender, EventArgs e)
    {
        Utils.Trace("speedTestButton_Click enter");

        TaskWrapper(SpeedTestAllMasterFoldersAsync);

        Utils.Trace("speedTestButton_Click exit");
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

    private void MonitoringButton_Click(object sender, EventArgs e)
    {
        Utils.TraceIn();

        if (mediaBackup.Config.MonitoringONOFF)
        {
            monitoringTimer.Stop();
            Utils.LogWithPushover(BackupAction.Monitoring, "Stopped");
            mediaBackup.Config.MonitoringONOFF = false;
        }
        else
        {
            Utils.LogWithPushover(BackupAction.Monitoring, "Started");

            MonitoringTimer_Tick(null, null);

            monitoringTimer.Interval = mediaBackup.Config.MonitoringInterval * 1000;
            monitoringTimer.Start();
            mediaBackup.Config.MonitoringONOFF = true;
        }

        UpdateMonitoringButton();

        Utils.TraceOut();
    }

    private void MonitoringTimer_Tick(object sender, EventArgs e)
    {
        TaskWrapper(monitoringAction);
    }

    [SupportedOSPlatform("windows")]
    private void MonitorServices()
    {
        //Utils.Trace("MonitorServices enter");

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

        //Utils.Trace("MonitorServices exit");
    }

    [SupportedOSPlatform("windows")]
    private void KillProcessesButton_Click(object sender, EventArgs e)
    {
        Utils.Trace("killProcessesButton_Click enter");

        foreach (var monitor in mediaBackup.Config.Monitors)
        {
            if (monitor.ProcessToKill.HasValue())
            {
                var processesToKill = monitor.ProcessToKill.Split(',');

                foreach (var toKill in processesToKill)
                {
                    Utils.LogWithPushover(BackupAction.Monitoring, PushoverPriority.Normal, $"Stopping all '{toKill}' processes that match");

                    _ = Utils.KillProcesses(toKill);
                }
            }

            if (!monitor.ServiceToRestart.HasValue()) continue;

            Utils.LogWithPushover(BackupAction.Monitoring, PushoverPriority.Normal, $"Stopping '{monitor.ServiceToRestart}'");

            if (!Utils.StopService(monitor.ServiceToRestart, 5000))
                Utils.LogWithPushover(BackupAction.Monitoring, PushoverPriority.High, $"Failed to stop the service '{monitor.Name}'");
        }

        Utils.Trace("killProcessesButton_Click exit");
    }

    private void TestPushoverLowButton_Click(object sender, EventArgs e)
    {
        Utils.Trace("testPushoverLowButton_Click enter");

        Utils.LogWithPushover(BackupAction.General, PushoverPriority.Low, "Low priority test\nLine 2\nLine 3");

        Utils.Trace("testPushoverLowButton_Click exit");
    }

    private void Main_FormClosed(object sender, FormClosedEventArgs e)
    {
        Utils.Trace("Main_FormClosed enter");

        Utils.LogWithPushover(BackupAction.General, PushoverPriority.High, "BackupManager stopped");

        Utils.Trace("Main_FormClosed exit");

        Utils.BackupLogFile();
    }

    [SupportedOSPlatform("windows")]
    private void StopProcessButton_Click(object sender, EventArgs e)
    {
        Utils.Trace("stopProcessButton_Click enter");

        if (processesComboBox.SelectedItem != null)
        {
            var monitorName = processesComboBox.SelectedItem.ToString();

            var monitor = mediaBackup.Config.Monitors.First(m => m.Name == monitorName);

            if (monitor.ProcessToKill.HasValue())
            {
                var processesToKill = monitor.ProcessToKill.Split(',');

                foreach (var toKill in processesToKill)
                {
                    Utils.LogWithPushover(BackupAction.Monitoring, PushoverPriority.Normal, $"Stopping all '{toKill}' processes that match");

                    _ = Utils.KillProcesses(toKill);
                }
            }

            if (monitor.ServiceToRestart.HasValue())
            {
                Utils.LogWithPushover(BackupAction.Monitoring, PushoverPriority.Normal, $"Stopping '{monitor.ServiceToRestart}'");

                if (!Utils.StopService(monitor.ServiceToRestart, 5000))
                    Utils.LogWithPushover(BackupAction.Monitoring, PushoverPriority.High, $"Failed to stop the service '{monitor.Name}'");
            }
        }

        Utils.Trace("stopProcessButton_Click exit");
    }

    private void ListFilesWithDuplicateContentHashcodesButton_Click(object sender, EventArgs e)
    {
        Utils.Log("Checking for files with Duplicate ContentsHash in _Movies and _TV");

        Dictionary<string, BackupFile> allFilesUniqueContentsHash = new();
        List<BackupFile> backupFilesWithDuplicates = new();

        foreach (var f in mediaBackup.BackupFiles)
        {
            if (f.FullPath.Contains("_Movies") || f.FullPath.Contains("_TV"))
            {
                if (allFilesUniqueContentsHash.TryGetValue(f.ContentsHash, out var originalFile))
                {
                    backupFilesWithDuplicates.Add(f);

                    if (!backupFilesWithDuplicates.Contains(originalFile)) backupFilesWithDuplicates.Add(originalFile);
                }
                else
                {
                    allFilesUniqueContentsHash.Add(f.ContentsHash, f);
                }
            }
        }

        foreach (var f in backupFilesWithDuplicates)
        {
            Utils.Log($"{f.FullPath} has a duplicate");
        }
    }

    private void CheckDeleteAndCopyAllBackupDisksButton_Click(object sender, EventArgs e)
    {
        // Check the current backup disk connected
        // when its finished prompt for another disk and wait
        Utils.Trace("CheckDeleteAndCopyAllBackupDisksButton_Click enter");

        if (MessageBox.Show(Resources.Main_AreYouSureDelete, Resources.Main_DeleteExtraTitle, MessageBoxButtons.YesNo) == DialogResult.Yes)
            TaskWrapper(CheckConnectedDiskAndCopyFilesRepeaterAsync, true);

        Utils.Trace("CheckDeleteAndCopyAllBackupDisksButton_Click exit");
    }

    private void WaitForNewDisk(string message)
    {
        Utils.Trace("WaitForNewDisk enter");

        UpdateStatusLabel(message);
        Task.Delay(5000).Wait();

        Utils.Trace("WaitForNewDisk exit");
    }

    private void CancelButton_Click(object sender, EventArgs e)
    {
        tokenSource.Cancel();

        toolStripStatusLabel.Text = Resources.Main_Cancelling;

        if (Utils.CopyProcess != null && !Utils.CopyProcess.HasExited) Utils.CopyProcess?.Kill();

        cancelButton.Enabled = false;
        longRunningActionExecutingRightNow = false;
        ResetAllControls();

        UpdateMediaFilesCountDisplay();

        tokenSource = null;
    }

    private void CheckAllBackupDisksButton_Click(object sender, EventArgs e)
    {
        // Check the current backup disk connected
        // when its finished prompt for another disk and wait

        Utils.Trace("CheckAllBackupDisksButton_Click enter");

        if (MessageBox.Show(Resources.Main_AreYouSureDelete, Resources.Main_DeleteExtraTitle, MessageBoxButtons.YesNo) == DialogResult.Yes)
            TaskWrapper(CheckConnectedDiskAndCopyFilesRepeaterAsync, false);

        Utils.Trace("CheckAllBackupDisksButton_Click exit");
    }

    private void PushoverOnOffButton_Click(object sender, EventArgs e)
    {
        mediaBackup.Config.PushoverONOFF = !mediaBackup.Config.PushoverONOFF;
        UpdateSendingPushoverButton();
    }

    private void UpdateSendingPushoverButton()
    {
        pushoverOnOffButton.Text = mediaBackup.Config.PushoverONOFF ? "Sending = ON" : "Sending = OFF";
    }

    private void UpdateMonitoringButton()
    {
        monitoringButton.Text = mediaBackup.Config.MonitoringONOFF ? "Monitoring = ON" : "Monitoring = OFF";
    }

    private void UpdateSpeedTestDisksButton()
    {
        speedTestDisksButton.Text = mediaBackup.Config.SpeedTestONOFF ? "Speed Test Disks = ON" : "Speed Test Disks = OFF";
    }

    private void UpdateScheduledBackupButton()
    {
        scheduledBackupTimerButton.Text = mediaBackup.Config.ScheduledBackupONOFF ? "Backup = ON" : "Backup = OFF";
    }

    private void PushoverLowCheckBox_CheckedChanged(object sender, EventArgs e)
    {
        mediaBackup.Config.PushoverSendLowONOFF = pushoverLowCheckBox.Checked;
    }

    private void PushoverNormalCheckBox_CheckedChanged(object sender, EventArgs e)
    {
        mediaBackup.Config.PushoverSendNormalONOFF = pushoverNormalCheckBox.Checked;
    }

    private void PushoverHighCheckBox_CheckedChanged(object sender, EventArgs e)
    {
        mediaBackup.Config.PushoverSendHighONOFF = pushoverHighCheckBox.Checked;
    }

    private void PushoverEmergencyCheckBox_CheckedChanged(object sender, EventArgs e)
    {
        mediaBackup.Config.PushoverSendEmergencyONOFF = pushoverEmergencyCheckBox.Checked;
    }

    private void SpeedTestDisksButton_Click(object sender, EventArgs e)
    {
        mediaBackup.Config.SpeedTestONOFF = !mediaBackup.Config.SpeedTestONOFF;
        UpdateSpeedTestDisksButton();
    }

    private void RefreshBackupDiskButton_Click(object sender, EventArgs e)
    {
        Utils.Trace("RefreshBackupDiskButton_Click enter");

        TaskWrapper(SetupBackupDiskAsync);

        Utils.Trace("RefreshBackupDiskButton_Click exit");
    }

    private void ListFilesMarkedAsDeletedButton_Click(object sender, EventArgs e)
    {
        Utils.TraceIn();

        var files = mediaBackup.GetBackupFilesMarkedAsDeleted();

        Utils.Log("Listing files marked as deleted");

        var backupFiles = files as BackupFile[] ?? files.ToArray();

        foreach (var file in backupFiles)
        {
            Utils.Log($"{file.FullPath} at {Utils.FormatSize(file.Length)} on {file.Disk}");
        }

        Utils.Log($"{backupFiles.Length} files at {Utils.FormatSize(backupFiles.Sum(p => p.Length))}");

        Utils.TraceOut();
    }

    private void ScheduledBackupRunNowButton_Click(object sender, EventArgs e)
    {
        Utils.TraceIn();

        TaskWrapper(ScheduledBackupAsync);

        Utils.TraceOut();
    }

    private void UpdateUI_Tick(object sender, EventArgs e)
    {
        timeToNextRunTextBox.Text = trigger == null || !updateUITimer.Enabled ? string.Empty : trigger.TimeToNextTrigger().ToString("h'h 'mm'm'");

        foldersToScanTextBox.Text = mediaBackup.FoldersToScan.Count.ToString();
        fileChangesDetectedTextBox.Text = FolderChanges.Count.ToString();
    }

    private void FileWatcherButton_Click(object sender, EventArgs e)
    {
        mediaBackup.Config.MasterFoldersFileChangeWatchersONOFF = !mediaBackup.Config.MasterFoldersFileChangeWatchersONOFF;
        SetupFileWatchers();
    }

    private void SetupFileWatchers()
    {
        if (mediaBackup.Config.MasterFoldersFileChangeWatchersONOFF)
        {
            fileWatcherButton.Text = Resources.Main_SetupFileWatchers_On;
            CreateFileSystemWatchers();
        }
        else
        {
            fileWatcherButton.Text = Resources.Main_SetupFileWatchers_Off;
            RemoveFileSystemWatchers();
        }

        processFolderChangesTimer.Enabled = mediaBackup.Config.MasterFoldersFileChangeWatchersONOFF;
        scanFoldersTimer.Enabled = mediaBackup.Config.MasterFoldersFileChangeWatchersONOFF;
    }

    private void ProcessFolderChangesTimer_Tick(object sender, EventArgs e)
    {
        Utils.TraceIn();
        Utils.Trace($"folderChanges.Count = {FolderChanges.Count}");

        // every few seconds we move through the changes List and put the folders we need to check in our other list
        if (FolderChanges.Count == 0)
        {
            Utils.TraceOut();
            return;
        }

        var toSave = false;

        for (var i = FolderChanges.Count - 1; i >= 0; i--)
        {
            var folderChange = FolderChanges.ElementAt(i);

            Utils.Trace($"path {folderChange.Key}");
            _ = mediaBackup.GetFoldersForPath(folderChange.Key, out var masterFolder, out var indexFolder, out _);

            Utils.Trace($"masterFolder {masterFolder}");
            Utils.Trace($"indexFolder {indexFolder}");

            if (masterFolder != null && indexFolder != null)
            {
                var parentFolder = mediaBackup.GetParentFolder(folderChange.Key);
                Utils.Trace($"parentFolder {parentFolder}");

                var scanFolder = parentFolder ?? Path.Combine(masterFolder, indexFolder);

                if (mediaBackup.FoldersToScan.Any(f => f.Path == scanFolder))
                {
                    var scannedFolder = mediaBackup.FoldersToScan.First(f => f.Path == scanFolder);

                    if (folderChange.Value > scannedFolder.ModifiedDateTime)
                    {
                        scannedFolder.ModifiedDateTime = folderChange.Value;
                        toSave = true;
                    }
                }
                else
                {
                    mediaBackup.FoldersToScan.Add(new Folder(scanFolder, folderChange.Value));
                    toSave = true;
                }
            }

            _ = FolderChanges.Remove(folderChange.Key);
        }

        if (toSave) mediaBackup.Save();

        Utils.TraceOut();
    }

    private void ScanFoldersTimer_Tick(object sender, EventArgs e)
    {
        Utils.TraceIn();
        Utils.Trace($"mediaBackup.FoldersToScan.Count = {mediaBackup.FoldersToScan.Count}");

        var toSave = false;

        if (!longRunningActionExecutingRightNow)
        {
            if (mediaBackup.FoldersToScan.Count == 0)
            {
                Utils.TraceOut();
                return;
            }

            for (var i = mediaBackup.FoldersToScan.Count - 1; i >= 0; i--)
            {
                var folderToScan = mediaBackup.FoldersToScan[i];

                if (folderToScan.ModifiedDateTime.AddSeconds(mediaBackup.Config.MasterFolderScanMinimumAgeBeforeScanning) >= DateTime.Now) continue;

                mediaBackup.ClearFlags();

                var fileCountInFolderBefore = mediaBackup.BackupFiles.Count(b => b.FullPath.StartsWith(folderToScan.Path));

                _ = mediaBackup.GetFoldersForPath(folderToScan.Path, out var masterFolder, out var indexFolder, out _);

                var searchOption = folderToScan.Path == Path.Combine(masterFolder, indexFolder) ? SearchOption.TopDirectoryOnly : SearchOption.AllDirectories;

                if (ScanSingleFolder(folderToScan.Path, searchOption))
                {
                    var removedFilesCount = 0;
                    var markedAsDeletedFilesCount = 0;

                    _ = mediaBackup.FoldersToScan.Remove(folderToScan);

                    UpdateSymbolicLinkForFolder(folderToScan.Path);

                    // instead of removing files that are no longer found in a folder we now flag them as deleted so we can report them later
                    // unless they aren't on a backup disk in which case they are removed now 
                    var files = searchOption == SearchOption.AllDirectories
                        ? mediaBackup.BackupFiles.Where(b => !b.Flag && b.FullPath.StartsWith(folderToScan.Path)).ToList()
                        : mediaBackup.BackupFiles.Where(b => !b.Flag && b.FullPath.StartsWith(folderToScan.Path) && !b.RelativePath.Contains('\\')).ToList();

                    for (var j = files.Count - 1; j >= 0; j--)
                    {
                        var backupFile = files[j];

                        if (string.IsNullOrEmpty(backupFile.Disk))
                        {
                            mediaBackup.RemoveFile(backupFile);
                            removedFilesCount++;
                        }
                        else
                        {
                            backupFile.Deleted = true;
                            markedAsDeletedFilesCount++;
                        }
                    }

                    var fileCountInFolderAfter = mediaBackup.BackupFiles.Count(b => b.FullPath.StartsWith(folderToScan.Path));
                    var filesNotOnBackupDiskCount = mediaBackup.GetBackupFilesWithDiskEmpty().Count();

                    var text =
                        $"Folder scan completed. {fileCountInFolderBefore} files before and now {fileCountInFolderAfter} files. {markedAsDeletedFilesCount} marked as deleted and {removedFilesCount} removed. {filesNotOnBackupDiskCount} to backup.";
                    Utils.Log(BackupAction.ScanFolders, text);
                    toSave = true;
                }
                else
                {
                    var text = $"Folder scan skipped. It will be scanned again in {Utils.FormatTimeFromSeconds(mediaBackup.Config.MasterFoldersScanTimer)}.";
                    Utils.LogWithPushover(BackupAction.ScanFolders, text);
                }
            }

            if (toSave)
            {
                mediaBackup.Save();
                UpdateStatusLabel("Saved.");
                UpdateUI_Tick(null, null);
                UpdateMediaFilesCountDisplay();
            }
            else
            {
                UpdateStatusLabel("");
            }
        }

        Utils.TraceOut();
    }

    private void Main_FormClosing(object sender, FormClosingEventArgs e)
    {
        if (mediaBackup.FoldersToScan.Count <= 0 && FolderChanges.Count <= 0) return;

        if (MessageBox.Show(Resources.Main_FoldersQueued, Resources.Main_ScanFoldersTitle, MessageBoxButtons.YesNo) != DialogResult.Yes) return;

        ProcessFolderChangesTimer_Tick(null, null);
        ScanFoldersTimer_Tick(null, null);
    }

    private void ListFoldersToScanButton_Click(object sender, EventArgs e)
    {
        Utils.TraceIn();

        Utils.Log("Listing folderChanges detected");

        foreach (var folderChange in FolderChanges)
        {
            Utils.Log($"{folderChange.Key} changed at {folderChange.Value}");
        }

        Utils.Log("Listing FoldersToScan queued");

        foreach (var folderToScan in mediaBackup.FoldersToScan)
        {
            Utils.Log($"{folderToScan.Path} changed at {folderToScan.ModifiedDateTime}");
        }

        Utils.TraceOut();
    }

    private void RecreateAllMkLinksButton_Click(object sender, EventArgs e)
    {
        CreateLinksForIndexFolder("_Movies");
        CreateLinksForIndexFolder("_Movies (non-tmdb)");
        CreateLinksForIndexFolder("_TV");
        CreateLinksForIndexFolder("_TV (non-tvdb)");
    }

    private void CreateLinksForIndexFolder(string folder)
    {
        Utils.Trace("CreateLinks enter");
        Utils.Trace($"Param folderToCheck = {folder}");

        var backupFiles = mediaBackup.BackupFiles.Where(b => b.IndexFolder == folder);

        foreach (var backupFile in backupFiles)
        {
            CreateLinkForBackupFile(backupFile);
        }

        Utils.Trace("CreateLinks exit");
    }

    private void UpdateSymbolicLinkForFolder(string folderPath)
    {
        Utils.Trace("UpdateSymbolicLinkForFolder enter");
        Utils.Trace($"Param folderPath = {folderPath}");

        if (!Directory.Exists(folderPath))
        {
            Utils.Trace("UpdateSymbolicLinkForFolder exit as folderPath empty");
            return;
        }

        foreach (var a in mediaBackup.Config.SymbolicLinks)
        {
            var m = Regex.Match(folderPath, a.FileDiscoveryRegEx);

            if (m.Success)
            {
                var path = Path.Combine(a.RootFolder, a.RelativePath);
                var pathToTarget = a.PathToTarget;

                for (var i = 0; i < m.Groups.Count; i++)
                {
                    if (m.Groups[i].GetType() == typeof(Group))
                    {
                        var g = m.Groups[i];
                        path = path.Replace($"${i}", g.Value);
                        pathToTarget = pathToTarget.Replace($"${i}", g.Value);
                    }
                }

                if (pathToTarget == null)
                {
                    Utils.Trace("UpdateSymbolicLinkForFolder exit pathToTarget=null");
                    return;
                }

                if (Directory.Exists(path) && Utils.IsDirectoryEmpty(path))
                {
                    Utils.Trace("Deleting link directory as its empty");
                    Directory.Delete(path, true);
                }

                if (Directory.Exists(path)) continue;

                Utils.Trace($"Creating new symbolic link at {path} with target {pathToTarget}");
                _ = Directory.CreateSymbolicLink(path, pathToTarget);
            }
        }

        Utils.Trace("UpdateSymbolicLinkForFolder exit");
    }

    private void CreateLinkForBackupFile(BackupFile backupFile)
    {
        var assetType = string.Empty;
        var pathToTarget = mediaBackup.GetParentFolder(backupFile.FullPath);

        if (backupFile.IndexFolder.StartsWith("_Movies"))
            assetType = "_Movies";
        else if (backupFile.IndexFolder.StartsWith("_TV")) assetType = "_TV";

        if (assetType is "_Movies" or "_TV")
        {
            var path = Path.Combine(mediaBackup.Config.SymbolicLinksRootFolder, assetType, new DirectoryInfo(pathToTarget).Name);

            if (Directory.Exists(path) && Utils.IsDirectoryEmpty(path)) Directory.Delete(path, true);

            if (!Directory.Exists(path)) _ = Directory.CreateSymbolicLink(path, pathToTarget);
        }
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

    private void CreateFileSystemWatchers()
    {
        Utils.TraceIn();

        foreach (var masterFolder in mediaBackup.Config.MasterFolders)
        {
            FileSystemWatcher watcher = new(masterFolder)
            {
                NotifyFilter = NotifyFilters.Attributes | NotifyFilters.CreationTime | NotifyFilters.DirectoryName | NotifyFilters.FileName |
                               NotifyFilters.LastAccess | NotifyFilters.LastWrite | NotifyFilters.Security | NotifyFilters.Size
            };

            watcher.Changed += FileSystemWatcher_OnSomethingHappened;
            watcher.Deleted += FileSystemWatcher_OnSomethingHappened;
            watcher.Renamed += FileSystemWatcher_OnSomethingHappened;
            watcher.Error += FileSystemWatcher_OnError;

            watcher.Filter = "*.*";
            watcher.IncludeSubdirectories = true;
            watcher.EnableRaisingEvents = true;

            watcherList.Add(watcher);
        }

        Utils.TraceOut();
    }

    private void RemoveFileSystemWatchers()
    {
        Utils.TraceIn();

        foreach (var watcher in watcherList)
        {
            watcher.Dispose();
        }

        watcherList.Clear();
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

    private void CheckConnectedBackupDiskButton_Click(object sender, EventArgs e)
    {
        Utils.TraceIn();

        TaskWrapper(CheckConnectedDiskAndCopyFilesAsync, false, false);

        Utils.TraceOut();
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
        // rebuilds the source filename from MasterFolder+IndexFolder+Path
        // checks the file still exists there
        // if it does compare the hashcodes and update results
        // force a recalc of both the hashes to check the files can both be read correctly

        var disk = SetupBackupDisk();

        var folderToCheck = disk.BackupPath;
        Utils.LogWithPushover(BackupAction.CheckBackupDisk, $"Started checking backup disk {folderToCheck}");
        UpdateStatusLabel($"Checking backup disk {folderToCheck}");

        long readSpeed = 0, writeSpeed = 0;

        if (mediaBackup.Config.SpeedTestONOFF)
        {
            var diskTestSize = disk.Free > Utils.ConvertMBtoBytes(mediaBackup.Config.SpeedTestFileSize)
                ? Utils.ConvertMBtoBytes(mediaBackup.Config.SpeedTestFileSize)
                : disk.Free - Utils.BytesInOneKilobyte;

            UpdateStatusLabel($"Speed testing {folderToCheck}");
            Utils.DiskSpeedTest(folderToCheck, diskTestSize, mediaBackup.Config.SpeedTestIterations, out readSpeed, out writeSpeed);

            disk.UpdateSpeeds(readSpeed, writeSpeed);
        }

        var text = $"Name: {disk.Name}\nTotal: {disk.CapacityFormatted}\nFree: {disk.FreeFormatted}\n" +
                   $"Read: {Utils.FormatSpeed(readSpeed)}\nWrite: {Utils.FormatSpeed(writeSpeed)}";

        var diskInfoMessageWasTheLastSent = true;

        Utils.LogWithPushover(BackupAction.CheckBackupDisk, text);

        if (disk.Free < Utils.ConvertMBtoBytes(mediaBackup.Config.BackupDiskMinimumCriticalSpace))
            Utils.LogWithPushover(BackupAction.CheckBackupDisk, PushoverPriority.High, $"{disk.FreeFormatted} free is very low. Prepare new backup disk");

        var filesToReset = mediaBackup.GetBackupFilesOnBackupDisk(disk.Name, true);

        foreach (var fileName in filesToReset)
        {
            fileName.ClearDiskChecked();
        }

        UpdateMediaFilesCountDisplay();

        UpdateStatusLabel($"Scanning {folderToCheck}");

        var backupDiskFiles = Utils.GetFiles(folderToCheck, "*", SearchOption.AllDirectories, FileAttributes.Hidden);

        EnableProgressBar(0, backupDiskFiles.Length);

        for (var i = 0; i < backupDiskFiles.Length; i++)
        {
            var backupFileFullPath = backupDiskFiles[i];
            var backupFileIndexFolderRelativePath = backupFileFullPath[(folderToCheck.Length + 1)..];

            UpdateStatusLabel($"Scanning {folderToCheck}", i + 1);

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
                            // There was an error with the hashcodes of the source file anf the file on the backup disk
                            Utils.LogWithPushover(BackupAction.CheckBackupDisk, PushoverPriority.High,
                                $"There was an error with the hashcodes on the source and backup disk. It's likely the source file has changed since the last backup of {backupFile.FullPath}. It could be that the source file or destination file are corrupted or in use by another process.");

                            diskInfoMessageWasTheLastSent = false;
                        }

                        continue;
                    }
                }
                else
                {
                    // Backup doesn't exist in the master folder anymore
                    // so delete it
                    mediaBackup.RemoveFile(backupFile);
                }
            }
            else
            {
                // The file on the backup disk isn't found in the masterfolder anymore
                // it could be that we've renamed it in the master folder
                // We could just let it get deleted off the backup disk and copied again next time
                // Alternatively, find it by the contents hashcode as that's (almost guaranteed unique)
                // and then rename it 
                // if we try to rename and it exists at the destination already then we delete the file instead
                var hashToCheck = Utils.GetShortMd5HashFromFile(backupFileFullPath);

                var file = mediaBackup.GetBackupFileFromContentsHashcode(hashToCheck);

                if (file != null && file.Length != 0 && file.BackupDiskNumber == 0)
                {
                    var destFileName = file.BackupDiskFullPath(disk.BackupPath);
                    Utils.LogWithPushover(BackupAction.CheckBackupDisk, PushoverPriority.Normal, $"Renaming {backupFileFullPath} to {destFileName}");

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
                    {
                        Utils.FileMove(backupFileFullPath, destFileName);
                    }

                    // This forces a hash check on the source and backup disk files
                    Utils.Trace($"Checking hash for {file.Hash}");
                    var returnValue = file.CheckContentHashes(disk);

                    if (returnValue == false)
                    {
                        // There was an error with the hashcodes of the source file anf the file on the backup disk
                        Utils.LogWithPushover(BackupAction.CheckBackupDisk, PushoverPriority.High,
                            $"There was an error with the hashcodes on the source and backup disk. It's likely the sourcefile has changed since the last backup of {file.FullPath}. It could be that the source file or destination file are corrupted though.");

                        diskInfoMessageWasTheLastSent = false;
                    }

                    continue;
                }
            }

            // Extra file on a backup disk
            if (deleteExtraFiles)
            {
                Utils.LogWithPushover(BackupAction.CheckBackupDisk, $"Extra file {backupFileFullPath} on backup disk {disk.Name} now deleted");
                Utils.FileDelete(backupFileFullPath);
                diskInfoMessageWasTheLastSent = false;
            }
            else
            {
                Utils.LogWithPushover(BackupAction.CheckBackupDisk, $"Extra file {backupFileFullPath} on backup disk {disk.Name}");
                diskInfoMessageWasTheLastSent = false;
            }
        }

        UpdateStatusLabel($"Deleting {folderToCheck} empty folders");

        var directoriesDeleted = Utils.DeleteEmptyDirectories(folderToCheck);

        foreach (var directory in directoriesDeleted)
        {
            Utils.Log(BackupAction.CheckBackupDisk, $"Deleted empty folder {directory}");
        }

        disk.UpdateDiskChecked();

        if (!UpdateCurrentBackupDiskInfo(disk))
        {
            Utils.LogWithPushover(BackupAction.BackupFiles, PushoverPriority.Emergency, $"Error updating info for backup disk {disk.Name}");
            return null;
        }

        UpdateMediaFilesCountDisplay();

        mediaBackup.Save();
        UpdateStatusLabel("Saved.");

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

        var currentConnectedBackupDiskName = BackupDisk.GetBackupFolderName(backupDiskTextBox.Text);

        while (currentConnectedBackupDiskName != backupDisk)
        {
            Utils.LogWithPushover(BackupAction.General, PushoverPriority.High, $"Connect new backup drive to restore from {backupDisk}");

            var answer = MessageBox.Show(string.Format(Resources.Main_BackupDiskConnectCorrectDisk2, backupDisk), Resources.Main_BackupDiskConnectCorrectDisk,
                MessageBoxButtons.YesNo);

            switch (answer)
            {
                case DialogResult.No:
                    return false;
                case DialogResult.Yes:
                    currentConnectedBackupDiskName = BackupDisk.GetBackupFolderName(backupDiskTextBox.Text);
                    break;
            }
        }

        return Utils.TraceOut(true);
    }

    private void CopyFilesToBackupDiskButton_Click(object sender, EventArgs e)
    {
        Utils.TraceIn();

        TaskWrapper(CopyFilesAsync, true);

        Utils.TraceOut();
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
        const string nextDiskMessage = "Please insert the next backup disk now";

        var disk = mediaBackup.GetBackupDisk(backupDiskTextBox.Text);

        while (disk == null)
        {
            WaitForNewDisk(nextDiskMessage);
            disk = mediaBackup.GetBackupDisk(backupDiskTextBox.Text);
        }

        _ = UpdateCurrentBackupDiskInfo(disk);

        return disk;
    }

    private void CopyFiles(bool showCompletedMessage)
    {
        Utils.TraceIn();

        var disk = SetupBackupDisk();

        Utils.LogWithPushover(BackupAction.BackupFiles, "Started");

        UpdateStatusLabel("Copying");

        IEnumerable<BackupFile> filesToBackup = mediaBackup.GetBackupFilesWithDiskEmpty().OrderByDescending(q => q.Length);

        var backupFiles = filesToBackup.ToArray();
        var sizeOfFiles = backupFiles.Sum(x => x.Length);
        var totalFileCount = backupFiles.Length;

        Utils.LogWithPushover(BackupAction.BackupFiles, $"{totalFileCount:n0} files to backup at {Utils.FormatSize(sizeOfFiles)}");

        var outOfDiskSpaceMessageSent = false;

        var fileCounter = 0;

        // Start with a 30MB/s copy speed as a guess
        var lastCopySpeed = Utils.ConvertMBtoBytes(30);

        var remainingSizeOfFilesToCopy = sizeOfFiles;

        // number of bytes copied so far so we can track the percentage complete
        long copiedSoFar = 0;

        _ = Utils.GetDiskInfo(backupDiskTextBox.Text, out var availableSpace, out _);

        var remainingDiskSpace = availableSpace - Utils.ConvertMBtoBytes(mediaBackup.Config.BackupDiskMinimumFreeSpaceToLeave);
        var sizeOfCopy = remainingDiskSpace < sizeOfFiles ? remainingDiskSpace : sizeOfFiles;

        if (sizeOfCopy == 0)

            // This avoids any division by zero errors later
            sizeOfCopy = 1;

        // We use 100 as the max because the actual number of bytes could be far too large 
        EnableProgressBar(0, 100);

        foreach (var backupFile in backupFiles)
        {
            try
            {
                fileCounter++;
                UpdateStatusLabel("Copying", Convert.ToInt32(copiedSoFar * 100 / sizeOfCopy));
                UpdateMediaFilesCountDisplay();

                if (string.IsNullOrEmpty(backupFile.IndexFolder)) throw new ApplicationException("Index folder is empty");

                var destinationFileName = backupFile.BackupDiskFullPath(disk.BackupPath);

                // We use a temporary name for the copy first and then rename it after
                // This is in case the Backup is aborted during the copy
                // This file will be seen on the next scan and removed
                var destinationFileNameTemp = destinationFileName + ".copying";

                var sourceFileName = backupFile.FullPath;
                FileInfo sourceFileInfo = new(sourceFileName);
                var sourceFileSize = Utils.FormatSize(sourceFileInfo.Length);

                if (File.Exists(destinationFileName))
                {
                    Utils.LogWithPushover(BackupAction.BackupFiles,
                        $"[{fileCounter}/{totalFileCount}]\nSkipping copy of {sourceFileName} as it exists already.");
                    UpdateStatusLabel($"Skipping {Path.GetFileName(sourceFileName)}", Convert.ToInt32(copiedSoFar * 100 / sizeOfCopy));

                    // it could be that the source file hash changed after we read it (we read the hash, updated the master file and then copied it)
                    // in which case check the source hash again and then check the copied file 
                    if (!backupFile.CheckContentHashes(disk))

                        // There was an error with the hashcodes of the source file anf the file on the backup disk
                        Utils.LogWithPushover(BackupAction.BackupFiles, PushoverPriority.High,
                            $"There was an error with the hashcodes on the source master folder and the backup disk. Its likely the sourcefile has changed since the last backup of {backupFile.FullPath} to {destinationFileName}");
                }
                else
                {
                    UpdateStatusLabel($"Copying {Path.GetFileName(sourceFileName)}", Convert.ToInt32(copiedSoFar * 100 / sizeOfCopy));

                    _ = Utils.GetDiskInfo(backupDiskTextBox.Text, out availableSpace, out _);

                    if (availableSpace > Utils.ConvertMBtoBytes(mediaBackup.Config.BackupDiskMinimumFreeSpaceToLeave) + sourceFileInfo.Length)
                    {
                        outOfDiskSpaceMessageSent = false;

                        var formattedEndDateTime = string.Empty;

                        if (lastCopySpeed > 0)
                        {
                            // copy speed is known
                            // remaining size is the smallest of remaining disk size-critical space to leave free OR
                            // size of remaining files to copy

                            remainingDiskSpace = availableSpace - Utils.ConvertMBtoBytes(mediaBackup.Config.BackupDiskMinimumFreeSpaceToLeave);

                            var sizeOfCopyRemaining = remainingDiskSpace < remainingSizeOfFilesToCopy ? remainingDiskSpace : remainingSizeOfFilesToCopy;
                            double numberOfSecondsOfCopyRemaining = sizeOfCopyRemaining / lastCopySpeed;

                            var rightNow = DateTime.Now;

                            var estimatedFinishDateTime = rightNow.AddSeconds(numberOfSecondsOfCopyRemaining);
                            formattedEndDateTime = ". Estimated finish by " + estimatedFinishDateTime.ToString("HH:mm");

                            // could be the following day
                            if (estimatedFinishDateTime.DayOfWeek != rightNow.DayOfWeek)
                                formattedEndDateTime = ". Estimated finish by tomorrow at " + estimatedFinishDateTime.ToString("HH:mm");

                            UpdateEstimatedFinish(estimatedFinishDateTime);
                        }

                        Utils.LogWithPushover(BackupAction.BackupFiles,
                            $"[{fileCounter}/{totalFileCount}] {Utils.FormatSize(availableSpace)} free.\nCopying {sourceFileName} at {sourceFileSize}{formattedEndDateTime}");

                        Utils.FileDelete(destinationFileNameTemp);

                        var sw = Stopwatch.StartNew();
                        _ = Utils.FileCopy(sourceFileName, destinationFileNameTemp);
                        sw.Stop();
                        var timeTaken = sw.Elapsed.TotalSeconds;

                        // We need to check this here in case Cancel was clicked during the copy of the file
                        if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();

                        Utils.FileMove(destinationFileNameTemp, destinationFileName);

                        Utils.Trace($"timeTaken {timeTaken}");
                        Utils.Trace($"sourceFileInfo.Length {sourceFileInfo.Length}");

                        lastCopySpeed = timeTaken > 0 ? Convert.ToInt64(sourceFileInfo.Length / timeTaken) : 0;

                        var copySpeed = lastCopySpeed > 0 ? Utils.FormatSpeed(lastCopySpeed) : "a very fast speed";

                        Utils.Trace($"Copy complete at {copySpeed}");

                        // Make sure its not readonly
                        Utils.ClearFileAttribute(destinationFileName, FileAttributes.ReadOnly);

                        // it could be that the source file hash changed after we read it (we read the hash, updated the master file and then copied it)
                        // in which case check the source hash again and then check the copied file 

                        if (!backupFile.CheckContentHashes(disk))

                            // There was an error with the hashcodes of the source file anf the file on the backup disk
                            Utils.LogWithPushover(BackupAction.BackupFiles, PushoverPriority.High,
                                $"There was an error with the hashcodes on the source and backup disk. Its likely the sourcefile has changed since the last backup of {backupFile.FullPath} to {destinationFileName}");
                    }
                    else
                    {
                        if (!outOfDiskSpaceMessageSent)
                        {
                            Utils.LogWithPushover(BackupAction.BackupFiles,
                                $"[{fileCounter}/{totalFileCount}] {Utils.FormatSize(availableSpace)} free.\nSkipping {sourceFileName} as not enough free space");
                            outOfDiskSpaceMessageSent = true;
                        }
                    }
                }

                remainingSizeOfFilesToCopy -= backupFile.Length;
                copiedSoFar += backupFile.Length;
            }

            catch (FileNotFoundException)
            {
                Utils.LogWithPushover(BackupAction.BackupFiles, PushoverPriority.High,
                    $"{backupFile.FullPath} is not found. It's most likely been replaced since our scan.");
            }

            catch (IOException ex)
            {
                // Sometimes during a copy we get this if we lose the connection to the source NAS drive
                Utils.LogWithPushover(BackupAction.BackupFiles, PushoverPriority.Emergency, $"IOException during copy. Skipping file. Details {ex}");
            }

            _ = UpdateCurrentBackupDiskInfo(disk);
            UpdateEstimatedFinish();
        }

        UpdateMediaFilesCountDisplay();

        if (!UpdateCurrentBackupDiskInfo(disk))
        {
            Utils.LogWithPushover(BackupAction.BackupFiles, PushoverPriority.Emergency, $"Error updating info for backup disk {disk.Name}");
            return;
        }

        mediaBackup.Save();
        UpdateStatusLabel("Saved.");

        var filesStillNotOnBackupDisk = mediaBackup.GetBackupFilesWithDiskEmpty();

        var text = string.Empty;

        var stillNotOnBackupDisk = filesStillNotOnBackupDisk as BackupFile[] ?? filesStillNotOnBackupDisk.ToArray();

        if (stillNotOnBackupDisk.Any())
        {
            sizeOfFiles = stillNotOnBackupDisk.Sum(p => p.Length);

            text = $"{stillNotOnBackupDisk.Length:n0} files still to backup at {Utils.FormatSize(sizeOfFiles)}.\n";
        }

        Utils.LogWithPushover(BackupAction.BackupFiles, text + $"{disk.FreeFormatted} free on backup disk");

        if (showCompletedMessage) Utils.LogWithPushover(BackupAction.BackupFiles, PushoverPriority.High, "Completed");

        Utils.TraceOut();
    }

    private void UpdateEstimatedFinish()
    {
        estimatedFinishTimeTextBox.Invoke(x => x.Text = string.Empty);
    }

    private void UpdateEstimatedFinish(DateTime estimatedFinishDateTime)
    {
        estimatedFinishTimeTextBox.Invoke(x => x.Text = estimatedFinishDateTime.ToString("HH:mm"));
    }

    private void ListFilesNotOnBackupDiskButton_Click(object sender, EventArgs e)
    {
        Utils.TraceIn();

        IEnumerable<BackupFile> filesNotOnBackupDisk = mediaBackup.GetBackupFilesWithDiskEmpty().OrderByDescending(p => p.Length);

        Utils.Log("Listing files not on a backup disk");

        var notOnBackupDisk = filesNotOnBackupDisk.ToArray();

        foreach (var file in notOnBackupDisk)
        {
            Utils.Log($"{file.FullPath} at {Utils.FormatSize(file.Length)}");
        }

        Utils.Log($"{notOnBackupDisk.Length} files at {Utils.FormatSize(notOnBackupDisk.Sum(p => p.Length))}");

        Utils.TraceOut();
    }

    private void RecalculateAllHashesButton_Click(object sender, EventArgs e)
    {
        Utils.Trace("recalculateAllHashesButton_Click enter");

        if (MessageBox.Show(Resources.Main_RecalculateAllHashes, Resources.Main_RecalculateAllHashesTitle, MessageBoxButtons.YesNo) == DialogResult.Yes)
        {
            foreach (var backupFile in mediaBackup.BackupFiles)
            {
                backupFile.UpdateContentsHash();
            }

            mediaBackup.Save();
        }

        Utils.Trace("recalculateAllHashesButton_Click exit");
    }

    private void CopyFilesAsync(bool showCompletedMessage)
    {
        longRunningActionExecutingRightNow = true;
        DisableControlsForAsyncTasks();

        CopyFiles(showCompletedMessage);

        ResetAllControls();
        longRunningActionExecutingRightNow = false;
    }

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

        var nextDiskMessage = "Please insert the next backup disk now";

        while (true)
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

    private void ScanFolderAsync()
    {
        longRunningActionExecutingRightNow = true;
        DisableControlsForAsyncTasks();

        _ = ScanFolders();

        ResetAllControls();
        longRunningActionExecutingRightNow = false;
    }

    public void TaskWrapper(Action methodName)
    {
        if (methodName is null) throw new ArgumentNullException(nameof(methodName));

        tokenSource = new CancellationTokenSource();
        ct = tokenSource.Token;
        var t = Task.Run(() => methodName(), ct).ContinueWith(u =>
        {
            if (u.Exception != null)
            {
                Utils.Log("Exception occured. Cancelling operation.");
                _ = MessageBox.Show(string.Format(Resources.Main_TaskWrapperException, u.Exception));

                CancelButton_Click(null, null);
            }
        }, default, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.FromCurrentSynchronizationContext());
    }

    public void TaskWrapper(Action<bool> methodName, bool param1)
    {
        if (methodName is null) throw new ArgumentNullException(nameof(methodName));

        tokenSource = new CancellationTokenSource();
        ct = tokenSource.Token;
        var t = Task.Run(() => methodName(param1), ct).ContinueWith(u =>
        {
            if (u.Exception != null)
            {
                Utils.Log("Exception occured. Cancelling operation.");
                _ = MessageBox.Show(string.Format(Resources.Main_TaskWrapperException, u.Exception));

                CancelButton_Click(null, null);
            }
        }, default, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.FromCurrentSynchronizationContext());
    }

    public void TaskWrapper(Action<bool, bool> methodName, bool param1, bool param2)
    {
        if (methodName is null) throw new ArgumentNullException(nameof(methodName));

        tokenSource = new CancellationTokenSource();
        ct = tokenSource.Token;
        var t = Task.Run(() => methodName(param1, param2), ct).ContinueWith(u =>
        {
            if (u.Exception != null)
            {
                Utils.Log("Exception occured. Cancelling operation.");
                _ = MessageBox.Show(string.Format(Resources.Main_TaskWrapperException, u.Exception));

                CancelButton_Click(null, null);
            }
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

    private void UpdateMasterFilesButton_Click(object sender, EventArgs e)
    {
        Utils.Trace("UpdateMasterFilesButton_Click enter");

        if (!longRunningActionExecutingRightNow)
            if (MessageBox.Show(Resources.Main_UpdateMasterFiles, Resources.Main_UpdateMasterFilesTitle, MessageBoxButtons.YesNo) == DialogResult.Yes)
                TaskWrapper(ScanFolderAsync);

        Utils.Trace("UpdateMasterFilesButton_Click exit");
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

        if (filters.Any(pattern => Regex.IsMatch(fileName, pattern)))
        {
            Utils.LogWithPushover(BackupAction.ScanFolders, PushoverPriority.Normal, $"File matches RegEx and so will be deleted {filePath}");
            Utils.FileDelete(filePath);
            return true;
        }

        return false;
    }

    private void UpdateStatusLabel(string text, int value)
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

    private void UpdateStatusLabel(string text)
    {
        UpdateStatusLabel(text, 0);
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

    /// <summary>
    ///     Scans all MasterFolders and IndexFolders
    /// </summary>
    /// <returns>True if successful otherwise False</returns>
    private bool ScanFolders()
    {
        Utils.Trace("ScanFolders enter");

        var filters = mediaBackup.GetFilters();

        long readSpeed = 0, writeSpeed = 0;

        mediaBackup.ClearFlags();

        Utils.LogWithPushover(BackupAction.ScanFolders, "Started");
        UpdateStatusLabel("Started");

        foreach (var masterFolder in mediaBackup.Config.MasterFolders)
        {
            UpdateStatusLabel($"Scanning {masterFolder}");

            if (!Directory.Exists(masterFolder)) Utils.LogWithPushover(BackupAction.ScanFolders, PushoverPriority.High, $"{masterFolder} is not available");

            if (Directory.Exists(masterFolder))
            {
                if (!Utils.IsFolderWritable(masterFolder))
                    Utils.LogWithPushover(BackupAction.ScanFolders, PushoverPriority.High, $"{masterFolder} is not writable");

                _ = Utils.GetDiskInfo(masterFolder, out var freeSpaceOnCurrentMasterFolder, out var totalBytesOnMasterFolderDisk);

                if (mediaBackup.Config.SpeedTestONOFF)
                {
                    UpdateStatusLabel($"Speed testing {masterFolder}");
                    Utils.DiskSpeedTest(masterFolder, Utils.ConvertMBtoBytes(mediaBackup.Config.SpeedTestFileSize), mediaBackup.Config.SpeedTestIterations,
                        out readSpeed, out writeSpeed);
                }

                var totalBytesOnMasterFolderDiskFormatted = Utils.FormatSize(totalBytesOnMasterFolderDisk);
                var freeSpaceOnCurrentMasterFolderFormatted = Utils.FormatSize(freeSpaceOnCurrentMasterFolder);
                var readSpeedFormatted = Utils.FormatSpeed(readSpeed);
                var writeSpeedFormatted = Utils.FormatSpeed(writeSpeed);

                var text =
                    $"{masterFolder}\nTotal: {totalBytesOnMasterFolderDiskFormatted}\nFree: {freeSpaceOnCurrentMasterFolderFormatted}\nRead: {readSpeedFormatted}\nWrite: {writeSpeedFormatted}";

                Utils.LogWithPushover(BackupAction.ScanFolders, text);

                if (mediaBackup.Config.SpeedTestONOFF)
                {
                    if (readSpeed < Utils.ConvertMBtoBytes(mediaBackup.Config.MasterFolderMinimumReadSpeed))
                        Utils.LogWithPushover(BackupAction.ScanFolders, PushoverPriority.High,
                            $"Read speed is below MinimumCritical of {Utils.FormatSpeed(Utils.ConvertMBtoBytes(mediaBackup.Config.MasterFolderMinimumReadSpeed))}");

                    if (writeSpeed < Utils.ConvertMBtoBytes(mediaBackup.Config.MasterFolderMinimumWriteSpeed))
                        Utils.LogWithPushover(BackupAction.ScanFolders, PushoverPriority.High,
                            $"Write speed is below MinimumCritical of {Utils.FormatSpeed(Utils.ConvertMBtoBytes(mediaBackup.Config.MasterFolderMinimumWriteSpeed))}");
                }

                if (freeSpaceOnCurrentMasterFolder < Utils.ConvertMBtoBytes(mediaBackup.Config.MasterFolderMinimumCriticalSpace))
                    Utils.LogWithPushover(BackupAction.ScanFolders, PushoverPriority.High, $"Free space on {masterFolder} is too low");

                UpdateStatusLabel($"Deleting empty folders in {masterFolder}");

                var directoriesDeleted = Utils.DeleteEmptyDirectories(masterFolder);

                foreach (var directory in directoriesDeleted)
                {
                    Utils.Log(BackupAction.ScanFolders, $"Deleted empty folder {directory}");
                }

                UpdateStatusLabel($"Scanning {masterFolder}");

                // Check for files in the root of the master folder alongside te indexfolders
                var filesInRootOfMasterFolder = Utils.GetFiles(masterFolder, filters, SearchOption.TopDirectoryOnly);

                foreach (var file in filesInRootOfMasterFolder)
                {
                    Utils.Trace($"Checking {file}");

                    if (CheckForFilesToDelete(file))
                    {
                    }
                }

                if (mediaBackup.Config.IndexFolders.Any(indexFolder => !ScanSingleFolder(Path.Combine(masterFolder, indexFolder), SearchOption.AllDirectories)))
                    return false;
            }
            else
            {
                Utils.LogWithPushover(BackupAction.ScanFolders, PushoverPriority.High, $"{masterFolder} doesn't exist");
            }
        }

        UpdateStatusLabel("Scanning completed.");

        foreach (var rule in mediaBackup.Config.FileRules)
        {
            if (!rule.Matched) Utils.LogWithPushover(BackupAction.ScanFolders, PushoverPriority.High, $"{rule.Name} didn't match any files");
        }

        // instead of removing files that are no longer found in Master Folders we now flag them as deleted so we can report them later
        foreach (var backupFile in mediaBackup.BackupFiles.Where(backupFile => !backupFile.Flag && backupFile.DiskChecked.HasValue()))
        {
            backupFile.Deleted = true;
            backupFile.Flag = true;
        }

        mediaBackup.RemoveFilesWithFlag(false, true);
        mediaBackup.UpdateLastFullScan();
        mediaBackup.Save();

        UpdateStatusLabel("Saved.");
        UpdateMediaFilesCountDisplay();

        var totalFiles = mediaBackup.BackupFiles.Count;

        var totalFileSize = mediaBackup.BackupFiles.Sum(p => p.Length);

        var filesNotOnBackupDisk = mediaBackup.GetBackupFilesWithDiskEmpty();

        var notOnBackupDisk = filesNotOnBackupDisk as BackupFile[] ?? filesNotOnBackupDisk.ToArray();
        var fileSizeToCopy = notOnBackupDisk.Sum(p => p.Length);

        Utils.LogWithPushover(BackupAction.ScanFolders, $"{totalFiles:n0} files at {Utils.FormatSize(totalFileSize)}");

        var oldestFile = mediaBackup.GetOldestFile();

        if (oldestFile != null)
        {
            var oldestFileDate = DateTime.Parse(oldestFile.DiskChecked);

            var days = DateTime.Today.Subtract(oldestFileDate).Days;

            var daysText = days == 1 ? string.Empty : "(s)";

            Utils.LogWithPushover(BackupAction.ScanFolders,
                $"Oldest backup date is {days:n0} day{daysText} ago on {oldestFileDate.ToShortDateString()} on {oldestFile.Disk}");
        }

        Utils.LogWithPushover(BackupAction.ScanFolders, $"{notOnBackupDisk.Length:n0} files to backup at {Utils.FormatSize(fileSizeToCopy)}");

        Utils.LogWithPushover(BackupAction.ScanFolders, "Completed");
        Utils.Trace("ScanFolders exit");
        return true;
    }

    /// <summary>
    ///     Scan the folder provided.
    /// </summary>
    /// <param name="folderToCheck">The full path to scan</param>
    /// <param name="searchOption">Whether to search subfolders</param>
    /// <returns>True if the scan was successful otherwise False.</returns>
    private bool ScanSingleFolder(string folderToCheck, SearchOption searchOption)
    {
        Utils.Trace("ScanSingleFolder enter");
        Utils.Trace($"Params: folderToCheck={folderToCheck} searchOption={searchOption}");

        if (Directory.Exists(folderToCheck))
        {
            var subFolderText = searchOption == SearchOption.TopDirectoryOnly ? "folder only" : "and subfolders";
            Utils.LogWithPushover(BackupAction.ScanFolders, $"{folderToCheck}");
            Utils.Trace($"{folderToCheck} {subFolderText}");

            UpdateStatusLabel($"Scanning {folderToCheck}");

            var filters = mediaBackup.GetFilters();
            var files = Utils.GetFiles(folderToCheck, filters, searchOption);

            EnableProgressBar(0, files.Length);

            for (var i = 0; i < files.Length; i++)
            {
                var file = files[i];
                Utils.Trace($"Checking {file}");

                UpdateStatusLabel($"Scanning {folderToCheck}", i + 1);

                if (CheckForFilesToDelete(file)) continue;

                // RegEx file name rules
                foreach (var rule in mediaBackup.Config.FileRules)
                {
                    if (Regex.IsMatch(file, rule.FileDiscoveryRegEx))
                    {
                        if (!rule.Matched)
                        {
                            Utils.Trace($"{rule.Name} matched file {file}");
                            rule.Matched = true;
                        }

                        // if it does then the second regex must be true
                        if (!Regex.IsMatch(file, rule.FileTestRegEx))
                        {
                            Utils.Trace($"File {file} matched by {rule.FileDiscoveryRegEx} but doesn't match {rule.FileTestRegEx}");
                            Utils.LogWithPushover(BackupAction.ScanFolders, PushoverPriority.High, $"{rule.Name} {rule.Message} {file}");
                        }
                    }
                }

                if (!mediaBackup.EnsureFile(file))
                {
                    Utils.Trace("ScanSingleFolder exit with false");
                    return false;
                }

                UpdateMediaFilesCountDisplay();
            }
        }

        Utils.Trace("ScanSingleFolder exit with true");
        return true;
    }
}