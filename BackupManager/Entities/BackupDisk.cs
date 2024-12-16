// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="BackupDisk.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

using BackupManager.Extensions;

namespace BackupManager.Entities;

[SuppressMessage("ReSharper", "MemberCanBeInternal")]
[SuppressMessage("ReSharper", "AutoPropertyCanBeMadeGetOnly.Global")]
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
[SuppressMessage("ReSharper", "UnusedMember.Global")]
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public sealed class BackupDisk : IEquatable<BackupDisk>
{
    private readonly string name;

    private long capacity;

    private DateTime? checkedTime;

    private long free;

    private string lastReadSpeed;

    private string lastWriteSpeed;

    private long totalFiles;

    private long deletedFilesCount;

    private long filesSize;

    public BackupDisk() { }

    internal BackupDisk(string diskName, string backupShare)
    {
        Name = diskName;
        BackupShare = backupShare;
        _ = CheckForValidBackupShare(BackupShare);
    }

    /// <summary>
    ///     The name of the backup disk and the main directory on the disk. Typically like 'backup 23'
    /// </summary>
    public string Name
    {
        get => name;

        init
        {
            if (name == value) return;

            name = value;
            Changed = true;
        }
    }

    /// <summary>
    ///     A date/time this disk was last checked. Returns null if no value
    /// </summary>
    public DateTime? CheckedTime
    {
        get => checkedTime;

        set
        {
            // If you clear the DiskChecked then we automatically clear the Disk property too
            if (checkedTime == value) return;

            checkedTime = value;
            Changed = true;
        }
    }

    /// <summary>
    ///     Capacity of the disk in bytes
    /// </summary>
    public long Capacity
    {
        get => capacity;

        set
        {
            if (capacity == value) return;

            capacity = value;
            Changed = true;
        }
    }

    /// <summary>
    ///     Total number of files on the disk
    /// </summary>
    public long TotalFiles
    {
        get => totalFiles;

        set
        {
            if (totalFiles == value) return;

            totalFiles = value;
            Changed = true;
        }
    }

    /// <summary>
    ///     Available space on the disk in bytes
    /// </summary>
    public long Free
    {
        get => free;

        set
        {
            if (free == value) return;

            free = value;
            Changed = true;
        }
    }

    /// <summary>
    ///     Available space on the disk in bytes including the space if the files marked deleted are removed
    /// </summary>
    [XmlIgnore]
    public long TotalFree => DeletedFilesSize + Free;

    /// <summary>
    ///     The count of the files marked as deleted on this disk
    /// </summary>
    [XmlIgnore]
    public long DeletedFilesCount
    {
        get => deletedFilesCount;

        set
        {
            if (deletedFilesCount == value) return;

            deletedFilesCount = value;
            Changed = true;
        }
    }

    /// <summary>
    ///     The size of the files marked as deleted on this disk
    /// </summary>
    [XmlIgnore]
    public long DeletedFilesSize => Capacity - Free - FilesSize;

    /// <summary>
    ///     The size of the files not marked as deleted on this disk
    /// </summary>
    [XmlIgnore]
    public long FilesSize
    {
        get => filesSize;

        set
        {
            if (filesSize == value) return;

            filesSize = value;
            Changed = true;
        }
    }

    /// <summary>
    ///     The current backup share. Typically like '//media/backup'
    /// </summary>
    [XmlIgnore]
    public string BackupShare { get; set; }

    /// <summary>
    ///     The full path to the main backup directory. Typically like '//media/backup/backup23'
    /// </summary>
    [XmlIgnore]
    public string BackupPath => Path.Combine(BackupShare, Name);

    /// <summary>
    ///     This is set to True if any data has changed, and we need to Save.
    /// </summary>
    [XmlIgnore]
    public bool Changed { get; set; }

    /// <summary>
    ///     The capacity of the disk formatted for display like '12.6TB'
    /// </summary>
    [XmlIgnore]
    public string CapacityFormatted => Capacity.SizeSuffix();

    /// <summary>
    ///     The free space of the disk formatted for display like '1.2GB'
    /// </summary>
    [XmlIgnore]
    public string FreeFormatted => Free.SizeSuffix();

    /// <summary>
    ///     The last read speed of this disk as a formatted string
    /// </summary>
    public string LastReadSpeed
    {
        get => lastReadSpeed;

        set
        {
            if (lastReadSpeed == value) return;

            lastReadSpeed = value;
            Changed = true;
        }
    }

    /// <summary>
    ///     The last write speed of this disk as a formatted string
    /// </summary>
    public string LastWriteSpeed
    {
        get => lastWriteSpeed;

        set
        {
            if (lastWriteSpeed == value) return;

            lastWriteSpeed = value;
            Changed = true;
        }
    }

    /// <summary>
    ///     Gets the number only of this disk. Typically used for sorting disk lists.
    /// </summary>
    [XmlIgnore]
    public int Number
    {
        get
        {
            var diskNumberString = Name.SubstringAfter(' ');
            return diskNumberString.HasValue() ? int.Parse(diskNumberString) : 0;
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
    public bool Update(IEnumerable<BackupFile> backupFiles)
    {
        if (!CheckForValidBackupShare(BackupShare)) return false;

        // Now scan disk for info;
        var result = Utils.GetDiskInfo(BackupShare, out var availableSpace, out var totalBytes);
        if (!result) return false;

        Free = availableSpace;
        Capacity = totalBytes;
        TotalFiles = backupFiles?.Count(p => p.Disk == Name) ?? 0;
        return true;
    }

    /// <summary>
    ///     Gets the backup directory name from the path provided
    /// </summary>
    /// <param name="path">The path to the backup disk</param>
    /// <returns>The backup directory name or null if it couldn't be determined.</returns>
    public static string GetBackupDirectoryName(string path)
    {
        if (path.HasNoValue()) return null;

        DirectoryInfo sharePathDirectoryInfo = new(path);
        if (!sharePathDirectoryInfo.Exists) return null;

        var directoriesInRootDirectory = sharePathDirectoryInfo.GetDirectories().Where(static file => ((file.Attributes & FileAttributes.Hidden) == 0) & ((file.Attributes & FileAttributes.System) == 0));

        // In here there should be 1 directory starting with 'backup '
        var inRootDirectory = directoriesInRootDirectory as DirectoryInfo[] ?? directoriesInRootDirectory.ToArray();
        if (inRootDirectory.Length != 1) return null;

        var firstDirectory = inRootDirectory.Single();
        return !firstDirectory.Name.StartsWithIgnoreCase("backup ") ? null : firstDirectory.Name;
    }

    /// <summary>
    ///     Returns True if the path contains a valid backup directory like 'backup 23'.
    /// </summary>
    /// <param name="sharePath">The path to the backup share directory</param>
    /// <returns>False is the path doesn't contain a valid directory.</returns>
    public static bool CheckForValidBackupShare(string sharePath)
    {
        return GetBackupDirectoryName(sharePath).HasValue();
    }

    /// <summary>
    ///     Updates the DiskChecked with the current date and time.
    /// </summary>
    public void UpdateDiskChecked()
    {
        CheckedTime = DateTime.Now;
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