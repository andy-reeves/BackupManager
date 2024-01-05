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
            // The monitor is down
            monitor.UpdateFailures(DateTime.Now);
            if (monitor.FailureRetryExceeded) continue;

            var s = monitor.Failures.Count > 1 ? "s" : string.Empty;

            Utils.LogWithPushover(BackupAction.Monitoring, PushoverPriority.High,
                $"'{monitor.Name}' is down. {monitor.Failures.Count} failure{s} in the last {Utils.FormatTimeFromSeconds(monitor.FailureTimePeriod)}.");

            // check the latest version of this service available against the version running
            // if a newer version is available then send a message but do not stop/start the services

            if (monitor.ApplicationType > ApplicationType.Unknown)
            {
                Utils.Trace($"ApplicationType is {monitor.ApplicationType}");
                Utils.Trace($"BranchName is {monitor.BranchName}");
                var installedVersion = Utils.GetApplicationVersionNumber(monitor.ApplicationType);
                var availableVersion = Utils.GetLatestApplicationVersionNumber(monitor.ApplicationType, monitor.BranchName);
                Utils.Trace($"Installed is {installedVersion}");
                Utils.Trace($"Available is {availableVersion}");

                if (installedVersion != string.Empty && Utils.VersionIsNewer(installedVersion, availableVersion))
                {
                    Utils.LogWithPushover(BackupAction.Monitoring, PushoverPriority.High,
                        $"Newer version of service {monitor.ApplicationType} available so not stopping/starting service. Version {installedVersion} is installed and {availableVersion} is available.");
                    continue;
                }
            }

            if (monitor.ProcessToKill.HasValue())
            {
                var processesToKill = monitor.ProcessToKill.Split(',');

                foreach (var toKill in processesToKill)
                {
                    Utils.LogWithPushover(BackupAction.Monitoring, PushoverPriority.Normal,
                        $"Stopping all '{toKill}' processes that match");
                    _ = Utils.KillProcesses(toKill);
                }
            }

            if (monitor.ApplicationToStart.HasValue())
            {
                Utils.LogWithPushover(BackupAction.Monitoring, PushoverPriority.Normal, $"Starting {monitor.ApplicationToStart}");
                var processToStart = Environment.ExpandEnvironmentVariables(monitor.ApplicationToStart);

                if (File.Exists(processToStart))
                {
                    var newProcess = Process.Start(processToStart, monitor.ApplicationToStartArguments);

                    if (newProcess == null)
                    {
                        Utils.LogWithPushover(BackupAction.Monitoring, PushoverPriority.High,
                            $"Failed to start the new process '{monitor.Name}'");
                    }
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

            Utils.LogWithPushover(BackupAction.Monitoring, PushoverPriority.Normal, $"Restarting '{monitor.ServiceToRestart}'");

            if (Utils.RestartService(monitor.ServiceToRestart, monitor.Timeout * 1000))
                Utils.LogWithPushover(BackupAction.Monitoring, PushoverPriority.Normal, $"'{monitor.Name}' started");
            else
            {
                Utils.LogWithPushover(BackupAction.Monitoring, PushoverPriority.High,
                    $"Failed to restart the service '{monitor.Name}'");
            }
        }

        if (mediaBackup.Config.MonitoringCheckLatestVersions)
        {
            // all services checked so now just check for new versions and report
            foreach (var monitor in mediaBackup.Config.Monitors)
            {
                if (monitor.ApplicationType == ApplicationType.Unknown) continue;

                var installedVersion = Utils.GetApplicationVersionNumber(monitor.ApplicationType);
                var availableVersion = Utils.GetLatestApplicationVersionNumber(monitor.ApplicationType);

                if (!monitor.LogIssues || installedVersion == string.Empty ||
                    !Utils.VersionIsNewer(installedVersion, availableVersion))
                    continue;

                monitor.LogIssues = false;

                Utils.LogWithPushover(BackupAction.Monitoring, PushoverPriority.High,
                    $"Newer version of service {monitor.ApplicationType} is available. Version {installedVersion} is installed and {availableVersion} is available.");
            }
        }
        monitoringExecutingRightNow = false;
    }
}