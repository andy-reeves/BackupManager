namespace BackupManager.Entities
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Xml.Serialization;

    [DebuggerDisplay("RelativePath = {RelativePath}")]
    public class BackupFile
    {
        private string fullPath;

        private string hash;

        private string contentsHash;

        private string fileName;

        private string disk;

        private string diskChecked;

        /// <summary>
        /// The relative path of the file.
        /// </summary>
        [XmlElement("Path")]
        public string RelativePath;

        public string MasterFolder;

        public string IndexFolder;

        /// <summary>
        /// The MD5 hash of the file contents.
        /// </summary>
        [XmlElement("Hash")]
        public string ContentsHash
        {
            get
            {
                if (string.IsNullOrEmpty(contentsHash))
                {
                    UpdateContentsHash();
                }

                return contentsHash;
            }
            set
            {
                if (value != contentsHash)
                {
                    contentsHash = value;
                    // force the combined hash to be re-calculated
                    hash = null;
                }
            }
        }

        /// <summary>
        /// The last modified date/time of the file.
        /// </summary>
        public DateTime LastWriteTime;

        /// <summary>
        /// The size of the file in bytes.
        /// </summary>
        public long Length;

        [XmlIgnore]
        public bool Flag;

        /// <summary>
        /// The full path to the backup file on the source disk.
        /// </summary>
        [XmlIgnore()]
        public string FullPath
        {
            get
            {
                if (fullPath == null)
                {
                    fullPath = Path.Combine(MasterFolder, IndexFolder, RelativePath);
                }

                return fullPath;
            }
        }

        /// <summary>
        /// Gets the number only of this disk this file is on. 0 if not backed up
        /// </summary>
        [XmlIgnore()]
        public int BackupDiskNumber
        {
            get
            {
                if (string.IsNullOrEmpty(Disk)) { return 0; }

                string diskNumberString = Disk.SubstringAfter(' ');
                if (string.IsNullOrEmpty(diskNumberString)) { return 0; }

                return int.Parse(diskNumberString);
            }
        }

        /// <summary>
        /// This is a combination key of index folder and relative path.
        /// </summary>
        [XmlIgnore()]
        public string Hash
        {
            get
            {
                if (hash == null)
                {
                    hash = Path.Combine(IndexFolder, RelativePath);
                }

                return hash;
            }
        }

        /// <summary>
        /// A date/time this file was last checked. If this is cleared then the Disk is automatically set to null also. Returns string.Empty if no value
        /// </summary>
        public string DiskChecked
        {
            get
            {
                return string.IsNullOrEmpty(diskChecked) ? string.Empty : diskChecked;
            }

            set
            {
                // If you clear the DiskChecked then we automatically clear the Disk property too
                if (string.IsNullOrEmpty(value))
                {
                    disk = null;
                }

                diskChecked = value;
            }
        }

        /// <summary>
        /// The backup disk this file is on or string.Empty if its not on a backup yet. If this is cleared then the DiskChecked is also cleared.
        /// </summary>
        public string Disk
        {
            get
            {
                return string.IsNullOrEmpty(disk) ? string.Empty : disk;
            }

            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    diskChecked = null;
                }

                disk = value;
            }
        }

        public BackupFile()
        {
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="fullPath"></param>
        /// <param name="masterFolder"></param>
        /// <param name="indexFolder"></param>
        public BackupFile(string fullPath, string masterFolder, string indexFolder)
        {
            SetFullPath(fullPath, masterFolder, indexFolder);
        }

        /// <summary>
        /// Updates the DiskChecked with the current date as 'yyyy-MM-dd' and the backup disk provided.
        /// </summary>
        /// <param name="backupDisk">The disk this file ewas checked on.</param>
        public void UpdateDiskChecked(string backupDisk)
        {
            disk = backupDisk;
            diskChecked = DateTime.Now.ToString("yyyy-MM-dd");
        }

        /// <summary>
        /// Resets Disk and DiskChecked to null.
        /// </summary>
        public void ClearDiskChecked()
        {
            disk = null;
            diskChecked = null;
        }

        public void SetFullPath(string fullPath, string masterFolder, string indexFolder)
        {
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException();
            }

            this.fullPath = fullPath;

            RelativePath = GetRelativePath(fullPath, masterFolder, indexFolder);

            MasterFolder = masterFolder;
            IndexFolder = indexFolder;

            UpdateContentsHash();
            UpdateLastWriteTime();
            UpdateFileLength();
        }
        /// <summary>
        /// Returns the remaining path from fullPath after masterFolder and indexFolder
        /// </summary>
        /// <param name="fullPath"></param>
        /// <param name="masterFolder"></param>
        /// <param name="indexFolder"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        internal static string GetRelativePath(string fullPath, string masterFolder, string indexFolder)
        {
            string combinedPath = Path.Combine(masterFolder, indexFolder);

            if (!fullPath.StartsWith(combinedPath))
            {
                throw new ArgumentException();
            }

            return fullPath.SubstringAfter(combinedPath, StringComparison.CurrentCultureIgnoreCase).TrimStart(new[] { '\\' });
        }

        /// <summary>
        /// Returns the filename and extension of the BackupFile.
        /// </summary>
        /// <returns>The filename and extension of the file</returns>
        public string FileName
        {
            get
            {
                if (string.IsNullOrEmpty(fileName))
                {
                    fileName = Path.GetFileName(FullPath);
                }

                return fileName;
            }
        }

        /// <summary>
        /// Calculates the hash of the file contents from the Source location which also resets the hash of the filename-contents too if changed.
        /// </summary>
        /// <exception cref="ApplicationException"></exception>
        public void UpdateContentsHash(string newContentsHash)
        {
            if (newContentsHash == Utils.ZeroByteHash)
            {
                throw new ApplicationException($"Zerobyte Hashcode");
            }

            ContentsHash = newContentsHash;
        }

        /// <summary>
        /// Calculates the hash of the file contents from the Source location which also resets the hash of the filename-contents too if changed.
        /// </summary>
        /// <exception cref="ApplicationException"></exception>
        public void UpdateContentsHash()
        {
            UpdateContentsHash(Utils.GetShortMd5HashFromFile(FullPath));
        }

        /// <summary>
        /// Updates the LastWriteTime of the file from the file on the source disk. If LastWriteTime isn't set it uses LastAccessTime instead
        /// </summary>
        public void UpdateLastWriteTime()
        {
            LastWriteTime = Utils.GetFileLastWriteTime(FullPath);
        }

        /// <summary>
        /// Updates the file length of the file from the source disk. If its 0 then an Exception is thrown.
        /// </summary>
        /// <exception cref="ApplicationException"></exception>
        public void UpdateFileLength()
        {
            Length = Utils.GetFileLength(FullPath);

            if (Length == 0)
            {
                throw new ApplicationException($"File '{FullPath}' has 0 length");
            }
        }

        /// <summary>
        /// Checks the files hash at the source location and at the backup location match. Updates DiskChecked accordingly.
        /// </summary>
        /// <param name="disk">The BackupDisk the BackupFile is on</param>
        /// <exception cref="ApplicationException"></exception>
        /// <returns>False is the hashes are different</returns>
        public bool CheckContentHashes(BackupDisk disk)
        {
            if (!File.Exists(FullPath))
            {
                return false;   
            }

            if (!File.Exists(Path.Combine(disk.BackupPath, IndexFolder, RelativePath)))
            {
                return false;
            }

            string hashFromSourceFile = Utils.GetShortMd5HashFromFile(FullPath);

            if (hashFromSourceFile == Utils.ZeroByteHash)
            {
                throw new ApplicationException($"ERROR: {FullPath} has zerobyte hashcode");
            }

            string hashFrombackupDiskFile = Utils.GetShortMd5HashFromFile(Path.Combine(disk.BackupPath, IndexFolder, RelativePath));

            if (hashFrombackupDiskFile == Utils.ZeroByteHash)
            {
                throw new ApplicationException($"ERROR: {hashFrombackupDiskFile} has zerobyte hashcode");
            }

            if (hashFrombackupDiskFile != hashFromSourceFile)
            {
                // Hashes are now different on source and backup
                return false;
            }

            // Hashes match so update it and the backup checked date too
            UpdateDiskChecked(disk.Name);
            return true;
        }
    }
}
