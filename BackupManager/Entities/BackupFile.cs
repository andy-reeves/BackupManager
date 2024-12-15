// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="BackupFile.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Xml.Serialization;

using BackupManager.Extensions;
using BackupManager.Properties;

namespace BackupManager.Entities;

[SuppressMessage("ReSharper", "MemberCanBeInternal")]
[SuppressMessage("ReSharper", "AutoPropertyCanBeMadeGetOnly.Global")]
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
[SuppressMessage("ReSharper", "UnusedMember.Global")]
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
[DebuggerDisplay("RelativePath = {" + nameof(RelativePath) + "}")]
[SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "<Pending>")]
public sealed class BackupFile : IEquatable<BackupFile>
{
    private string contentsHash;

    private bool deleted;

    private string directory;

    private string disk;

    private DateTime? diskCheckedTime;

    private string extension;

    private string fileName;

    private DateTime lastWriteTime;

    private long length;

    private string relativePath;

    public BackupFile() { }

    /// <summary>
    /// </summary>
    /// <param name="fullPath">The full path to the file.</param>
    /// <param name="directory">The directory the file is in.</param>
    public BackupFile(string fullPath, string directory)
    {
        if (Utils.StringContainsFixedSpace(fullPath)) throw new ArgumentException(Resources.FixedSpace, fullPath);
        if (Utils.StringContainsFixedSpace(directory)) throw new ArgumentException(Resources.FixedSpace, directory);

        SetFullPath(fullPath, directory);
    }

    /// <summary>
    ///     This is set to True if any data has changed, and we need to Save.
    /// </summary>
    [XmlIgnore]
    public bool Changed { get; set; }

    /// <summary>
    ///     The relative path of the file. Doesn't include Directory.
    /// </summary>
    [XmlElement("Path")]
    public string RelativePath
    {
        get => relativePath;

        set
        {
            if (Utils.StringContainsFixedSpace(value)) value = Utils.ReplaceFixedSpace(value);
            if (relativePath == value) return;

            relativePath = value;
            extension = null;
            Changed = true;
        }
    }

    /// <summary>
    ///     This gets set to true for files no longer found.
    /// </summary>
    public bool Deleted
    {
        get => deleted;

        set
        {
            if (deleted == value) return;

            deleted = value;
            Changed = true;
        }
    }

    /// <summary>
    ///     The Directory this file is located at. Like '//nas1/assets1/_Movies'
    /// </summary>
    public string Directory
    {
        get => directory;

        set
        {
            if (Utils.StringContainsFixedSpace(value)) value = Utils.ReplaceFixedSpace(value);
            if (directory == value) return;

            directory = value;
            Changed = true;
        }
    }

    /// <summary>
    ///     The MD5 hash of the file contents.
    /// </summary>
    [XmlElement("Hash")]
    public string ContentsHash
    {
        get
        {
            // Empty files are allowed so empty contentsHash is also fine
            if (contentsHash == null) UpdateContentsHash();
            return contentsHash;
        }

        set
        {
            if (contentsHash == value) return;

            contentsHash = value;
            Changed = true;
        }
    }

    /// <summary>
    ///     The last modified date/time of the file.
    /// </summary>
    public DateTime LastWriteTime
    {
        get => lastWriteTime;

        set
        {
            if (lastWriteTime == value) return;

            lastWriteTime = value;
            Changed = true;
        }
    }

    /// <summary>
    ///     The size of the file in bytes.
    /// </summary>
    public long Length
    {
        get => length;

        set
        {
            if (length == value) return;

            length = value;
            Changed = true;
        }
    }

    [XmlIgnore] public bool Flag { get; set; }

    [XmlIgnore] public bool BeingCheckedNow { get; set; }

    /// <summary>
    ///     The full path to the backup file on the source disk. Returns string.Empty if Directory or RelativePath
    ///     are null
    /// </summary>
    [XmlIgnore]
    public string FullPath
    {
        get
        {
            // always calculate the FullPath in case theDirectory or RelativePath properties have been changed.
            if (Directory == null || RelativePath == null) return string.Empty;

            return Path.Combine(Directory, RelativePath);
        }
    }

    [XmlIgnore]
    public string Extension
    {
        get
        {
            if (RelativePath == null) return string.Empty;

            return extension ??= Path.GetExtension(RelativePath);
        }
    }

    /// <summary>
    ///     Gets the number only of this disk this file is on. 0 if not backed up
    /// </summary>
    [XmlIgnore]
    public int BackupDiskNumber
    {
        get
        {
            if (Disk.HasNoValue()) return 0;

            var diskNumberString = Disk.SubstringAfter(' ');
            return diskNumberString.HasValue() ? int.Parse(diskNumberString) : 0;
        }
    }

    /// <summary>
    ///     This is a combination key of index directory and relative path.
    /// </summary>
    [XmlIgnore]
    public string Hash => Path.Combine(Utils.GetIndexFolder(Directory), RelativePath);

    /// <summary>
    ///     A date/time this file was last checked. If this is cleared then the Disk is automatically set to null also. Returns
    ///     null if no value
    /// </summary>
    public DateTime? DiskCheckedTime
    {
        get => diskCheckedTime;

        set
        {
            // If you clear the DiskChecked then we automatically clear the Disk property too
            if (diskCheckedTime == value) return;

            if (!value.HasValue) disk = null;
            diskCheckedTime = value;
            Changed = true;
        }
    }

    /// <summary>
    ///     The backup disk this file is on or string.Empty if its not on a backup yet. If this is cleared then the DiskChecked
    ///     is also cleared.
    /// </summary>
    public string Disk
    {
        get => disk.HasValue() ? disk : string.Empty;

        set
        {
            if (disk == value) return;

            if (value.HasNoValue()) diskCheckedTime = null;
            disk = value;
            Changed = true;
        }
    }

    /// <summary>
    ///     Returns the filename and extension of the BackupFile.
    /// </summary>
    /// <returns>The filename and extension of the file</returns>
    public string FileName
    {
        get
        {
            if (fileName.HasNoValue()) fileName = Path.GetFileName(FullPath);
            return fileName;
        }
    }

    public bool Equals(BackupFile other)
    {
        return null != other && FullPath == other.FullPath;
    }

    /// <summary>
    ///     The full path to the backup file on the backup disk.
    /// </summary>
    /// <param name="backupPath">The path to the current backup disk.</param>
    public string BackupDiskFullPath(string backupPath)
    {
        // always calculate path in case the Directory or RelativePath properties have been changed.
        return Path.Combine(backupPath, Utils.GetIndexFolder(Directory), RelativePath);
    }

    /// <summary>
    ///     Updates the DiskChecked with the current date as 'yyyy-MM-dd' and the backup disk provided.
    /// </summary>
    /// <param name="backupDisk">The disk this file was checked on.</param>
    public void UpdateDiskChecked(string backupDisk)
    {
        if (backupDisk != Disk && Disk.HasValue()) Utils.Log($"{FullPath} was on {Disk} but now on {backupDisk}");
        disk = backupDisk;
        diskCheckedTime = DateTime.Now;
        Changed = true;
    }

    /// <summary>
    ///     Resets Disk and DiskChecked to null.
    /// </summary>
    public void ClearDiskChecked()
    {
        if (disk == null && diskCheckedTime == null) return;

        disk = null;
        diskCheckedTime = null;
        Changed = true;
    }

    public void SetFullPath(string fullPath, string newDirectory)
    {
        if (!File.Exists(fullPath)) throw new FileNotFoundException();

        RelativePath = GetRelativePath(fullPath, newDirectory);
        Directory = newDirectory;
        fileName = Path.GetFileName(fullPath);
        extension = Path.GetExtension(fullPath);
        UpdateContentsHash();
        UpdateLastWriteTime();
        UpdateFileLength();
    }

    /// <summary>
    ///     Returns the remaining path from fullPath after directory
    /// </summary>
    /// <param name="fullPath"></param>
    /// <param name="directory"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    internal static string GetRelativePath(string fullPath, string directory)
    {
        return !fullPath.StartsWithIgnoreCase(directory) ? throw new ArgumentException(Resources.FullPathNotCorrect, nameof(fullPath)) : fullPath.SubstringAfterIgnoreCase(directory).TrimStart(['\\']);
    }

    /// <summary>
    ///     Updates the hash of the file contents to newContentsHash.
    /// </summary>
    /// <exception cref="ArgumentNullException"></exception>
    private void UpdateContentsHash(string newContentsHash)
    {
        if (newContentsHash == null) throw new ArgumentNullException(nameof(newContentsHash), Resources.HashCodeNotNull);

        if (contentsHash == newContentsHash) return;

        contentsHash = newContentsHash;
        Changed = true;
    }

    /// <summary>
    ///     Calculates the hash of the file contents from the Source location.
    /// </summary>
    /// <exception cref="ApplicationException"></exception>
    public void UpdateContentsHash()
    {
        UpdateContentsHash(Utils.File.GetShortMd5Hash(FullPath));
    }

    /// <summary>
    ///     Updates the LastWriteTime of the file from the file on the source disk. If LastWriteTime isn't set it uses
    ///     LastAccessTime instead
    /// </summary>
    public void UpdateLastWriteTime()
    {
        Utils.TraceIn();
        LastWriteTime = Utils.File.GetLastWriteTime(FullPath);
        Utils.TraceOut();
    }

    /// <summary>
    ///     Updates the file length of the file from the source disk. Zero byte files are allowed.
    /// </summary>
    public void UpdateFileLength()
    {
        Utils.TraceIn();
        Length = Utils.File.GetLength(FullPath);
        Utils.TraceOut();
    }

    /// <summary>
    ///     Checks the files hash at the source location and at the backup location match. Updates Deleted to False and
    ///     DiskChecked and ContentsHash
    ///     accordingly.
    /// </summary>
    /// <param name="backupDisk">The BackupDisk the BackupFile is on</param>
    /// <exception cref="ApplicationException"></exception>
    /// <returns>
    ///     False is the hashes are different, or if either file is not found
    /// </returns>
    public bool CheckContentHashes(BackupDisk backupDisk)
    {
        if (!File.Exists(FullPath)) return false;

        var pathToBackupDiskFile = Path.Combine(backupDisk.BackupPath, Utils.GetIndexFolder(Directory), RelativePath);
        if (!File.Exists(pathToBackupDiskFile)) return false;

        UpdateContentsHash();

        // Update the LastWriteTime and Length because they may be checked next
        UpdateLastWriteTime();
        UpdateFileLength();

        // now check the hashes
        var hashFromBackupDiskFile = Utils.File.GetShortMd5Hash(pathToBackupDiskFile);

        if (hashFromBackupDiskFile != ContentsHash)
        {
            // Hashes are now different on source and backup
            return false;
        }

        // Hashes match so update it as checked and the backup checked date too
        UpdateDiskChecked(backupDisk.Name);
        Deleted = false;
        return true;
    }

    public override string ToString()
    {
        return FullPath;
    }

    public override bool Equals(object obj)
    {
        return Equals(obj as BackupFile);
    }

    public override int GetHashCode()
    {
        return FullPath.GetHashCode();
    }
}