// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="BackupFile.cs" company="Andy Reeves">
//
//  </copyright>
//  --------------------------------------------------------------------------------------------------------------------

namespace BackupManager.Entities
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Xml.Serialization;

    [DebuggerDisplay("RelativePath = {RelativePath}")]
    public class BackupFile
    {
        private string contentsHash;

        private string fileName;

        private string disk;

        private string diskChecked;

        /// <summary>
        /// The relative path of the file. Doesn't include MasterFolder or IndexFolder.
        /// </summary>
        [XmlElement("Path")]
        public string RelativePath;

        /// <summary>
        /// The MasterFolder this file is located at. Like \\nas1\assets1
        /// </summary>
        public string MasterFolder;

        /// <summary>
        /// The IndexFolder this file is located at. Like _Movies or _TV
        /// </summary>
        public string IndexFolder;

        /// <summary>
        /// This gets set to true for files no longer found in a Master Folder.
        /// </summary>
        public bool Deleted;

        /// <summary>
        /// The MD5 hash of the file contents.
        /// </summary>
        [XmlElement("Hash")]
        public string ContentsHash
        {
            get
            {
                // Empty files are allowed so empty contentsHash is also fine
                if (contentsHash == null)
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
                // always calculate the FullPath in case the MasterFolder, IndexFolder or RelativePath properties have been changed.
                return Path.Combine(MasterFolder, IndexFolder, RelativePath);
            }
        }

        /// <summary>
        /// The full path to the backup file on the backup disk.
        /// </summary>
        /// <param name="backupPath">The path to the current backup disk.</param>
        public string BackupDiskFullPath(string backupPath)
        {
            // always calculate path in case the IndexFolder or RelativePath properties have been changed.
            return Path.Combine(backupPath, IndexFolder, RelativePath);
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
                return string.IsNullOrEmpty(diskNumberString) ? 0 : int.Parse(diskNumberString);
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
                return Path.Combine(IndexFolder, RelativePath);
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
        /// <param name="backupDisk">The disk this file was checked on.</param>
        public void UpdateDiskChecked(string backupDisk)
        {
            if (backupDisk != Disk && Disk.HasValue())
            {
                Utils.Log($"{FullPath} was on {Disk} but now on {backupDisk}");
            }
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

            return !fullPath.StartsWith(combinedPath)
                ? throw new ArgumentException()
                : fullPath.SubstringAfter(combinedPath, StringComparison.CurrentCultureIgnoreCase).TrimStart(new[] { '\\' });
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
        /// Updates the hash of the file contents to newContentsHash.
        /// </summary>
        /// <exception cref="ApplicationException"></exception>
        private void UpdateContentsHash(string newContentsHash)
        {
            if (newContentsHash == Utils.ZeroByteHash)
            {
                throw new ApplicationException($"Zerobyte Hashcode");
            }

            ContentsHash = newContentsHash;
        }

        /// <summary>
        /// Calculates the hash of the file contents from the Source location.
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
        /// Updates the file length of the file from the source disk. Zero byte files are allowed.
        /// </summary>
        public void UpdateFileLength()
        {
            Length = Utils.GetFileLength(FullPath);
        }

        /// <summary>
        /// Checks the files hash at the source location and at the backup location match. Updates DiskChecked and ContentsHash accordingly.
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

            string pathToBackupDiskFile = Path.Combine(disk.BackupPath, IndexFolder, RelativePath);

            if (!File.Exists(pathToBackupDiskFile))
            {
                return false;
            }

            string hashFromSourceFile = Utils.GetShortMd5HashFromFile(FullPath);

            if (hashFromSourceFile == Utils.ZeroByteHash)
            {
                throw new ApplicationException($"ERROR: {FullPath} has zerobyte hashcode");
            }

            ContentsHash = hashFromSourceFile;

            string hashFrombackupDiskFile = Utils.GetShortMd5HashFromFile(pathToBackupDiskFile);

            if (hashFrombackupDiskFile == Utils.ZeroByteHash)
            {
                throw new ApplicationException($"ERROR: {hashFrombackupDiskFile} has zerobyte hashcode");
            }

            if (hashFrombackupDiskFile != hashFromSourceFile)
            {
                // Hashes are now different on source and backup
                return false;
            }

            // Hashes match so update it as checked and the backup checked date too
            UpdateDiskChecked(disk.Name);

            return true;
        }
    }
}
