// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="FileSystemWatcher.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Timers;

using BackupManager.Entities;
using BackupManager.Extensions;
using BackupManager.Properties;

namespace BackupManager;

internal sealed class FileSystemWatcher
{
    private const int NotifyFiltersValidMask = (int)(NotifyFilters.Attributes | NotifyFilters.CreationTime | NotifyFilters.DirectoryName |
                                                     NotifyFilters.FileName | NotifyFilters.LastAccess | NotifyFilters.LastWrite |
                                                     NotifyFilters.Security | NotifyFilters.Size);

    private readonly List<System.IO.FileSystemWatcher> watcherList = new();

    private string[] directories = Array.Empty<string>();

    private string filter = "*.*";

    private bool includeSubdirectories;

    private NotifyFilters notifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName;

    private int processChangesInterval = 30;

    private Timer processChangesTimer;

    private string regExFilter;

    private bool reset;

    private Timer scanFoldersTimer;

    private int scanInterval = 60;

    internal bool Running { get; private set; }

    /// <summary>
    ///     The paths to the directories we will monitor.
    /// </summary>
    internal string[] Directories
    {
        get => directories;

        set
        {
            if (directories == value) return;

            directories = value;
            Restart();
        }
    }

    /// <summary>
    ///     Minimum time in seconds since this directory or any items changed in the directory) was last changed before we will
    ///     raise any scan folders events. Default is 300 seconds.
    /// </summary>
    internal int MinimumAgeBeforeScanEventRaised { get; set; } = 300;

    /// <summary>
    ///     Time in seconds before we process the file system changes detected and put their directories in the
    ///     DirectoriesToScan collection.
    ///     many seconds. Default is 30 seconds.
    /// </summary>
    public int ProcessChangesInterval
    {
        get => processChangesInterval;

        set
        {
            if (processChangesInterval == value) return;

            processChangesInterval = value;
            if (processChangesTimer != null) processChangesTimer.Interval = value * 1000;
        }
    }

    /// <summary>
    ///     Interval in seconds between scan folder events being raised. Default is 60 seconds.
    /// </summary>
    public int ScanInterval
    {
        get => scanInterval;

        set
        {
            if (scanInterval == value) return;

            scanInterval = value;
            if (scanFoldersTimer != null) scanFoldersTimer.Interval = value * 1000;
        }
    }

    /// <summary>
    ///     This is a Collection of files/folders where changes have been detected and the last time they changed.
    /// </summary>
    internal BlockingCollection<FileSystemEntry> FileSystemChanges { get; } = new();

    /// <summary>
    ///     These are the directories we will raise events on when they are old enough.
    /// </summary>
    internal BlockingCollection<FileSystemEntry> DirectoriesToScan { get; } = new();

    /// <summary>
    ///     If you change these after starting then we stop and start again.
    /// </summary>
    [SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global")]
    [SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "<Pending>")]

    internal NotifyFilters NotifyFilter
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
        get => filter;

        set
        {
            if (filter == value) return;

            filter = value;
            Restart();
        }
    }

    /// <summary>
    ///     Gets or sets the RegexFilter string, used to determine what files are monitored in a directory. They match the
    ///     Filter first and then this.
    ///     Use .*(?&lt;!\.tmp)$ to match everything accept *.tmp files for example
    /// </summary>
    public string RegexFilter
    {
        get => regExFilter;

        set
        {
            if (regExFilter == value) return;

            regExFilter = value;
            Restart();
        }
    }

    /// <summary>
    ///     True to monitor subdirectories as well.
    /// </summary>
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
    ///     If we're already running then this Stops and Starts again. It won't start if its not already started.
    /// </summary>
    private void Restart()
    {
        if (!Running) return;

        Stop();
        Running = Start();
    }

    /// <summary>
    ///     Raised when the directories are ready to be scanned.
    /// </summary>
    public event EventHandler<FileSystemWatcherEventArgs> ReadyToScan;

    /// <summary>
    ///     Raised when an error occurs.
    /// </summary>
    public event EventHandler<ErrorEventArgs> Error;

    private static void CheckPathValidity(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        // Early check for directory parameter so that an exception can be thrown as early as possible
        if (path.Length == 0) throw new ArgumentException(string.Format(Resources.InvalidDirName, path), nameof(path));
        if (!Directory.Exists(path)) throw new ArgumentException(string.Format(Resources.InvalidDirNameNotExists, path), nameof(path));
    }

    /// <summary>
    ///     If any properties change then we need to call Reset.
    /// </summary>
    /// <returns>True if Reset was successful</returns>
    private bool Reset()
    {
        Utils.TraceIn();

        // Check current paths are valid
        foreach (var directory in Directories)
        {
            CheckPathValidity(directory);
        }
        RemoveFileSystemWatchers();

        foreach (var watcher in Directories.Select(directory => new System.IO.FileSystemWatcher(directory)
                 {
                     EnableRaisingEvents = true,
                     Filter = Filter,
                     IncludeSubdirectories = IncludeSubdirectories,
                     InternalBufferSize = 48 * 1024,
                     NotifyFilter = NotifyFilter
                 }))
        {
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
        processChangesTimer.Interval = ProcessChangesInterval * 1000;
        processChangesTimer.AutoReset = false;
        processChangesTimer.Enabled = true;

        if (scanFoldersTimer == null)
        {
            scanFoldersTimer = new Timer();
            scanFoldersTimer.Elapsed += ScanDirectoriesTimerElapsed;
        }
        scanFoldersTimer.Interval = ScanInterval * 1000;
        scanFoldersTimer.AutoReset = false;
        scanFoldersTimer.Enabled = true;
        return Utils.TraceOut(reset = true);
    }

    /// <summary>
    ///     Starts monitoring the directories for changes.
    /// </summary>
    /// <returns>True if the monitoring has started now or was running already</returns>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="ArgumentException">If any of the directories to monitor do not exist</exception>
    internal bool Start()
    {
        Utils.TraceIn();
        if (Running) return Utils.TraceOut(Running = true);

        // if we've not run reset before then do it now
        if (!reset)
        {
            if (!Reset())
            {
                // if reset fails set running=false and return
                return Utils.TraceOut(Running = false);
            }
        }
        else
        {
            // Check current paths are valid
            foreach (var directory in Directories)
            {
                CheckPathValidity(directory);
            }
        }

        foreach (var watcher in watcherList)
        {
            watcher.EnableRaisingEvents = true;
        }
        if (FileSystemChanges.Count > 0) processChangesTimer?.Start();
        if (DirectoriesToScan.Count > 0) scanFoldersTimer?.Start();
        return Utils.TraceOut(Running = true);
    }

    /// <summary>
    ///     Stops monitoring the directories for any more changes. Events no longer raised.
    /// </summary>
    /// <returns>True if we've stopped successfully or were already stopped</returns>
    public void Stop()
    {
        if (!Running) return;

        processChangesTimer?.Stop();
        scanFoldersTimer?.Stop();

        foreach (var watcher in watcherList)
        {
            watcher.EnableRaisingEvents = false;
        }
        Running = false;
        Utils.TraceOut(true);
    }

    /// <summary>
    ///     Executes when any changes to items in the monitored directories are detected.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void OnSomethingHappened(object sender, FileSystemEventArgs e)
    {
        Utils.TraceIn($"e.FullPath = {e.FullPath}");

        if (e.ChangeType is not WatcherChangeTypes.Created and not WatcherChangeTypes.Changed and not WatcherChangeTypes.Deleted
            and not WatcherChangeTypes.Renamed)
        {
            Utils.TraceOut("OnSomethingHappened exit as not created/changed/deleted/renamed");
            return;
        }

        // check the Regex to filter more
        if (!RegexFilter.HasValue() || Regex.IsMatch(e.FullPath, RegexFilter))
        {
            var entry = new FileSystemEntry(e.FullPath, DateTime.Now);

            if (FileSystemChanges.Contains(entry))
            {
                Utils.Trace("Updating ModifiedTime");
                var fileSystemEntry = FileSystemChanges.First(f => f.Path == e.FullPath);
                fileSystemEntry.ModifiedDateTime = DateTime.Now;
            }
            else
            {
                Utils.Trace("Adding");
                FileSystemChanges.Add(new FileSystemEntry(e.FullPath, DateTime.Now));
            }

            // As soon as there's something changed we start our event timers
            StartTimers();
        }
        Utils.TraceOut();
    }

    private void StartTimers()
    {
        Utils.TraceIn();
        processChangesTimer.Start();
        scanFoldersTimer.Start();
        Utils.TraceOut();
    }

    private void ScanDirectoriesTimerElapsed(object sender, ElapsedEventArgs e)
    {
        Utils.TraceIn();

        if (DirectoriesToScan.Count > 0 &&
            DirectoriesToScan.Count(folderToScan => folderToScan.ModifiedDateTime.AddSeconds(MinimumAgeBeforeScanEventRaised) < DateTime.Now) ==
            DirectoriesToScan.Count)
        {
            // All the folders are old enough so raise the ReadyToScan event
            var args = new FileSystemWatcherEventArgs(DirectoriesToScan);
            var handler = ReadyToScan;
            handler?.Invoke(this, args);
        }
        if (DirectoriesToScan.Count > 0) scanFoldersTimer.Start();
        Utils.TraceOut();
    }

    /// <summary>
    ///     Processes the FileSystemEntries of changes. Any new directories are added and any we've already added have their
    ///     Modified DateTime updated.
    /// </summary>
    /// <param name="source"></param>
    /// <param name="e"></param>
    private void ProcessChangesTimerElapsed(object source, ElapsedEventArgs e)
    {
        Utils.TraceIn();

        // every few seconds we move through the changes List and put the folders we need to check in our other list
        if (FileSystemChanges.Count == 0)
        {
            Utils.TraceOut();
            return;
        }

        foreach (var fileOrFolderChange in FileSystemChanges.GetConsumingEnumerable())
        {
            Utils.Trace($"fileOrFolderChange.Path = {fileOrFolderChange.Path}");

            // What about deleted files and folders?
            // Check to see if the path exists as a file 
            // if it does use the parent folder
            // if its not an existing file check its a folder
            // if it is use its full path
            // if its not a folder either then use its full path and its parent
            List<FileSystemEntry> foldersToScan = new();

            if (File.Exists(fileOrFolderChange.Path) || !Directory.Exists(fileOrFolderChange.Path))
                foldersToScan.Add(new FileSystemEntry(new FileInfo(fileOrFolderChange.Path).DirectoryName));
            else if (Directory.Exists(fileOrFolderChange.Path) || !File.Exists(fileOrFolderChange.Path))
                foldersToScan.Add(new FileSystemEntry(fileOrFolderChange.Path));

            foreach (var folderToScan in foldersToScan)
            {
                Utils.Trace($"folderToScan = {folderToScan}");

                if (DirectoriesToScan.Any(f => f.Path == folderToScan.Path))
                {
                    var fileSystemEntry = DirectoriesToScan.First(f => f.Path == folderToScan.Path);
                    if (fileOrFolderChange.ModifiedDateTime <= fileSystemEntry.ModifiedDateTime) continue;

                    Utils.Trace($"Updating ModifiedDateTime to {fileOrFolderChange.ModifiedDateTime}");
                    fileSystemEntry.ModifiedDateTime = fileOrFolderChange.ModifiedDateTime;
                }
                else
                {
                    Utils.Trace("Adding to collection");
                    DirectoriesToScan.Add(new FileSystemEntry(folderToScan.Path, fileOrFolderChange.ModifiedDateTime));
                }
            }
        }
        if (DirectoriesToScan.Count > 0) scanFoldersTimer.Start();
        Utils.Trace($"FileSystemChanges.Count = {FileSystemChanges.Count}");
        Utils.Trace($"DirectoriesToScan.Count = {DirectoriesToScan.Count}");
        Utils.TraceOut();
    }

    /// <summary>
    ///     Calls Dispose on all watchers we have and clears the list.
    /// </summary>
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

    /// <summary>
    ///     Any errors detected calls this.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void OnError(object sender, ErrorEventArgs e)
    {
        if (sender is not System.IO.FileSystemWatcher watcher) return;

        var watcherPath = watcher.Path;

        if (!Directory.Exists(watcherPath))
        {
            var ex = new DirectoryNotFoundException(string.Format(Resources.DirectoryNotFound, watcherPath), e.GetException());
            e = new ErrorEventArgs(ex);
        }

        // Stop stops the timers and disables the watchers
        Stop();
        var handler = Error;
        handler?.Invoke(watcher, e);
    }
}

/// <summary>
///     Used to pass the Directories changed to any events.
/// </summary>
internal sealed class FileSystemWatcherEventArgs : EventArgs
{
    internal FileSystemWatcherEventArgs(BlockingCollection<FileSystemEntry> directoriesToScan)
    {
        Utils.TraceIn();
        Directories = new FileSystemEntry[directoriesToScan.Count];
        directoriesToScan.CopyTo(Directories, 0);

        // Empty the DirectoriesToScan because we've copied it into the array to return
        while (directoriesToScan.TryTake(out _)) { }
        Utils.TraceOut();
    }

    /// <summary>
    ///     An Array of FileSystemEntry that have been changed
    /// </summary>
    internal FileSystemEntry[] Directories { get; }
}