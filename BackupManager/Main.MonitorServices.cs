﻿// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="Main.MonitorServices.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;

using BackupManager.Extensions;

namespace BackupManager;

internal sealed partial class Main
{
    [SupportedOSPlatform("windows")]
    private void MonitorServices()
    {
        foreach (var monitor in mediaBackup.Config.Monitors.Where(static monitor =>
                     monitor.Port > 0 ? !Utils.ConnectionExists(monitor.Url, monitor.Port) : !Utils.UrlExists(monitor.Url, monitor.Timeout * 1000)))
        {
            // The monitor is down
            monitor.UpdateFailures(DateTime.Now);
            if (monitor.FailureRetryExceeded) continue;

            var s = monitor.Failures.Count > 1 ? "s" : string.Empty;

            Utils.LogWithPushover(BackupAction.Monitoring, PushoverPriority.High,
                $"'{monitor.Name}' is down. {monitor.Failures.Count} failure{s} in the last {Utils.FormatTimeFromSeconds(monitor.FailureTimePeriod)}.");

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
                Utils.LogWithPushover(BackupAction.Monitoring, PushoverPriority.Normal, $"Starting {monitor.ApplicationToStart}");
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

            Utils.LogWithPushover(BackupAction.Monitoring, PushoverPriority.Normal, $"Restarting '{monitor.ServiceToRestart}'");

            if (Utils.RestartService(monitor.ServiceToRestart, monitor.Timeout * 1000))
                Utils.LogWithPushover(BackupAction.Monitoring, PushoverPriority.Normal, $"'{monitor.Name}' started");
            else
                Utils.LogWithPushover(BackupAction.Monitoring, PushoverPriority.High, $"Failed to restart the service '{monitor.Name}'");
        }
    }
}