// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="Main.ScheduledBackup.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;

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

        try
        {
            Utils.LogWithPushover(BackupAction.ScheduledBackup, Resources.Main_Started);
            UpdateStatusLabel(string.Format(Resources.Main_Scanning, string.Empty));

            if (mediaBackup.Config.MonitoringOnOff)
            {
                Utils.LogWithPushover(BackupAction.ScheduledBackup,
                    $"Service monitoring is running every {Utils.FormatTimeFromSeconds(mediaBackup.Config.MonitoringInterval)}");
            }
            else
                Utils.LogWithPushover(BackupAction.ScheduledBackup, PushoverPriority.High, "Service monitoring is not running");
            long oldFileCount = mediaBackup.BackupFiles.Count;
            _ = DateTime.TryParse(mediaBackup.DirectoriesLastFullScan, out var backupFileDate);

            // Update the master files if we've not been monitoring directories directly
            if (!mediaBackup.Config.DirectoriesFileChangeWatcherOnOff ||
                backupFileDate.AddDays(mediaBackup.Config.DirectoriesDaysBetweenFullScan) < DateTime.Now)
                ScanAllDirectories(true);
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
            Utils.LogWithPushover(BackupAction.ScheduledBackup, Resources.Main_Completed);
            ResetAllControls();
            longRunningActionExecutingRightNow = false;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            Utils.Trace("Cancelling ScheduledBackupAsync");
            ASyncTasksCleanUp();
        }
        catch (Exception u)
        {
            Utils.LogWithPushover(BackupAction.Error, PushoverPriority.High,
                string.Format(Resources.Main_TaskWrapperException, u));
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