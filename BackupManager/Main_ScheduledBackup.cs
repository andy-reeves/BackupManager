using System;

namespace BackupManager;

partial class Main
{
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
        SetupDailyTrigger(mediaBackup.Config.ScheduledBackupOnOff);
        Utils.Trace($"TriggerHour={trigger.TriggerHour}");
        ResetAllControls();
        longRunningActionExecutingRightNow = false;
    }
}