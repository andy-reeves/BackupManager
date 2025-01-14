// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="Main.ScheduledBackup.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Threading;

using BackupManager.Properties;

namespace BackupManager;

internal sealed partial class Main
{
    private void ScheduledBackupAsync(CancellationToken ct)
    {
        try
        {
            Utils.TraceIn();
            if (longRunningActionExecutingRightNow) return;

            DisableControlsForAsyncTasks(ct);
            Utils.LogWithPushover(BackupAction.ScheduledBackup, Resources.Started, false, true);
            UpdateStatusLabel(ct, string.Format(Resources.Scanning, string.Empty));

            if (mediaBackup.Config.MonitoringOnOff)
            {
                Utils.LogWithPushover(BackupAction.ScheduledBackup,
                    string.Format(Resources.ServiceMonitoringIsRunning, Utils.FormatTimeFromSeconds(mediaBackup.Config.MonitoringInterval / 1000)));
            }
            else
                Utils.LogWithPushover(BackupAction.ScheduledBackup, PushoverPriority.High, Resources.ServiceMonitoringNotRunning);
            long oldFileCount = mediaBackup.BackupFiles.Count;
            _ = DateTime.TryParse(mediaBackup.DirectoriesLastFullScan, out var backupFileDate);

            if (!mediaBackup.Config.DirectoriesFileChangeWatcherOnOff || backupFileDate.AddDays(mediaBackup.Config.DirectoriesDaysBetweenFullScan) < DateTime.Now)
            {
                Utils.LogWithPushover(BackupAction.ScanDirectory, Resources.ScheduledBackupAsyncDoingAFullScan);

                // if file watching is off, or it's been a number of days since last full scan
                ScanAllDirectories(true, ct);
            }
            UpdateSymbolicLinks(ct);

            if (mediaBackup.Config.BackupDiskDifferenceInFileCountAllowedPercentage != 0)
            {
                var minimumFileCountAllowed = oldFileCount - oldFileCount * mediaBackup.Config.BackupDiskDifferenceInFileCountAllowedPercentage / 100;
                long newFileCount = mediaBackup.BackupFiles.Count;
                if (newFileCount < minimumFileCountAllowed) throw new ApplicationException(Resources.FilesCountIsTooLow);
            }

            // checks for backup disks not verified in > xx days
            CheckForOldBackupDisks();

            // Check the connected backup disk (removing any extra files we don't need)
            _ = CheckConnectedDisk(true, ct);

            // Copy any files that need a backup
            CopyFiles(true, ct);
            Utils.Trace($"TriggerHour={_trigger.TriggerHour}");
            Utils.LogWithPushover(BackupAction.ScheduledBackup, Resources.Completed, true);
            ResetAllControls();
        }
        finally
        {
            Utils.TraceOut();
        }
    }

    private void SetupDailyTrigger(bool addTrigger, DateTime executeTime)
    {
        Utils.TraceIn();
        updateUITimer.Enabled = true; // because we want to update the directory tracking every 1 min or so anyway

        if (addTrigger)
        {
            _trigger = new DailyTrigger(executeTime);
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