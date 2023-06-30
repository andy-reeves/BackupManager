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
        public string Name;

        /// <summary>
        /// Date the disk was last scanned
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

        [XmlIgnore]
        public string BackupShare;

        [XmlIgnore]
        public string BackupPath { get => Path.Combine(BackupShare, Name); }

        [XmlIgnore]
        public string CapacityFormatted { get => Utils.FormatSize(Capacity); }

        [XmlIgnore]
        public string FreeFormatted { get => Utils.FormatSize(Free); }

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
        /// Gets the number only of this disk
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
        /// Updates the file count on this disk and the total and freespace.
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
            var firstDirectory = directoriesInRootFolder.Single();

            if (!firstDirectory.Name.StartsWith("backup ", StringComparison.CurrentCultureIgnoreCase))
            {
                return null;
            }

            return firstDirectory.Name;
        }

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
