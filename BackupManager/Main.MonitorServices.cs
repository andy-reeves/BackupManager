// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="Main.MonitorServices.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

using BackupManager.Entities;
using BackupManager.Extensions;
using BackupManager.Properties;

namespace BackupManager;

internal sealed partial class Main
{
    private void MonitorServices()
    {
        if (monitoringExecutingRightNow) return;

        monitoringExecutingRightNow = true;

        foreach (var monitor in mediaBackup.Config.Monitors.Where(static monitor => monitor.Port > 0 ? !Utils.ConnectionExists(monitor.Url, monitor.Port) : !Utils.UrlExists(monitor.Url, monitor.Timeout)))
        {
            monitor.UpdateFailures(DateTime.Now);
            if (monitor.FailureRetryExceeded) continue;

            var s = monitor.Failures.Count > 1 ? "s" : string.Empty;
            Utils.LogWithPushover(BackupAction.ApplicationMonitoring, PushoverPriority.High, string.Format(Resources.ServiceIsDown, monitor.Name, monitor.Failures.Count, s, Utils.FormatTimeFromSeconds(monitor.FailureTimePeriod / 1000)));
            if (monitor.ApplicationType > ApplicationType.Unknown && ApplicationMonitorNewerVersionCheck(monitor)) continue;

            Utils.Wait(monitor.DelayBeforeRestarting);
            if (monitor.ProcessToKill.HasValue()) MonitorKillProcesses(monitor);
            if (monitor.ApplicationToStart.HasValue()) MonitorApplicationToStart(monitor);
            if (monitor.ServiceToRestart.HasValue()) MonitorServiceToRestart(monitor);
        }
        if (mediaBackup.Config.MonitoringCheckLatestVersions) MonitorCheckLatestVersions();
        if (mediaBackup.Config.DirectoriesToHealthCheckOnOff) DirectoriesHealthCheck();
        monitoringExecutingRightNow = false;
    }

    private void DirectoriesHealthCheck()
    {
        // check the backup directories and the health check directories too
        foreach (var directory in mediaBackup.Config.DirectoriesToBackup.Concat(mediaBackup.Config.DirectoriesToHealthCheck))
        {
            if (!Utils.Directory.IsWritable(directory))
            {
                Utils.LogWithPushover(BackupAction.ApplicationMonitoring, PushoverPriority.Emergency, string.Format(Resources.DirectoryIsNotWritable, directory));

                // Turn off any more directory monitoring
                mediaBackup.Config.DirectoriesToHealthCheckOnOff = false;
            }
            _ = Utils.GetDiskInfo(directory, out var freeSpaceOnRootDirectoryDisk, out _);
            if (freeSpaceOnRootDirectoryDisk >= Utils.ConvertMBtoBytes(mediaBackup.Config.DirectoriesMinimumCriticalSpace)) continue;

            Utils.LogWithPushover(BackupAction.ApplicationMonitoring, PushoverPriority.High, $"Free space on {directory} is too low");

            // Turn off any more directory monitoring
            mediaBackup.Config.DirectoriesToHealthCheckOnOff = false;
        }
    }

    private void MonitorCheckLatestVersions()
    {
        // all services checked so now just check for new versions and report
        foreach (var monitor in mediaBackup.Config.Monitors)
        {
            try
            {
                if (monitor.ApplicationType == ApplicationType.Unknown) continue;

                var installedVersion = Utils.GetApplicationVersionNumber(monitor.ApplicationType);
                var availableVersion = Utils.GetLatestApplicationVersionNumber(monitor.ApplicationType);
                if (!monitor.LogIssues || installedVersion.HasNoValue() || availableVersion.HasNoValue() || !Utils.VersionIsNewer(installedVersion, availableVersion)) continue;

                monitor.LogIssues = false;
                Utils.LogWithPushover(BackupAction.ApplicationMonitoring, PushoverPriority.High, string.Format(Resources.NewerVersionOfApplicationAvailable, monitor.ApplicationType, installedVersion, availableVersion));
            }
            catch (NotSupportedException)
            {
                Utils.LogWithPushover(BackupAction.ApplicationMonitoring, PushoverPriority.High, string.Format(Resources.MonitorCheckLatestVersionsCouldNotBeChecked, monitor.ApplicationType));
            }
        }
    }

    private static void MonitorServiceToRestart(ProcessServiceMonitor monitor)
    {
        Utils.LogWithPushover(BackupAction.ApplicationMonitoring, PushoverPriority.High, string.Format(Resources.Restarting, monitor.ServiceToRestart));

        if (Utils.RestartService(monitor.ServiceToRestart, monitor.Timeout))
            Utils.LogWithPushover(BackupAction.ApplicationMonitoring, string.Format(Resources.MonitorServicesStarted, monitor.Name));
        else
            Utils.LogWithPushover(BackupAction.ApplicationMonitoring, PushoverPriority.High, string.Format(Resources.FailedToRestartService, monitor.Name));
    }

    private static void MonitorApplicationToStart(ProcessServiceMonitor monitor)
    {
        Utils.LogWithPushover(BackupAction.ApplicationMonitoring, string.Format(Resources.Starting, monitor.ApplicationToStart));
        var processToStart = Environment.ExpandEnvironmentVariables(monitor.ApplicationToStart);

        if (File.Exists(processToStart))
        {
            var newProcess = Process.Start(processToStart, monitor.ApplicationToStartArguments);

            if (newProcess == null)
                Utils.LogWithPushover(BackupAction.ApplicationMonitoring, PushoverPriority.High, string.Format(Resources.FailedToStart, monitor.Name));
            else
                Utils.LogWithPushover(BackupAction.ApplicationMonitoring, string.Format(Resources.MonitorServicesStarted, monitor.Name));
        }
        else
            Utils.LogWithPushover(BackupAction.ApplicationMonitoring, PushoverPriority.High, string.Format(Resources.FailedToStartAsNotFound, monitor.Name, monitor.ApplicationToStart, processToStart));
    }

    private static void MonitorKillProcesses(ProcessServiceMonitor monitor)
    {
        var processesToKill = monitor.ProcessToKill.Split(',');

        foreach (var toKill in processesToKill)
        {
            Utils.LogWithPushover(BackupAction.ApplicationMonitoring, PushoverPriority.High, string.Format(Resources.StoppingAllProcessesThatMatch, toKill));
            _ = Utils.KillProcesses(toKill);
        }
    }

    private static bool ApplicationMonitorNewerVersionCheck(ProcessServiceMonitor monitor)
    {
        Utils.Trace($"ApplicationType is {monitor.ApplicationType}");
        Utils.Trace($"BranchName is {monitor.BranchName}");
        var installedVersion = Utils.GetApplicationVersionNumber(monitor.ApplicationType);
        var availableVersion = Utils.GetLatestApplicationVersionNumber(monitor.ApplicationType, monitor.BranchName);
        Utils.Trace($"Installed is {installedVersion}");
        Utils.Trace($"Available is {availableVersion}");
        if (!Utils.VersionIsNewer(installedVersion, availableVersion)) return false;

        Utils.LogWithPushover(BackupAction.ApplicationMonitoring, PushoverPriority.High, string.Format(Resources.NewerVersionOfServiceAvailable, monitor.ApplicationType, installedVersion, availableVersion));
        return true;
    }
}