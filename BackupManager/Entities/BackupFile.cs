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

        private string disk;

        private string diskChecked;

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
        /// This is a combination key of the hash of the file contents and the file name.
        /// </summary>
        [XmlIgnore()]
        public string Hash
        {
            get
            {
                if (hash == null)
                {
                    hash = GetFileHash(ContentsHash, RelativePath);
                }

                return hash;
            }
        }

        public string DiskChecked
        {
            get
            {
                return diskChecked;
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

        public string Disk
        {
            get
            {
                return disk;
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

        private static string GetRelativePath(string fullPath, string masterFolder, string indexFolder)
        {
            string combinedPath = Path.Combine(masterFolder, indexFolder);

            if (!fullPath.StartsWith(combinedPath))
            {
                throw new ArgumentException();
            }

            return fullPath.SubstringAfter(combinedPath, StringComparison.CurrentCultureIgnoreCase).TrimStart(new[] { '\\' });
        }
        /// <summary>
        /// Returns a hash of the contents follwed by '-' and then the leafname
        /// </summary>
        /// <param name="hash"></param>
        /// <param name="path"></param>
        /// <returns></returns>
        public static string GetFileHash(string hash, string path)
        {
            return hash + "-" + Path.GetFileName(path);
        }

        public string GetFileName()
        {
            return Path.GetFileName(fullPath);
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
            string newContentsHash = Utils.GetShortMd5HashFromFile(FullPath);
            UpdateContentsHash(newContentsHash);
        }

        public void UpdateLastWriteTime()
        {
            LastWriteTime = Utils.GetFileLastWriteTime(FullPath);
        }

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
        /// <exception cref="Exception"></exception>
        public void CheckContentHashes(BackupDisk disk)
        {
            string backupDiskFullPath = Path.Combine(disk.BackupPath, IndexFolder, RelativePath);
            string sourceDiskFullPath = FullPath;

            string hashFromSourceFile = Utils.GetShortMd5HashFromFile(sourceDiskFullPath);

            if (hashFromSourceFile == Utils.ZeroByteHash)
            {
                throw new Exception($"ERROR: { sourceDiskFullPath } has zerobyte hashcode");
            }

            string hashFrombackupDiskFile = Utils.GetShortMd5HashFromFile(backupDiskFullPath);

            if (hashFrombackupDiskFile == Utils.ZeroByteHash)
            {
                throw new Exception($"ERROR: { hashFrombackupDiskFile } has zerobyte hashcode");
            }

            if (hashFrombackupDiskFile == hashFromSourceFile)
            {
                // Hashes match so update it and the backup checked date too
                UpdateContentsHash(hashFrombackupDiskFile);
                UpdateDiskChecked(disk.Name);
            }
            else
            {
                throw new Exception($"ERROR: {hashFromSourceFile} and {hashFrombackupDiskFile} have different hashcodes");
            }
        }
    }
}
