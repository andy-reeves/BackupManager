// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="BackupDisk.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

using BackupManager.Extensions;

namespace BackupManager.Entities;

public class BackupDisk : IEquatable<BackupDisk>
{
    public BackupDisk() { }

    public BackupDisk(string diskName, string backupShare)
    {
        Name = diskName;
        BackupShare = backupShare;
        _ = CheckForValidBackupShare(BackupShare);
    }

    /// <summary>
    ///     The name of the backup disk and the main folder on the disk. Typically like 'backup 23'
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    ///     Date the disk was last scanned and checked
    /// </summary>
    public string Checked { get; set; }

    /// <summary>
    ///     Capacity of the disk in bytes
    /// </summary>
    public long Capacity { get; set; }

    /// <summary>
    ///     Total number of files on the disk
    /// </summary>
    public long TotalFiles { get; set; }

    /// <summary>
    ///     Available space on the disk in bytes
    /// </summary>
    public long Free { get; set; }

    /// <summary>
    ///     The current backup share. Typically like '//media/backup'
    /// </summary>
    [XmlIgnore]
    public string BackupShare { get; set; }

    /// <summary>
    ///     The full path to the main backup folder. Typically like '//media/backup/backup23'
    /// </summary>
    [XmlIgnore]
    public string BackupPath => Path.Combine(BackupShare, Name);

    /// <summary>
    ///     The capacity of the disk formatted for display like '12.6TB'
    /// </summary>
    [XmlIgnore]
    public string CapacityFormatted => Utils.FormatSize(Capacity);

    /// <summary>
    ///     The free space of the disk formatted for display like '1.2GB'
    /// </summary>
    [XmlIgnore]
    public string FreeFormatted => Utils.FormatSize(Free);

    /// <summary>
    ///     The last read speed of this disk as a formatted string
    /// </summary>
    public string LastReadSpeed { get; set; }

    /// <summary>
    ///     The last write speed of this disk as a formatted string
    /// </summary>
    public string LastWriteSpeed { get; set; }

    /// <summary>
    ///     Gets the number only of this disk. Typically used for sorting disk lists.
    /// </summary>
    [XmlIgnore]
    public int Number
    {
        get
        {
            var diskNumberString = Name.SubstringAfter(' ');
            return string.IsNullOrEmpty(diskNumberString) ? 0 : int.Parse(diskNumberString);
        }
    }

    public bool Equals(BackupDisk other)
    {
        return null != other && Name == other.Name;
    }

    /// <summary>
    ///     Updates the file count on this disk and the total and free space. It uses backupFiles to get the count of the files
    ///     on this disk
    /// </summary>
    /// <param name="backupFiles"></param>
    /// <returns></returns>
    public bool Update(Collection<BackupFile> backupFiles)
    {
        if (!CheckForValidBackupShare(BackupShare)) return false;

        // Now scan disk for info;
        var result = Utils.GetDiskInfo(BackupShare, out var availableSpace, out var totalBytes);
        if (!result) return false;

        Free = availableSpace;
        Capacity = totalBytes;
        TotalFiles = backupFiles.Count(p => p.Disk == Name);
        return true;
    }

    /// <summary>
    ///     Gets the backup directory name from the path provided
    /// </summary>
    /// <param name="path">The path to the backup disk</param>
    /// <returns>The backup directory name or null if it couldn't be determined.</returns>
    public static string GetBackupDirectoryName(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;

        DirectoryInfo sharePathDirectoryInfo = new(path);
        if (!sharePathDirectoryInfo.Exists) return null;

        var directoriesInRootDirectory = sharePathDirectoryInfo.GetDirectories()
            .Where(file => ((file.Attributes & FileAttributes.Hidden) == 0) & ((file.Attributes & FileAttributes.System) == 0));

        // In here there should be 1 directory starting with 'backup '
        var inRootFolder = directoriesInRootDirectory as DirectoryInfo[] ?? directoriesInRootDirectory.ToArray();
        if (inRootFolder.Length != 1) return null;

        var firstDirectory = inRootFolder.Single();
        return !firstDirectory.Name.StartsWith("backup ", StringComparison.CurrentCultureIgnoreCase) ? null : firstDirectory.Name;
    }

    /// <summary>
    ///     Returns True if the path contains a valid backup folder like 'backup 23'.
    /// </summary>
    /// <param name="sharePath">The path to the backup share folder</param>
    /// <returns>False is the path doesn't contain a valid folder.</returns>
    public static bool CheckForValidBackupShare(string sharePath)
    {
        return !string.IsNullOrEmpty(GetBackupDirectoryName(sharePath));
    }

    /// <summary>
    ///     Updates the DiskChecked with the current date as 'yyyy-MM-dd'.
    /// </summary>
    public void UpdateDiskChecked()
    {
        Checked = DateTime.Now.ToString("yyyy-MM-dd");
    }

    /// <summary>
    ///     Update the disk speeds if > 0
    /// </summary>
    /// <param name="readSpeed"></param>
    /// <param name="writeSpeed"></param>
    internal void UpdateSpeeds(long readSpeed, long writeSpeed)
    {
        if (readSpeed > 0) LastReadSpeed = Utils.FormatSpeed(readSpeed);
        if (writeSpeed > 0) LastWriteSpeed = Utils.FormatSpeed(writeSpeed);
    }

    public override bool Equals(object obj)
    {
        return Equals(obj as BackupDisk);
    }

    public override int GetHashCode()
    {
        return Number.GetHashCode();
    }

    public override string ToString()
    {
        return Name;
    }
}