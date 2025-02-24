﻿// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="FileSystemWatcher.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Timers;

using BackupManager.Entities;
using BackupManager.Extensions;
using BackupManager.Properties;

namespace BackupManager;

internal sealed class FileSystemWatcher
{
    private const int NOTIFY_FILTERS_VALID_MASK = (int)(NotifyFilters.Attributes | NotifyFilters.CreationTime | NotifyFilters.DirectoryName | NotifyFilters.FileName |
                                                        NotifyFilters.LastAccess | NotifyFilters.LastWrite | NotifyFilters.Security | NotifyFilters.Size);

    private static readonly object _lock = new();

    private readonly List<System.IO.FileSystemWatcher> watcherList = [];

    private string[] directories = [];

    private string filter = "*.*";

    private bool includeSubdirectories;

    private NotifyFilters notifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName;

    private int processChangesInterval = 30_000;

    private Timer processChangesTimer;

    private string regExFilter;

    private bool reset;

    private Timer scanDirectoriesTimer;

    private int scanInterval = 60_000;

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
    ///     Minimum time in milliseconds since this directory (or any items changed in the directory) was last changed before
    ///     we
    ///     will raise any scan directories events. Default is 300 seconds.
    /// </summary>
    internal int MinimumAgeBeforeScanEventRaised { get; set; } = 300_000;

    /// <summary>
    ///     Time in milliseconds before we process the file system changes detected and put their directories in the
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
            if (processChangesTimer != null) processChangesTimer.Interval = value;
        }
    }

    /// <summary>
    ///     Interval in milliseconds between scan directory events being raised. Default is 60 seconds.
    /// </summary>
    public int ScanInterval
    {
        get => scanInterval;

        set
        {
            if (scanInterval == value) return;

            scanInterval = value;
            if (scanDirectoriesTimer != null) scanDirectoriesTimer.Interval = value;
        }
    }

    /// <summary>
    ///     This is a Collection of files/directories where changes have been detected and the last time they changed.
    /// </summary>
    internal ConcurrentSet<FileSystemEntry> FileSystemChanges { get; } = [];

    /// <summary>
    ///     These are the directories we will raise events on when they are old enough.
    /// </summary>
    internal ConcurrentSet<FileSystemEntry> DirectoriesToScan { get; } = [];

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
            if (((int)value & ~NOTIFY_FILTERS_VALID_MASK) != 0)
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

        _ = Stop();
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
        if (path.Length == 0) throw new ArgumentException(string.Format(Resources.DirectoryNotFound3, path), nameof(path));
        if (!Directory.Exists(path)) throw new ArgumentException(string.Format(Resources.DirectoryNotFound2, path), nameof(path));
    }

    /// <summary>
    ///     If any properties change then we need to call Reset.
    /// </summary>
    /// <returns>True if Reset was successful</returns>
    public bool Reset()
    {
        Utils.TraceIn();

        // Check current paths are valid
        foreach (var directory in Directories)
        {
            CheckPathValidity(directory);
        }
        RemoveFileSystemWatchers();
        var sw = Stopwatch.StartNew();

        foreach (var watcher in Directories.Select(directory => new System.IO.FileSystemWatcher(directory)
                 {
                     EnableRaisingEvents = true,
                     Filter = Filter,
                     IncludeSubdirectories = IncludeSubdirectories,
                     InternalBufferSize = 64 * Utils.BYTES_IN_ONE_KILOBYTE,
                     NotifyFilter = NotifyFilter
                 }))
        {
            watcher.Error += OnError;
            watcher.Changed += (_, e) => Task.Run(() => OnSomethingHappened(e));
            watcher.Deleted += (_, e) => Task.Run(() => OnSomethingHappened(e));
            watcher.Renamed += (_, e) => Task.Run(() => OnSomethingHappened(e));
            watcher.Created += (_, e) => Task.Run(() => OnSomethingHappened(e));
            watcherList.Add(watcher);
        }
        Utils.Trace($"Creating FSW took {sw.Elapsed}");

        // Set up the timers
        if (processChangesTimer == null)
        {
            processChangesTimer = new Timer();
            processChangesTimer.Elapsed += ProcessChangesTimerElapsed;
        }
        processChangesTimer.Interval = ProcessChangesInterval;
        processChangesTimer.AutoReset = false;
        processChangesTimer.Enabled = true;

        if (scanDirectoriesTimer == null)
        {
            scanDirectoriesTimer = new Timer();
            scanDirectoriesTimer.Elapsed += ScanDirectoriesTimerElapsed;
        }
        scanDirectoriesTimer.Interval = ScanInterval;
        scanDirectoriesTimer.AutoReset = false;
        scanDirectoriesTimer.Enabled = true;
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
        if (DirectoriesToScan.Count > 0) scanDirectoriesTimer?.Start();
        return Utils.TraceOut(Running = true);
    }

    /// <summary>
    ///     Stops monitoring the directories for any more changes. Events no longer raised.
    /// </summary>
    /// <returns>True if we've stopped successfully or were already stopped</returns>
    public bool Stop()
    {
        if (!Running) return true;

        processChangesTimer?.Stop();
        scanDirectoriesTimer?.Stop();

        foreach (var watcher in watcherList)
        {
            watcher.EnableRaisingEvents = false;
        }
        Running = false;
        return Utils.TraceOut(true);
    }

    /// <summary>
    ///     Executes when any changes to items in the monitored directories are detected.
    /// </summary>
    /// <param name="e"></param>
    private void OnSomethingHappened(FileSystemEventArgs e)
    {
        if (e.ChangeType is not WatcherChangeTypes.Created and not WatcherChangeTypes.Changed and not WatcherChangeTypes.Deleted and not WatcherChangeTypes.Renamed) return;
        if (Utils.GetFileSystemEntryType(e.FullPath) == FileSystemEntryType.Directory) return;
        if (RegexFilter.HasValue() && !Regex.IsMatch(e.FullPath, RegexFilter)) return;
        if (e.FullPath.Contains(Utils.IS_DIRECTORY_WRITABLE_GUID) || e.FullPath.Contains(Utils.SPEED_TEST_GUID)) return;

        _ = FileSystemChanges.AddOrUpdate(new FileSystemEntry(e.FullPath, DateTime.Now));

        // As soon as there's something changed we start our event timers
        StartTimers();
    }

    private void StartTimers()
    {
        if (processChangesTimer is { Enabled: false }) processChangesTimer.Start();
        if (scanDirectoriesTimer is { Enabled: false }) scanDirectoriesTimer.Start();
    }

    private void ScanDirectoriesTimerElapsed(object sender, ElapsedEventArgs e)
    {
        Utils.TraceIn();

        lock (_lock)
        {
            //select the directories that are old enough now
            // raise event for them only
            // remove them from our list and set another timer
            var dirsToRaiseEventFor = DirectoriesToScan.Where(d => d.ModifiedDateTime.AddMilliseconds(MinimumAgeBeforeScanEventRaised) < DateTime.Now).ToArray();

            if (dirsToRaiseEventFor.Length > 0)
            {
                foreach (var a in dirsToRaiseEventFor)
                {
                    _ = DirectoriesToScan.Remove(a);
                }

                //  raise the ReadyToScan event for directories that are old enough
                var args = new FileSystemWatcherEventArgs(dirsToRaiseEventFor);
                var handler = ReadyToScan;
                handler?.Invoke(this, args);
            }
        }
        if (DirectoriesToScan.Count > 0 || FileSystemChanges.Count > 0) scanDirectoriesTimer.Start();
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

        // every few seconds we move through the changes List and put the directories we need to check in our other list
        if (FileSystemChanges.Count == 0)
        {
            Utils.TraceOut();
            return;
        }

        foreach (var fileOrDirectoryChange in FileSystemChanges.ToArray())
        {
            Utils.Trace($"fileOrFolderChange.Path = {fileOrDirectoryChange.Path}");

            var directoryToScan = Utils.GetFileSystemEntryType(fileOrDirectoryChange.Path) switch
            {
                FileSystemEntryType.File or FileSystemEntryType.Missing => new FileInfo(fileOrDirectoryChange.Path).DirectoryName,
                _ => fileOrDirectoryChange.Path
            };
            Utils.Trace($"directoryToScan = {directoryToScan}");
            var newItem = new FileSystemEntry(directoryToScan, fileOrDirectoryChange.ModifiedDateTime);
            _ = DirectoriesToScan.AddOrUpdate(newItem);
            _ = FileSystemChanges.Remove(fileOrDirectoryChange);
        }
        if (DirectoriesToScan.Count > 0) scanDirectoriesTimer.Start();
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
        Utils.Trace($"watcherList count = {watcherList.Count}");
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
            var ex = new DirectoryNotFoundException(string.Format(Resources.DirectoryNotFound1, watcherPath), e.GetException());
            e = new ErrorEventArgs(ex);
        }

        // Stop stops the timers and disables the watchers
        _ = Stop();
        var handler = Error;
        handler?.Invoke(watcher, e);
    }
}

/// <summary>
///     Used to pass the Directories changed to any events.
/// </summary>
internal sealed class FileSystemWatcherEventArgs : EventArgs
{
    internal FileSystemWatcherEventArgs(string directory)
    {
        Directories = [new FileSystemEntry(directory)];
    }

    internal FileSystemWatcherEventArgs(IEnumerable<BackupFile> backupFiles)
    {
        var hash = new HashSet<string>();

        foreach (var directoryName in backupFiles.Select(static dir => Path.GetDirectoryName(dir.FullPath)))
        {
            _ = hash.Add(directoryName);
        }
        var fse = new FileSystemEntry[hash.Count];
        var i = 0;

        foreach (var h in hash)
        {
            fse[i++] = new FileSystemEntry(h);
        }
        Directories = fse;
    }

    internal FileSystemWatcherEventArgs(FileSystemEntry[] directoriesToScan)
    {
        Utils.TraceIn();
        Directories = directoriesToScan;
        Utils.TraceOut();
    }

    /// <summary>
    ///     An Array of FileSystemEntry that have been changed
    /// </summary>
    internal FileSystemEntry[] Directories { get; }
}