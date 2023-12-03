// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="FileSystemWatcherTest5.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

#if DEBUG
using System.Diagnostics.CodeAnalysis;

using BackupManager;

using FileSystemWatcher = BackupManager.FileSystemWatcher;

namespace TestProject;

[SuppressMessage("ReSharper", "MemberCanBeFileLocal")]
[SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "<Pending>")]
public sealed class FileSystemWatcherTest5
{
    private int test5EventsCounter;

    private int test5EventsErrorCounter;

    private int test5ExpectedEventFolderCount;

    private FileSystemWatcher? watcher;

    private readonly string monitoringPath3DeletedAfterABit = Path.Combine(Path.GetTempPath(), "Test5MonitoringFolder3");

    [Fact]
    public void FileSystemWatcherTest()
    {
        test5EventsCounter = 0;
        const int waitInMilliseconds = 150;
        var monitoringPath1 = Path.Combine(Path.GetTempPath(), "Test5MonitoringFolder1");
        var monitoringPath2 = Path.Combine(Path.GetTempPath(), "Test5MonitoringFolder2");
        Utils.EnsureDirectoriesForDirectoryPath(monitoringPath1);
        Utils.EnsureDirectoriesForDirectoryPath(monitoringPath2);
        Utils.EnsureDirectoriesForDirectoryPath(monitoringPath3DeletedAfterABit);

        watcher = new FileSystemWatcher
        {
            Filter = "*.*",
            IncludeSubdirectories = true,
            ScanInterval = 50,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
            Directories = new[] { monitoringPath1, monitoringPath2, monitoringPath3DeletedAfterABit },
            ProcessChangesInterval = 50,
            MinimumAgeBeforeScanEventRaised = 50
        };
        watcher.ReadyToScan += FileSystemWatcher_ReadyToScan3;
        watcher.Error += FileSystemWatcher_ErrorTest3;
        Assert.False(watcher.Running);
        watcher.Start();
        Assert.True(watcher.Running);

        //delete a folder while we're monitoring it
        if (Directory.Exists(monitoringPath3DeletedAfterABit)) Directory.Delete(monitoringPath3DeletedAfterABit, true);
        test5ExpectedEventFolderCount = 3;

        // now create the folders and file again
        Utils.CreateFile(Path.Combine(monitoringPath1, "test1.txt"));
        Utils.CreateFile(Path.Combine(monitoringPath2, "test2.txt"));
        Utils.CreateFile(Path.Combine(monitoringPath2, "subFolder", "test3.txt"));
        Utils.Wait(waitInMilliseconds);
        Assert.True(test5EventsErrorCounter == 2);
        Assert.True(watcher.Running);
        test5ExpectedEventFolderCount = 4;
        Utils.CreateFile(Path.Combine(monitoringPath1, "test1.txt"));
        Utils.CreateFile(Path.Combine(monitoringPath2, "test2.txt"));
        Utils.CreateFile(Path.Combine(monitoringPath2, "subFolder", "test3.txt"));
        Utils.CreateFile(Path.Combine(monitoringPath3DeletedAfterABit, "test4.txt"));
        Utils.Wait(waitInMilliseconds);
        Assert.True(test5EventsCounter == 1);
        Assert.True(test5EventsErrorCounter == 2);
        Assert.True(watcher.Running);

        // Stop everything
        watcher.Stop();
        Assert.False(watcher.Running);
        Assert.True(watcher.FileSystemChanges.Count == 0, nameof(FileSystemWatcher.FileSystemChanges.Count));
        Assert.True(watcher.DirectoriesToScan.Count == 0, nameof(FileSystemWatcher.DirectoriesToScan.Count));

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
        test5EventsErrorCounter++;
        if (watcher == null) return;

        Utils.EnsureDirectoriesForDirectoryPath(monitoringPath3DeletedAfterABit);
        Assert.True(watcher.Reset());
        watcher.Start();
        Assert.True(watcher.Running);
        test5EventsErrorCounter++;
    }

    private void FileSystemWatcher_ReadyToScan3(object? sender, FileSystemWatcherEventArgs e)
    {
        if (sender is not FileSystemWatcher watcher2) return;

        Assert.True(e.Directories.Length == test5ExpectedEventFolderCount, nameof(e.Directories.Length));
        Assert.True(watcher2.FileSystemChanges.Count == 0, nameof(FileSystemWatcher.FileSystemChanges.Count));
        Assert.True(watcher2.DirectoriesToScan.Count == 0, nameof(FileSystemWatcher.DirectoriesToScan.Count));
        test5EventsCounter++;
    }
}
#endif