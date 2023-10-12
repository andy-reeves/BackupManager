// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="BackupFileSystemWatcher.cs" company="Andy Reeves">
//
//  </copyright>
//  --------------------------------------------------------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

    // Filters collection
    private readonly NormalizedFilterCollection filters = new();

    private readonly List<FileSystemWatcher> watcherList = new();

    private string[] foldersToMonitor = Array.Empty<string>();

    private bool includeSubdirectories;

    private NotifyFilters notifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName;

    private Timer processChangesTimer;

    private bool reset;

    private Timer scanFoldersTimer;

    private bool started;

    /// <summary>
    ///     The folder paths we will monitor
    /// </summary>
    public string[] FoldersToMonitor
    {
        get => foldersToMonitor;

        set
        {
            if (foldersToMonitor == value) return;

            foldersToMonitor = value;
            Restart();
        }
    }

    /// <summary>
    ///     Minimum time in seconds since this folder was last changed before we will raise any scan folders events. Default is
    ///     300 seconds
    /// </summary>
    public int MinimumAgeBeforeScanning { get; set; } = 300;

    /// <summary>
    ///     Time in seconds before we process the files changed to see if they match the RegExs into FilesToMatch. We do this
    ///     because they list might be very large and the RegExs may take a while. So we capture them all. Then wait for this
    ///     many seconds. Then RegEx them. Finally we check the Collection to see if we should scan them. When they are old
    ///     enough we raise the events. Default is 30 seconds
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
    public BlockingCollection<Folder> FoldersToScan { get; } = new();

    /// <summary>
    ///     If you change these after starting then we stop and start again
    /// </summary>
    public NotifyFilters NotifyFilter
    {
        get => notifyFilter;

        set
        {
            if (((int)value & ~NotifyFiltersValidMask) != 0)
                throw new ArgumentException(string.Format(Resources.InvalidEnumArgument, nameof(value), (int)value, nameof(NotifyFilters)));

            if (notifyFilter == value) return;

            notifyFilter = value;
            Restart();
        }
    }

    /// <summary>
    ///     Gets or sets the filter string, used to determine what files are monitored in a directory.
    /// </summary>
    public string Filter
    {
        get => Filters.Count == 0 ? "*" : Filters[0];

        set
        {
            Filters.Clear();
            Filters.Add(value);
            Restart();
        }
    }

    public Collection<string> Filters => filters;

    public bool IncludeSubdirectories
    {
        get => includeSubdirectories;

        set
        {
            if (includeSubdirectories == value) return;

            includeSubdirectories = value;
            Restart();
        }
    }

    /// <summary>
    ///     If we're already running then this Stops and Starts again. It won't start if its not already started
    /// </summary>
    private void Restart()
    {
        if (!started) return;

        Stop();
        started = Start();
    }

    public event EventHandler<BackupFileSystemWatcherEventArgs> ReadyToScan;

    public event EventHandler<ErrorEventArgs> Error;

    private static void CheckPathValidity(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        // Early check for directory parameter so that an exception can be thrown as early as possible.
        if (path.Length == 0) throw new ArgumentException(string.Format(Resources.InvalidDirName, path), nameof(path));
        if (!Directory.Exists(path)) throw new ArgumentException(string.Format(Resources.InvalidDirName_NotExists, path), nameof(path));
    }

    /// <summary>
    ///     If any properties change then we need to call Reset
    /// </summary>
    /// <returns></returns>
    private bool Reset()
    {
        Utils.TraceIn();

        // Check current paths are valid
        foreach (var folder in FoldersToMonitor)
        {
            CheckPathValidity(folder);
        }
        RemoveFileSystemWatchers();

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

        // Setup the timers
        if (processChangesTimer == null)
        {
            processChangesTimer = new Timer();
            processChangesTimer.Elapsed += ProcessChangesTimerElapsed;
        }
        processChangesTimer.Interval = ProcessChangesTimer * 1000;
        processChangesTimer.AutoReset = false;
        processChangesTimer.Enabled = true;

        if (scanFoldersTimer == null)
        {
            scanFoldersTimer = new Timer();
            scanFoldersTimer.Elapsed += ScanFoldersTimerElapsed;
        }
        scanFoldersTimer.Interval = ScanTimer * 1000;
        scanFoldersTimer.AutoReset = false;
        scanFoldersTimer.Enabled = true;
        return Utils.TraceOut(reset = true);
    }

    /// <summary>
    ///     Starts monitoring the folders for changes.
    /// </summary>
    /// <returns>True if the monitoring has started now or was running already</returns>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="ArgumentException">If any of the folders to monitor do not exist</exception>
    public bool Start()
    {
        Utils.TraceIn();
        if (started) return true;

        if (!reset) Reset();

        // Check current paths are valid
        foreach (var folder in FoldersToMonitor)
        {
            CheckPathValidity(folder);
        }

        foreach (var watcher in watcherList)
        {
            watcher.EnableRaisingEvents = true;
        }
        processChangesTimer?.Start();
        scanFoldersTimer?.Start();
        return Utils.TraceOut(started = true);
    }

    /// <summary>
    ///     Stops monitoring the folders for any more changes. Events no longer raised
    /// </summary>
    /// <returns>True if we've stopped successfully or were already stopped</returns>
#pragma warning disable CA1822 // Mark members as static
    public bool Stop()
#pragma warning restore CA1822 // Mark members as static
    {
        if (!started) return true;

        processChangesTimer?.Stop();
        scanFoldersTimer?.Stop();

        foreach (var watcher in watcherList)
        {
            watcher.EnableRaisingEvents = false;
        }
        started = false;
        return Utils.TraceOut(true);
    }

    /// <summary>
    ///     Clears the two collections of files and folders
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

        // Empty the FoldersToScan
        while (FoldersToScan.TryTake(out _)) { }
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
            if (started) processChangesTimer.Start();
            Utils.TraceOut();
            return;
        }

        foreach (var fileOrFolderChange in FileOrFolderChanges.GetConsumingEnumerable())
        {
            Utils.Trace($"fileOrFolderChange.Path = {fileOrFolderChange.Path}");

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
        if (started) processChangesTimer.Start();
        Utils.TraceOut();
    }

    private void RemoveFileSystemWatchers()
    {
        Utils.TraceIn();

        foreach (var watcher in watcherList)
        {
            watcher.Dispose();
        }
        watcherList.Clear();
        Utils.TraceOut();
    }

    private void OnError(object sender, ErrorEventArgs e)
    {
        if (sender is not FileSystemWatcher watcher) return;

        var watcherPath = watcher.Path;

        if (!Directory.Exists(watcherPath))
        {
            var ex = new DirectoryNotFoundException($"Directory {watcherPath} not found.", e.GetException());
            e = new ErrorEventArgs(ex);
        }

        // Stop stops the timers and disables the watchers
        Stop();
        var handler = Error;
        handler?.Invoke(watcher, e);
    }

    private sealed class NormalizedFilterCollection : Collection<string>
    {
        internal NormalizedFilterCollection() : base(new ImmutableStringList()) { }

        protected override void InsertItem(int index, string item)
        {
            base.InsertItem(index, string.IsNullOrEmpty(item) || item == "*.*" ? "*" : item);
        }

        protected override void SetItem(int index, string item)
        {
            base.SetItem(index, string.IsNullOrEmpty(item) || item == "*.*" ? "*" : item);
        }

        internal string[] GetFilters()
        {
            return ((ImmutableStringList)Items).Items;
        }

        /// <summary>
        ///     List that maintains its underlying data in an immutable array, such that the list
        ///     will never modify an array returned from its Items property. This is to allow
        ///     the array to be enumerated safely while another thread might be concurrently mutating
        ///     the collection.
        /// </summary>
        private sealed class ImmutableStringList : IList<string>
        {
            public string[] Items = Array.Empty<string>();

            public string this[int index]
            {
                get
                {
                    var items = Items;
                    if ((uint)index >= (uint)items.Length) throw new ArgumentOutOfRangeException(nameof(index));

                    return items[index];
                }

                set
                {
                    var clone = (string[])Items.Clone();
                    clone[index] = value;
                    Items = clone;
                }
            }

            public int Count => Items.Length;

            public bool IsReadOnly => false;

            public void Add(string item)
            {
                // Collection<T> doesn't use this method.
                throw new NotSupportedException();
            }

            public void Clear()
            {
                Items = Array.Empty<string>();
            }

            public bool Contains(string item)
            {
                return Array.IndexOf(Items, item) != -1;
            }

            public void CopyTo(string[] array, int arrayIndex)
            {
                Items.CopyTo(array, arrayIndex);
            }

            public IEnumerator<string> GetEnumerator()
            {
                return ((IEnumerable<string>)Items).GetEnumerator();
            }

            public int IndexOf(string item)
            {
                return Array.IndexOf(Items, item);
            }

            public void Insert(int index, string item)
            {
                var items = Items;
                var newItems = new string[items.Length + 1];
                items.AsSpan(0, index).CopyTo(newItems);
                items.AsSpan(index).CopyTo(newItems.AsSpan(index + 1));
                newItems[index] = item;
                Items = newItems;
            }

            public bool Remove(string item)
            {
                // Collection<T> doesn't use this method.
                throw new NotSupportedException();
            }

            public void RemoveAt(int index)
            {
                var items = Items;
                var newItems = new string[items.Length - 1];
                items.AsSpan(0, index).CopyTo(newItems);
                items.AsSpan(index + 1).CopyTo(newItems.AsSpan(index));
                Items = newItems;
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }
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