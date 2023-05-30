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

		public string DiskChecked;

		public long TotalSize;

		public long TotalFiles;

		public long FreeSpace;

		[XmlIgnore]
		public string BackupShare;

		public BackupDisk()
		{
		}

		public BackupDisk(string diskName, string backupShare)
		{
			this.Name = diskName;
			this.BackupShare = backupShare;

			BackupDisk.CheckForValidBackupShare(this.BackupShare);
		}
		public bool Update(Collection<BackupFile> backupFiles)
		{
			if (!BackupDisk.CheckForValidBackupShare(this.BackupShare))
			{
				return false;
			}

			// Now scan disk for info;
			long availableSpace;
			long totalBytes;
			var result = Utils.GetDiskInfo(this.BackupShare, out availableSpace, out totalBytes);

			if (!result)
            {
				return false;
            }

			this.FreeSpace = availableSpace;
			this.TotalSize = totalBytes;

			IEnumerable<BackupFile> files =
			   backupFiles.Where(p => p.BackupDisk == this.Name);

			this.TotalFiles = files.Count();

			return true;
		}

		public static string GetBackupFolderName(string sharePath)
		{
			var a = new DirectoryInfo(sharePath);

			var b = from file in a.GetDirectories()
					where
						(((file.Attributes & FileAttributes.Hidden) == 0) & ((file.Attributes & FileAttributes.System) == 0))
					select file;

			// In here there should be 1 directory starting with 'backup '

			if (b.Count() != 1)
			{
				return null;
			}
			var c = b.Single();

			if (!c.Name.StartsWith("backup ", StringComparison.CurrentCultureIgnoreCase))
			{
				return null;
			}

			return c.Name;
		}

		public static bool CheckForValidBackupShare(string sharePath)
		{
			var a = new DirectoryInfo(sharePath);

			if (a == null || !a.Exists)
			{
				return false;
			}

			var b = from file in a.GetDirectories()
					where
						(((file.Attributes & FileAttributes.Hidden) == 0) & ((file.Attributes & FileAttributes.System) == 0))
					select file;

			if (b.Count() == 0)
			{
				return false;
			}
			if (b.Count() != 1)
			{
				return false;
			}

			var c = b.Single();

			if (!c.Name.StartsWith("backup ", StringComparison.CurrentCultureIgnoreCase))
			{
				return false;
			}

			return true;
		}
	}
}
