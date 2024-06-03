// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="Config.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Xml.Schema;
using System.Xml.Serialization;

using BackupManager.Properties;

namespace BackupManager.Entities;

[SuppressMessage("ReSharper", "MemberCanBeInternal")]
[SuppressMessage("ReSharper", "AutoPropertyCanBeMadeGetOnly.Global")]
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
[SuppressMessage("ReSharper", "UnusedMember.Global")]
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
[SuppressMessage("ReSharper", "CollectionNeverUpdated.Global")]
[SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "<Pending>")]
[SuppressMessage("ReSharper", "IdentifierTypo")]
[SuppressMessage("ReSharper", "CommentTypo")]
public sealed class Config
{
    [XmlArrayItem("Directory")] public Collection<string> DirectoriesToBackup { get; set; } = new();

    [XmlArrayItem("Directory")] public Collection<string> DirectoriesToHealthCheck { get; set; } = new();

    [XmlArrayItem("FilterRegEx")] public Collection<string> Filters { get; set; } = new();

    [XmlArrayItem("FilesToDeleteRegEx")] public Collection<string> FilesToDelete { get; set; }

    [XmlArrayItem("DiskToSkip")] public Collection<string> DisksToSkipOnRestore { get; set; }

    [XmlArrayItem("Monitor")] public Collection<ProcessServiceMonitor> Monitors { get; set; }

    [XmlArrayItem("FileRenameRule")] public Collection<FileRenameRule> FileRenameRules { get; set; }

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
    ///     The backup disk path
    /// </summary>
    public string BackupDisk { get; set; }

    /// <summary>
    ///     Minimum space before we throw a critical Disk space message in MB for backup disks
    /// </summary>
    public long BackupDiskMinimumCriticalSpace { get; set; }

    /// <summary>
    ///     Minimum space on a backup disk in MB for backup disks
    /// </summary>
    public long BackupDiskMinimumFreeSpaceToLeave { get; set; }

    /// <summary>
    ///     Number of days before running a full directories scan
    /// </summary>
    public int DirectoriesDaysBetweenFullScan { get; set; }

    /// <summary>
    ///     True and the Directories are monitored for changes
    /// </summary>
    public bool DirectoriesFileChangeWatcherOnOff { get; set; }

    /// <summary>
    ///     Whether to check video files are named corrected and rename them if not
    /// </summary>
    public bool DirectoriesRenameVideoFilesOnOff { get; set; }

    /// <summary>
    ///     Whether to check video files are named corrected and rename them if not when doing full scans
    /// </summary>
    public bool DirectoriesRenameVideoFilesForFullScansOnOff { get; set; }

    /// <summary>
    ///     If an error occurs whilst monitoring Directories we wait for this long before attempting to Reset the Watcher in
    ///     milliseconds (120)
    /// </summary>
    public int DirectoriesFileChangeWatcherRestartDelay { get; set; }

    /// <summary>
    ///     The Regex used to filter file changes in the FileSystemWatcher
    /// </summary>
    public string DirectoriesFilterRegEx { get; set; }

    /// <summary>
    ///     Any files matching this will be checked for correct name and renamed if not correct
    /// </summary>
    public string DirectoriesRenameVideoFilesRegEx { get; set; }

    /// <summary>
    ///     The minimum age of changes in directories before we schedule a scan in milliseconds
    /// </summary>
    public int DirectoriesMinimumAgeBeforeScanning { get; set; }

    /// <summary>
    ///     Minimum space before we throw a critical Disk space message in MB for directories
    /// </summary>
    public long DirectoriesMinimumCriticalSpace { get; set; }

    /// <summary>
    ///     Minimum Read Speed in MB/s
    /// </summary>
    public int DirectoriesMinimumReadSpeed { get; set; }

    /// <summary>
    ///     Minimum Write Speed in MB/s
    /// </summary>
    public int DirectoriesMinimumWriteSpeed { get; set; }

    /// <summary>
    ///     How often we process the detected directory changes in milliseconds
    /// </summary>
    public int DirectoriesProcessChangesTimer { get; set; }

    /// <summary>
    ///     How often we scan the directories we've detected changes on in milliseconds
    /// </summary>
    public int DirectoriesScanTimer { get; set; }

    /// <summary>
    ///     The number of recent file changes to scan for changed directories
    /// </summary>
    public int FilesToScanForChanges { get; set; }

    /// <summary>
    ///     Any files matching this will be checked to report unique content hash codes
    /// </summary>
    public string DuplicateContentHashCodesDiscoveryRegex { get; set; }

    /// <summary>
    ///     The capturing group is this Regex must be unique for all matching files
    /// </summary>
    public string DuplicateFilesRegex { get; set; }

    /// <summary>
    ///     When true check for latest versions of applications
    /// </summary>
    public bool MonitoringCheckLatestVersions { get; set; }

    /// <summary>
    ///     Monitoring Interval in milliseconds
    /// </summary>
    public int MonitoringInterval { get; set; }

    /// <summary>
    ///     If True the service monitoring will start when the application starts
    /// </summary>
    public bool MonitoringOnOff { get; set; }

    /// <summary>
    ///     Delay before monitoring starts until after the first monitoring interval
    /// </summary>
    public bool MonitoringStartDelayOnOff { get; set; }

    /// <summary>
    ///     A PlexPass token
    /// </summary>
    public string PlexToken { get; set; }

    [XmlArrayItem("Token")] public Collection<string> PushoverAppTokens { get; set; } = new();

    [XmlIgnore] public string PushoverAppTokenToUse { get; set; }

    /// <summary>
    ///     If True Pushover messages are sent
    /// </summary>
    public bool PushoverOnOff { get; set; }

    /// <summary>
    ///     If TRUE Pushover Emergency priority messages will be sent when required
    /// </summary>
    public bool PushoverSendEmergencyOnOff { get; set; }

    /// <summary>
    ///     If TRUE Pushover High priority messages will be sent when required
    /// </summary>
    public bool PushoverSendHighOnOff { get; set; }

    /// <summary>
    ///     If TRUE Pushover Low/Lowest priority messages will be sent when required
    /// </summary>
    public bool PushoverSendLowOnOff { get; set; }

    /// <summary>
    ///     If TRUE Pushover Normal priority messages will be sent when required
    /// </summary>
    public bool PushoverSendNormalOnOff { get; set; }

    public string PushoverUserKey { get; set; }

    /// <summary>
    ///     Sends a high priority message once this limit is passed
    /// </summary>
    public int PushoverWarningMessagesRemaining { get; set; }

    /// <summary>
    ///     If True the scheduled backup will start when scheduled
    /// </summary>
    public bool ScheduledBackupOnOff { get; set; }

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
    public bool SpeedTestOnOff { get; set; }

    /// <summary>
    ///     True to check directories for health
    /// </summary>
    public bool DirectoriesToHealthCheckOnOff { get; set; }

    public static Config Load(string path)
    {
        try
        {
            if (!Utils.ValidateXmlFromResources(path, "BackupManager.ConfigSchema.xsd")) throw new XmlSchemaValidationException("Config.xml failed validation");

            var xRoot = new XmlRootAttribute { ElementName = "Config", Namespace = "ConfigSchema.xsd", IsNullable = true };
            Config config;
            XmlSerializer serializer = new(typeof(Config), xRoot);

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
            throw new ApplicationException(string.Format(Resources.UnableToLoadXml, "Config.xml", ex));
        }
        catch (XmlSchemaValidationException ex)
        {
            throw new ApplicationException(string.Format(Resources.UnableToLoadXml, "Config.xml failed validation", ex));
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
            {
                if (property.PropertyType == typeof(Collection<ProcessServiceMonitor>) || property.PropertyType == typeof(Collection<FileRule>) || property.PropertyType == typeof(List<DateTime>) ||
                    property.PropertyType == typeof(Collection<FileRenameRule>) || property.PropertyType == typeof(Collection<string>) || property.PropertyType == typeof(Collection<SymbolicLink>))
                {
                    Utils.Log(BackupAction.General, $"{property.Name}:");
                    var propertyValues = (ICollection)property.GetValue(obj);
                    if (propertyValues == null) continue;

                    foreach (var propertyValue in propertyValues)
                    {
                        LogPropertiesForType(propertyValue);
                    }
                    if (propertyValues.Count == 0) Utils.Log(BackupAction.General, "<none>");
                }
                else
                {
                    if (property.PropertyType.IsGenericType)
                        Utils.LogWithPushover(BackupAction.Error, PushoverPriority.High, $"Unknown Config parameter type detected: {property.Name}");
                    else
                    {
                        parameterText = myType.Name == "String" ? $"{obj}\n" : $"{myType.Name}.{property.Name} : {property.GetValue(obj)}\n";
                        Utils.Log(BackupAction.General, parameterText);
                    }
                }
            }
        }
    }
}