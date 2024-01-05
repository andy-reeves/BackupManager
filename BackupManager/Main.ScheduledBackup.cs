// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="Main.ScheduledBackup.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using BackupManager.Entities;
using BackupManager.Properties;

namespace BackupManager;

internal sealed partial class Main
{
    private void ScheduledBackupAsync()
    {
        Utils.TraceIn();

        if (longRunningActionExecutingRightNow)
        {
            Utils.TraceOut();
            return;
        }
        longRunningActionExecutingRightNow = true;
        DisableControlsForAsyncTasks();
        Utils.LogWithPushover(BackupAction.ScanDirectory, "Started");
        UpdateStatusLabel(string.Format(Resources.Main_Scanning, string.Empty));
        fileBlockingCollection = new BlockingCollection<string>();
        directoryScanBlockingCollection = new BlockingCollection<DirectoryScan>();

        if (mediaBackup.Config.MonitoringOnOff)
        {
            Utils.LogWithPushover(BackupAction.General,
                $"Service monitoring is running every {Utils.FormatTimeFromSeconds(mediaBackup.Config.MonitoringInterval)}");
        }
        else
            Utils.LogWithPushover(BackupAction.General, PushoverPriority.High, "Service monitoring is not running");
        long oldFileCount = mediaBackup.BackupFiles.Count;
        var doFullBackup = false;
        _ = DateTime.TryParse(mediaBackup.DirectoriesLastFullScan, out var backupFileDate);
        if (backupFileDate.AddDays(mediaBackup.Config.DirectoriesDaysBetweenFullScan) < DateTime.Now) doFullBackup = true;

        try
        {
            tokenSource?.Dispose();
            tokenSource = new CancellationTokenSource();
            ct = tokenSource.Token;
            var scanId = Guid.NewGuid().ToString();

            // Update the master files if we've not been monitoring directories directly
            if (!mediaBackup.Config.DirectoriesFileChangeWatcherOnOff || doFullBackup)
            {
                // split the directories into group by the disk name
                var diskNames = Utils.GetDiskNames(mediaBackup.Config.Directories);
                RootDirectoryChecks(mediaBackup.Config.Directories);
                var tasks = new List<Task>(diskNames.Length);

                tasks.AddRange(diskNames.Select(diskName => Utils.GetDirectoriesForDisk(diskName, mediaBackup.Config.Directories))
                    .Select(directoriesOnDisk => TaskWrapper(GetFilesAsync, directoriesOnDisk, scanId)));
                Task.WhenAll(tasks).Wait(ct);
                mediaBackup.Save();

                foreach (var scan in directoryScanBlockingCollection)
                {
                    mediaBackup.DirectoryScans.Add(scan);
                }
                mediaBackup.ClearFlags();
                _ = ProcessFiles(fileBlockingCollection, scanId, ct);
                var filesToRemoveOrMarkDeleted = mediaBackup.BackupFiles.Where(static b => !b.Flag).ToArray();
                RemoveOrDeleteFiles(filesToRemoveOrMarkDeleted, out _, out _);
                mediaBackup.UpdateLastFullScan();
                mediaBackup.Save();
            }
            UpdateSymbolicLinks(ct);

            if (mediaBackup.Config.BackupDiskDifferenceInFileCountAllowedPercentage != 0)
            {
                var minimumFileCountAllowed = oldFileCount -
                                              oldFileCount * mediaBackup.Config.BackupDiskDifferenceInFileCountAllowedPercentage /
                                              100;
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
            Utils.Trace($"TriggerHour={_trigger.TriggerHour}");
            ResetAllControls();
            longRunningActionExecutingRightNow = false;
        }
        catch (Exception u)
        {
            Utils.Trace("Exception in the TaskWrapper");

            if (u.Message == "The operation was canceled.")
                Utils.LogWithPushover(BackupAction.General, PushoverPriority.Normal, "Cancelling");
            else
            {
                Utils.LogWithPushover(BackupAction.General, PushoverPriority.High,
                    string.Format(Resources.Main_TaskWrapperException, u));
            }
            ASyncTasksCleanUp();
        }
        Utils.TraceOut();
    }

    private void SetupDailyTrigger(bool addTrigger)
    {
        Utils.TraceIn();
        updateUITimer.Enabled = true; // because we want to update the directory tracking every 1 min or so anyway

        if (addTrigger)
        {
            _trigger = new DailyTrigger(scheduledDateTimePicker.Value);
            _trigger.OnTimeTriggered += scheduledBackupAction;
            UpdateUI_Tick(null, null);
        }
        else
        {
            if (_trigger != null)
            {
                _trigger.OnTimeTriggered -= scheduledBackupAction;
                _trigger = null;
            }
            timeToNextRunTextBox.Text = string.Empty;
        }
        Utils.TraceOut();
    }
}