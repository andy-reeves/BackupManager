// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="MediaBackup.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Xml.Schema;
using System.Xml.Serialization;

using BackupManager.Extensions;
using BackupManager.Properties;

namespace BackupManager.Entities;

[SuppressMessage("ReSharper", "MemberCanBeInternal")]
[SuppressMessage("ReSharper", "AutoPropertyCanBeMadeGetOnly.Global")]
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
[SuppressMessage("ReSharper", "UnusedMember.Global")]
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
[SuppressMessage("ReSharper", "CollectionNeverUpdated.Global")]
[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
[SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "<Pending>")]
[Serializable]
[XmlRoot("MediaBackup")]
public sealed class MediaBackup
{
    // We need to a hash of the index folder and relative path
    // we do this so we can look up files quickly by 
    // contents hashes are not unique. Duplicate files in different locations
    // The only guaranteed unique value is the indexfolder and relative path
    // We don't want to delete the file off backup and then copy it again so we try a rename
    // as long as the file has the same indexfolder and relative path we can find it and rename it
    // This happened with The Porridge movie which is also stored as a Tv episode.
    private readonly Dictionary<string, BackupFile> indexFolderAndRelativePath = new(StringComparer.CurrentCultureIgnoreCase);

    internal readonly FileSystemWatcher Watcher = new();

    /// <summary>
    ///     The DateTime of the last full directories scan
    /// </summary>
    private string directoriesLastFullScan;

    private string mediaBackupPath;

    public MediaBackup()
    {
        BackupFiles = new Collection<BackupFile>();
        BackupDisks = new Collection<BackupDisk>();
        DirectoriesToScan = new Collection<FileSystemEntry>();
        DirectoryChanges = new Collection<FileSystemEntry>();
    }

    public MediaBackup(string mediaBackupPath)
    {
        this.mediaBackupPath = mediaBackupPath;
    }

    [XmlIgnore] public Config Config { get; set; }

    [XmlArrayItem("BackupFile")] public Collection<BackupFile> BackupFiles { get; set; }

    [XmlArrayItem("BackupDisk")] public Collection<BackupDisk> BackupDisks { get; set; }

    [XmlArrayItem("Directory")] public Collection<FileSystemEntry> DirectoriesToScan { get; set; }

    /// <summary>
    ///     We use this to save the xml. Its copied from the static property before saving and after loading
    /// </summary>
    [XmlArrayItem("FileSystemEntry")]
    public Collection<FileSystemEntry> DirectoryChanges { get; set; }

    public string DirectoriesLastFullScan
    {
        get => string.IsNullOrEmpty(directoriesLastFullScan) ? string.Empty : directoriesLastFullScan;

        set => directoriesLastFullScan = value;
    }

    /// <summary>
    ///     Creates a backup of the current xml file
    /// </summary>
    public void BackupMediaFile()
    {
        // take a copy of the xml file
        var destinationPath = GetMediaBackupDestinationPath();
        _ = Utils.FileCopy(mediaBackupPath, destinationPath);
    }

    private static string GetMediaBackupDestinationPath()
    {
        string destinationPath;

        do
        {
            var destinationFileName = "MediaBackup-" + DateTime.Now.ToString("yy-MM-dd-HH-mm-ss.ff") + ".xml";

            destinationPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "BackupManager_Backups",
                destinationFileName);
        } while (File.Exists(destinationPath));
        return destinationPath;
    }

    public static MediaBackup Load(string path)
    {
        try
        {
            var sw = Stopwatch.StartNew();
            var xsdPath = Path.Combine(Path.GetDirectoryName(path) ?? throw new InvalidOperationException(), "MediaBackupSchema.xsd");
            if (!Utils.ValidateXml(path, xsdPath)) throw new XmlSchemaValidationException("MediaBackup.xml failed validation");

            Utils.Trace($"Time to validate xml was {sw.Elapsed}");
            sw.Restart();

            var xRoot = new XmlRootAttribute
            {
                ElementName = "MediaBackup", Namespace = "http://tempuri.org/MediaBackupSchema.xsd", IsNullable = true
            };
            MediaBackup mediaBackup;
            XmlSerializer serializer = new(typeof(MediaBackup), xRoot);

            using (FileStream stream = new(path, FileMode.Open, FileAccess.Read))
            {
                mediaBackup = serializer.Deserialize(stream) as MediaBackup;
            }
            Utils.Trace($"Time to Deserialize xml was {sw.Elapsed}");
            if (mediaBackup == null) return null;

            mediaBackup.mediaBackupPath = path;

            foreach (var backupFile in mediaBackup.BackupFiles)
            {
                if (!mediaBackup.indexFolderAndRelativePath.ContainsKey(backupFile.Hash))
                    mediaBackup.indexFolderAndRelativePath.Add(backupFile.Hash, backupFile);
                else
                    throw new ApplicationException(string.Format(Resources.DuplicateContentsHashCode, backupFile.FileName));

                if (!backupFile.DiskChecked.HasValue() || !backupFile.Disk.HasValue()) backupFile.ClearDiskChecked();
            }
            var directoryName = new FileInfo(path).DirectoryName;
            if (directoryName == null) return null;

            var config = Config.Load(Path.Combine(directoryName, "Config.xml"));
            if (config != null) mediaBackup.Config = config;
            return mediaBackup;
        }
        catch (InvalidOperationException ex)
        {
            throw new ApplicationException(string.Format(Resources.UnableToLoadXml, "MediaBackup.xml", ex));
        }
        catch (XmlSchemaValidationException ex)
        {
            throw new ApplicationException(string.Format(Resources.UnableToLoadXml, "MediaBackup.xml failed validation", ex));
        }
    }

    /// <summary>
    ///     Returns the Directory, and RelativePath for the path provided.
    /// </summary>
    /// <param name="path">Full path to the file</param>
    /// <param name="directory"></param>
    /// <param name="relativePath"></param>
    /// <returns>False if the MasterFolder or IndexFolder cannot be found.</returns>
    public bool GetFoldersForPath(string path, out string directory, out string relativePath)
    {
        directory = null;
        relativePath = null;
        var pathWithTerminatingString = Utils.EnsurePathHasATerminatingSeparator(path);

        foreach (var masterDirectory in Config.Directories.Where(masterDirectory =>
                     pathWithTerminatingString.StartsWith(Utils.EnsurePathHasATerminatingSeparator(masterDirectory),
                         StringComparison.InvariantCultureIgnoreCase)))
        {
            relativePath = BackupFile.GetRelativePath(path, masterDirectory);
            directory = masterDirectory;
            return true;
        }
        return false;
    }

    /// <summary>
    ///     Updates the DateTime of the last full directories scan.
    /// </summary>
    public void UpdateLastFullScan()
    {
        directoriesLastFullScan = DateTime.Now.ToString(Resources.DateTime_yyyyMMdd);
    }

    public void Save()
    {
        BackupMediaFile();
        DirectoryChanges = new Collection<FileSystemEntry>(Watcher.FileSystemChanges.ToList());
        DirectoriesToScan = new Collection<FileSystemEntry>(Watcher.DirectoriesToScan.ToList());
        var xRoot = new XmlRootAttribute { ElementName = "MediaBackup", Namespace = "http://tempuri.org/MediaBackupSchema.xsd", IsNullable = true };
        XmlSerializer xmlSerializer = new(typeof(MediaBackup), xRoot);
        if (File.Exists(mediaBackupPath)) File.SetAttributes(mediaBackupPath, FileAttributes.Normal);
        using StreamWriter streamWriter = new(mediaBackupPath);
        xmlSerializer.Serialize(streamWriter, this);
    }

    /// <summary>
    ///     Gets a BackupFile representing the file from the contents Hashcode provided
    /// </summary>
    /// <param name="value">The contents Hashcode of the file to find.</param>
    /// <returns>Null if it wasn't found or null if more than 1</returns>
    public BackupFile GetBackupFileFromContentsHashcode(string value)
    {
        Utils.TraceIn();
        var count = BackupFiles.Count(a => a.ContentsHash == value);

        switch (count)
        {
            case 0:
                return Utils.TraceOut<BackupFile>("exit1");
            case > 1:
                Utils.Trace(string.Format(Resources.DuplicateContentsHashCode, value));
                return Utils.TraceOut<BackupFile>("exit2");
        }
        var file = BackupFiles.First(q => q.ContentsHash == value);
        return Utils.TraceOut(file);
    }

    /// <summary>
    ///     Gets a BackupFile representing the file at fullPath
    /// </summary>
    /// <param name="fullPath">The fullPath to the file.</param>
    /// <returns>Null if it wasn't found or couldn't be created maybe locked by another process</returns>
    public BackupFile GetBackupFile(string fullPath)
    {
        Utils.TraceIn(fullPath);
        if (!File.Exists(fullPath) || !Utils.IsFileAccessible(fullPath)) return null;

        if (!GetFoldersForPath(fullPath, out var directory, out var relativePath))
            throw new ArgumentException(Resources.MediaBackup_UnableToDetermineDirectoryOrRelativePath, nameof(fullPath));
        if (string.IsNullOrEmpty(directory)) throw new ArgumentException(Resources.DirectoryEmpty);

        // we hash the path of the file so we can look it up quickly
        // then we check the ModifiedTime and size
        // if these have changed we redo the hash
        // files with same hash are allowed (Porridge TV ep and movie)
        // files can't have same hash and same filename though
        var hashKey = Path.Combine(Utils.GetIndexFolder(directory), relativePath);

        // if this path is already added then return it
        if (indexFolderAndRelativePath.TryGetValue(hashKey, out var backupFile))
        {
            // consider a file a.txt that's on //nas1/assets1/_TV and on //nas1/assets4/_TV too
            // this has same index folder and path but its a different file
            string hashOfContents;

            if (backupFile.Directory != directory)
            {
                // This is similar file in different directories
                // This also happens if a file is moved from 1 directory to another
                // its old location is still in the xml but the new location will be found on disk
                Utils.Trace($"Duplicate file detected at {fullPath} and {backupFile.FullPath}");

                // First we can check the hash of both
                // its its the same hash then we can assume the file has just been moved
                hashOfContents = Utils.GetShortMd5HashFromFile(fullPath);

                if (hashOfContents == backupFile.ContentsHash)
                {
                    Utils.Trace($"Changing Directory on {backupFile.FullPath} to {directory}");
                    backupFile.Directory = directory;
                }
                else
                {
                    Utils.Trace("Hashes are different on the duplicate files");
                    return Utils.TraceOut<BackupFile>();
                }
            }
            else
            {
                // check the timestamp against what we have
                var lastWriteTimeFromMasterFile = Utils.GetFileLastWriteTime(fullPath);

                // if the file on disk is different then check the hash 
                if (backupFile.LastWriteTime != lastWriteTimeFromMasterFile)
                {
                    // update the timestamp as its changed/missing
                    backupFile.LastWriteTime = lastWriteTimeFromMasterFile;
                    hashOfContents = Utils.GetShortMd5HashFromFile(fullPath);

                    // has the contents hash changed too?
                    if (hashOfContents != backupFile.ContentsHash)
                    {
                        backupFile.UpdateContentsHash();

                        // clear the backup details as the master file hash has changed
                        backupFile.ClearDiskChecked();
                    }
                }
                backupFile.UpdateFileLength();
            }

            // Now we check the full path has not changed the UPPER or lowercase anywhere
            // we're not case sensitive but we want it to match the casing in the directory
            if (fullPath != backupFile.FullPath) backupFile.SetFullPath(fullPath, directory);
            return Utils.TraceOut(backupFile);
        }
        backupFile = new BackupFile(fullPath, directory);
        Utils.Trace($"Adding backup file {backupFile.RelativePath}");
        BackupFiles.Add(backupFile);
        indexFolderAndRelativePath.Add(backupFile.Hash, backupFile);
        return Utils.TraceOut(backupFile);
    }

    /// <summary>
    ///     Ensures the BackupFile exists and sets the Flag=TRUE. Sets Deleted=FALSE.
    /// </summary>
    /// <param name="path">Full path to the file in the directory</param>
    /// <returns>Null is the file  was locked or an error occurred</returns>
    internal bool EnsureFile(string path)
    {
        Utils.TraceIn();
        var backupFile = GetBackupFile(path);
        if (backupFile == null) return Utils.TraceOut(false);

        backupFile.Deleted = false;
        backupFile.Flag = true;
        return Utils.TraceOut(true);
    }

    /// <summary>
    ///     Returns the path to the parent directory of the file provided. That's the path to the first directory after a
    ///     backup directory
    /// </summary>
    /// <param name="path"></param>
    /// <returns>Null if the Path doesn't contain a backup Directory or if its in the root of the backup directory</returns>
    public string GetParentPath(string path)
    {
        return (from directory in Config.Directories
            where path.Contains(directory + "\\")
            select path.SubstringAfter(directory + "\\", StringComparison.CurrentCultureIgnoreCase)
            into pathAfterSlash
            let lastSlashLocation = pathAfterSlash.IndexOf('\\')
            select lastSlashLocation < 0 ? null : path[..(lastSlashLocation + (path.Length - pathAfterSlash.Length))]).FirstOrDefault();
    }

    public string GetFilters()
    {
        return string.Join(",", Config.Filters.ToArray());
    }

    /// <summary>
    ///     Get a BackupDisk for the current backupShare
    /// </summary>
    /// <param name="backupShare"></param>
    /// <returns>Null if disk is not connected</returns>
    public BackupDisk GetBackupDisk(string backupShare)
    {
        Utils.TraceIn();

        // try and find a disk based on the disk name only
        // if more than 1 disk than return the first one
        var diskName = BackupDisk.GetBackupDirectoryName(backupShare);
        if (string.IsNullOrEmpty(diskName)) return Utils.TraceOut<BackupDisk>();

        var backupDisk = BackupDisks.FirstOrDefault(x => x.Name == diskName);

        if (backupDisk != null)
        {
            backupDisk.BackupShare = backupShare;
            return Utils.TraceOut(backupDisk);
        }
        BackupDisk disk = new(diskName, backupShare);
        BackupDisks.Add(disk);
        return Utils.TraceOut(disk);
    }

    /// <summary>
    ///     Gets a BackupFile from the path provided. Path should include indexfolder and relativePath and not a full path
    /// </summary>
    /// <param name="path"></param>
    /// <returns>Null if it doesn't exist.</returns>
    public BackupFile GetBackupFileFromHashKey(string path)
    {
        return indexFolderAndRelativePath.TryGetValue(path, out var backupFile) ? backupFile : null;
    }

    /// <summary>
    ///     Get BackupFiles on the diskName provided. Optionally including files marked as Deleted
    /// </summary>
    /// <param name="diskName"></param>
    /// <param name="includeDeletedFiles"></param>
    /// <returns></returns>
    public IEnumerable<BackupFile> GetBackupFilesOnBackupDisk(string diskName, bool includeDeletedFiles)
    {
        return includeDeletedFiles
            ? BackupFiles.Where(p => p.Disk.Equals(diskName, StringComparison.CurrentCultureIgnoreCase))
            : BackupFiles.Where(p => p.Disk.Equals(diskName, StringComparison.CurrentCultureIgnoreCase) && !p.Deleted);
    }

    /// <summary>
    ///     Get BackupFiles that are in the Directory provided. Includes files marked as Deleted
    /// </summary>
    /// <param name="directory"></param>
    /// <returns></returns>
    public IEnumerable<BackupFile> GetBackupFilesInDirectory(string directory)
    {
        return BackupFiles.Where(p => p.Directory == directory).OrderBy(static q => q.BackupDiskNumber);
    }

    /// <summary>
    ///     Get BackupFiles that are marked as Deleted
    /// </summary>
    /// <returns></returns>
    public IEnumerable<BackupFile> GetBackupFilesMarkedAsDeleted()
    {
        return BackupFiles.Where(static p => p.Deleted).OrderBy(static q => q.BackupDiskNumber);
    }

    /// <summary>
    ///     Get BackupFiles that are NOT marked as Deleted
    /// </summary>
    /// <returns></returns>
    public IEnumerable<BackupFile> GetBackupFilesNotMarkedAsDeleted()
    {
        return BackupFiles.Where(static p => !p.Deleted);
    }

    /// <summary>
    ///     Get BackupFiles where Disk is null or Empty (does not included MarkedAsDeleted files)
    /// </summary>
    /// <returns></returns>
    public IEnumerable<BackupFile> GetBackupFilesWithDiskEmpty()
    {
        return BackupFiles.Where(static p => string.IsNullOrEmpty(p.Disk) && !p.Deleted);
    }

    /// <summary>
    ///     Returns True if this path exists already. Path should contain indexfolder and relative path.
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    public bool Contains(string path)
    {
        return indexFolderAndRelativePath.ContainsKey(path);
    }

    /// <summary>
    ///     Sets the Flag to False on all BackupFiles
    /// </summary>
    public void ClearFlags()
    {
        foreach (var backupFile in BackupFiles)
        {
            backupFile.Flag = false;
        }
    }

    /// Removes any files that have a matching flag value as the one provided.
    public void RemoveFilesWithFlag(bool flag, bool clearHashes)
    {
        Collection<BackupFile> filesToRemove = new();

        foreach (var backupFile in BackupFiles.Where(backupFile => backupFile.Flag == flag))
        {
            filesToRemove.Add(backupFile);
        }

        foreach (var backupFile in filesToRemove)
        {
            if (clearHashes) indexFolderAndRelativePath.Remove(backupFile.Hash);
            if (BackupFiles.Contains(backupFile)) _ = BackupFiles.Remove(backupFile);
        }
    }

    /// <summary>
    ///     Returns the oldest BackupFile we have using the DiskChecked property.
    /// </summary>
    /// <returns></returns>
    public BackupFile GetOldestFile()
    {
        var oldestFileDate = DateTime.Today;
        BackupFile oldestFile = null;

        foreach (var backupFile in BackupFiles)
        {
            if (!backupFile.DiskChecked.HasValue()) continue;

            var backupFileDate = DateTime.Parse(backupFile.DiskChecked);
            if (backupFileDate >= oldestFileDate && oldestFile != null) continue;

            oldestFileDate = backupFileDate;
            oldestFile = backupFile;
        }
        return oldestFile;
    }

    /// <summary>
    ///     Removes a file from our collection
    /// </summary>
    /// <param name="backupFile"></param>
    internal void RemoveFile(BackupFile backupFile)
    {
        if (indexFolderAndRelativePath.ContainsKey(backupFile.Hash)) _ = indexFolderAndRelativePath.Remove(backupFile.Hash);
        if (BackupFiles.Contains(backupFile)) _ = BackupFiles.Remove(backupFile);
    }
}