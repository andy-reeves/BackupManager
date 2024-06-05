// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="ProcessServiceMonitor.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Xml.Serialization;

namespace BackupManager.Entities;

[SuppressMessage("ReSharper", "MemberCanBeInternal")]
[SuppressMessage("ReSharper", "AutoPropertyCanBeMadeGetOnly.Global")]
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
[SuppressMessage("ReSharper", "UnusedMember.Global")]
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
[SuppressMessage("ReSharper", "CollectionNeverUpdated.Global")]
[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
public class ProcessServiceMonitor
{
    /// <summary>
    ///     The list of DateTimes of the last failures to occur
    /// </summary>
    [XmlIgnore] internal readonly List<DateTime> Failures = new();

    [XmlIgnore] internal bool LogIssues = true;

    public ApplicationType ApplicationType { get; set; }

    /// <summary>
    ///     The branch name to use to check the current available version. Defaults to 'master'.
    /// </summary>
    public string BranchName { get; set; } = "master";

    /// <summary>
    ///     The Url to monitor
    /// </summary>
    public string Url { get; set; }

    /// <summary>
    ///     The timeout in milliseconds
    /// </summary>
    public int Timeout { get; set; }

    /// <summary>
    ///     The delay in milliseconds before attempting the recovery of the service in seconds. Default 30,000 milliseconds.
    /// </summary>
    public int DelayBeforeRestarting { get; set; } = 30_000;

    /// <summary>
    ///     The name of any processes to kill if the monitor is detected down. Wildcards allowed.
    /// </summary>
    public string ProcessToKill { get; set; }

    /// <summary>
    ///     Full path to application to start if the monitor is detected down. Environment variables are expanded.
    /// </summary>
    public string ApplicationToStart { get; set; }

    /// <summary>
    ///     Any arguments to pass to the application to be started.
    /// </summary>
    public string ApplicationToStartArguments { get; set; }

    /// <summary>
    ///     The display name of this monitor.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    ///     If the port specified is greater than 0 then the connection is checked. Otherwise, it's assumed to be a URL.
    /// </summary>
    public int Port { get; set; }

    /// <summary>
    ///     Any service names to restart if this monitor is detected down.
    /// </summary>
    public string ServiceToRestart { get; set; }

    /// <summary>
    ///     Number of milliseconds to count the number of service/process failures. If this is exceeded then the service
    ///     stop/restart is no longer attempted
    /// </summary>
    public int FailureTimePeriod { get; set; }

    /// <summary>
    ///     The maximum number of failures in FailureTimePeriod before we stop trying to start services again
    /// </summary>
    public int MaximumFailures { get; set; }

    /// <summary>
    ///     Once we've failed too much we set this to True and don't try anymore
    /// </summary>
    [XmlIgnore]
    public bool FailureRetryExceeded { get; set; }

    public void UpdateFailures(DateTime newFailure)
    {
        Utils.TraceIn();

        if (FailureRetryExceeded)
        {
            _ = Utils.TraceOut("FailureRetryExceeded=TRUE");
            return;
        }
        Failures.Add(newFailure);

        for (var i = Failures.Count - 1; i >= 0; i--)
        {
            var a = Failures[i];
            if (a >= DateTime.Now.AddMilliseconds(-FailureTimePeriod)) continue;

            Utils.Trace("UpdateFailures removing old failure as expired");
            _ = Failures.Remove(a);
        }

        if (Failures.Count > MaximumFailures)
        {
            Utils.Trace("Setting FailureRetryExceeded=True");
            FailureRetryExceeded = true;
        }
        else
        {
            Utils.Trace("Setting FailureRetryExceeded=False");
            FailureRetryExceeded = false;
        }
        _ = Utils.TraceOut($"Failures.Count = {Failures.Count}");
    }
}