// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="Config.cs" company="Andy Reeves">
//
//  </copyright>
//  --------------------------------------------------------------------------------------------------------------------

namespace BackupManager.Entities
{
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Xml.Serialization;

    public class Config
    {
        [XmlArrayItem("MasterFolder")]
        public Collection<string> MasterFolders;

        [XmlArrayItem("IndexFolder")]
        public Collection<string> IndexFolders;

        [XmlArrayItem("FilterRegEx")]
        public Collection<string> Filters;

        [XmlArrayItem("FilesToDeleteRegEx")]
        public Collection<string> FilesToDelete;

        [XmlArrayItem("DiskToSkip")]
        public Collection<string> DisksToSkipOnRestore;

        [XmlArrayItem("Monitor")]
        public Collection<ProcessServiceMonitor> Monitors;

        [XmlIgnore()]
        public Collection<FileRule> FileRules;

        /// <summary>
        /// If True the scheduled backup will start when the application starts
        /// </summary>
        public bool StartScheduledBackup;

        /// <summary>
        /// If True the service monitoring will start when the application starts
        /// </summary>
        public bool StartMonitoring;

        /// <summary>
        /// If True Pushover messages are sent
        /// </summary>
        public bool StartSendingPushoverMessages;

        /// <summary>
        /// The start time for the scheduled backup
        /// </summary>
        public string ScheduledBackupStartTime;

        public int DifferenceInFileCountAllowedPercentage;

        /// <summary>
        /// Number of disk speed test iterations to execute
        /// </summary>
        public int SpeedTestIterations;

        /// <summary>
        /// Size of test file for disk speed tests in MB
        /// </summary>
        public int SpeedTestFileSize;

        /// <summary>
        /// Interval in seconds
        /// </summary>
        public int MonitorInterval;

        public string PushoverAppToken;

        public string PushoverUserKey;

        /// <summary>
        /// Minimum space before we throw a critical Disk space message in MB for  Config.MasterFolders
        /// </summary>
        public long MinimumCriticalMasterFolderSpace;

        /// <summary>
        /// Minimum space before we throw a critical Disk space message in MB for backup disks
        /// </summary>
        public long MinimumCriticalBackupDiskSpace;

        /// <summary>
        /// Minimum space on a backup disk in MB for backup disks
        /// </summary>
        public long MinimumFreeSpaceToLeaveOnBackupDisk;

        /// <summary>
        /// Days To Report Old Backup Disks
        /// </summary>
        public int DaysToReportOldBackupDisks;

        /// <summary>
        /// Config.MinimumMasterFolderReadSpeed in MB/s
        /// </summary>
        public int MinimumMasterFolderReadSpeed;

        /// <summary>
        /// Config.MinimumMasterFolderWriteSpeed in MB/s
        /// </summary>
        public int MinimumMasterFolderWriteSpeed;

        /// <summary>
        /// True to exexcute disk speed tests
        /// </summary>
        public bool DiskSpeedTests;

        public Config()
        {
            MasterFolders = new Collection<string>();
            IndexFolders = new Collection<string>();
            Filters = new Collection<string>();
        }

        public static Config Load(string path)
        {
            Config config;
            XmlSerializer serializer = new XmlSerializer(typeof(Config));

            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                config = serializer.Deserialize(stream) as Config;
            }

            Rules rules = Rules.Load(Path.Combine(new FileInfo(path).DirectoryName, "Rules.xml"));

            if (rules != null)
            {
                config.FileRules = rules.FileRules;
            }

            return config ?? null;
        }

        internal void LogParameters()
        {
            string text = string.Empty;
            foreach (string masterFolder in MasterFolders)
            {
                text += $"{masterFolder}\n";
            }

            string parameterText = $" MasterFolders:\n{text}";

            text = string.Empty;
            foreach (string indexFolder in IndexFolders)
            {
                text += $"{indexFolder}\n";
            }
            parameterText += $"IndexFolders:\n{text}";

            text = string.Empty;
            foreach (string filter in Filters)
            {
                text += $"{filter}\n";
            }
            parameterText += $"Filters:\n{text}";

            text = string.Empty;
            foreach (string filesToDelete in FilesToDelete)
            {
                text += $"{filesToDelete}\n";
            }
            parameterText += $"FilesToDelete:\n{text}";

            text = string.Empty;
            foreach (string disksToSkip in DisksToSkipOnRestore)
            {
                text += $"{disksToSkip}\n";
            }
            parameterText += $"DisksToSkipOnRestore:\n{text}";
            Utils.Log(BackupAction.General, parameterText);

            text = string.Empty;
            foreach (ProcessServiceMonitor monitor in Monitors)
            {
                text += $"Monitor.Name: {monitor.Name}\n";
                text += $"Monitor.ProcessToKill: {monitor.ProcessToKill}\n";
                text += $"Monitor.Url: {monitor.Url}\n";
                text += $"Monitor.ApplicationToStart: {monitor.ApplicationToStart}\n";
                text += $"Monitor.ApplicationToStartArguments: {monitor.ApplicationToStartArguments}\n";
                text += $"Monitor.Port: {monitor.Port}\n";
                text += $"Monitor.ServiceToRestart: {monitor.ServiceToRestart}\n";
                text += $"Monitor.Timeout: {monitor.Timeout}\n";
            }
            parameterText = $"Monitors:\n{text}";
            Utils.Log(BackupAction.General, parameterText);

            text = string.Empty;
            foreach (FileRule rule in FileRules)
            {
                text += $"FileRule.Number: {rule.Number}\n";
                text += $"FileRule.Name: {rule.Name}\n";
                text += $"FileRule.FileDiscoveryRegEx: {rule.FileDiscoveryRegEx}\n";
                text += $"FileRule.FileTestRegEx: {rule.FileTestRegEx}\n";
                text += $"FileRule.Message: {rule.Message}\n";
            }
            parameterText = $"FileRules:\n{text}";
            Utils.Log(BackupAction.General, parameterText);

            text = $"StartMonitoring : {StartMonitoring}\n";
            text += $"StartScheduledBackup : {StartScheduledBackup}\n";
            text += $"MonitorInterval : {MonitorInterval}\n";
            text += $"ScheduledBackupStartTime : {ScheduledBackupStartTime}\n";
            text += $"DifferenceInFileCountAllowedPercentage : {DifferenceInFileCountAllowedPercentage}\n";
            text += $"PushoverAppToken : {PushoverAppToken}\n";
            text += $"PushoverUserKey : {PushoverUserKey}\n";
            text += $"MinimumCriticalMasterFolderSpace : {MinimumCriticalMasterFolderSpace}\n";
            text += $"MinimumCriticalBackupDiskSpace : {MinimumCriticalBackupDiskSpace}\n";
            text += $"MinimumFreeSpaceToLeaveOnBackupDisk : {MinimumFreeSpaceToLeaveOnBackupDisk}\n";
            text += $"MinimumMasterFolderReadSpeed : {MinimumMasterFolderReadSpeed}\n";
            text += $"MinimumMasterFolderWriteSpeed : {MinimumMasterFolderWriteSpeed}\n";
            text += $"DaysToReportOldBackupDisks : {DaysToReportOldBackupDisks}\n";
            text += $"DiskSpeedTests : {DiskSpeedTests}\n";
            text += $"SpeedTestFileSize : {SpeedTestFileSize}\n";
            text += $"SpeedTestIterations : {SpeedTestIterations}\n";
            text += $"StartSendingPushoverMessages : {StartSendingPushoverMessages}\n";

            parameterText = text;
            Utils.Log(BackupAction.General, parameterText);
        }
    }
}
