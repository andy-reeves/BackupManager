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

        [XmlArrayItem("BackupDisk")]
        public Collection<BackupDisk> BackupDisks;

        public bool StartScheduledBackup;

        public int ScheduledBackupRepeatInterval;

        public int DifferenceInFileCountAllowedPercentage;

        public string PushoverAppToken;

        public string PushoverUserKey;

        // Minimum space before we throw a critical Disk space message in GB for MasterFolders
        public int MinimumCriticalMasterFolderSpace;

        // Minimum space before we throw a critical Disk space message in GB for backup disks
        public int MinimumCriticalBackupDiskSpace;

        // Minimum space on a backup disk in MB for backup disks
        public int MinimumFreeSpaceToLeaveOnBackupDrive;

        //Days To Report Old Backup Disks
        public int DaysToReportOldBackupDisks;

        // We need to store 2 hashes
        // 1 hash is from the file content hashcodes and the leafname of the file. This allows for files to have duplicate contents but be stored
        // somewhere else as a different name. Thie happened with The Porridge movie which is also stored as a Tv episode.
        // The other hash is of the file full path names as these are unique too and lets us look them up faster without reading the disk
        private readonly Hashtable hashesAndFileNames = new Hashtable(StringComparer.CurrentCultureIgnoreCase);

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

                if (!mediaBackup.hashesAndFileNames.Contains(backupFile.Hash) && !mediaBackup.paths.Contains(backupFile.FullPath))
                {
                    mediaBackup.hashesAndFileNames.Add(backupFile.Hash, backupFile);
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

        public static string GetRelativePathFromBackupPath(string fullPath, string backupFolder,
                                                           string insertedBackupDrive)
        {
            var driveAndPath = Path.Combine(insertedBackupDrive, backupFolder);

            if (!fullPath.StartsWith(driveAndPath))
            {
                throw new ArgumentException();
            }

            return
                fullPath.SubstringAfter(driveAndPath, StringComparison.CurrentCultureIgnoreCase).TrimStart(new[] { '\\' });
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="fullPath">Full fullPath.</param>
        /// <param name="masterFolder"></param>
        /// <param name="indexFolder"></param>
        /// <returns></returns>
        public BackupFile GetBackupFile(string fullPath, string masterFolder, string indexFolder)
        {
            // we hash the path of the file so we can look it up quickly
            // then we check the ModifiedTime and size
            // if these have changed we redo the hash
            // files with same hash are allowed (Porridge TV ep and movie)
            // files can't have same hash and same filename though

            string hash;

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

                    hash = Utils.GetShortMd5HashFromFile(fullPath);

                    // has the hash changed too?
                    if (hash != backupFile.ContentsHash)
                    {
                        if (hashesAndFileNames.Contains(backupFile.Hash))
                        {
                            hashesAndFileNames.Remove(backupFile.Hash);
                        }

                        backupFile.ContentsHash = hash;
                        hashesAndFileNames.Add(backupFile.Hash, backupFile);

                        // clear the backup details as the master file hash has changed
                        backupFile.Disk = null;
                        backupFile.DiskChecked = null;
                    }
                }

                // Check the file length
                if (backupFile.Length == 0)
                {
                    backupFile.UpdateFileLength();
                }

                return backupFile;
            }

            hash = Utils.GetShortMd5HashFromFile(fullPath);

            string hashKey = BackupFile.GetFileHash(hash, fullPath);

            if (hashesAndFileNames.Contains(hashKey))
            {
                var b = (BackupFile)hashesAndFileNames[hashKey];

                if (paths.Contains(b.FullPath))
                {
                    paths.Remove(b.FullPath);
                }

                b.SetFullPath(fullPath, masterFolder, indexFolder);
                paths.Add(fullPath, b);

                return b;
            }

            backupFile = new BackupFile(fullPath, masterFolder, indexFolder);
            BackupFiles.Add(backupFile);

            hashesAndFileNames.Add(backupFile.Hash, backupFile);
            paths.Add(fullPath, backupFile);

            return backupFile;
        }

        public BackupFile GetBackupFile(string fileName)
        {
            // try and find a file based on the filename only
            // if more than 1 file than return the first one

            foreach (BackupFile backupFile in BackupFiles)
            {
                if (backupFile.GetFileName().StartsWith(fileName))
                {
                    return backupFile;
                }
            }

            return null;
        }

        public BackupDisk GetBackupDisk(string backupShare)
        {
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
            return disk;
        }

        public BackupFile GetBackupFile(string hash, string path)
        {
            string hashKey = BackupFile.GetFileHash(hash, path);

            if (hashesAndFileNames.Contains(hashKey))
            {
                return (BackupFile)hashesAndFileNames[hashKey];
            }

            return null;
        }

        public IEnumerable<BackupFile> GetBackupFilesWithDiskCheckedEmpty()
        {
            return BackupFiles.Where(p => string.IsNullOrEmpty(p.DiskChecked));
        }

        public IEnumerable<BackupFile> GetBackupFilesWithDiskEmpty()
        {
            return BackupFiles.Where(p => string.IsNullOrEmpty(p.Disk));
        }

        public bool Contains(string hash, string path)
        {
            string hashKey = BackupFile.GetFileHash(hash, path);
            return hashesAndFileNames.Contains(hashKey);
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
                    if (hashesAndFileNames.Contains(backupFile.Hash))
                    {
                        hashesAndFileNames.Remove(backupFile.Hash);
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
    }
}
