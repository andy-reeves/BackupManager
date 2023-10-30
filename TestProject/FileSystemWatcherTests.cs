// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="FileSystemWatcherTests.cs" company="Andy Reeves">
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
public sealed class FileSystemWatcherTests
{
    private const int WaitInSeconds = 8;

    private int test1EventsCounter;

    private int test1ExpectedEventFolderCount;

    private int test2ExpectedEventFolderCount;

    private int test3EventsCounter;

    private int test3EventsErrorCounter;

    private int test3ExpectedEventFolderCount;

    private int test4EventsCounter;

    private int test4ExpectedEventFolderCount;

    [Fact]
    public void FileSystemWatcherTest1()
    {
        test1EventsCounter = 0;
        test1ExpectedEventFolderCount = 3;
        var monitoringPath1 = Path.Combine(Path.GetTempPath(), "MonitoringFolder1");
        var monitoringPath2 = Path.Combine(Path.GetTempPath(), "MonitoringFolder2");
        Utils.EnsureDirectoriesForDirectoryPath(monitoringPath1);
        Utils.EnsureDirectoriesForDirectoryPath(monitoringPath2);
        var watcher = new FileSystemWatcher();
        Assert.True(watcher.Filter == "*.*", nameof(watcher.Filter));
        Assert.True(watcher.IncludeSubdirectories == false, nameof(watcher.IncludeSubdirectories));
        Assert.True(watcher.ScanInterval == 60, nameof(watcher.ScanInterval));
        Assert.True(watcher.Directories.Length == 0, nameof(watcher.Directories.Length));
        Assert.True(watcher.NotifyFilter == (NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName), nameof(watcher.NotifyFilter));
        Assert.True(watcher.ProcessChangesInterval == 30, nameof(watcher.ProcessChangesInterval));
        Assert.True(watcher.DirectoriesToScan.Count == 0, nameof(FileSystemWatcher.DirectoriesToScan.Count));
        Assert.True(watcher.FileSystemChanges.Count == 0, nameof(FileSystemWatcher.FileSystemChanges.Count));
        watcher.Filter = "*.*";
        watcher.IncludeSubdirectories = true;
        watcher.ScanInterval = 1;
        watcher.MinimumAgeBeforeScanEventRaised = 1;
        watcher.Directories = new[] { monitoringPath1, monitoringPath2 };
        watcher.ProcessChangesInterval = 1;
        watcher.ReadyToScan += FileSystemWatcher_ReadyToScan1;
        Assert.True(watcher.DirectoriesToScan.Count == 0, nameof(FileSystemWatcher.DirectoriesToScan.Count));
        Assert.True(watcher.FileSystemChanges.Count == 0, nameof(FileSystemWatcher.FileSystemChanges.Count));
        Assert.False(watcher.Running);
        watcher.Start();
        Assert.True(watcher.Running);
        CreateFile(Path.Combine(monitoringPath1, "test1.txt"));
        CreateFile(Path.Combine(monitoringPath2, "test2.txt"));
        CreateFile(Path.Combine(monitoringPath2, "subFolder", "test3.txt"));
        Utils.Wait(WaitInSeconds);
        Assert.True(test1EventsCounter == 1);
        Assert.True(watcher.FileSystemChanges.Count == 0, nameof(FileSystemWatcher.FileSystemChanges.Count));
        Assert.True(watcher.DirectoriesToScan.Count == 0, nameof(FileSystemWatcher.DirectoriesToScan.Count));
        Assert.True(watcher.Running);
        watcher.Stop();
        Assert.False(watcher.Running);

        //Unhook event handlers
        watcher.ReadyToScan -= FileSystemWatcher_ReadyToScan1;

        // Delete the folders we created
        if (Directory.Exists(monitoringPath1)) Directory.Delete(monitoringPath1, true);
        if (Directory.Exists(monitoringPath2)) Directory.Delete(monitoringPath2, true);
    }

    /// <summary>
    ///     Checks the watcher will not start with missing folders
    /// </summary>
    [Fact]
    public void FileSystemWatcherTest2()
    {
        test2ExpectedEventFolderCount = 3;
        var monitoringPath1 = Path.Combine(Path.GetTempPath(), "MonitoringFolder1");
        var monitoringPath2 = Path.Combine(Path.GetTempPath(), "MonitoringFolder2");
        var monitoringPath3Missing = Path.Combine(Path.GetTempPath(), "MonitoringFolder3");
        if (Directory.Exists(monitoringPath3Missing)) Directory.Delete(monitoringPath3Missing, true);
        Utils.EnsureDirectoriesForDirectoryPath(monitoringPath1);
        Utils.EnsureDirectoriesForDirectoryPath(monitoringPath2);
        var watcher = new FileSystemWatcher { Directories = new[] { monitoringPath1, monitoringPath2, monitoringPath3Missing } };
        watcher.ReadyToScan += FileSystemWatcher_ReadyToScan2;
        Assert.Throws<ArgumentException>(() => watcher.Start());
        Assert.False(watcher.Running);

        //Unhook event handlers
        watcher.ReadyToScan -= FileSystemWatcher_ReadyToScan2;

        // Delete the folders we created
        if (Directory.Exists(monitoringPath1)) Directory.Delete(monitoringPath1, true);
        if (Directory.Exists(monitoringPath2)) Directory.Delete(monitoringPath2, true);
    }

    private static void CreateFile(string filePath)
    {
        Utils.EnsureDirectoriesForFilePath(filePath);
        File.AppendAllText(filePath, "test");
    }

    [Fact]
    public void FileSystemWatcherTest3()
    {
        test3EventsCounter = 0;
        var monitoringPath1 = Path.Combine(Path.GetTempPath(), "MonitoringFolder1");
        var monitoringPath2 = Path.Combine(Path.GetTempPath(), "MonitoringFolder2");
        var monitoringPath3DeletedAfterABit = Path.Combine(Path.GetTempPath(), "MonitoringFolder3");
        Utils.EnsureDirectoriesForDirectoryPath(monitoringPath1);
        Utils.EnsureDirectoriesForDirectoryPath(monitoringPath2);
        Utils.EnsureDirectoriesForDirectoryPath(monitoringPath3DeletedAfterABit);

        var watcher = new FileSystemWatcher
        {
            Filter = "*.*",
            IncludeSubdirectories = true,
            ScanInterval = 1,
            Directories = new[] { monitoringPath1, monitoringPath2, monitoringPath3DeletedAfterABit },
            ProcessChangesInterval = 1,
            MinimumAgeBeforeScanEventRaised = 1
        };
        watcher.ReadyToScan += FileSystemWatcher_ReadyToScan3;
        watcher.Error += FileSystemWatcher_ErrorTest3;
        Assert.False(watcher.Running);
        watcher.Start();
        Assert.True(watcher.Running);
        test3ExpectedEventFolderCount = 4;
        CreateFile(Path.Combine(monitoringPath1, "test1.txt"));
        CreateFile(Path.Combine(monitoringPath2, "test2.txt"));
        CreateFile(Path.Combine(monitoringPath2, "subFolder", "test3.txt"));
        CreateFile(Path.Combine(monitoringPath3DeletedAfterABit, "test4.txt"));
        Utils.Wait(WaitInSeconds);
        Assert.True(test3EventsCounter == 1);
        Assert.True(watcher.FileSystemChanges.Count == 0, nameof(FileSystemWatcher.FileSystemChanges.Count));
        Assert.True(watcher.DirectoriesToScan.Count == 0, nameof(FileSystemWatcher.DirectoriesToScan.Count));
        Assert.True(watcher.Running);
        watcher.Stop();
        Assert.False(watcher.Running);

        // Now delete a folder we are monitoring after we've stopped
        if (Directory.Exists(monitoringPath3DeletedAfterABit)) Directory.Delete(monitoringPath3DeletedAfterABit, true);

        // should fail to restart because a folder is missing now
        Assert.Throws<ArgumentException>(() => watcher.Start());

        // now create the folder again and start
        Utils.EnsureDirectoriesForDirectoryPath(monitoringPath3DeletedAfterABit);
        Assert.False(watcher.Running);
        watcher.Start();
        Assert.True(watcher.Running);
        test3ExpectedEventFolderCount = 4;
        CreateFile(Path.Combine(monitoringPath1, "test1.txt"));
        CreateFile(Path.Combine(monitoringPath2, "test2.txt"));
        CreateFile(Path.Combine(monitoringPath2, "subFolder", "test3.txt"));
        CreateFile(Path.Combine(monitoringPath3DeletedAfterABit, "test4.txt"));
        Utils.Wait(WaitInSeconds);
        Assert.True(test3EventsCounter == 2);

        //delete a folder while we're monitoring it
        if (Directory.Exists(monitoringPath3DeletedAfterABit)) Directory.Delete(monitoringPath3DeletedAfterABit, true);
        test3ExpectedEventFolderCount = 4;

        // now create the folders and file again
        CreateFile(Path.Combine(monitoringPath1, "test1.txt"));
        CreateFile(Path.Combine(monitoringPath2, "test2.txt"));
        CreateFile(Path.Combine(monitoringPath2, "subFolder", "test3.txt"));
        Utils.Wait(WaitInSeconds);
        Assert.True(test3EventsErrorCounter == 1);
        Assert.False(watcher.Running);

        // should fail to restart because a folder is missing now
        Assert.Throws<ArgumentException>(() => watcher.Start());

        // now create the folder again and start
        Utils.EnsureDirectoriesForDirectoryPath(monitoringPath3DeletedAfterABit);
        Assert.False(watcher.Running);
        watcher.Start();
        Assert.True(watcher.Running);
        test3ExpectedEventFolderCount = 4;
        CreateFile(Path.Combine(monitoringPath1, "test1.txt"));
        CreateFile(Path.Combine(monitoringPath2, "test2.txt"));
        CreateFile(Path.Combine(monitoringPath2, "subFolder", "test3.txt"));
        CreateFile(Path.Combine(monitoringPath3DeletedAfterABit, "test4.txt"));
        Utils.Wait(WaitInSeconds);
        Assert.True(test3EventsCounter == 3);
        Assert.True(test3EventsErrorCounter == 1);
        Assert.True(watcher.Running);
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

    [Fact]
    public void FileSystemWatcherTest4()
    {
        test4EventsCounter = 0;
        var monitoringPath1 = Path.Combine(Path.GetTempPath(), "MonitoringFolder1");
        var monitoringPath2 = Path.Combine(Path.GetTempPath(), "MonitoringFolder2");
        Utils.EnsureDirectoriesForDirectoryPath(monitoringPath1);
        Utils.EnsureDirectoriesForDirectoryPath(monitoringPath2);
        var firstFileDirectoryPath = Path.Combine(monitoringPath1, @"TVShow\Season 1");
        var secondFileDirectoryPath = Path.Combine(monitoringPath2, @"MovieName");

        var watcher = new FileSystemWatcher
        {
            Filter = "*.*",
            IncludeSubdirectories = true,
            ScanInterval = 1,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
            Directories = new[] { monitoringPath1, monitoringPath2 },
            ProcessChangesInterval = 1,
            MinimumAgeBeforeScanEventRaised = 1
        };
        watcher.ReadyToScan += FileSystemWatcher_ReadyToScan4;
        Assert.False(watcher.Running);
        watcher.Start();
        Assert.True(watcher.Running);

        // Create a file in a subfolder of a monitored folder that already exists
        test4ExpectedEventFolderCount = 2;
        CreateFile(Path.Combine(firstFileDirectoryPath, "test1.txt"));
        CreateFile(Path.Combine(secondFileDirectoryPath, "test2.txt"));
        Utils.Wait(WaitInSeconds);
        Assert.Equal(1, test4EventsCounter);
        Assert.True(watcher.FileSystemChanges.Count == 0, nameof(FileSystemWatcher.FileSystemChanges.Count));
        Assert.True(watcher.DirectoriesToScan.Count == 0, nameof(FileSystemWatcher.DirectoriesToScan.Count));

        // Create a file in a subfolder of a monitored folder that is a folder created after the monitor started
        test4ExpectedEventFolderCount = 2;
        CreateFile(Path.Combine(monitoringPath1, "NewFolder", "test1.txt"));
        CreateFile(Path.Combine(monitoringPath2, "subFolder", "test2.txt"));
        Utils.Wait(WaitInSeconds);
        Assert.True(test4EventsCounter == 2);
        test4ExpectedEventFolderCount = 2;

        // now delete the files 
        Utils.FileDelete(Path.Combine(monitoringPath1, "NewFolder", "test1.txt"));
        Utils.FileDelete(Path.Combine(monitoringPath2, "subFolder", "test2.txt"));
        Utils.Wait(WaitInSeconds);
        Assert.True(test4EventsCounter == 3);
        watcher.Stop();
        Assert.False(watcher.Running);
        Assert.True(watcher.FileSystemChanges.Count == 0, nameof(FileSystemWatcher.FileSystemChanges.Count));
        Assert.True(watcher.DirectoriesToScan.Count == 0, nameof(FileSystemWatcher.DirectoriesToScan.Count));

        //Unhook event handlers
        watcher.ReadyToScan -= FileSystemWatcher_ReadyToScan4;

        // Delete the folders we created
        if (Directory.Exists(monitoringPath1)) Directory.Delete(monitoringPath1, true);
        if (Directory.Exists(monitoringPath2)) Directory.Delete(monitoringPath2, true);
    }

    private void FileSystemWatcher_ErrorTest3(object? sender, ErrorEventArgs e)
    {
        Assert.Contains("MonitoringFolder3 not found", e.GetException().Message);
        test3EventsErrorCounter++;
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

    private void FileSystemWatcher_ReadyToScan3(object? sender, FileSystemWatcherEventArgs e)
    {
        if (sender is not FileSystemWatcher watcher) return;

        Assert.True(e.Directories.Length == test3ExpectedEventFolderCount, nameof(e.Directories.Length));
        Assert.True(watcher.FileSystemChanges.Count == 0, nameof(FileSystemWatcher.FileSystemChanges.Count));
        Assert.True(watcher.DirectoriesToScan.Count == 0, nameof(FileSystemWatcher.DirectoriesToScan.Count));
        test3EventsCounter++;
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
#endif