using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

using BackupManager;
using BackupManager.Extensions;

using FileSystemWatcher = BackupManager.FileSystemWatcher;

namespace TestProject;

[SuppressMessage("ReSharper", "MemberCanBeFileLocal")]
[SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "<Pending>")]
public sealed class FileSystemWatcherTests2
{
    private static readonly string _testPath;

    private static readonly ConcurrentDictionary<string, bool> _filesDictionary = new();

    private static readonly ConcurrentDictionary<string, bool> _dirDictionary = new();

    private static int _eventCounter;

    static FileSystemWatcherTests2()
    {
        _testPath = Path.Combine(Utils.GetProjectPath(typeof(MediaHelperTests)), @"..\..\FSWTest");
        _testPath = Path.GetFullPath(_testPath);
    }

    /// <summary>
    ///     Tests this instance.
    /// </summary>
    [Fact]
    public void FileSystemWatcherTest2A()
    {
        const int itemsToCreate = 100;
        const int milliSecondsToWaitForCompletion = 50;

        // before:
        // 10 in 1s and 100 in 5s but 10_000 fails with max buffer in 10s

        // now working:
        // 100 in 50ms  64k buffer
        // 100 in 250ms 64k buffer
        // 10k in  5s   64k buffer
        // 50k in 10s   64k buffer 

        //and also:
        // 50k in 20s   10k buffer

        // Set up the FSW
        // create xxx files in xxx directories in the source folder and keep a list
        // when the ReadyToScan fires check the file/directory its firing for and mark them as done
        // should only be a few seconds
        if (Directory.Exists(_testPath)) Directory.Delete(_testPath, true);
        _ = Directory.CreateDirectory(_testPath);

        var fileSystemWatcher = new FileSystemWatcher
        {
            Filter = "*.*",
            RegexFilter = "",
            ProcessChangesInterval = 1,
            ScanInterval = 1,
            MinimumAgeBeforeScanEventRaised = 1,
            Directories = new[] { _testPath },
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.LastAccess | NotifyFilters.CreationTime | NotifyFilters.FileName |
                           NotifyFilters.DirectoryName
        };
        fileSystemWatcher.ReadyToScan += FileSystemWatcher_ReadyToScan;
        fileSystemWatcher.Error += FileSystemWatcher_OnError;
        _ = fileSystemWatcher.Start();

        // Create the number of folders and put a file in each
        for (var i = 0; i < itemsToCreate; i++)
        {
            var dirPath = Path.Combine(_testPath, $"Dir{i}");
            var filePath = Path.Combine(dirPath, $"file{i}.txt");
            Utils.File.Create(filePath);
            _ = _filesDictionary.TryAdd(filePath, false);
            _ = _dirDictionary.TryAdd(dirPath, false);
        }
        Utils.Wait(milliSecondsToWaitForCompletion);

        // Check they've all been raised to our ReadyToScan handler
        for (var i = 0; i < itemsToCreate; i++)
        {
            var dirPath = Path.Combine(_testPath, $"Dir{i}");
            var returnValue = _dirDictionary.TryGetValue(dirPath, out var value);
            Assert.True(returnValue, $"Couldn't get value for {dirPath}");
            Assert.True(value, $"{dirPath} is still False");
        }
        Assert.Empty(fileSystemWatcher.FileSystemChanges);
        Assert.Empty(fileSystemWatcher.DirectoriesToScan);
        Utils.Trace($"event count ended = {_eventCounter}");
    }

    private static void FileSystemWatcher_OnError(object? sender, ErrorEventArgs e)
    {
        var ex = e.GetException();
        Assert.False(ex.Message.StartsWithIgnoreCase("too many"), "Too many Exception in FSW");
    }

    private static void FileSystemWatcher_ReadyToScan(object? sender, FileSystemWatcherEventArgs e)
    {
        _eventCounter++;
        Utils.Trace($"event count = {_eventCounter}");

        foreach (var entry in e.Directories)
        {
            if (Utils.GetFileSystemEntryType(entry.Path) == FileSystemEntryType.File)
            {
                var returnValue = _filesDictionary.TryGetValue(entry.Path, out var currentValue);
                Assert.True(returnValue, $"Couldn't find {entry.Path}");
                if (currentValue) Assert.Fail($"Key {entry.Path} is already True");
                returnValue = _filesDictionary.TryUpdate(entry.Path, true, false);
                Assert.True(returnValue, $"Couldn't update {entry.Path} to True");
            }
            else
            {
                if (entry.Path == _testPath) return;

                var returnValue = _dirDictionary.TryGetValue(entry.Path, out var currentValue);
                Assert.True(returnValue, $"Couldn't find {entry.Path}");
                if (currentValue) continue;

                returnValue = _dirDictionary.TryUpdate(entry.Path, true, currentValue);
                Assert.True(returnValue, $"Couldn't update {entry.Path} to True");
            }
        }
    }
}
