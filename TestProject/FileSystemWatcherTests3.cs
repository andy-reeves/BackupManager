// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="FileSystemWatcherTests3.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;

using BackupManager;

using FileSystemWatcher = BackupManager.FileSystemWatcher;

namespace TestProject;

[SuppressMessage("ReSharper", "MemberCanBeFileLocal")]
[SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "<Pending>")]
public sealed class FileSystemWatcherTests3
{
    private int test3EventsCounter;

    private int test3EventsErrorCounter;

    private int test3ExpectedEventFolderCount;

    [Fact]
    public void FileSystemWatcherTest3()
    {
        test3EventsCounter = 0;
        const int waitInMilliseconds = 250;
        var monitoringPath1 = Path.Combine(Path.GetTempPath(), "Test3MonitoringFolder1");
        var monitoringPath2 = Path.Combine(Path.GetTempPath(), "Test3MonitoringFolder2");
        var monitoringPath3DeletedAfterABit = Path.Combine(Path.GetTempPath(), "Test3MonitoringFolder3");
        Utils.EnsureDirectoriesForDirectoryPath(monitoringPath1);
        Utils.EnsureDirectoriesForDirectoryPath(monitoringPath2);
        Utils.EnsureDirectoriesForDirectoryPath(monitoringPath3DeletedAfterABit);

        var watcher = new FileSystemWatcher
        {
            Filter = "*.*",
            IncludeSubdirectories = true,
            ScanInterval = 5,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
            Directories = new[] { monitoringPath1, monitoringPath2, monitoringPath3DeletedAfterABit },
            ProcessChangesInterval = 5,
            MinimumAgeBeforeScanEventRaised = 5
        };
        watcher.ReadyToScan += FileSystemWatcher_ReadyToScan3;
        watcher.Error += FileSystemWatcher_ErrorTest3;
        Assert.False(watcher.Running);
        _ = watcher.Start();
        Assert.True(watcher.Running);
        test3ExpectedEventFolderCount = 4;
        Utils.CreateFile(Path.Combine(monitoringPath1, "test1.txt"));
        Utils.CreateFile(Path.Combine(monitoringPath2, "test2.txt"));
        Utils.CreateFile(Path.Combine(monitoringPath2, "subFolder", "test3.txt"));
        Utils.CreateFile(Path.Combine(monitoringPath3DeletedAfterABit, "test4.txt"));
        Utils.Wait(waitInMilliseconds);
        Assert.Equal(1, test3EventsCounter);
        Assert.True(watcher.FileSystemChanges.Count == 0, nameof(FileSystemWatcher.FileSystemChanges.Count));
        Assert.True(watcher.DirectoriesToScan.Count == 0, nameof(FileSystemWatcher.DirectoriesToScan.Count));
        Assert.True(watcher.Running);
        _ = watcher.Stop();
        Assert.False(watcher.Running);

        // Now delete a folder we are monitoring after we've stopped
        if (Directory.Exists(monitoringPath3DeletedAfterABit)) Directory.Delete(monitoringPath3DeletedAfterABit, true);

        // should fail to restart because a folder is missing now
        _ = Assert.Throws<ArgumentException>(() => watcher.Start());

        // now create the folder again and start
        Utils.EnsureDirectoriesForDirectoryPath(monitoringPath3DeletedAfterABit);
        Assert.False(watcher.Running);
        _ = watcher.Start();
        Assert.True(watcher.Running);
        test3ExpectedEventFolderCount = 4;
        Utils.CreateFile(Path.Combine(monitoringPath1, "test1.txt"));
        Utils.CreateFile(Path.Combine(monitoringPath2, "test2.txt"));
        Utils.CreateFile(Path.Combine(monitoringPath2, "subFolder", "test3.txt"));
        Utils.CreateFile(Path.Combine(monitoringPath3DeletedAfterABit, "test4.txt"));
        Utils.Wait(waitInMilliseconds);
        Assert.Equal(2, test3EventsCounter);

        //delete a folder while we're monitoring it
        if (Directory.Exists(monitoringPath3DeletedAfterABit)) Directory.Delete(monitoringPath3DeletedAfterABit, true);
        test3ExpectedEventFolderCount = 4;

        // now create the folders and file again
        Utils.CreateFile(Path.Combine(monitoringPath1, "test1.txt"));
        Utils.CreateFile(Path.Combine(monitoringPath2, "test2.txt"));
        Utils.CreateFile(Path.Combine(monitoringPath2, "subFolder", "test3.txt"));
        Utils.Wait(waitInMilliseconds);
        Assert.Equal(1, test3EventsErrorCounter);
        Assert.False(watcher.Running);

        // should fail to restart because a folder is missing now
        _ = Assert.Throws<ArgumentException>(() => watcher.Start());

        // now create the folder again and start
        Utils.EnsureDirectoriesForDirectoryPath(monitoringPath3DeletedAfterABit);
        Assert.False(watcher.Running);
        _ = watcher.Start();
        Assert.True(watcher.Running);
        test3ExpectedEventFolderCount = 4;
        Utils.CreateFile(Path.Combine(monitoringPath1, "test1.txt"));
        Utils.CreateFile(Path.Combine(monitoringPath2, "test2.txt"));
        Utils.CreateFile(Path.Combine(monitoringPath2, "subFolder", "test3.txt"));
        Utils.CreateFile(Path.Combine(monitoringPath3DeletedAfterABit, "test4.txt"));
        Utils.Wait(waitInMilliseconds);
        Assert.Equal(3, test3EventsCounter);
        Assert.Equal(1, test3EventsErrorCounter);
        Assert.True(watcher.Running);
        _ = watcher.Stop();
        Assert.False(watcher.Running);
        Assert.Empty(watcher.FileSystemChanges);
        Assert.Empty(watcher.DirectoriesToScan);

        //Unhook event handlers
        watcher.ReadyToScan -= FileSystemWatcher_ReadyToScan3;
        watcher.Error -= FileSystemWatcher_ErrorTest3;

        // Delete the folders we created
        if (Directory.Exists(monitoringPath1)) Directory.Delete(monitoringPath1, true);
        if (Directory.Exists(monitoringPath2)) Directory.Delete(monitoringPath2, true);
        if (Directory.Exists(monitoringPath3DeletedAfterABit)) Directory.Delete(monitoringPath3DeletedAfterABit, true);
    }

    private void FileSystemWatcher_ErrorTest3(object? sender, ErrorEventArgs e)
    {
        Assert.Contains("MonitoringFolder3 not found", e.GetException().Message);
        test3EventsErrorCounter++;
    }

    private void FileSystemWatcher_ReadyToScan3(object? sender, FileSystemWatcherEventArgs e)
    {
        if (sender is not FileSystemWatcher watcher) return;

        Assert.True(e.Directories.Length == test3ExpectedEventFolderCount, nameof(e.Directories.Length));
        Assert.True(watcher.FileSystemChanges.Count == 0, nameof(FileSystemWatcher.FileSystemChanges.Count));
        Assert.True(watcher.DirectoriesToScan.Count == 0, nameof(FileSystemWatcher.DirectoriesToScan.Count));
        test3EventsCounter++;
    }
}

// #endif