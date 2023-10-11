// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="BackupFileSystemWatcher.cs" company="Andy Reeves">
//
//  </copyright>
//  --------------------------------------------------------------------------------------------------------------------

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Timers;

using BackupManager.Entities;

namespace BackupManager;

public class BackupFileSystemWatcher
{
    private static Timer _processChangesTimer;

    private static Timer _scanFoldersTimer;

    private static int _minimumAgeBeforeScanning;

    private static readonly List<FileSystemWatcher> _watcherList = new();

    private string filter;

    /// <summary>
    ///     The folder paths we will monitor
    /// </summary>
    public string[] FoldersToMonitor;

    /// <summary>
    ///     Minimum time in seconds since this folder was last changed before we will raise any scan folders events
    /// </summary>
    public int MinimumAgeBeforeScanning;

    /// <summary>
    ///     Time in seconds before we process the files changed to see if they match the RegExs into FilesToMatch. We do this
    ///     because they list might be very large and the
    ///     RegExs may take a while. So we capture them all. Then wait for this long. Then RegEx them. Finally we check this
    ///     Collection to see if we should scan them.
    ///     When they are old enough we raise the events.
    /// </summary>
    public int ProcessChangesTimer;

    /// <summary>
    ///     Time in seconds before we raise any scan folders events
    /// </summary>
    public int ScanTimer;

    /// <summary>
    ///     This is a Dictionary of files/folders where changes have been detected and the last time they changed
    /// </summary>
    internal static BlockingCollection<Folder> FileOrFolderChanges { get; } = new();

    /// <summary>
    ///     These are the folders we will raise events on when they are old enough
    /// </summary>
    public static BlockingCollection<Folder> FoldersToScan { get; set; } = new();

    public NotifyFilters NotifyFilter { get; set; }

    public string Filter
    {
        get => filter ??= "*";

        set => filter = value;
    }

    public bool IncludeSubdirectories { get; set; }

    public bool EnableRaisingEvents { get; set; }

    public static event EventHandler<BackupFileSystemWatcherEventArgs> ReadyToScan;

    public static event EventHandler<ErrorEventArgs> Error;

    public bool Start()
    {
        return CreateFileSystemWatchers();
    }

    public bool Stop()
    {
        _scanFoldersTimer.Stop();
        _processChangesTimer.Stop();
        ResetCollections();
        return RemoveFileSystemWatchers();
    }

    private static void ResetCollections()
    {
        while (FileOrFolderChanges.TryTake(out _)) { }

        while (FoldersToScan.TryTake(out _)) { }
    }

    private static void OnSomethingHappened(object sender, FileSystemEventArgs e)
    {
        Utils.TraceIn($"e.FullPath = {e.FullPath}");

        if (e.ChangeType is not WatcherChangeTypes.Created and not WatcherChangeTypes.Changed and not WatcherChangeTypes.Deleted
            and not WatcherChangeTypes.Renamed)
        {
            Utils.TraceOut("OnSomethingHappened exit as not created/changed/deleted/renamed");
            return;
        }

        // add this changed folder/file to the list to potentially scan
        FileOrFolderChanges.Add(new Folder(e.FullPath, DateTime.Now));
        Utils.TraceOut();
    }

    private bool CreateFileSystemWatchers()
    {
        Utils.TraceIn();

        foreach (var folder in FoldersToMonitor)
        {
            if (folder == null) throw new ArgumentNullException();
            if (!Directory.Exists(folder)) throw new ArgumentException($"Folder {folder} does nor exist");

            FileSystemWatcher watcher = new(folder) { NotifyFilter = NotifyFilter };
            watcher.Changed += OnSomethingHappened;
            watcher.Deleted += OnSomethingHappened;
            watcher.Renamed += OnSomethingHappened;
            watcher.Created += OnSomethingHappened;
            watcher.Error += OnError;
            watcher.Filter = Filter;
            watcher.IncludeSubdirectories = IncludeSubdirectories;
            watcher.EnableRaisingEvents = EnableRaisingEvents;
            _watcherList.Add(watcher);
        }
        _minimumAgeBeforeScanning = MinimumAgeBeforeScanning;

        // Create the timers
        _processChangesTimer = new Timer(ProcessChangesTimer * 1000);
        _processChangesTimer.Elapsed += ProcessChangesTimerElapsed;
        _processChangesTimer.AutoReset = false;
        _processChangesTimer.Enabled = true;
        _scanFoldersTimer = new Timer(ScanTimer * 1000);
        _scanFoldersTimer.Elapsed += ScanFoldersTimerElapsed;
        _scanFoldersTimer.AutoReset = false;
        _scanFoldersTimer.Enabled = true;
        return Utils.TraceOut(true);
    }

    private static void ScanFoldersTimerElapsed(object sender, ElapsedEventArgs e)
    {
        Utils.TraceIn();

        if (FoldersToScan.Count > 0 && FoldersToScan.Count(folderToScan => folderToScan.ModifiedDateTime.AddSeconds(_minimumAgeBeforeScanning) < DateTime.Now) ==
            FoldersToScan.Count)
        {
            // All the folders are old enough so raise the ReadyToScan event
            var args = new BackupFileSystemWatcherEventArgs(FoldersToScan);
            OnThresholdReached(args);
        }
        _scanFoldersTimer.Start();
        Utils.TraceOut();
    }

    protected static void OnThresholdReached(BackupFileSystemWatcherEventArgs e)
    {
        Utils.TraceIn();
        FoldersToScan = new BlockingCollection<Folder>();
        var handler = ReadyToScan;
        handler?.Invoke(null, e);
        Utils.TraceOut();
    }

    /// <summary>
    ///     Static to prevent multiple threads blocking each other
    /// </summary>
    /// <param name="source"></param>
    /// <param name="e"></param>
    private static void ProcessChangesTimerElapsed(object source, ElapsedEventArgs e)
    {
        Utils.TraceIn();

        // every few seconds we move through the changes List and put the folders we need to check in our other list
        if (FileOrFolderChanges.Count == 0)
        {
            _processChangesTimer.Start();
            Utils.TraceOut();
            return;
        }

        foreach (var fileOrFolderChange in FileOrFolderChanges.GetConsumingEnumerable())
        {
            Utils.Trace($"path = {fileOrFolderChange.Path}");

            // What about deleted files and folders?
            // Check to see if the path exists as a file 
            // if it does use the parent folder
            // if its not an existing file check its a folder
            // if it is use its full path
            // if its not a folder either then use its full path and its parent
            List<Folder> foldersToScan = new();

            if (File.Exists(fileOrFolderChange.Path) || !Directory.Exists(fileOrFolderChange.Path))
                foldersToScan.Add(new Folder(new FileInfo(fileOrFolderChange.Path).DirectoryName));
            else if (Directory.Exists(fileOrFolderChange.Path) || !File.Exists(fileOrFolderChange.Path)) foldersToScan.Add(new Folder(fileOrFolderChange.Path));

            foreach (var folderToScan in foldersToScan)
            {
                if (FoldersToScan.Any(f => f.Path == folderToScan.Path))
                {
                    var scannedFolder = FoldersToScan.First(f => f.Path == folderToScan.Path);

                    if (fileOrFolderChange.ModifiedDateTime > scannedFolder.ModifiedDateTime)
                        scannedFolder.ModifiedDateTime = fileOrFolderChange.ModifiedDateTime;
                }
                else
                    FoldersToScan.Add(new Folder(folderToScan.Path, fileOrFolderChange.ModifiedDateTime));
            }
        }
        _processChangesTimer.Start();
        Utils.TraceOut();
    }

    private static bool RemoveFileSystemWatchers()
    {
        Utils.TraceIn();

        foreach (var watcher in _watcherList)
        {
            watcher.Dispose();
        }
        _watcherList.Clear();
        return Utils.TraceOut(true);
    }

    private static void OnError(object sender, ErrorEventArgs e)
    {
        //BackupFileSystemWatcher has an exception. Typically a monitored folder cannot be accessed
        RemoveFileSystemWatchers();
        _scanFoldersTimer.Stop();
        _processChangesTimer.Stop();
        var handler = Error;
        handler?.Invoke(null, e);
    }
}

public class BackupFileSystemWatcherEventArgs : EventArgs
{
    public BackupFileSystemWatcherEventArgs(BlockingCollection<Folder> foldersToScan)
    {
        Utils.TraceIn();
        Folders = new Folder[foldersToScan.Count];
        foldersToScan.CopyTo(Folders, 0);
        Utils.TraceOut();
    }

    public Folder[] Folders { get; set; }
}