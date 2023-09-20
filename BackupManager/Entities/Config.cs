// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="Config.cs" company="Andy Reeves">
//
//  </copyright>
//  --------------------------------------------------------------------------------------------------------------------

namespace BackupManager.Entities
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Reflection;
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
                XmlSerializer serializer = new(typeof(Config));

                using (FileStream stream = new(path, FileMode.Open, FileAccess.Read))
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

        /// <summary>
        /// Uses Reflection to Log all the Config parameters
        /// </summary>
        internal void LogParameters()
        {
            LogFieldsForType(this);
        }

        private void LogFieldsForType(object obj)
        {
            Type myType = obj.GetType();
            FieldInfo[] fields = myType.GetFields();
            string parameterText;
            string text;

            foreach (FieldInfo field in fields)
            {
                if (field.FieldType == typeof(Collection<ProcessServiceMonitor>)
                       || field.FieldType == typeof(Collection<FileRule>)
                       || field.FieldType == typeof(List<DateTime>)
                       || field.FieldType == typeof(Collection<string>)
                     )
                {
                    text = string.Empty;
                    Utils.Log(BackupAction.General, $"{field.Name}:");

                    ICollection fieldValues = (ICollection)field.GetValue(obj);
                    foreach (object fieldValue in fieldValues)
                    {
                        LogFieldsForType(fieldValue);
                    }
                    if (fieldValues.Count == 0)
                    {
                        Utils.Log(BackupAction.General, "<none>");
                    }
                }
                else
                {
                    if (field.FieldType.IsGenericType)
                    {
                        Utils.LogWithPushover(BackupAction.General, PushoverPriority.High, $"Unknown Config parameter type detected: {field.Name}");
                    }
                    else
                    {
                        parameterText = myType.Name == "String" ? $"{obj}\n" : $"{myType.Name}.{field.Name} : {field.GetValue(obj)}\n";
                        Utils.Log(BackupAction.General, parameterText);
                    }
                }
            }
        }
    }
}
