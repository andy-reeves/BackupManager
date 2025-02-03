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
using System.Threading;
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

    private readonly object lockObject = new();

    internal readonly FileSystemWatcher Watcher = new();

    /// <summary>
    ///     The DateTime of the last full directories scan
    /// </summary>
    private string directoriesLastFullScan;

    internal string LastDuplicateDirectory;

    private string mediaBackupPath;

    public MediaBackup()
    {
        BackupFiles = [];
        BackupDisks = [];
        DirectoriesToScan = [];
        DirectoryChanges = [];
        TmdbMovies = [];
    }

    public MediaBackup(string mediaBackupPath)
    {
        this.mediaBackupPath = mediaBackupPath;
    }

    [XmlIgnore] public Config Config { get; set; }

    [XmlArrayItem("BackupFile")] public Collection<BackupFile> BackupFiles { get; set; }

    [XmlArrayItem("BackupDisk")] public Collection<BackupDisk> BackupDisks { get; set; }

    [XmlArrayItem("Directory")] public Collection<FileSystemEntry> DirectoriesToScan { get; set; }

    [XmlIgnore]
    [XmlArrayItem("TmdbMovie")]
    public Collection<TmdbItem> TmdbMovies { get; set; }

    [XmlIgnore]
    [XmlArrayItem("TmdbTvEpisode")]
    public Collection<TmdbItem> TmdbTvEpisodes { get; set; }

    /// <summary>
    ///     The Directories that have been changed
    /// </summary>
    [XmlArrayItem("FileSystemEntry")]
    public Collection<FileSystemEntry> DirectoryChanges { get; set; }

    /// <summary>
    ///     The Directory Scan history
    /// </summary>
    [XmlArrayItem("DirectoryScan")]
    public Collection<DirectoryScan> DirectoryScans { get; set; }

    public string DirectoriesLastFullScan
    {
        get => directoriesLastFullScan.HasValue() ? directoriesLastFullScan : string.Empty;

        set => directoriesLastFullScan = value;
    }

    /// <summary>
    ///     Creates a backup of the current xml file
    /// </summary>
    public void BackupMediaFile(CancellationToken ct)
    {
        // take a copy of the xml file
        var destinationPath = GetMediaBackupDestinationPath();
        _ = Utils.File.Copy(mediaBackupPath, destinationPath, ct);
    }

    private static string GetMediaBackupDestinationPath()
    {
        string destinationPath;

        do
        {
            var destinationFileName = "MediaBackup-" + DateTime.Now.ToString("yy-MM-dd-HH-mm-ss.ff") + ".xml";
            destinationPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "BackupManager_Backups", destinationFileName);
        } while (File.Exists(destinationPath));
        return destinationPath;
    }

    /// <summary>
    ///     Loads the main media xml and all the config
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    /// <exception cref="ApplicationException"></exception>
    public static MediaBackup Load(string path)
    {
        try
        {
            MediaBackup mediaBackup;

            // The path is null when tests are running as config is loaded separately
            if (path != null)
            {
                var sw = Stopwatch.StartNew();
                Utils.ValidateXmlFromResources(path, "BackupManager.MediaBackupSchema.xsd");
                Utils.Trace($"Time to validate xml was {sw.Elapsed}");
                sw.Restart();
                var xRoot = new XmlRootAttribute { ElementName = "MediaBackup", Namespace = "MediaBackupSchema.xsd", IsNullable = true };
                var serializer = new XmlSerializer(typeof(MediaBackup), xRoot);

                using (FileStream stream = new(path, FileMode.Open, FileAccess.Read))
                {
                    mediaBackup = serializer.Deserialize(stream) as MediaBackup;
                }
                Utils.Trace($"Time to Deserialize xml was {sw.Elapsed}");
                if (mediaBackup == null) return null;

                mediaBackup.mediaBackupPath = path;
                var directoryName = new FileInfo(path).DirectoryName;
                if (directoryName == null) return null;

                var config = Config.Load(Path.Combine(directoryName, "Config.xml"));
                if (config != null) mediaBackup.Config = config;
                var s2 = new XmlSerializer(typeof(Collection<TmdbItem>));
                using FileStream stream2 = new(path + ".tv.xml", FileMode.Open, FileAccess.Read);
                mediaBackup.TmdbTvEpisodes = s2.Deserialize(stream2) as Collection<TmdbItem>;
                var s3 = new XmlSerializer(typeof(Collection<TmdbItem>));
                using FileStream stream3 = new(path + ".movies.xml", FileMode.Open, FileAccess.Read);
                mediaBackup.TmdbMovies = s3.Deserialize(stream3) as Collection<TmdbItem>;
            }
            else
                mediaBackup = Utils.MediaBackup;

            foreach (var backupFile in mediaBackup.BackupFiles)
            {
                if (mediaBackup.indexFolderAndRelativePath.TryGetValue(backupFile.Hash, out var value))
                    throw new ApplicationException(string.Format(Resources.DuplicateContentsHashCode, backupFile.FileName, backupFile.FullPath, value.FullPath));

                mediaBackup.indexFolderAndRelativePath.Add(backupFile.Hash, backupFile);
                if (!backupFile.DiskCheckedTime.HasValue || backupFile.Disk.HasNoValue()) backupFile.ClearDiskChecked();
            }
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

    internal void CheckMovieAndTvForDuplicates()
    {
        // check the tv runtime cache results
        var dictionary = new Dictionary<string, TmdbItem>();

        for (var index = TmdbTvEpisodes.Count - 1; index >= 0; index--)
        {
            var episode = TmdbTvEpisodes[index];
            var key = episode.Id.Split(':');

            // key[1]==0 is the Specials folder which we don't check
            if (key[2] == "-1" || key[1] == "-1" || key[0] == "-1" || key[1] == "0")
                _ = TmdbTvEpisodes.Remove(episode);
            else
            {
                var result = dictionary.TryAdd(episode.Id, episode);

                if (!result)
                {
                    // remove the episode
                    _ = TmdbTvEpisodes.Remove(episode);
                }
            }
        }
        var dictionary2 = new Dictionary<string, TmdbItem>();

        // check the movie runtime cache results
        for (var index = TmdbMovies.Count - 1; index >= 0; index--)
        {
            var m = TmdbMovies[index];
            var result = dictionary2.TryAdd(m.Id, m);

            if (!result)
            {
                // remove the movie
                _ = TmdbMovies.Remove(m);
            }
        }
    }

    public void CheckForDuplicateTvEpisodesGlobally()
    {
        HashSet<string> hashSetForTvEpisodes = [];

        // for all tv episodes check season,edition and episode are unique
        var files = BackupFiles.Where(static f => !f.Deleted && Utils.File.IsTv(f.FullPath));

        foreach (var file in files)
        {
            if (Utils.MediaHelper.ExtendedBackupFileBase(file.FullPath) is not (VideoBackupFileBase { SpecialFeature: SpecialFeature.None } and TvEpisodeBackupFile tvEp))
                continue;
            if (hashSetForTvEpisodes.Add($"{tvEp.TvdbId}:{tvEp.Edition}:{tvEp.Season}:{tvEp.Episode}")) continue;

            Utils.LogWithPushover(BackupAction.General, PushoverPriority.High, $"Duplicate TV episode detected {file.FullPath}");
        }
    }

    public bool CheckTvEpisodeForDuplicate(string path, CancellationToken ct)
    {
        // get all the files in the directory alongside the file specified
        // check they all hae unique season:ep combinations
        var directory = new FileInfo(path).Directory?.FullName;
        if (directory == LastDuplicateDirectory) return true;

        var files = Utils.File.GetFiles(directory, "*", SearchOption.AllDirectories, 0, 0, ct);
        HashSet<string> hashSetForTvEpisodes = [];

        foreach (var file in files)
        {
            if (Utils.MediaHelper.ExtendedBackupFileBase(file) is not (VideoBackupFileBase { SpecialFeature: SpecialFeature.None } and TvEpisodeBackupFile tvEp)) continue;
            if (hashSetForTvEpisodes.Add($"{tvEp.TvdbId}:{tvEp.Edition}:{tvEp.Season}:{tvEp.Episode}")) continue;

            Utils.LogWithPushover(BackupAction.ProcessFiles, PushoverPriority.High, $"Duplicate TV episode detected {file}");
            LastDuplicateDirectory = directory;
            return false;
        }
        return true;
    }

    /// <summary>
    ///     Returns the runtime in minutes for the movie specified by path. Uses the cache first or calls the TmdbAPI if it's
    ///     not in the cache.
    /// </summary>
    /// <param name="path">Path to the movie file</param>
    /// <param name="useCache">True to load from the cache</param>
    /// <returns>-1 if the movie can't be found or the runtime in minutes</returns>
    public int GetMovieRuntime(string path, bool useCache = true)
    {
        if (!Utils.File.IsVideo(path)) return -1;

        var tmdbId = Utils.MediaHelper.GetTmdbId(path);
        if (tmdbId == -1) return -1;

        lock (lockObject)
        {
            var m = TmdbMovies.FirstOrDefault(x => x.Id == tmdbId.ToString()) ?? new TmdbItem(tmdbId.ToString());
            if (m.Runtime > 0 && useCache) return m.Runtime;

            Utils.Log(BackupAction.ProcessFiles, $"Getting runtime from TMDB API for {tmdbId} - {path}");
            var r = Utils.MediaHelper.GetMovieRuntimeFromTmdbApi(tmdbId);
            if (r == 1) return -1;

            m.Runtime = r;
            TmdbMovies.Add(m);
            return m.Runtime;
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

        foreach (var masterDirectory in Config.DirectoriesToBackup.Where(masterDirectory =>
                     pathWithTerminatingString.StartsWithIgnoreCase(Utils.EnsurePathHasATerminatingSeparator(masterDirectory))))
        {
            relativePath = BackupFile.GetRelativePath(path, masterDirectory);
            directory = masterDirectory;
            return true;
        }
        return false;
    }

    internal void DeleteScanReportsNotInList(string[] scanIdsToKeep)
    {
        Utils.TraceIn();
        var scans = DirectoryScans.ToArray();

        for (var i = scans.Length - 1; i >= 0; i--)
        {
            var scan = scans[i];
            if (scanIdsToKeep.Contains(scan.Id)) continue;

            DirectoryScans.RemoveAt(i);
        }
        Utils.TraceOut();
    }

    internal string[] GetLastScans(DirectoryScanType scanType, int howMany)
    {
        Utils.TraceIn();
        var directoriesCount = Config.DirectoriesToBackup.Count;
        const int marginOfErrorOnDirectoryCount = 5;
        var minCount = directoriesCount - marginOfErrorOnDirectoryCount;
        var maxCount = directoriesCount + marginOfErrorOnDirectoryCount;

        // filter by type and order the scans by startDate in Descending order
        // Move through the scans and find the top nn ids
        var sc = DirectoryScans.Where(s => s.TypeOfScan == scanType).OrderByDescending(static s => s.StartDateTime).ToArray();
        var list = new string[howMany];
        var index = howMany - 1;

        // We check the count to include only full scans
        foreach (var scan in from scan in sc where !list.Contains(scan.Id) let count = sc.Count(s => s.Id == scan.Id) where count.IsInRange(minCount, maxCount) select scan)
        {
            list[index] = scan.Id;
            index--;
            if (index < 0) break;
        }

        // Trim the list of empty entries
        return Utils.TraceOut(list.Where(static a => a.HasValue()).ToArray());
    }

    /// <summary>
    ///     Updates the DateTime of the last full directories scan.
    /// </summary>
    public void UpdateLastFullScan()
    {
        DirectoriesLastFullScan = DateTime.Now.ToString(Resources.DateTime_yyyyMMdd);
    }

    public void Save(CancellationToken ct)
    {
        DirectoryChanges = new Collection<FileSystemEntry>([.. Watcher.FileSystemChanges]);
        DirectoriesToScan = new Collection<FileSystemEntry>([.. Watcher.DirectoriesToScan]);
        BackupMediaFile(ct);

        // remove any directory scan that didn't complete check end date
        for (var index = DirectoryScans.Count - 1; index >= 0; index--)
        {
            var scan = DirectoryScans[index];
            if (scan.EndDateTime != DateTime.MinValue) continue;

            Utils.Trace($"DirectoryScans Removing as no end date on {scan.Path}");
            DirectoryScans.RemoveAt(index);
        }
        var xRoot = new XmlRootAttribute { ElementName = "MediaBackup", Namespace = "MediaBackupSchema.xsd", IsNullable = true };
        XmlSerializer xmlSerializer = new(typeof(MediaBackup), xRoot);
        if (File.Exists(mediaBackupPath)) File.SetAttributes(mediaBackupPath, FileAttributes.Normal);
        using StreamWriter streamWriter = new(mediaBackupPath);
        xmlSerializer.Serialize(streamWriter, this);

        // save the movies and tv episodes caches
        using StreamWriter streamWriter2 = new(mediaBackupPath + ".tv.xml");
        XmlSerializer xmlSerializer2 = new(typeof(Collection<TmdbItem>));
        xmlSerializer2.Serialize(streamWriter2, TmdbTvEpisodes);
        using StreamWriter streamWriter3 = new(mediaBackupPath + ".movies.xml");
        XmlSerializer xmlSerializer3 = new(typeof(Collection<TmdbItem>));
        xmlSerializer3.Serialize(streamWriter3, TmdbMovies);
        Utils.LogWithPushover(BackupAction.General, "MediaBackup.xml saved.");
    }

    /// <summary>
    ///     Gets a BackupFile representing the file from the contents Hashcode provided. Doesn't check files marked Deleted
    /// </summary>
    /// <param name="value">The contents Hashcode of the file to find.</param>
    /// <returns>Null if it wasn't found or more than 1 file found</returns>
    public BackupFile GetBackupFileFromContentsHashcode(string value)
    {
        Utils.TraceIn();

        // Copy to List to avoid modified collection errors
        var bFiles = BackupFiles.ToList();
        var count = bFiles.Count(a => a.ContentsHash == value && !a.Deleted);

        switch (count)
        {
            case 0:
                return Utils.TraceOut<BackupFile>("exit1");
            case > 1:
                var files = bFiles.Where(q => q.ContentsHash == value && !q.Deleted).Take(2).ToArray();
                Utils.Trace(string.Format(Resources.DuplicateContentsHashCode, value, files[0].FullPath, files[1].FullPath));
                return Utils.TraceOut<BackupFile>("exit2");
        }
        var file = bFiles.First(q => q.ContentsHash == value && !q.Deleted);
        return Utils.TraceOut(file);
    }

    /// <summary>
    ///     Gets a BackupFile representing the file at fullPath
    /// </summary>
    /// <param name="fullPath">The fullPath to the file.</param>
    /// <returns>Null if it wasn't found or couldn't be created maybe locked by another process</returns>
    [SuppressMessage("ReSharper", "CommentTypo")]
    [SuppressMessage("ReSharper", "GrammarMistakeInComment")]
    [SuppressMessage("ReSharper", "AssignNullToNotNullAttribute")]
    public BackupFile GetBackupFile(string fullPath)
    {
        Utils.TraceIn(fullPath);
        if (!File.Exists(fullPath)) return null;

        if (!GetFoldersForPath(fullPath, out var directory, out var relativePath))
            throw new ArgumentException(Resources.UnableToDetermineDirectoryOrRelativePath, nameof(fullPath));
        if (directory.HasNoValue()) throw new ArgumentException(Resources.DirectoryEmpty);

        // we hash the path of the file so we can look it up quickly
        // then we check the ModifiedTime and size
        // if these have changed we redo the hash
        // files with same hash are allowed (Porridge TV ep and movie)
        // files can't have same hash and same filename though
        var hashKey = Path.Combine(Utils.GetIndexFolder(directory), relativePath);
        Utils.Trace($"hashKey = {hashKey}");

        // if this hash is already added then return it

        if (indexFolderAndRelativePath.TryGetValue(hashKey, out var backupFile))
        {
            // consider a file a.txt that's on //nas1/assets1/_TV and on //nas1/assets4/_TV too
            // this has same index folder and path but it's a different file
            string hashOfContents;

            if (backupFile.Directory == directory)
            {
                Utils.Trace("directory equal");

                // check the timestamp against what we have
                var lastWriteTimeFromMasterFile = Utils.File.GetLastWriteTime(fullPath);

                // if the file on disk is different then check the hash
                if (backupFile.LastWriteTime != lastWriteTimeFromMasterFile)
                {
                    Utils.Trace(" update the timestamp as its changed/missing");

                    // update the timestamp as its changed/missing
                    backupFile.LastWriteTime = lastWriteTimeFromMasterFile;
                    hashOfContents = Utils.File.GetShortMd5Hash(fullPath);

                    // has the contents hash changed too?
                    if (hashOfContents != backupFile.ContentsHash)
                    {
                        backupFile.UpdateContentsHash();

                        // clear the backup details as the master file hash has changed
                        backupFile.ClearDiskChecked();
                    }
                }
                Utils.Trace("about to updateFileLength");
                Utils.Trace($"FullPath = {backupFile.FullPath}");
                Utils.Trace($"fullPath passed in  = {fullPath}");

                if (fullPath != backupFile.FullPath)
                {
                    Utils.LogWithPushover(BackupAction.General,
                        $"FullPath different so setting it now. backupFile.FullPath was {backupFile.FullPath} and now set to {fullPath}");
                    backupFile.SetFullPath(fullPath, directory);
                }
                Utils.Trace($"FullPath = {backupFile.FullPath}");
                Utils.Trace($"fullPath passed in  = {fullPath}");
                backupFile.UpdateFileLength(Config.DirectoriesMinimumReEncodeSizePercentage, Config.DirectoriesMaximumReEncodeSizePercentage);
            }
            else
            {
                // This is similar file in different directories
                // This also happens if a file is moved from 1 directory to another one
                // its old location is still in the xml but the new location will be found on disk
                Utils.Trace($"Duplicate file detected at {fullPath} and {backupFile.FullPath}");

                // First we can check the hash of both
                // its its the same hash then we can assume the file has just been moved
                hashOfContents = Utils.File.GetShortMd5Hash(fullPath);

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

            // Now we check the full path has not changed the UPPER or lowercase anywhere
            // we're not case-sensitive, but we want it to match the casing in the directory
            if (fullPath != backupFile.FullPath) backupFile.SetFullPath(fullPath, directory);
            return Utils.TraceOut(backupFile);
        }

        // If we get to here then we couldn't get the file from the path
        // We need to check the contents hash only because maybe we renamed the source file
        Utils.Trace("couldn't get the file from the path.  We need to check the contents hash only because maybe we renamed the source file");

        // this happened when we changed files from [SDTV-576p] to [DVD-576p]
        // What about files that have exactly the same contents but in a diferent location?
        // This happens for extras to movies where we've copied them into the other version of the movie folder and for
        // Friends on DVD and Bluray
        /* const bool allowDuplicateContentsHash = false;

        if (allowDuplicateContentsHash)
        {
            var contents = Utils.File.GetShortMd5Hash(fullPath);
            var f = GetBackupFileFromContentsHashcode(contents);

            if (f != null)
            {
                if (GetFoldersForPath(fullPath, out var dir, out _))
                {
                    // we have a matching file from contents only
                    f.SetFullPath(fullPath, dir);
                    return Utils.TraceOut(f);
                }
            }
        }*/
        backupFile = new BackupFile(fullPath, directory);
        Utils.Trace($"Adding backup file {backupFile.RelativePath}");
        BackupFiles.Add(backupFile);
        indexFolderAndRelativePath.Add(backupFile.Hash, backupFile);

        // Is thefile a video file we check for old files and size changes
        if (!Utils.File.IsVideo(fullPath)) return Utils.TraceOut(backupFile);

        // Before we return the file we check to see if a file existed in the xml before that had the same relative path
        // and same name (not including the extension. We check if it existed and the name was different only by [h264] to [h265] too
        var fileName1 = Path.Combine(Path.GetDirectoryName(relativePath), Path.GetFileNameWithoutExtension(relativePath));
        var fileName2 = fileName1.Replace("[h265]", "[h264]");
        var fileName3 = fileName1.Replace("[h265]", "[MPEG2]");
        var fileNamesToCheckFor = new[] { fileName1, fileName2, fileName3 };

        foreach (var file in from file in BackupFiles
                 let fileNameWithoutExtension = Path.Combine(Path.GetDirectoryName(relativePath), Path.GetFileNameWithoutExtension(file.FullPath))
                 where fileNameWithoutExtension.EqualsAnyIgnoreCase(fileNamesToCheckFor)
                 select file)
        {
            // Length was non-zero before so check how much it has changed - re-encodes are typically 50%-120% of original
            // if the paths are exactly the same then don't Log anything
            if (!file.FullPath.EqualsIgnoreCase(backupFile.FullPath))
            {
                var percentOfOriginal = backupFile.Length * 100 / file.Length;

                if (percentOfOriginal < Config.DirectoriesMinimumReEncodeSizePercentage || percentOfOriginal > Config.DirectoriesMaximumReEncodeSizePercentage)
                    Utils.LogWithPushover(BackupAction.ProcessFiles, PushoverPriority.High, $"{percentOfOriginal:0}% - {backupFile.FullPath} of the previous size");
                else
                {
                    // Inside the current config params for Pushover lgging so only in text log
                    Utils.Log(BackupAction.ProcessFiles, $"{percentOfOriginal:0}% - {backupFile.FullPath} of the previous size");
                }
                Utils.Log(BackupAction.ProcessFiles, $"Matched {file.FullPath} with new path {backupFile.FullPath}");
            }
            break;
        }
        return Utils.TraceOut(backupFile);
    }

    /// <summary>
    ///     Ensures the BackupFile exists and sets the Flag=TRUE. Sets Deleted=FALSE.
    /// </summary>
    /// <param name="path">Full path to the file in the directory</param>
    /// <returns>False if the file couldn't be checked</returns>
    /// <exception cref="IOException">If the file is locked</exception>
    internal bool EnsureFile(string path)
    {
        Utils.TraceIn(path);
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
    /// <returns>Null if the Path doesn't contain a backup Directory or if it's in the root of the backup directory</returns>
    public string GetParentPath(string path)
    {
        return (from directory in Config.DirectoriesToBackup
            where path.Contains(directory + "\\")
            select path.SubstringAfterIgnoreCase(directory + "\\")
            into pathAfterSlash
            let lastSlashLocation = pathAfterSlash.IndexOf('\\')
            select lastSlashLocation < 0 ? null : path[..(lastSlashLocation + (path.Length - pathAfterSlash.Length))]).FirstOrDefault();
    }

    public string GetFilters()
    {
        return string.Join(",", [.. Config.Filters]);
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
        if (diskName.HasNoValue()) return Utils.TraceOut<BackupDisk>();

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
        return indexFolderAndRelativePath.GetValueOrDefault(path);
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
    ///     Get BackupFiles that are in the Directory provided.
    /// </summary>
    /// <param name="directory"></param>
    /// <param name="includeDeletedFiles">True to include files marked Deleted</param>
    /// <returns></returns>
    public IEnumerable<BackupFile> GetBackupFilesInDirectory(string directory, bool includeDeletedFiles)
    {
        return includeDeletedFiles ? BackupFiles.Where(p => p.Directory == directory) : BackupFiles.Where(p => p.Directory == directory && !p.Deleted);
    }

    /// <summary>
    ///     Get BackupFiles. Either all of them or just those not Deleted.
    /// </summary>
    /// <returns></returns>
    public IEnumerable<BackupFile> GetBackupFiles(bool includeDeletedFiles)
    {
        return includeDeletedFiles ? BackupFiles : BackupFiles.Where(static p => !p.Deleted);
    }

    /// <summary>
    ///     Get BackupFiles that are marked as Deleted only
    /// </summary>
    /// <returns></returns>
    public IEnumerable<BackupFile> GetBackupFilesMarkedAsDeleted(bool orderByDiskNumber)
    {
        return orderByDiskNumber ? BackupFiles.Where(static p => p.Deleted).OrderBy(static q => q.BackupDiskNumber) : BackupFiles.Where(static p => p.Deleted);
    }

    /// <summary>
    ///     Get BackupFiles where Disk is null or Empty (does not include MarkedAsDeleted files)
    /// </summary>
    /// <returns></returns>
    public IEnumerable<BackupFile> GetBackupFilesWithDiskEmpty()
    {
        return BackupFiles.Where(static p => p.Disk.HasNoValue() && !p.Deleted);
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
    ///     Sets the Flag to False and BeingCheckedNow to False on all BackupFiles
    /// </summary>
    public void ClearFlags()
    {
        foreach (var backupFile in BackupFiles)
        {
            backupFile.Flag = false;
            backupFile.BeingCheckedNow = false;
        }
    }

    /// Removes any files that have a matching flag value as the one provided.
    public void RemoveFilesWithFlag(bool flag, bool clearHashes)
    {
        Collection<BackupFile> filesToRemove = [];

        foreach (var backupFile in BackupFiles.Where(backupFile => backupFile.Flag == flag))
        {
            filesToRemove.Add(backupFile);
        }

        foreach (var backupFile in filesToRemove)
        {
            if (clearHashes) _ = indexFolderAndRelativePath.Remove(backupFile.Hash);
            _ = BackupFiles.Remove(backupFile);
        }
    }

    /// <summary>
    ///     Returns the oldest BackupFile we have using the DiskChecked property.
    /// </summary>
    /// <returns>Null if there are no files</returns>
    public BackupFile GetOldestFile()
    {
        var oldestFileDate = DateTime.Today;
        var backupFilesArray = BackupFiles.ToArray();
        if (backupFilesArray.Length == 0) return null;

        BackupFile oldestFile = null;

        foreach (var backupFile in backupFilesArray)
        {
            if (!backupFile.DiskCheckedTime.HasValue || backupFile.BeingCheckedNow) continue;
            if (!backupFile.DiskCheckedTime.HasValue) continue;

            var backupFileDate = backupFile.DiskCheckedTime.Value;
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
        if (!BackupFiles.Contains(backupFile)) return;

        _ = BackupFiles.Remove(backupFile);
    }

    public int GetTvEpisodeRuntime(string path, bool useCache = true)
    {
        if (!Utils.File.IsVideo(path)) return -1;

        var id = Utils.MediaHelper.GetTvdbInfo(path, out var seasonNumber, out var episodeNumber);

        // season 0 is the specials
        if (id == -1 || seasonNumber == 0) return -1;

        var compoundId = $"{id}:{seasonNumber}:{episodeNumber}";

        lock (lockObject)
        {
            var m = TmdbTvEpisodes.FirstOrDefault(x => x.Id == compoundId) ?? new TmdbItem(compoundId);
            if (m.Runtime > 0 && useCache) return m.Runtime;

            Utils.Log(BackupAction.ProcessFiles, $"Getting runtime from TMDB API for {id}, season {seasonNumber}, episode {episodeNumber} - {path}");
            var r = Utils.MediaHelper.GetTvEpisodeRuntimeFromTmdbApi(id, seasonNumber, episodeNumber);
            if (r == -1) return -1;

            m.Runtime = r;
            TmdbTvEpisodes.Add(m);
            return m.Runtime;
        }
    }

    /// <summary>
    ///     Returns the video file runtime
    /// </summary>
    /// <param name="path"></param>
    /// <returns>-1 if file not found, or it's not a video file</returns>
    internal int GetVideoRuntime(string path)
    {
        try
        {
            if (!Utils.File.Exists(path)) return -1;
            if (!Utils.File.IsVideo(path)) return -1;

            var file = Utils.MediaHelper.ExtendedBackupFileBase(path);
            if (file is not VideoBackupFileBase videoFile) return -1;
            if (videoFile.SpecialFeature != SpecialFeature.None) return -1;

            return videoFile is MovieBackupFile ? GetMovieRuntime(path) : GetTvEpisodeRuntime(path);
        }
        catch (FileNotFoundException)
        {
            return -1;
        }
    }

    public void RemoveTvShowFromCache(int tvdbId)
    {
        var key = $"{tvdbId}:";

        for (var i = TmdbTvEpisodes.Count - 1; i >= 0; i--)
        {
            var m = TmdbTvEpisodes[i];
            if (m.Id.StartsWithIgnoreCase(key)) _ = TmdbTvEpisodes.Remove(m);
        }
    }

    internal void SetTvShowRuntime(int tvdbId, int runtime)
    {
        var key = $"{tvdbId}:";

        foreach (var tvEp in TmdbTvEpisodes.Where(tvEp => tvEp.Id.StartsWithIgnoreCase(key)))
        {
            tvEp.Runtime = runtime;
        }
    }
}