// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="Config.cs" company="Andy Reeves">
//
//  </copyright>
//  --------------------------------------------------------------------------------------------------------------------

namespace BackupManager.Entities
{
    using System;
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
        /// Days To Report Old Backup Disks
        /// </summary>
        public int BackupDiskDaysToReportSinceFilesChecked;

        /// <summary>
        /// If the new file count is more than this perencentage different we throw an eror
        /// </summary>
        public int BackupDiskDifferenceInFileCountAllowedPercentage;

        /// <summary>
        /// Minimum space before we throw a critical Disk space message in MB for backup disks
        /// </summary>
        public long BackupDiskMinimumCriticalSpace;

        /// <summary>
        /// Minimum space on a backup disk in MB for backup disks
        /// </summary>
        public long BackupDiskMinimumFreeSpaceToLeave;

        /// <summary>
        /// True and the Master Folders are monitored for changes
        /// </summary>
        public bool MasterFoldersFileChangeWatchersONOFF;

        /// <summary>
        /// Number of days before running a full master folder scan
        /// </summary>
        public int MasterFoldersDaysBetweenFullScan;

        /// <summary>
        /// Minimum space before we throw a critical Disk space message in MB for  Config.MasterFolders
        /// </summary>
        public long MasterFolderMinimumCriticalSpace;

        /// <summary>
        /// Config.MinimumMasterFolderReadSpeed in MB/s
        /// </summary>
        public int MasterFolderMinimumReadSpeed;

        /// <summary>
        /// Config.MinimumMasterFolderWriteSpeed in MB/s
        /// </summary>
        public int MasterFolderMinimumWriteSpeed;

        /// <summary>
        /// How often we process the detected folder changes in seconds
        /// </summary>
        public int MasterFoldersProcessChangesTimer;

        /// <summary>
        /// How often we scan the folders we've detected changes on in seconds
        /// </summary>
        public int MasterFoldersScanTimer;

        /// <summary>
        /// The minimum age of changes in folders before we schedule a scan in seconds
        /// </summary>
        public int MasterFolderScanMinimumAgeBeforeScanning;

        /// <summary>
        /// Interval in seconds
        /// </summary>
        public int MonitoringInterval;

        /// <summary>
        /// If True the service monitoring will start when the application starts
        /// </summary>
        public bool MonitoringONOFF;

        public string PushoverAppToken;

        /// <summary>
        /// If True Pushover messages are sent
        /// </summary>
        public bool PushoverONOFF;

        /// <summary>
        /// If TRUE Pushover Emergency priority messages will be sent when required
        /// </summary>
        public bool PushoverSendEmergencyONOFF;

        /// <summary>
        /// If TRUE Pushover High priority messages will be sent when required
        /// </summary>
        public bool PushoverSendHighONOFF;

        /// <summary>
        /// If TRUE Pushover Low/Lowest priority messages will be sent when required
        /// </summary>
        public bool PushoverSendLowONOFF;

        /// <summary>
        /// If TRUE Pushover Normal priority messages will be sent when required
        /// </summary>
        public bool PushoverSendNormalONOFF;

        public string PushoverUserKey;

        /// <summary>
        /// Sends a high priority message once this limit is passed
        /// </summary>
        public int PushoverWarningMessagesRemaining;

        /// <summary>
        /// If True the scheduled backup will start when scheduled
        /// </summary>
        public bool ScheduledBackupONOFF;

        /// <summary>
        /// If True the scheduled backup will start when the application starts
        /// </summary>
        public bool ScheduledBackupRunOnStartup;

        /// <summary>
        /// The start time for the scheduled backup
        /// </summary>
        public string ScheduledBackupStartTime;

        /// <summary>
        /// Size of test file for disk speed tests in MB
        /// </summary>
        public int SpeedTestFileSize;

        /// <summary>
        /// Number of disk speed test iterations to execute
        /// </summary>
        public int SpeedTestIterations;

        /// <summary>
        /// True to exexcute disk speed tests
        /// </summary>
        public bool SpeedTestONOFF;

        public Config()
        {
            MasterFolders = new Collection<string>();
            IndexFolders = new Collection<string>();
            Filters = new Collection<string>();
        }

        public static Config Load(string path)
        {
            try
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

                return config;
            }
            catch (InvalidOperationException ex)
            {
                throw new ApplicationException($"Unable to load config.xml {ex}");
            }
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
            Utils.Log(BackupAction.General, $"Monitors:\n{text}");

            text = string.Empty;
            foreach (FileRule rule in FileRules)
            {
                text += $"FileRule.Number: {rule.Number}\n";
                text += $"FileRule.Name: {rule.Name}\n";
                text += $"FileRule.FileDiscoveryRegEx: {rule.FileDiscoveryRegEx}\n";
                text += $"FileRule.FileTestRegEx: {rule.FileTestRegEx}\n";
                text += $"FileRule.Message: {rule.Message}\n";
            }
            Utils.Log(BackupAction.General, $"FileRules:\n{text}");

            text = $"BackupDiskDaysToReportSinceFilesChecked : {BackupDiskDaysToReportSinceFilesChecked}\n";
            text += $"BackupDiskDifferenceInFileCountAllowedPercentage : {BackupDiskDifferenceInFileCountAllowedPercentage}\n";
            text += $"BackupDiskMinimumCriticalSpace : {BackupDiskMinimumCriticalSpace}\n";
            text += $"BackupDiskMinimumFreeSpaceToLeave : {BackupDiskMinimumFreeSpaceToLeave}\n";

            text += $"MasterFoldersDaysBetweenFullScan : {MasterFoldersDaysBetweenFullScan}\n";
            text += $"MasterFoldersFileChangeWatchersONOFF : {MasterFoldersFileChangeWatchersONOFF}\n";
            text += $"MasterFolderMinimumCriticalSpace : {MasterFolderMinimumCriticalSpace}\n";
            text += $"MasterFolderMinimumReadSpeed : {MasterFolderMinimumReadSpeed}\n";
            text += $"MasterFolderMinimumWriteSpeed : {MasterFolderMinimumWriteSpeed}\n";
            text += $"MasterFoldersProcessChangesTimer : {MasterFoldersProcessChangesTimer}\n";
            text += $"MasterFoldersScanTimer : {MasterFoldersScanTimer}\n";
            text += $"MasterFolderScanMinimumAgeBeforeScanning : {MasterFolderScanMinimumAgeBeforeScanning}\n";
            Utils.Log(BackupAction.General, text);

            text = $"MonitoringInterval : {MonitoringInterval}\n";
            text += $"MonitoringONOFF : {MonitoringONOFF}\n";

            text += $"PushoverAppToken : {PushoverAppToken}\n";
            text += $"PushoverONOFF : {PushoverONOFF}\n";
            text += $"PushoverSendLowONOFF : {PushoverSendLowONOFF}\n";
            text += $"PushoverSendNormalONOFF : {PushoverSendNormalONOFF}\n";
            text += $"PushoverSendHighONOFF : {PushoverSendHighONOFF}\n";
            text += $"PushoverSendEmergencyONOFF : {PushoverSendEmergencyONOFF}\n";
            text += $"PushoverWarningMessagesRemaining : {PushoverWarningMessagesRemaining}\n";
            text += $"PushoverUserKey : {PushoverUserKey}\n";

            text += $"ScheduledBackupRunOnStartup : {ScheduledBackupRunOnStartup}\n";
            text += $"ScheduledBackupONOFF : {ScheduledBackupONOFF}\n";
            text += $"ScheduledBackupStartTime : {ScheduledBackupStartTime}\n";

            text += $"SpeedTestFileSize : {SpeedTestFileSize}\n";
            text += $"SpeedTestIterations : {SpeedTestIterations}\n";
            text += $"SpeedTestONOFF : {SpeedTestONOFF}\n";
            Utils.Log(BackupAction.General, text);
        }
    }
}
