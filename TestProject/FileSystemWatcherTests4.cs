// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="FileSystemWatcherTests4.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;

using BackupManager;

using FileSystemWatcher = BackupManager.FileSystemWatcher;

namespace TestProject;

[SuppressMessage("ReSharper", "MemberCanBeFileLocal")]
[SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "<Pending>")]
public sealed class FileSystemWatcherTests4
{
    private int test4EventsCounter;

    private int test4ExpectedEventFolderCount;

    //[Fact]
    public void FileSystemWatcherTest4()
    {
        test4EventsCounter = 0;
        const int waitInMilliseconds = 1000;
        var monitoringPath1 = Path.Combine(Path.GetTempPath(), "Test4MonitoringFolder1");
        var monitoringPath2 = Path.Combine(Path.GetTempPath(), "Test4MonitoringFolder2");
        Utils.EnsureDirectoriesForDirectoryPath(monitoringPath1);
        Utils.EnsureDirectoriesForDirectoryPath(monitoringPath2);
        var firstFileDirectoryPath = Path.Combine(monitoringPath1, @"TVShow\Season 1");
        var secondFileDirectoryPath = Path.Combine(monitoringPath2, @"MovieName");

        var watcher = new FileSystemWatcher
        {
            Filter = "*.*",
            IncludeSubdirectories = true,
            ScanInterval = 5,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
            Directories = new[] { monitoringPath1, monitoringPath2 },
            ProcessChangesInterval = 5,
            MinimumAgeBeforeScanEventRaised = 5
        };
        watcher.ReadyToScan += FileSystemWatcher_ReadyToScan4;
        Assert.False(watcher.Running);
        _ = watcher.Start();
        Assert.True(watcher.Running);

        // Create a file in a subfolder of a monitored folder that already exists
        test4ExpectedEventFolderCount = 2;
        Utils.CreateFile(Path.Combine(firstFileDirectoryPath, "test1.txt"));
        Utils.CreateFile(Path.Combine(secondFileDirectoryPath, "test2.txt"));
        Utils.Wait(waitInMilliseconds);
        Assert.Equal(1, test4EventsCounter);
        Assert.True(watcher.FileSystemChanges.Count == 0, nameof(FileSystemWatcher.FileSystemChanges.Count));
        Assert.True(watcher.DirectoriesToScan.Count == 0, nameof(FileSystemWatcher.DirectoriesToScan.Count));

        // Create a file in a subfolder of a monitored folder that is a folder created after the monitor started
        test4ExpectedEventFolderCount = 2;
        Utils.CreateFile(Path.Combine(monitoringPath1, "NewFolder", "test1.txt"));
        Utils.CreateFile(Path.Combine(monitoringPath2, "subFolder", "test2.txt"));
        Utils.Wait(waitInMilliseconds);
        Assert.Equal(2, test4EventsCounter);
        test4ExpectedEventFolderCount = 2;

        // now delete the files 
        Utils.FileDelete(Path.Combine(monitoringPath1, "NewFolder", "test1.txt"));
        Utils.FileDelete(Path.Combine(monitoringPath2, "subFolder", "test2.txt"));
        Utils.Wait(waitInMilliseconds);
        Assert.Equal(3, test4EventsCounter);
        _ = watcher.Stop();
        Assert.False(watcher.Running);
        Assert.True(watcher.FileSystemChanges.Count == 0, nameof(FileSystemWatcher.FileSystemChanges.Count));
        Assert.True(watcher.DirectoriesToScan.Count == 0, nameof(FileSystemWatcher.DirectoriesToScan.Count));

        //Unhook event handlers
        watcher.ReadyToScan -= FileSystemWatcher_ReadyToScan4;

        // Delete the folders we created
        if (Directory.Exists(monitoringPath1)) Directory.Delete(monitoringPath1, true);
        if (Directory.Exists(monitoringPath2)) Directory.Delete(monitoringPath2, true);
    }

    private void FileSystemWatcher_ReadyToScan4(object? sender, FileSystemWatcherEventArgs e)
    {
        if (sender is not FileSystemWatcher watcher) return;

        Assert.True(e.Directories.Length == test4ExpectedEventFolderCount, nameof(e.Directories.Length));

        foreach (var directory in e.Directories)
        {
            Utils.Trace(directory.Path);
        }
        Assert.True(watcher.FileSystemChanges.Count == 0, nameof(FileSystemWatcher.FileSystemChanges.Count));
        Assert.True(watcher.DirectoriesToScan.Count == 0, nameof(FileSystemWatcher.DirectoriesToScan.Count));
        test4EventsCounter++;
    }
}

// #endif