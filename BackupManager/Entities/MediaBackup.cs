// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="MediaBackup.cs" company="Andy Reeves">
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
    using System.Linq;
    using System.Xml.Serialization;

    public class MediaBackup
    {
        private string mediaBackupPath;

        [XmlArrayItem("MasterFolder")]
        public Collection<string> MasterFolders;

        [XmlArrayItem("IndexFolder")]
        public Collection<string> IndexFolders;

        [XmlArrayItem("FilterRegEx")]
        public Collection<string> Filters;

        [XmlArrayItem("FilesToDeleteRegEx")]
        public Collection<string> FilesToDelete;

        [XmlIgnore()]
        public Collection<FileRule> FileRules;

        [XmlArrayItem("DiskToSkip")]
        public Collection<string> DisksToSkipOnRestore;

        [XmlArrayItem("BackupFile")]
        public Collection<BackupFile> BackupFiles;

        [XmlArrayItem("Monitor")]
        public Collection<ProcessServiceMonitor> Monitors;

        [XmlArrayItem("BackupDisk")]
        public Collection<BackupDisk> BackupDisks;

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
        /// Minimum space before we throw a critical Disk space message in MB for MasterFolders
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
        /// MinimumMasterFolderReadSpeed in MB/s
        /// </summary>
        public int MinimumMasterFolderReadSpeed;

        /// <summary>
        /// MinimumMasterFolderWriteSpeed in MB/s
        /// </summary>
        public int MinimumMasterFolderWriteSpeed;

        /// <summary>
        /// True to exexcute disk speed tests
        /// </summary>
        public bool DiskSpeedTests;

        // We need to a hash of the index folder and relative path
        // we do this so we can look up files quickly by 
        // contents hashes are not unique. Duplicate files in different locations
        // The only guaranteed unique value is the indexfiolder and relative path
        // We dont want to delete the file off backup and then copy it again so we try a rename
        // as long as the file has the same indexfolder and relative path we can find it and rename it

        // This happened with The Porridge movie which is also stored as a Tv episode.
        private readonly Hashtable indexFolderAndRelativePath = new Hashtable(StringComparer.CurrentCultureIgnoreCase);

        public MediaBackup()
        {
            MasterFolders = new Collection<string>();
            IndexFolders = new Collection<string>();
            Filters = new Collection<string>();
            BackupFiles = new Collection<BackupFile>();
        }

        public MediaBackup(string mediaBackupPath)
        {
            this.mediaBackupPath = mediaBackupPath;
        }

        /// <summary>
        /// Creates a backup of the current xml file
        /// </summary>
        public void BackupMediaFile()
        {
            // take a copy of the xml file
            string destinationFileName = "MediaBackup-" + DateTime.Now.ToString("yy-MM-dd-HH-mm-ss.ff") + ".xml";
            string destinationPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "BackupManager_Backups", destinationFileName);
            Utils.FileCopy(mediaBackupPath, destinationPath);
        }

        public static MediaBackup Load(string path)
        {
            MediaBackup mediaBackup;
            XmlSerializer serializer = new XmlSerializer(typeof(MediaBackup));

            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                mediaBackup = serializer.Deserialize(stream) as MediaBackup;
            }

            if (mediaBackup == null)
            {
                return null;
            }

            mediaBackup.mediaBackupPath = path;

            foreach (string folder in mediaBackup.IndexFolders)
            {
                if (string.IsNullOrEmpty(folder))
                {
                    throw new ApplicationException("Empty indexfolders are not supported");
                }
            }

            bool toSave = false;

            foreach (BackupFile backupFile in mediaBackup.BackupFiles)
            {
                if (backupFile.ContentsHash == Utils.ZeroByteHash)
                {
                    throw new ApplicationException("Zerobyte Hash detected on load");
                }

                if (!mediaBackup.indexFolderAndRelativePath.Contains(backupFile.Hash))
                {
                    mediaBackup.indexFolderAndRelativePath.Add(backupFile.Hash, backupFile);
                }
                else
                {
                    // flag it for removal
                    backupFile.Flag = true;
                    toSave = true;
                }

                if (!backupFile.DiskChecked.HasValue())
                {
                    backupFile.ClearDiskChecked();
                }
            }

            if (toSave)
            {
                mediaBackup.RemoveFilesWithFlag(true, false);
                mediaBackup.Save();
                mediaBackup.ClearFlags();
            }

            // Attempt to load Rules.xml
            Rules rules = Rules.Load(Path.Combine(new FileInfo(path).DirectoryName, "Rules.xml"));

            if (rules != null)
            {
                mediaBackup.FileRules = rules.FileRules;
            }

            return mediaBackup;
        }

        public void Save()
        {
            BackupMediaFile();

            XmlSerializer xmlSerializer = new XmlSerializer(typeof(MediaBackup));

            if (File.Exists(mediaBackupPath))
            {
                File.SetAttributes(mediaBackupPath, FileAttributes.Normal);
            }

            using (StreamWriter streamWriter = new StreamWriter(mediaBackupPath))
            {
                xmlSerializer.Serialize(streamWriter, this);
            }
        }

        /// <summary>
        /// Gets a BackupFile representing the file from the contents Hashcode provided
        /// </summary>
        /// <param name="value">The contents Hashcode of the file to find.</param>
        /// <param name="masterFolder"></param>
        /// <param name="indexFolder"></param>
        /// <returns>Null if it wasn't found or null if more thsn 1</returns>
        public BackupFile GetBackupFileFromContentsHashcode(string value)
        {
            Utils.Trace("GetBackupFileFromContentsHashcode enter");

            int count = BackupFiles.Where(a => a.ContentsHash == value).Count();

            if (count == 0)
            {
                Utils.Trace("GetBackupFileFromContentsHashcode exit1");
                return null;
            }

            if (count > 1)
            {
                Utils.Trace($"More than 1 file with same ContentsHashcode {value}");
                Utils.Trace("GetBackupFileFromContentsHashcode exit2");
                return null;
            }

            BackupFile file = BackupFiles.First(q => q.ContentsHash == value);

            Utils.Trace("GetBackupFileFromContentsHashcode exit");
            return file;
        }

        /// <summary>
        /// Gets a BackupFile representing the file at fullPath
        /// </summary>
        /// <param name="fullPath">The fullPath to the file.</param>
        /// <param name="masterFolder"></param>
        /// <param name="indexFolder"></param>
        /// <returns>Null if it wasn't found or couldn't be created</returns>
        public BackupFile GetBackupFile(string fullPath, string masterFolder, string indexFolder)
        {
            Utils.Trace("GetBackupFile enter");
            Utils.Trace($"Params: path={fullPath}");

            // we hash the path of the file so we can look it up quickly
            // then we check the ModifiedTime and size
            // if these have changed we redo the hash
            // files with same hash are allowed (Porridge TV ep and movie)
            // files can't have same hash and same filename though

            string hashOfContents;

            BackupFile backupFile;

            string relativePath = BackupFile.GetRelativePath(fullPath, masterFolder, indexFolder);

            string hashKey = Path.Combine(indexFolder, relativePath);

            // if this path is already added then return it
            if (indexFolderAndRelativePath.Contains(hashKey))
            {
                backupFile = (BackupFile)indexFolderAndRelativePath[hashKey];

                // consider a file a.txt thats on \\nas1\assets1 in indexFolder _TV and on \\nas1\assets4 in _TV too
                // this has same index folder and path but its a different file
                if (backupFile.MasterFolder != masterFolder)
                {
                    // This is similar file in different master folders
                    // This also happens if a file is moved from 1 masterFolder to another
                    // its old location is still in the xml but the new location will be found on disk
                    Utils.Trace($"Duplicate file detected at {fullPath} and {backupFile.FullPath}");

                    // First we can check the hash of both
                    // its its the same hash then we can assume the file has just been moved
                    hashOfContents = Utils.GetShortMd5HashFromFile(fullPath);
                    if (hashOfContents == backupFile.ContentsHash)
                    {
                        Utils.Trace($"Changing masterFolder on {backupFile.FullPath} to {masterFolder}");
                        backupFile.MasterFolder = masterFolder;
                    }
                    else
                    {
                        Utils.Trace($"Hashes are different on the duplicate files");
                        Utils.Trace("GetBackupFile exit error");
                        return null;
                    }
                }
                else
                {
                    // check the timestamp against what we have
                    DateTime lastWriteTimeFromMasterFile = Utils.GetFileLastWriteTime(fullPath);

                    // if the file on disk is different then check the hash 
                    if (backupFile.LastWriteTime != lastWriteTimeFromMasterFile)
                    {
                        // update the timestamp as its changed/missing
                        backupFile.LastWriteTime = lastWriteTimeFromMasterFile;

                        hashOfContents = Utils.GetShortMd5HashFromFile(fullPath);

                        // has the contents hash changed too?
                        if (hashOfContents != backupFile.ContentsHash)
                        {
                            backupFile.UpdateContentsHash();

                            // clear the backup details as the master file hash has changed
                            backupFile.ClearDiskChecked();
                        }
                    }

                    // Check the file length
                    if (backupFile.Length == 0)
                    {
                        backupFile.UpdateFileLength();
                    }
                }

                Utils.Trace("GetBackupFile exit");
                return backupFile;
            }

            backupFile = new BackupFile(fullPath, masterFolder, indexFolder);
            Utils.Trace($"Adding backup file {backupFile.RelativePath}");
            BackupFiles.Add(backupFile);

            indexFolderAndRelativePath.Add(backupFile.Hash, backupFile);

            Utils.Trace("GetBackupFile exit");
            return backupFile;
        }

        /// <summary>
        /// Ensures the BackupFile exists and sets the Flag=TRUE 
        /// </summary>
        /// <param name="path"></param>
        /// <param name="masterFolder"></param>
        /// <param name="indexFolder"></param>
        /// <exception cref="ApplicationException"></exception>
        internal void EnsureFile(string path, string masterFolder, string indexFolder)
        {
            Utils.Trace("EnsureFile enter");

            // Empty Index folder is not allowed
            if (string.IsNullOrEmpty(indexFolder))
            {
                throw new ApplicationException("IndexFolder is empty. Not supported");
            }

            BackupFile backupFile = GetBackupFile(path, masterFolder, indexFolder)
                                    ?? throw new ApplicationException($"Duplicate hashcode detected indicated a copy of a file at {path}");
            backupFile.Flag = true;

            Utils.Trace("EnsureFile exit");
        }

        /// <summary>
        /// Get a BackupDisk for the current backupShare
        /// </summary>
        /// <param name="backupShare"></param>
        /// <returns>Null if disk is not connected</returns>
        public BackupDisk GetBackupDisk(string backupShare)
        {
            Utils.Trace("GetBackupDisk enter");

            // try and find a disk based on the diskname only
            // if more than 1 disk than return the first one
            string diskName = BackupDisk.GetBackupFolderName(backupShare);

            if (string.IsNullOrEmpty(diskName))
            {
                Utils.Trace($"GetBackupDisk exit with null");
                return null;
            }

            foreach (BackupDisk backupDisk in BackupDisks)
            {
                if (backupDisk.Name.Equals(diskName, StringComparison.CurrentCultureIgnoreCase))
                {
                    backupDisk.BackupShare = backupShare;
                    Utils.Trace($"GetBackupDisk exit with {backupDisk.Name}");
                    return backupDisk;
                }
            }

            BackupDisk disk = new BackupDisk(diskName, backupShare);
            BackupDisks.Add(disk);

            Utils.Trace("GetBackupDisk exit new disk");
            return disk;
        }

        /// <summary>
        /// Gets a BackupFile from the path provided. Path should include indexfolder and relativePath
        /// </summary>
        /// <param name="path"></param>
        /// <returns>Null if it doen't exist.</returns>
        public BackupFile GetBackupFile(string path)
        {
            return indexFolderAndRelativePath.Contains(path) ? (BackupFile)indexFolderAndRelativePath[path] : null;
        }

        /// <summary>
        /// Get BackupFiles on the diskName provided.
        /// </summary>
        /// <param name="diskName"></param>
        /// <returns></returns>
        public IEnumerable<BackupFile> GetBackupFilesOnBackupDisk(string diskName)
        {
            return BackupFiles.Where(p => p.Disk.Equals(diskName, StringComparison.CurrentCultureIgnoreCase));
        }

        /// <summary>
        /// Get BackupFiles that are in the MasterFolder provided
        /// </summary>
        /// <param name="masterFolder"></param>
        /// <returns></returns>
        public IEnumerable<BackupFile> GetBackupFilesInMasterFolder(string masterFolder)
        {
            return BackupFiles.Where(p => p.MasterFolder == masterFolder).OrderBy(q => q.BackupDiskNumber);
        }

        /// <summary>
        /// Get BackupFiles where Disk is null or Empty
        /// </summary>
        /// <returns></returns>
        public IEnumerable<BackupFile> GetBackupFilesWithDiskEmpty()
        {
            return BackupFiles.Where(p => string.IsNullOrEmpty(p.Disk));
        }

        /// <summary>
        /// Returns True if this path exists already. Path should contain indexfolder and relativepath.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public bool Contains(string path)
        {
            return indexFolderAndRelativePath.Contains(path);
        }

        /// <summary>
        /// Sets the Flag to False on all BackupFiles
        /// </summary>
        public void ClearFlags()
        {
            foreach (BackupFile backupFile in BackupFiles)
            {
                backupFile.Flag = false;
            }
        }

        /// Removes any files that have a matching flag value as the one provided.
        public void RemoveFilesWithFlag(bool flag, bool clearHashes)
        {
            Collection<BackupFile> filesToRemove = new Collection<BackupFile>();

            foreach (BackupFile backupFile in BackupFiles.Where(backupFile => backupFile.Flag == flag))
            {
                filesToRemove.Add(backupFile);
            }

            foreach (BackupFile backupFile in filesToRemove)
            {
                if (clearHashes)
                {
                    if (indexFolderAndRelativePath.Contains(backupFile.Hash))
                    {
                        indexFolderAndRelativePath.Remove(backupFile.Hash);
                    }
                }

                if (BackupFiles.Contains(backupFile))
                {
                    _ = BackupFiles.Remove(backupFile);
                }
            }
        }

        /// <summary>
        /// Returns the oldest BackupFile we have using the DiskChecked property.
        /// </summary>
        /// <returns></returns>
        public BackupFile GetOldestFile()
        {
            DateTime oldestFileDate = DateTime.Today;
            BackupFile oldestFile = null;

            foreach (BackupFile backupFile in BackupFiles)
            {
                if (backupFile.DiskChecked.HasValue())
                {
                    DateTime backupFileDate = DateTime.Parse(backupFile.DiskChecked);

                    if (backupFileDate < oldestFileDate)
                    {
                        oldestFileDate = backupFileDate;
                        oldestFile = backupFile;
                    }
                }
            }

            return oldestFile;
        }
        internal void LogParameters()
        {
            Utils.LogWithPushover(BackupAction.General, $"BackupManager started");

            string text = string.Empty;
            foreach (string masterFolder in MasterFolders)
            {
                text += $"{masterFolder}\n";
            }

            string parameterText = $"MasterFolders:\n{text}";

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
            text += $"StartScheduledBackup : {StartScheduledBackup}\n";
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
