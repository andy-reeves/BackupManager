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
using BackupManager.Properties;

namespace BackupManager;

public class BackupFileSystemWatcher
{
    private const int NotifyFiltersValidMask = (int)(NotifyFilters.Attributes | NotifyFilters.CreationTime | NotifyFilters.DirectoryName |
                                                     NotifyFilters.FileName | NotifyFilters.LastAccess | NotifyFilters.LastWrite | NotifyFilters.Security |
                                                     NotifyFilters.Size);

    private readonly List<FileSystemWatcher> watcherList = new();

    private NotifyFilters notifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName;

    private Timer processChangesTimer;

    private Timer scanFoldersTimer;

    /// <summary>
    ///     The folder paths we will monitor
    /// </summary>
    public string[] FoldersToMonitor { get; set; } = Array.Empty<string>();

    /// <summary>
    ///     Minimum time in seconds since this folder was last changed before we will raise any scan folders events. Default is
    ///     300 seconds
    /// </summary>
    public int MinimumAgeBeforeScanning { get; set; } = 300;

    /// <summary>
    ///     Time in seconds before we process the files changed to see if they match the RegExs into FilesToMatch. We do this
    ///     because they list might be very large and the
    ///     RegExs may take a while. So we capture them all. Then wait for this long. Then RegEx them. Finally we check this
    ///     Collection to see if we should scan them.
    ///     When they are old enough we raise the events.
    ///     Default is 30 seconds
    /// </summary>
    public int ProcessChangesTimer { get; set; } = 30;

    /// <summary>
    ///     Time in seconds before we raise any scan folders events. Default is 60 seconds
    /// </summary>
    public int ScanTimer { get; set; } = 60;

    /// <summary>
    ///     This is a Collection of files/folders where changes have been detected and the last time they changed
    /// </summary>
    internal BlockingCollection<Folder> FileOrFolderChanges { get; } = new();

    /// <summary>
    ///     These are the folders we will raise events on when they are old enough
    /// </summary>
    public BlockingCollection<Folder> FoldersToScan { get; set; } = new();

    public NotifyFilters NotifyFilter
    {
        get => notifyFilter;

        set
        {
            if (((int)value & ~NotifyFiltersValidMask) != 0)
                throw new ArgumentException(string.Format(Resources.InvalidEnumArgument, nameof(value), (int)value, nameof(NotifyFilters)));

            if (notifyFilter == value) return;

            notifyFilter = value;
            Stop();
            Start();
        }
    }

    public string Filter { get; set; } = "*";

    public bool IncludeSubdirectories { get; set; }

    public event EventHandler<BackupFileSystemWatcherEventArgs> ReadyToScan;

    public event EventHandler<ErrorEventArgs> Error;

    private void CheckPathValidity(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        // Early check for directory parameter so that an exception can be thrown as early as possible.
        if (path.Length == 0) throw new ArgumentException(string.Format(Resources.InvalidDirName, path), nameof(path));
        if (!Directory.Exists(path)) throw new ArgumentException(string.Format(Resources.InvalidDirName_NotExists, path), nameof(path));
    }

    /// <summary>
    ///     Starts monitoring the folders for changes.
    /// </summary>
    /// <returns>True if the monitoring has started</returns>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="ArgumentException">If any of the folders to monitor do not exist</exception>
    public bool Start()
    {
        Utils.TraceIn();

        // call Stop to remove all the old watchers
        Stop();

        // Check current paths are valid
        foreach (var folder in FoldersToMonitor)
        {
            CheckPathValidity(folder);
        }

        foreach (var folder in FoldersToMonitor)
        {
            FileSystemWatcher watcher = new(folder)
            {
                EnableRaisingEvents = true,
                Filter = Filter,
                IncludeSubdirectories = IncludeSubdirectories,
                InternalBufferSize = 48 * 1024,
                NotifyFilter = NotifyFilter
            };
            watcher.Error += OnError;
            watcher.Changed += OnSomethingHappened;
            watcher.Deleted += OnSomethingHappened;
            watcher.Renamed += OnSomethingHappened;
            watcher.Created += OnSomethingHappened;
            watcherList.Add(watcher);
        }

        //MinimumAgeBeforeScanning = MinimumAgeBeforeScanning;

        // Create the timers
        processChangesTimer = new Timer(ProcessChangesTimer * 1000);
        processChangesTimer.Elapsed += ProcessChangesTimerElapsed;
        processChangesTimer.AutoReset = false;
        processChangesTimer.Enabled = true;
        scanFoldersTimer = new Timer(ScanTimer * 1000);
        scanFoldersTimer.Elapsed += ScanFoldersTimerElapsed;
        scanFoldersTimer.AutoReset = false;
        scanFoldersTimer.Enabled = true;
        return Utils.TraceOut(true);
    }

    /// <summary>
    ///     Stops monitoring the folders for any more changes. Events no longer raised
    /// </summary>
    /// <returns></returns>
#pragma warning disable CA1822 // Mark members as static
    public bool Stop()
#pragma warning restore CA1822 // Mark members as static
    {
        processChangesTimer?.Stop();
        scanFoldersTimer?.Stop();
        RemoveFileSystemWatchers();
        return Utils.TraceOut(true);
    }

    /// <summary>
    ///     Clears the two static collections of files and folders
    /// </summary>
    /// <returns>True if they were cleared correctly</returns>
    public bool ResetFolderCollections()
    {
        Utils.TraceIn();

        while (FileOrFolderChanges.TryTake(out _)) { }

        while (FoldersToScan.TryTake(out _)) { }
        return Utils.TraceOut(true);
    }

    private void OnSomethingHappened(object sender, FileSystemEventArgs e)
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

    private void ScanFoldersTimerElapsed(object sender, ElapsedEventArgs e)
    {
        Utils.TraceIn();

        if (FoldersToScan.Count > 0 && FoldersToScan.Count(folderToScan => folderToScan.ModifiedDateTime.AddSeconds(MinimumAgeBeforeScanning) < DateTime.Now) ==
            FoldersToScan.Count)
        {
            // All the folders are old enough so raise the ReadyToScan event
            var args = new BackupFileSystemWatcherEventArgs(FoldersToScan);
            OnThresholdReached(this, args);
        }
        scanFoldersTimer.Start();
        Utils.TraceOut();
    }

    protected void OnThresholdReached(object sender, BackupFileSystemWatcherEventArgs e)
    {
        Utils.TraceIn();
        FoldersToScan = new BlockingCollection<Folder>();
        var handler = ReadyToScan;
        handler?.Invoke(sender, e);
        Utils.TraceOut();
    }

    /// <summary>
    ///     Static to prevent multiple threads blocking each other
    /// </summary>
    /// <param name="source"></param>
    /// <param name="e"></param>
    private void ProcessChangesTimerElapsed(object source, ElapsedEventArgs e)
    {
        Utils.TraceIn();

        // every few seconds we move through the changes List and put the folders we need to check in our other list
        if (FileOrFolderChanges.Count == 0)
        {
            processChangesTimer.Start();
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
                Utils.Trace($"folderToScan = {folderToScan}");

                if (FoldersToScan.Any(f => f.Path == folderToScan.Path))
                {
                    var scannedFolder = FoldersToScan.First(f => f.Path == folderToScan.Path);
                    if (fileOrFolderChange.ModifiedDateTime <= scannedFolder.ModifiedDateTime) continue;

                    Utils.Trace($"Updating ModifiedDateTime to {fileOrFolderChange.ModifiedDateTime}");
                    scannedFolder.ModifiedDateTime = fileOrFolderChange.ModifiedDateTime;
                }
                else
                {
                    Utils.Trace("Adding to collection");
                    FoldersToScan.Add(new Folder(folderToScan.Path, fileOrFolderChange.ModifiedDateTime));
                }
            }
        }
        processChangesTimer.Start();
        Utils.TraceOut();
    }

    private bool RemoveFileSystemWatchers()
    {
        Utils.TraceIn();

        foreach (var watcher in watcherList)
        {
            watcher.Dispose();
        }
        watcherList.Clear();
        return Utils.TraceOut(true);
    }

    private void OnError(object sender, ErrorEventArgs e)
    {
        if (sender is not FileSystemWatcher a) return;

        var b = a.Path;

        if (!Directory.Exists(b))
        {
            var ex = new DirectoryNotFoundException($"Directory {b} not found.", e.GetException());
            e = new ErrorEventArgs(ex);
        }
        else
        {
            // not sure what the error was so just throw it
            RemoveFileSystemWatchers();
        }
        scanFoldersTimer.Stop();
        processChangesTimer.Stop();
        var handler = Error;
        handler?.Invoke(a, e);
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

    /// <summary>
    ///     An Array of Folders that have been changed
    /// </summary>
    public Folder[] Folders { get; }
}