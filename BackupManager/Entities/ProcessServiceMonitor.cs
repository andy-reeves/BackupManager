// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="ProcessServiceMonitor.cs" company="Andy Reeves">
//
//  </copyright>
//  --------------------------------------------------------------------------------------------------------------------

namespace BackupManager.Entities
{
    using System;
    using System.Collections.Generic;
    using System.Xml.Serialization;

    public class ProcessServiceMonitor
    {
        /// <summary>
        ///     The list of DateTimes of the last failures to occur
        /// </summary>
        [XmlIgnore] internal List<DateTime> Failures = new();

        /// <summary>
        ///     The Url to monitor
        /// </summary>
        public string Url { get; set; }

        /// <summary>
        ///     The timeout in seconds
        /// </summary>
        public int Timeout { get; set; }

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
        ///     If the port specified is greater than 0 then the connection is checked. Otherwise its assumed to be a URL.
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        ///     Any service names to restart if this monitor is detected down.
        /// </summary>
        public string ServiceToRestart { get; set; }

        /// <summary>
        ///     Number of seconds to count the number of service/process failures. If this is exceeded then the a service
        ///     stop/restart is no longer attempted
        /// </summary>
        public int FailureTimePeriod { get; set; }

        /// <summary>
        ///     The maximum number of failures in FailureTimePeriod before we stop trying to start services again
        /// </summary>
        public int MaximumFailures { get; set; }

        /// <summary>
        ///     Once we've failed too much we set this to TRUE and don't try anymore
        /// </summary>
        [XmlIgnore]
        public bool FailureRetryExceeded { get; set; }

        public void UpdateFailures(DateTime newFailure)
        {
            Utils.Trace("UpdateFailures enter");
            if (FailureRetryExceeded)
            {
                Utils.Trace("UpdateFailures exit as FailureRetry=TRUE");
                return;
            }

            Failures.Add(newFailure);

            for (int i = Failures.Count - 1; i >= 0; i--)
            {
                DateTime a = Failures[i];
                if (a < DateTime.Now.AddSeconds(-FailureTimePeriod))
                {
                    Utils.Trace("UpdateFailures removing old failure as expired");
                    _ = Failures.Remove(a);
                }
            }

            if (Failures.Count > MaximumFailures)
            {
                Utils.Trace("UpdateFailures setting FailureRetry=TRUE");
                FailureRetryExceeded = true;
            }

            Utils.Trace($"Failures.Count = {Failures.Count}");
            Utils.Trace("UpdateFailures exit");
        }
    }
}