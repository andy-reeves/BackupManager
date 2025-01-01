// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="FileSystemWatcherTests1.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;

using BackupManager;

using FileSystemWatcher = BackupManager.FileSystemWatcher;

namespace TestProject;

[SuppressMessage("ReSharper", "MemberCanBeFileLocal")]
[SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "<Pending>")]
public sealed class FileSystemWatcherTests1
{
    private int test1EventsCounter;

    private int test1ExpectedEventFolderCount;

    private int test2ExpectedEventFolderCount;

    [Fact]
    public void FileSystemWatcherTest1A()
    {
        test1EventsCounter = 0;
        test1ExpectedEventFolderCount = 3;
        const int waitInMilliseconds = 250;
        var monitoringPath1 = Path.Combine(Path.GetTempPath(), "Test1MonitoringFolder1");
        var monitoringPath2 = Path.Combine(Path.GetTempPath(), "Test1MonitoringFolder2");
        Utils.Directory.EnsurePath(monitoringPath1);
        Utils.Directory.EnsurePath(monitoringPath2);
        var watcher = new FileSystemWatcher();
        Assert.True(watcher.Filter == "*.*", nameof(watcher.Filter));
        Assert.False(watcher.IncludeSubdirectories, nameof(watcher.IncludeSubdirectories));
        Assert.True(watcher.ScanInterval == 60_000, nameof(watcher.ScanInterval));
        Assert.True(watcher.Directories.Length == 0, nameof(watcher.Directories.Length));
        Assert.True(watcher.NotifyFilter == (NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName), nameof(watcher.NotifyFilter));
        Assert.True(watcher.ProcessChangesInterval == 30_000, nameof(watcher.ProcessChangesInterval));
        Assert.True(watcher.DirectoriesToScan.Count == 0, nameof(FileSystemWatcher.DirectoriesToScan.Count));
        Assert.True(watcher.FileSystemChanges.Count == 0, nameof(FileSystemWatcher.FileSystemChanges.Count));
        watcher.Filter = "*.*";
        watcher.IncludeSubdirectories = true;
        watcher.ScanInterval = 50;
        watcher.MinimumAgeBeforeScanEventRaised = 50;
        watcher.Directories = [monitoringPath1, monitoringPath2];
        watcher.ProcessChangesInterval = 50;
        watcher.ReadyToScan += FileSystemWatcher_ReadyToScan1;
        Assert.True(watcher.DirectoriesToScan.Count == 0, nameof(FileSystemWatcher.DirectoriesToScan.Count));
        Assert.True(watcher.FileSystemChanges.Count == 0, nameof(FileSystemWatcher.FileSystemChanges.Count));
        Assert.False(watcher.Running);
        _ = watcher.Start();
        Assert.True(watcher.Running);
        Utils.File.Create(Path.Combine(monitoringPath1, "test1.txt"));
        Utils.File.Create(Path.Combine(monitoringPath2, "test2.txt"));
        Utils.File.Create(Path.Combine(monitoringPath2, "subFolder", "test3.txt"));
        Utils.Wait(waitInMilliseconds);
        Assert.Equal(1, test1EventsCounter);
        Assert.True(watcher.FileSystemChanges.Count == 0, nameof(FileSystemWatcher.FileSystemChanges.Count));
        Assert.True(watcher.DirectoriesToScan.Count == 0, nameof(FileSystemWatcher.DirectoriesToScan.Count));
        Assert.True(watcher.Running);
        _ = watcher.Stop();
        Assert.False(watcher.Running);

        //Unhook event handlers
        watcher.ReadyToScan -= FileSystemWatcher_ReadyToScan1;

        // Delete the folders we created
        if (Directory.Exists(monitoringPath1)) _ = Utils.Directory.Delete(monitoringPath1, true);
        if (Directory.Exists(monitoringPath2)) _ = Utils.Directory.Delete(monitoringPath2, true);
    }

    /// <summary>
    ///     Checks the watcher will not start with missing folders
    /// </summary>
    [Fact]
    public void FileSystemWatcherTest1B()
    {
        test2ExpectedEventFolderCount = 3;
        var monitoringPath1 = Path.Combine(Path.GetTempPath(), "Test2MonitoringFolder1");
        var monitoringPath2 = Path.Combine(Path.GetTempPath(), "Test2MonitoringFolder2");
        var monitoringPath3Missing = Path.Combine(Path.GetTempPath(), "Test2MonitoringFolder3");
        if (Directory.Exists(monitoringPath3Missing)) _ = Utils.Directory.Delete(monitoringPath3Missing, true);
        Utils.Directory.EnsurePath(monitoringPath1);
        Utils.Directory.EnsurePath(monitoringPath2);

        var watcher = new FileSystemWatcher
        {
            Directories = [monitoringPath1, monitoringPath2, monitoringPath3Missing], NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName
        };
        watcher.ReadyToScan += FileSystemWatcher_ReadyToScan2;
        _ = Assert.Throws<ArgumentException>(() => watcher.Start());
        Assert.False(watcher.Running);

        //Unhook event handlers
        watcher.ReadyToScan -= FileSystemWatcher_ReadyToScan2;

        // Delete the folders we created
        if (Directory.Exists(monitoringPath1)) _ = Utils.Directory.Delete(monitoringPath1, true);
        if (Directory.Exists(monitoringPath2)) _ = Utils.Directory.Delete(monitoringPath2, true);
    }

    private void FileSystemWatcher_ReadyToScan1(object? sender, FileSystemWatcherEventArgs e)
    {
        if (sender is not FileSystemWatcher watcher) return;

        foreach (var folder in e.Directories)
        {
            Utils.Trace($"{folder.Path} at {folder.ModifiedDateTime}");
        }
        Assert.True(e.Directories.Length == test1ExpectedEventFolderCount, nameof(e.Directories.Length));
        Assert.True(watcher.FileSystemChanges.Count == 0, nameof(FileSystemWatcher.FileSystemChanges.Count));
        Assert.True(watcher.DirectoriesToScan.Count == 0, nameof(FileSystemWatcher.DirectoriesToScan.Count));
        test1EventsCounter += 1;
    }

    private void FileSystemWatcher_ReadyToScan2(object? sender, FileSystemWatcherEventArgs e)
    {
        if (sender is not FileSystemWatcher watcher) return;

        foreach (var folder in e.Directories)
        {
            Utils.Trace($"{folder.Path} at {folder.ModifiedDateTime}");
        }
        Assert.True(e.Directories.Length == test2ExpectedEventFolderCount, nameof(e.Directories.Length));
        Assert.True(watcher.FileSystemChanges.Count == 0, nameof(FileSystemWatcher.FileSystemChanges.Count));
        Assert.True(watcher.DirectoriesToScan.Count == 0, nameof(FileSystemWatcher.DirectoriesToScan.Count));
    }
}

// #endif