namespace BackupManager.Entities
{
    public class Monitor
    {
        public string Url;
        /// <summary>
        /// The timeout in seconds
        /// </summary>
        public int Timeout;

        /// <summary>
        /// The name of any processes to kill if the monitor is detected down. Wildcards allowed.
        /// </summary>
        public string ProcessToKill;

        /// <summary>
        /// Full path to application to start if the monitor is detected down. Environment variables are expanded.
        /// </summary>
        public string ApplicationToStart;
        /// <summary>
        /// Any arguments to pass to the application to be started.
        /// </summary>
        public string ApplicationToStartArguments;

        /// <summary>
        /// The display name of this monitor.
        /// </summary>
        public string Name;

        /// <summary>
        /// If the port specified is greater than 0 then the connection is checked. Otherwise its assumed to be a URL.
        /// </summary>
        public int Port;

        /// <summary>
        /// Any servie names to restart if this monitor is detected down.
        /// </summary>
        public string ServiceToRestart;
    }
}
