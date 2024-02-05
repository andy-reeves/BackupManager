// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="Main.MonitorServices.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;

using BackupManager.Entities;
using BackupManager.Extensions;
using BackupManager.Properties;

namespace BackupManager;

internal sealed partial class Main
{
    [SupportedOSPlatform("windows")]
    private void MonitorServices()
    {
        if (monitoringExecutingRightNow) return;

        monitoringExecutingRightNow = true;

        foreach (var monitor in mediaBackup.Config.Monitors.Where(static monitor =>
                     monitor.Port > 0
                         ? !Utils.ConnectionExists(monitor.Url, monitor.Port)
                         : !Utils.UrlExists(monitor.Url, monitor.Timeout * 1000)))
        {
            monitor.UpdateFailures(DateTime.Now);
            if (monitor.FailureRetryExceeded) continue;

            var s = monitor.Failures.Count > 1 ? "s" : string.Empty;

            Utils.LogWithPushover(BackupAction.ApplicationMonitoring, PushoverPriority.High,
                string.Format(Resources.ServiceIsDown, monitor.Name, monitor.Failures.Count, s,
                    Utils.FormatTimeFromSeconds(monitor.FailureTimePeriod)));
            if (monitor.ApplicationType > ApplicationType.Unknown && MonitorTypeUnknown(monitor)) continue;

            if (monitor.ProcessToKill.HasValue()) MonitorKillProcesses(monitor);
            if (monitor.ApplicationToStart.HasValue()) MonitorApplicationToStart(monitor);
            if (monitor.ServiceToRestart.HasValue()) MonitorServiceToRestart(monitor);
        }
        if (mediaBackup.Config.MonitoringCheckLatestVersions) MonitorCheckLatestVersions();
        CheckDirectoriesAreWritable();
        monitoringExecutingRightNow = false;
    }

    private void CheckDirectoriesAreWritable()
    {
        foreach (var directory in mediaBackup.Config.Directories.Where(static directory => !Utils.IsDirectoryWritable(directory)))
        {
            Utils.LogWithPushover(BackupAction.ApplicationMonitoring, PushoverPriority.High,
                string.Format(Resources.DirectoryIsNotWritable, directory));

            // Turn off any more monitoring
            mediaBackup.Config.MonitoringOnOff = false;
            UpdateMonitoringButton();
        }
    }

    private void MonitorCheckLatestVersions()
    {
        // all services checked so now just check for new versions and report
        foreach (var monitor in mediaBackup.Config.Monitors)
        {
            if (monitor.ApplicationType == ApplicationType.Unknown) continue;

            var installedVersion = Utils.GetApplicationVersionNumber(monitor.ApplicationType);
            var availableVersion = Utils.GetLatestApplicationVersionNumber(monitor.ApplicationType);

            if (!monitor.LogIssues || installedVersion == string.Empty || availableVersion == string.Empty ||
                !Utils.VersionIsNewer(installedVersion, availableVersion))
                continue;

            monitor.LogIssues = false;

            Utils.LogWithPushover(BackupAction.ApplicationMonitoring, PushoverPriority.High,
                $"Newer version of service {monitor.ApplicationType} is available. Version {installedVersion} is installed and {availableVersion} is available.");
        }
    }

    [SupportedOSPlatform("windows")]
    private static void MonitorServiceToRestart(ProcessServiceMonitor monitor)
    {
        Utils.LogWithPushover(BackupAction.ApplicationMonitoring, PushoverPriority.Normal,
            string.Format(Resources.Restarting, monitor.ServiceToRestart));

        if (Utils.RestartService(monitor.ServiceToRestart, monitor.Timeout * 1000))
        {
            Utils.LogWithPushover(BackupAction.ApplicationMonitoring, PushoverPriority.Normal,
                string.Format(Resources.MonitorServicesStarted, monitor.Name));
        }
        else
        {
            Utils.LogWithPushover(BackupAction.ApplicationMonitoring, PushoverPriority.High,
                string.Format(Resources.FailedToRestartService, monitor.Name));
        }
    }

    private static void MonitorApplicationToStart(ProcessServiceMonitor monitor)
    {
        Utils.LogWithPushover(BackupAction.ApplicationMonitoring, PushoverPriority.Normal,
            string.Format(Resources.Starting, monitor.ApplicationToStart));
        var processToStart = Environment.ExpandEnvironmentVariables(monitor.ApplicationToStart);

        if (File.Exists(processToStart))
        {
            var newProcess = Process.Start(processToStart, monitor.ApplicationToStartArguments);

            if (newProcess == null)
                Utils.LogWithPushover(BackupAction.ApplicationMonitoring, PushoverPriority.High,
                    string.Format(Resources.FailedToStart, monitor.Name));
            else
            {
                Utils.LogWithPushover(BackupAction.ApplicationMonitoring, PushoverPriority.Normal,
                    string.Format(Resources.MonitorServicesStarted, monitor.Name));
            }
        }
        else
        {
            Utils.LogWithPushover(BackupAction.ApplicationMonitoring, PushoverPriority.High,
                string.Format(Resources.FailedToStartAsNotFound, monitor.Name, monitor.ApplicationToStart, processToStart));
        }
    }

    private static void MonitorKillProcesses(ProcessServiceMonitor monitor)
    {
        var processesToKill = monitor.ProcessToKill.Split(',');

        foreach (var toKill in processesToKill)
        {
            Utils.LogWithPushover(BackupAction.ApplicationMonitoring, PushoverPriority.Normal, $"Stopping all '{toKill}' processes that match");
            _ = Utils.KillProcesses(toKill);
        }
    }

    private static bool MonitorTypeUnknown(ProcessServiceMonitor monitor)
    {
        Utils.Trace($"ApplicationType is {monitor.ApplicationType}");
        Utils.Trace($"BranchName is {monitor.BranchName}");
        var installedVersion = Utils.GetApplicationVersionNumber(monitor.ApplicationType);
        var availableVersion = Utils.GetLatestApplicationVersionNumber(monitor.ApplicationType, monitor.BranchName);
        Utils.Trace($"Installed is {installedVersion}");
        Utils.Trace($"Available is {availableVersion}");

        if (installedVersion.HasValue() && availableVersion.HasValue() && Utils.VersionIsNewer(installedVersion, availableVersion))
        {
            Utils.LogWithPushover(BackupAction.ApplicationMonitoring, PushoverPriority.High,
                string.Format(Resources.NewerVersionOfServiceAvailable, monitor.ApplicationType, installedVersion, availableVersion));
            return true;
        }
        return false;
    }
}