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

        //If the file name contains an edition like '{edition-EXTENDED EDITION}' then check its one of these
        [XmlArrayItem("Edition")]
        public Collection<string> EditionsAllowed;

        [XmlArrayItem("VideoFormat")]
        public Collection<string> VideoFoldersFormatsAllowed;

        [XmlArrayItem("DiskToSkip")]
        public Collection<string> DisksToSkipOnRestore;

        [XmlArrayItem("BackupFile")]
        public Collection<BackupFile> BackupFiles;

        [XmlArrayItem("Monitor")]
        public Collection<Monitor> Monitors;

        [XmlArrayItem("BackupDisk")]
        public Collection<BackupDisk> BackupDisks;

        /// <summary>
        /// If True the scheduled backup will start when the application starts at the scheudled time
        /// </summary>
        public bool StartScheduledBackup;

        /// <summary>
        /// The start time for the scheudled backup
        /// </summary>
        public string ScheduledBackupStartTime;

        public int DifferenceInFileCountAllowedPercentage;

        /// <summary>
        /// Interval in seconds
        /// </summary>
        public int MonitorInterval;


        public string PushoverAppToken;

        public string PushoverUserKey;

        /// <summary>
        /// Minimum space before we throw a critical Disk space message in GB for MasterFolders
        /// </summary>
        public long MinimumCriticalMasterFolderSpace;

        /// <summary>
        /// Minimum space before we throw a critical Disk space message in GB for backup disks
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

        // We need to store 2 hashes
        // the main hash is keyed on the FullPath (in lowercase) and retuerns a BackupFile
        // the second hash is keyed off the contents hash of the file and the relative path
        // (doesn't include the masterfolder or indexfolder)
        // we do this so we can look up files quickly by 
        // contents hashes are not unique. Duplicate files in different locations
        // The only guaranteed unique value is the fullpath
        // The second hashtable is far when we've moved files from one master folder to another.
        // We dont want to delete the file off backup and then copy it again so we try a rename
        // as long as the file has the same relative path we can find it and rename it

        // This happened with The Porridge movie which is also stored as a Tv episode.
        private readonly Hashtable contentsHashIndexFolderAndRelativePath = new Hashtable(StringComparer.CurrentCultureIgnoreCase);

        private readonly Hashtable paths = new Hashtable(StringComparer.CurrentCultureIgnoreCase);

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

        public void BackupMediaFile()
        {
            // take a copy of the xml file
            string destinationFileName = "MediaBackup-" + DateTime.Now.ToString("yy-MM-dd-HH-mm-ss.ff") + ".xml";
            string destinationPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), destinationFileName);

            File.Copy(mediaBackupPath, destinationPath);
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

                if (!mediaBackup.contentsHashIndexFolderAndRelativePath.Contains(backupFile.Hash) && !mediaBackup.paths.Contains(backupFile.FullPath))
                {
                    mediaBackup.contentsHashIndexFolderAndRelativePath.Add(backupFile.Hash, backupFile);
                    mediaBackup.paths.Add(backupFile.FullPath, backupFile);
                }
                else
                {
                    // flag it for removal
                    backupFile.Flag = true;
                    toSave = true;
                }
            }

            if (toSave)
            {
                mediaBackup.RemoveFilesWithFlag(true, false);
                mediaBackup.Save();
                mediaBackup.ClearFlags();
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
        /// 
        /// </summary>
        /// <param name="backupFileFullPath"></param>
        /// <returns>Null if the backupFileFullPath doesn't start with any MasterFolders/IndexFolder combination</returns>
        internal string GetRelativePath(string backupFileFullPath)
        {
            foreach (string indexFolder in
            // from the full backupdisk path provided we need to find the relative path
            // it's some combination of MasterFolder and IndexFolder but we dont know which

            from indexFolder in IndexFolders

            where backupFileFullPath.StartsWith(indexFolder, StringComparison.CurrentCultureIgnoreCase)
            select indexFolder)
            {
                return backupFileFullPath.Substring(indexFolder.Length + 1);
            }

            return null;
        }

        internal string GetIndexFolder(string backupFileFullPath)
        {
            foreach (string indexFolder in
            // from the backupdisk path provided we need to find the relative path
            from indexFolder in IndexFolders
            where backupFileFullPath.StartsWith(indexFolder, StringComparison.CurrentCultureIgnoreCase)
            select indexFolder)
            {
                return indexFolder;
            }

            return null;
        }

        /// <summary>
        /// Gets a BackupFile representing the file at fullPath
        /// </summary>
        /// <param name="fullPath">The fullPath to the file.</param>
        /// <param name="masterFolder"></param>
        /// <param name="indexFolder"></param>
        /// <returns></returns>
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

            // if this path is already added then return it
            if (paths.Contains(fullPath))
            {
                // check the timestamp against what we have
                backupFile = (BackupFile)paths[fullPath];

                var t = backupFile.LastWriteTime;
                var u = Utils.GetFileLastWriteTime(fullPath);

                // if the file on disk is different then check the hash 
                if (t != u)
                {
                    // update the timestamp as its changed/missing
                    backupFile.LastWriteTime = u;

                    hashOfContents = Utils.GetShortMd5HashFromFile(fullPath);

                    // has the contents hash changed too?
                    if (hashOfContents != backupFile.ContentsHash)
                    {
                        if (contentsHashIndexFolderAndRelativePath.Contains(backupFile.Hash))
                        {
                            contentsHashIndexFolderAndRelativePath.Remove(backupFile.Hash);
                        }

                        backupFile.UpdateContentsHash();
                        contentsHashIndexFolderAndRelativePath.Add(backupFile.Hash, backupFile);

                        // clear the backup details as the master file hash has changed
                        backupFile.ClearDiskChecked();
                    }
                }

                // Check the file length
                if (backupFile.Length == 0)
                {
                    backupFile.UpdateFileLength();
                }

                Utils.Trace("GetBackupFile exit1");
                return backupFile;
            }

            hashOfContents = Utils.GetShortMd5HashFromFile(fullPath);

            string relativePath = BackupFile.GetRelativePath(fullPath, masterFolder, indexFolder);

            string hashKey = BackupFile.GetFileHash(hashOfContents, Path.Combine(indexFolder, relativePath));

            if (contentsHashIndexFolderAndRelativePath.Contains(hashKey))
            {
                backupFile = (BackupFile)contentsHashIndexFolderAndRelativePath[hashKey];

                if (paths.Contains(backupFile.FullPath))
                {
                    paths.Remove(backupFile.FullPath);
                }

                backupFile.SetFullPath(fullPath, masterFolder, indexFolder);
                paths.Add(fullPath, backupFile);

                Utils.Trace("GetBackupFile exit2");
                return backupFile;
            }

            backupFile = new BackupFile(fullPath, masterFolder, indexFolder);
            BackupFiles.Add(backupFile);

            contentsHashIndexFolderAndRelativePath.Add(backupFile.Hash, backupFile);
            paths.Add(fullPath, backupFile);

            Utils.Trace("GetBackupFile exit3");
            return backupFile;
        }

        /// <summary>
        /// Get a BackupDisk for the current backupShare
        /// </summary>
        /// <param name="backupShare"></param>
        /// <returns></returns>
        public BackupDisk GetBackupDisk(string backupShare)
        {
            Utils.Trace("GetBackupDisk enter");

            // try and find a disk based on the diskname only
            // if more than 1 disk than return the first one
            string diskName = BackupDisk.GetBackupFolderName(backupShare);

            if (string.IsNullOrEmpty(diskName))
            {
                return null;
            }

            foreach (BackupDisk backupDisk in BackupDisks)
            {
                if (backupDisk.Name.Equals(diskName, StringComparison.CurrentCultureIgnoreCase))
                {
                    backupDisk.BackupShare = backupShare;
                    return backupDisk;
                }
            }

            BackupDisk disk = new BackupDisk(diskName, backupShare);
            BackupDisks.Add(disk);

            Utils.Trace("GetBackupDisk exit");
            return disk;
        }

        /// <summary>
        /// Gets a BackupFile from the hash and path provided.
        /// </summary>
        /// <param name="hash"></param>
        /// <param name="path"></param>
        /// <returns>Null if it doen't exist.</returns>
        public BackupFile GetBackupFile(string hash, string path)
        {
            string hashKey = BackupFile.GetFileHash(hash, path);
            return contentsHashIndexFolderAndRelativePath.Contains(hashKey) ? (BackupFile)contentsHashIndexFolderAndRelativePath[hashKey] : null;
        }

        /// <summary>
        /// Gets a BackupFile from the fullPath provided.
        /// </summary>
        /// <param name="fullPath"></param>
        /// <returns>Null if it doen't exist.</returns>
        public BackupFile GetBackupFile(string fullPath)
        {
            return paths.Contains(fullPath) ? (BackupFile)paths[fullPath] : null;
        }

        public IEnumerable<BackupFile> GetBackupFilesWithDiskCheckedEmpty()
        {
            return BackupFiles.Where(p => string.IsNullOrEmpty(p.DiskChecked));
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
        /// Get BackupFiles where Disk is not null or Empty
        /// </summary>
        /// <returns></returns>
        public IEnumerable<BackupFile> GetBackupFilesWithDiskEmpty()
        {
            return BackupFiles.Where(p => string.IsNullOrEmpty(p.Disk));
        }

        /// <summary>
        /// Returns True if this hash and relativePath exist already
        /// </summary>
        /// <param name="hash"></param>
        /// <param name="relativePath"></param>
        /// <returns></returns>
        public bool Contains(string hash, string relativePath)
        {
            return contentsHashIndexFolderAndRelativePath.Contains(BackupFile.GetFileHash(hash, relativePath));
        }

        /// <summary>
        /// Returns True if this hash and fullPath (from a MasterFolder) exist already
        /// </summary>
        /// <param name="fullPath"></param>
        /// <returns></returns>
        public bool Contains(string fullPath)
        {
            return paths.Contains(fullPath);
        }

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
            var filesToRemove = new Collection<BackupFile>();

            foreach (BackupFile backupFile in BackupFiles.Where(backupFile => backupFile.Flag == flag))
            {
                filesToRemove.Add(backupFile);
            }

            foreach (BackupFile backupFile in filesToRemove)
            {
                if (clearHashes)
                {
                    if (contentsHashIndexFolderAndRelativePath.Contains(backupFile.Hash))
                    {
                        contentsHashIndexFolderAndRelativePath.Remove(backupFile.Hash);
                    }

                    if (paths.Contains(backupFile.FullPath))
                    {
                        paths.Remove(backupFile.FullPath);
                    }
                }

                if (BackupFiles.Contains(backupFile))
                {
                    BackupFiles.Remove(backupFile);
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

            foreach (var backupFile in BackupFiles)
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
        internal void LogParameters(string logFile)
        {
            Utils.LogWithPushover(PushoverUserKey, PushoverAppToken, logFile, BackupAction.General,
             $"Application started");

            string text = string.Empty;
            foreach (string masterFolder in MasterFolders)
            {
                text += $"{masterFolder}\n";
            }
            Utils.LogWithPushover(PushoverUserKey, PushoverAppToken, logFile, BackupAction.General, $"MasterFolders:\n{text}");

            text = string.Empty;
            foreach (string indexFolder in IndexFolders)
            {
                text += $"{indexFolder}\n";
            }
            Utils.LogWithPushover(PushoverUserKey, PushoverAppToken, logFile, BackupAction.General, $"IndexFolders:\n{text}");

            text = string.Empty;
            foreach (string filter in Filters)
            {
                text += $"{filter}\n";
            }
            Utils.LogWithPushover(PushoverUserKey, PushoverAppToken, logFile, BackupAction.General, $"Filters:\n{text}");

            text = string.Empty;
            foreach (string edition in EditionsAllowed)
            {
                text += $"{edition}\n";
            }
            Utils.LogWithPushover(PushoverUserKey, PushoverAppToken, logFile, BackupAction.General, $"Editions:\n{text}");

            text = string.Empty;
            foreach (string videoFormatsAllowed in VideoFoldersFormatsAllowed)
            {
                text += $"{videoFormatsAllowed}\n";
            }
            Utils.LogWithPushover(PushoverUserKey, PushoverAppToken, logFile, BackupAction.General, $"VideoFormatsAllowed:\n{text}");

            text = string.Empty;
            foreach (string disksToSkip in DisksToSkipOnRestore)
            {
                text += $"{disksToSkip}\n";
            }
            Utils.LogWithPushover(PushoverUserKey, PushoverAppToken, logFile, BackupAction.General, $"DisksToSkipOnRestore:\n{text}");

            Utils.LogWithPushover(PushoverUserKey, PushoverAppToken, logFile, BackupAction.General,
               $"StartScheduledBackup : {StartScheduledBackup}");

            Utils.LogWithPushover(PushoverUserKey, PushoverAppToken, logFile, BackupAction.General,
                $"ScheduledBackupStartTime : {ScheduledBackupStartTime}");

            Utils.LogWithPushover(PushoverUserKey, PushoverAppToken, logFile, BackupAction.General,
                $"DifferenceInFileCountAllowedPercentage : {DifferenceInFileCountAllowedPercentage}");

            Utils.LogWithPushover(PushoverUserKey, PushoverAppToken, logFile, BackupAction.General,
                $"PushoverAppToken : {PushoverAppToken}");

            Utils.LogWithPushover(PushoverUserKey, PushoverAppToken, logFile, BackupAction.General,
                $"PushoverUserKey : {PushoverUserKey}");

            Utils.LogWithPushover(PushoverUserKey, PushoverAppToken, logFile, BackupAction.General,
                $"MinimumCriticalMasterFolderSpace : {MinimumCriticalMasterFolderSpace}");

            Utils.LogWithPushover(PushoverUserKey, PushoverAppToken, logFile, BackupAction.General,
                $"MinimumCriticalBackupDiskSpace : {MinimumCriticalBackupDiskSpace}");

            Utils.LogWithPushover(PushoverUserKey, PushoverAppToken, logFile, BackupAction.General,
                $"MinimumFreeSpaceToLeaveOnBackupDisk : {MinimumFreeSpaceToLeaveOnBackupDisk}");

            Utils.LogWithPushover(PushoverUserKey, PushoverAppToken, logFile, BackupAction.General,
                $"MinimumMasterFolderReadSpeed : {MinimumMasterFolderReadSpeed}");

            Utils.LogWithPushover(PushoverUserKey, PushoverAppToken, logFile, BackupAction.General,
                $"MinimumMasterFolderWriteSpeed : {MinimumMasterFolderWriteSpeed}");

            Utils.LogWithPushover(PushoverUserKey, PushoverAppToken, logFile, BackupAction.General,
                $"DaysToReportOldBackupDisks : {DaysToReportOldBackupDisks}");

            Utils.LogWithPushover(PushoverUserKey, PushoverAppToken, logFile, BackupAction.General,
               $"MonitorInterval : {MonitorInterval}");

        }
    }
}
