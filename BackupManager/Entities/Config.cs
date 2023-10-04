// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="Config.cs" company="Andy Reeves">
//
//  </copyright>
//  --------------------------------------------------------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Xml.Serialization;

namespace BackupManager.Entities;

public class Config
{
    public Config()
    {
        MasterFolders = new Collection<string>();
        IndexFolders = new Collection<string>();
        Filters = new Collection<string>();
    }

    [XmlArrayItem("MasterFolder")] public Collection<string> MasterFolders { get; set; }

    [XmlArrayItem("IndexFolder")] public Collection<string> IndexFolders { get; set; }

    [XmlArrayItem("FilterRegEx")] public Collection<string> Filters { get; set; }

    [XmlArrayItem("FilesToDeleteRegEx")] public Collection<string> FilesToDelete { get; set; }

    [XmlArrayItem("DiskToSkip")] public Collection<string> DisksToSkipOnRestore { get; set; }

    [XmlArrayItem("Monitor")] public Collection<ProcessServiceMonitor> Monitors { get; set; }

    [XmlIgnore] public Collection<FileRule> FileRules { get; set; }

    [XmlArrayItem("SymbolicLink")] public Collection<SymbolicLink> SymbolicLinks { get; set; }

    /// <summary>
    ///     Days To Report Old Backup Disks
    /// </summary>
    public int BackupDiskDaysToReportSinceFilesChecked { get; set; }

    /// <summary>
    ///     If the new file count is more than this percentage different we throw an error
    /// </summary>
    public int BackupDiskDifferenceInFileCountAllowedPercentage { get; set; }

    /// <summary>
    ///     Minimum space before we throw a critical Disk space message in MB for backup disks
    /// </summary>
    public long BackupDiskMinimumCriticalSpace { get; set; }

    /// <summary>
    ///     Minimum space on a backup disk in MB for backup disks
    /// </summary>
    public long BackupDiskMinimumFreeSpaceToLeave { get; set; }

    /// <summary>
    ///     True and the Master Folders are monitored for changes
    /// </summary>
    public bool MasterFoldersFileChangeWatchersONOFF { get; set; }

    /// <summary>
    ///     Number of days before running a full master folder scan
    /// </summary>
    public int MasterFoldersDaysBetweenFullScan { get; set; }

    /// <summary>
    ///     Minimum space before we throw a critical Disk space message in MB for  Config.MasterFolders
    /// </summary>
    public long MasterFolderMinimumCriticalSpace { get; set; }

    /// <summary>
    ///     Config.MinimumMasterFolderReadSpeed in MB/s
    /// </summary>
    public int MasterFolderMinimumReadSpeed { get; set; }

    /// <summary>
    ///     Config.MinimumMasterFolderWriteSpeed in MB/s
    /// </summary>
    public int MasterFolderMinimumWriteSpeed { get; set; }

    /// <summary>
    ///     How often we process the detected folder changes in seconds
    /// </summary>
    public int MasterFoldersProcessChangesTimer { get; set; }

    /// <summary>
    ///     How often we scan the folders we've detected changes on in seconds
    /// </summary>
    public int MasterFoldersScanTimer { get; set; }

    /// <summary>
    ///     The minimum age of changes in folders before we schedule a scan in seconds
    /// </summary>
    public int MasterFolderScanMinimumAgeBeforeScanning { get; set; }

    /// <summary>
    ///     The root folder where Directory SymbolicLinks are created
    /// </summary>
    public string SymbolicLinksRootFolder { get; set; }

    /// <summary>
    ///     Interval in seconds
    /// </summary>
    public int MonitoringInterval { get; set; }

    /// <summary>
    ///     If True the service monitoring will start when the application starts
    /// </summary>
    public bool MonitoringONOFF { get; set; }

    public string PushoverAppToken { get; set; }

    /// <summary>
    ///     If True Pushover messages are sent
    /// </summary>
    public bool PushoverONOFF { get; set; }

    /// <summary>
    ///     If TRUE Pushover Emergency priority messages will be sent when required
    /// </summary>
    public bool PushoverSendEmergencyONOFF { get; set; }

    /// <summary>
    ///     If TRUE Pushover High priority messages will be sent when required
    /// </summary>
    public bool PushoverSendHighONOFF { get; set; }

    /// <summary>
    ///     If TRUE Pushover Low/Lowest priority messages will be sent when required
    /// </summary>
    public bool PushoverSendLowONOFF { get; set; }

    /// <summary>
    ///     If TRUE Pushover Normal priority messages will be sent when required
    /// </summary>
    public bool PushoverSendNormalONOFF { get; set; }

    public string PushoverUserKey { get; set; }

    /// <summary>
    ///     Sends a high priority message once this limit is passed
    /// </summary>
    public int PushoverWarningMessagesRemaining { get; set; }

    /// <summary>
    ///     If True the scheduled backup will start when scheduled
    /// </summary>
    public bool ScheduledBackupONOFF { get; set; }

    /// <summary>
    ///     If True the scheduled backup will start when the application starts
    /// </summary>
    public bool ScheduledBackupRunOnStartup { get; set; }

    /// <summary>
    ///     The start time for the scheduled backup
    /// </summary>
    public string ScheduledBackupStartTime { get; set; }

    /// <summary>
    ///     Size of test file for disk speed tests in MB
    /// </summary>
    public int SpeedTestFileSize { get; set; }

    /// <summary>
    ///     Number of disk speed test iterations to execute
    /// </summary>
    public int SpeedTestIterations { get; set; }

    /// <summary>
    ///     True to execute disk speed tests
    /// </summary>
    public bool SpeedTestONOFF { get; set; }

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

            var directoryName = new FileInfo(path).DirectoryName;
            if (directoryName == null) return config;
            var rules = Rules.Load(Path.Combine(directoryName, "Rules.xml"));

            if (rules == null) return config;
            if (config != null) config.FileRules = rules.FileRules;

            return config;
        }
        catch (InvalidOperationException ex)
        {
            throw new ApplicationException($"Unable to load config.xml {ex}");
        }
    }

    /// <summary>
    ///     Uses Reflection to Log all the Config parameters
    /// </summary>
    internal void LogParameters()
    {
        LogPropertiesForType(this);
    }

    private static void LogPropertiesForType(object obj)
    {
        var myType = obj.GetType();
        var properties = myType.GetProperties();
        string parameterText;

        if (myType == typeof(string))
        {
            parameterText = $"{obj}\n";
            Utils.Log(BackupAction.General, parameterText);
        }
        else
        {
            foreach (var property in properties)
                if (property.PropertyType == typeof(Collection<ProcessServiceMonitor>)
                    || property.PropertyType == typeof(Collection<FileRule>)
                    || property.PropertyType == typeof(List<DateTime>)
                    || property.PropertyType == typeof(Collection<string>)
                    || property.PropertyType == typeof(Collection<SymbolicLink>)
                   )
                {
                    Utils.Log(BackupAction.General, $"{property.Name}:");

                    var propertyValues = (ICollection)property.GetValue(obj);
                    if (propertyValues == null) continue;
                    foreach (var propertyValue in propertyValues) LogPropertiesForType(propertyValue);

                    if (propertyValues.Count == 0) Utils.Log(BackupAction.General, "<none>");
                }
                else
                {
                    if (property.PropertyType.IsGenericType)
                    {
                        Utils.LogWithPushover(BackupAction.General, PushoverPriority.High,
                            $"Unknown Config parameter type detected: {property.Name}");
                    }
                    else
                    {
                        parameterText = myType.Name == "String"
                            ? $"{obj}\n"
                            : $"{myType.Name}.{property.Name} : {property.GetValue(obj)}\n";
                        Utils.Log(BackupAction.General, parameterText);
                    }
                }
        }
    }
}