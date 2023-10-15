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
[DebuggerDisplay("RelativePath = {RelativePath}")]
[SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "<Pending>")]
public sealed class BackupFile : IEquatable<BackupFile>
{
    private string contentsHash;

    private string disk;

    private string diskChecked;

    private string fileName;

    public BackupFile() { }

    /// <summary>
    /// </summary>
    /// <param name="fullPath"></param>
    /// <param name="masterFolder"></param>
    /// <param name="indexFolder"></param>
    public BackupFile(string fullPath, string masterFolder, string indexFolder)
    {
        SetFullPath(fullPath, masterFolder, indexFolder);
    }

    /// <summary>
    ///     The relative path of the file. Doesn't include MasterFolder or IndexFolder.
    /// </summary>
    [XmlElement("Path")]
    public string RelativePath { get; set; }

    /// <summary>
    ///     The MasterFolder this file is located at. Like '//nas1/assets1'
    /// </summary>
    public string MasterFolder { get; set; }

    /// <summary>
    ///     The IndexFolder this file is located at. Like _Movies or _TV
    /// </summary>
    public string IndexFolder { get; set; }

    /// <summary>
    ///     This gets set to true for files no longer found in a MasterFolder.
    /// </summary>
    public bool Deleted { get; set; }

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

        set => contentsHash = value;
    }

    /// <summary>
    ///     The last modified date/time of the file.
    /// </summary>
    public DateTime LastWriteTime { get; set; }

    /// <summary>
    ///     The size of the file in bytes.
    /// </summary>
    public long Length { get; set; }

    [XmlIgnore] public bool Flag { get; set; }

    /// <summary>
    ///     The full path to the backup file on the source disk. Returns String.Empty if any of Master, Index or RelativePath
    ///     are null
    /// </summary>
    [XmlIgnore]
    public string FullPath
    {
        get
        {
            // always calculate the FullPath in case the MasterFolder, IndexFolder or RelativePath properties have been changed.
            if (MasterFolder == null || IndexFolder == null || RelativePath == null) return string.Empty;

            return Path.Combine(MasterFolder, IndexFolder, RelativePath);
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
            if (string.IsNullOrEmpty(Disk)) return 0;

            var diskNumberString = Disk.SubstringAfter(' ');
            return string.IsNullOrEmpty(diskNumberString) ? 0 : int.Parse(diskNumberString);
        }
    }

    /// <summary>
    ///     This is a combination key of index folder and relative path.
    /// </summary>
    [XmlIgnore]
    public string Hash => Path.Combine(IndexFolder, RelativePath);

    /// <summary>
    ///     A date/time this file was last checked. If this is cleared then the Disk is automatically set to null also. Returns
    ///     string.Empty if no value
    /// </summary>
    public string DiskChecked
    {
        get => string.IsNullOrEmpty(diskChecked) ? string.Empty : diskChecked;

        set
        {
            // If you clear the DiskChecked then we automatically clear the Disk property too
            if (string.IsNullOrEmpty(value)) disk = null;
            diskChecked = value;
        }
    }

    /// <summary>
    ///     The backup disk this file is on or string.Empty if its not on a backup yet. If this is cleared then the DiskChecked
    ///     is also cleared.
    /// </summary>
    public string Disk
    {
        get => string.IsNullOrEmpty(disk) ? string.Empty : disk;

        set
        {
            if (string.IsNullOrEmpty(value)) diskChecked = null;
            disk = value;
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
            if (string.IsNullOrEmpty(fileName)) fileName = Path.GetFileName(FullPath);
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
        // always calculate path in case the IndexFolder or RelativePath properties have been changed.
        return Path.Combine(backupPath, IndexFolder, RelativePath);
    }

    /// <summary>
    ///     Updates the DiskChecked with the current date as 'yyyy-MM-dd' and the backup disk provided.
    /// </summary>
    /// <param name="backupDisk">The disk this file was checked on.</param>
    public void UpdateDiskChecked(string backupDisk)
    {
        if (backupDisk != Disk && Disk.HasValue()) Utils.Log($"{FullPath} was on {Disk} but now on {backupDisk}");
        disk = backupDisk;
        diskChecked = DateTime.Now.ToString(Resources.DateTime_yyyyMMdd);
    }

    /// <summary>
    ///     Resets Disk and DiskChecked to null.
    /// </summary>
    public void ClearDiskChecked()
    {
        disk = null;
        diskChecked = null;
    }

    public void SetFullPath(string fullPath, string masterFolder, string indexFolder)
    {
        if (!File.Exists(fullPath)) throw new FileNotFoundException();

        RelativePath = GetRelativePath(fullPath, masterFolder, indexFolder);
        MasterFolder = masterFolder;
        IndexFolder = indexFolder;
        UpdateContentsHash();
        UpdateLastWriteTime();
        UpdateFileLength();
    }

    /// <summary>
    ///     Returns the remaining path from fullPath after masterFolder and indexFolder
    /// </summary>
    /// <param name="fullPath"></param>
    /// <param name="masterFolder"></param>
    /// <param name="indexFolder"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    internal static string GetRelativePath(string fullPath, string masterFolder, string indexFolder)
    {
        var combinedPath = Path.Combine(masterFolder, indexFolder);

        return !fullPath.StartsWith(combinedPath, StringComparison.CurrentCultureIgnoreCase)
            ? throw new ArgumentException(Resources.BackupFile_The_fullPathNotCorrect, nameof(fullPath))
            : fullPath.SubstringAfter(combinedPath, StringComparison.CurrentCultureIgnoreCase).TrimStart(new[] { '\\' });
    }

    /// <summary>
    ///     Updates the hash of the file contents to newContentsHash.
    /// </summary>
    /// <exception cref="ApplicationException"></exception>
    private void UpdateContentsHash(string newContentsHash)
    {
        contentsHash = newContentsHash ?? throw new ArgumentNullException(nameof(newContentsHash), Resources.BackupFile_HashCodeNotNull);
    }

    /// <summary>
    ///     Calculates the hash of the file contents from the Source location.
    /// </summary>
    /// <exception cref="ApplicationException"></exception>
    public void UpdateContentsHash()
    {
        UpdateContentsHash(Utils.GetShortMd5HashFromFile(FullPath));
    }

    /// <summary>
    ///     Updates the LastWriteTime of the file from the file on the source disk. If LastWriteTime isn't set it uses
    ///     LastAccessTime instead
    /// </summary>
    public void UpdateLastWriteTime()
    {
        LastWriteTime = Utils.GetFileLastWriteTime(FullPath);
    }

    /// <summary>
    ///     Updates the file length of the file from the source disk. Zero byte files are allowed.
    /// </summary>
    public void UpdateFileLength()
    {
        Length = Utils.GetFileLength(FullPath);
    }

    /// <summary>
    ///     Checks the files hash at the source location and at the backup location match. Updates DiskChecked and ContentsHash
    ///     accordingly.
    /// </summary>
    /// <param name="backupDisk">The BackupDisk the BackupFile is on</param>
    /// <exception cref="ApplicationException"></exception>
    /// <returns>
    ///     False is the hashes are different, or if the files are not found or or the source or backup file are not
    ///     accessible
    /// </returns>
    public bool CheckContentHashes(BackupDisk backupDisk)
    {
        if (!File.Exists(FullPath) || !Utils.IsFileAccessible(FullPath)) return false;

        var pathToBackupDiskFile = Path.Combine(backupDisk.BackupPath, IndexFolder, RelativePath);
        if (!File.Exists(pathToBackupDiskFile) || !Utils.IsFileAccessible(pathToBackupDiskFile)) return false;

        var hashFromSourceFile = Utils.GetShortMd5HashFromFile(FullPath);
        ContentsHash = hashFromSourceFile;
        var hashFromBackupDiskFile = Utils.GetShortMd5HashFromFile(pathToBackupDiskFile);

        if (hashFromBackupDiskFile != hashFromSourceFile)

            // Hashes are now different on source and backup
            return false;

        // Hashes match so update it as checked and the backup checked date too
        UpdateDiskChecked(backupDisk.Name);
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