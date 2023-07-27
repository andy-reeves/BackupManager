namespace BackupManager.Entities
{
    using System;
    using System.Xml.Serialization;
    using System.IO;
    using System.Linq;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;

    public class BackupDisk
    {
        /// <summary>
        /// The name of the backup disk and the main folder on the disk. Typically like 'backup 23'
        /// </summary>
        public string Name;

        /// <summary>
        /// Date the disk was last scanned and checked
        /// </summary>
		public string Checked;

        /// <summary>
        /// Capacity of the disk in bytes
        /// </summary>
		public long Capacity;

        /// <summary>
        /// Total number of files on the disk
        /// </summary>
		public long TotalFiles;

        /// <summary>
        /// Available space on the disk in bytes
        /// </summary>
		public long Free;

        /// <summary>
        /// The current backup share. Typically like '\\media\backup'
        /// </summary>
        [XmlIgnore]
        public string BackupShare;

        /// <summary>
        /// The full path to the main backup folder. Typically like '\\media\backup\backup23'
        /// </summary>
        [XmlIgnore]
        public string BackupPath { get => Path.Combine(BackupShare, Name); }

        /// <summary>
        /// The capacilty of the disk formatted for display like '12.6TB'
        /// </summary>
        [XmlIgnore]
        public string CapacityFormatted { get => Utils.FormatSize(Capacity); }

        /// <summary>
        /// Thge free space of the disk formatted for display like '1.2GB'
        /// </summary>
        [XmlIgnore]
        public string FreeFormatted { get => Utils.FormatSize(Free); }

        /// <summary>
        /// The last read speed of this disk as a formatted string
        /// </summary>
        public string LastReadSpeed;

        /// <summary>
        /// The last write speed of this disk as a formatted string
        /// </summary>
        public string LastWriteSpeed;

        public BackupDisk()
        {
        }

        public BackupDisk(string diskName, string backupShare)
        {
            Name = diskName;
            BackupShare = backupShare;

            CheckForValidBackupShare(BackupShare);
        }

        /// <summary>
        /// Gets the number only of this disk. Typically used for sorting disk lists.
        /// </summary>
        [XmlIgnore()]
        public int Number
        {
            get
            {
                string diskNumberString = Name.SubstringAfter(' ');
                if (string.IsNullOrEmpty(diskNumberString)) { return 0; }

                return int.Parse(diskNumberString);
            }
        }

        /// <summary>
        /// Updates the file count on this disk and the total and free space.
        /// </summary>
        /// <param name="backupFiles"></param>
        /// <returns></returns>
        public bool Update(Collection<BackupFile> backupFiles)
        {
            if (!CheckForValidBackupShare(this.BackupShare))
            {
                return false;
            }

            // Now scan disk for info;
            long availableSpace;
            long totalBytes;
            var result = Utils.GetDiskInfo(BackupShare, out availableSpace, out totalBytes);

            if (!result)
            {
                return false;
            }

            Free = availableSpace;
            Capacity = totalBytes;

            IEnumerable<BackupFile> files = backupFiles.Where(p => p.Disk == Name);

            TotalFiles = files.Count();

            return true;
        }

        /// <summary>
        /// Gets the backup folder name from the sharePath provided
        /// </summary>
        /// <param name="sharePath">The path to the backyup disk</param>
        /// <returns>The backup folder name or null if it couldn't be determined.</returns>
        public static string GetBackupFolderName(string sharePath)
        {
            if (string.IsNullOrEmpty(sharePath)) return null;

            DirectoryInfo sharePathDirectoryInfo = new DirectoryInfo(sharePath);

            if (sharePathDirectoryInfo == null || !sharePathDirectoryInfo.Exists)
            {
                return null;
            }

            IEnumerable<DirectoryInfo> directoriesInRootFolder = from file in sharePathDirectoryInfo.GetDirectories()
                                           where
                                               ((file.Attributes & FileAttributes.Hidden) == 0) & ((file.Attributes & FileAttributes.System) == 0)
                                           select file;

            // In here there should be 1 directory starting with 'backup '
            if (directoriesInRootFolder.Count() != 1)
            {
                return null;
            }

            DirectoryInfo firstDirectory = directoriesInRootFolder.Single();

            if (!firstDirectory.Name.StartsWith("backup ", StringComparison.CurrentCultureIgnoreCase))
            {
                return null;
            }

            return firstDirectory.Name;
        }

        /// <summary>
        /// Returns True if the sharePath contains a valid backup folder like 'backup 23'.
        /// </summary>
        /// <param name="sharePath">The path to the backup share folder</param>
        /// <returns>False is the sharePath doesn't contain a valid folder.</returns>
        public static bool CheckForValidBackupShare(string sharePath)
        {
            return !string.IsNullOrEmpty(GetBackupFolderName(sharePath));
        }

        /// <summary>
        /// Updates the DiskChecked with the current date as 'yyyy-MM-dd'.
        /// </summary>
        public void UpdateDiskChecked()
        {
            Checked = DateTime.Now.ToString("yyyy-MM-dd");
        }
    }
}
