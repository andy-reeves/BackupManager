// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="UtilsUnitTests.cs" company="Andy Reeves">
//
//  </copyright>
//  --------------------------------------------------------------------------------------------------------------------

using System.Diagnostics;

using BackupManager;

#if DEBUG

namespace TestProject
{
    public class BackupFileSystemWatcherTests
    {
        private static int _test1EventsCounter;

        private static int _test3EventsCounter;

        private static int _test3EventsErrorCounter;

        private static int _test3ExpectedEventFolderCount;

        [Fact]
        public void BackupFileSystemWatcherTest1()
        {
            const int waitInSeconds = 4;
            var monitoringPath1 = Path.Combine(Path.GetTempPath(), "MonitoringFolder1");
            var monitoringPath2 = Path.Combine(Path.GetTempPath(), "MonitoringFolder2");
            EnsureFoldersForDirectoryPath(monitoringPath1);
            EnsureFoldersForDirectoryPath(monitoringPath2);
            var watcher = new BackupFileSystemWatcher();
            Assert.True(watcher.Filter == "*", nameof(watcher.Filter));
            Assert.True(watcher.IncludeSubdirectories == false, nameof(watcher.IncludeSubdirectories));
            Assert.True(watcher.ScanTimer == 60, nameof(watcher.ScanTimer));
            Assert.True(watcher.FoldersToMonitor.Length == 0, nameof(watcher.FoldersToMonitor.Length));
            Assert.True(watcher.NotifyFilter == (NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName), nameof(watcher.NotifyFilter));
            Assert.True(watcher.ProcessChangesTimer == 30, nameof(watcher.ProcessChangesTimer));
            Assert.True(BackupFileSystemWatcher.ResetFolderCollections(), nameof(BackupFileSystemWatcher.ResetFolderCollections));
            Assert.True(BackupFileSystemWatcher.FoldersToScan.Count == 0, nameof(BackupFileSystemWatcher.FoldersToScan.Count));
            Assert.True(BackupFileSystemWatcher.FileOrFolderChanges.Count == 0, nameof(BackupFileSystemWatcher.FileOrFolderChanges.Count));
            watcher.Filter = "*";
            watcher.IncludeSubdirectories = true;
            watcher.ScanTimer = 2;
            BackupFileSystemWatcher.MinimumAgeBeforeScanning = 2;
            watcher.FoldersToMonitor = new[] { monitoringPath1, monitoringPath2 };
            watcher.ProcessChangesTimer = 1;
            BackupFileSystemWatcher.ReadyToScan += BackupFileSystemWatcher_ReadyToScan1;
            Assert.True(BackupFileSystemWatcher.FoldersToScan.Count == 0, nameof(BackupFileSystemWatcher.FoldersToScan.Count));
            Assert.True(BackupFileSystemWatcher.FileOrFolderChanges.Count == 0, nameof(BackupFileSystemWatcher.FileOrFolderChanges.Count));
            watcher.Start();
            CreateFile(Path.Combine(monitoringPath1, "test1.txt"));
            CreateFile(Path.Combine(monitoringPath2, "test2.txt"));
            CreateFile(Path.Combine(monitoringPath2, "subFolder", "test3.txt"));
            Wait(waitInSeconds);
            Assert.True(_test1EventsCounter == 1);
            Assert.True(BackupFileSystemWatcher.FileOrFolderChanges.Count == 0, nameof(BackupFileSystemWatcher.FileOrFolderChanges.Count));
            Assert.True(BackupFileSystemWatcher.FoldersToScan.Count == 0, nameof(BackupFileSystemWatcher.FoldersToScan.Count));
            watcher.Stop();

            //Unhook event handlers
            BackupFileSystemWatcher.ReadyToScan -= BackupFileSystemWatcher_ReadyToScan1;

            // Delete the folders we created
            if (Directory.Exists(monitoringPath1)) Directory.Delete(monitoringPath1, true);
            if (Directory.Exists(monitoringPath2)) Directory.Delete(monitoringPath2, true);
        }

        /// <summary>
        ///     Checks the watcher will not start with missing folders
        /// </summary>
        [Fact]
        public void BackupFileSystemWatcherTest2()
        {
            var monitoringPath1 = Path.Combine(Path.GetTempPath(), "MonitoringFolder1");
            var monitoringPath2 = Path.Combine(Path.GetTempPath(), "MonitoringFolder2");
            var monitoringPath3Missing = Path.Combine(Path.GetTempPath(), "MonitoringFolder3");
            if (Directory.Exists(monitoringPath3Missing)) Directory.Delete(monitoringPath3Missing, true);
            EnsureFoldersForDirectoryPath(monitoringPath1);
            EnsureFoldersForDirectoryPath(monitoringPath2);
            var watcher = new BackupFileSystemWatcher { FoldersToMonitor = new[] { monitoringPath1, monitoringPath2, monitoringPath3Missing } };
            BackupFileSystemWatcher.ReadyToScan += BackupFileSystemWatcher_ReadyToScan2;
            Assert.Throws<ArgumentException>(() => watcher.Start());

            //Unhook event handlers
            BackupFileSystemWatcher.ReadyToScan -= BackupFileSystemWatcher_ReadyToScan2;

            // Delete the folders we created
            if (Directory.Exists(monitoringPath1)) Directory.Delete(monitoringPath1, true);
            if (Directory.Exists(monitoringPath2)) Directory.Delete(monitoringPath2, true);
        }

        private static void CreateFile(string filePath)
        {
            Utils.EnsureDirectories(filePath);
            File.AppendAllText(filePath, "test");
        }

        private static void EnsureFoldersForDirectoryPath(string directoryPath)
        {
            Utils.EnsureDirectories(Path.Combine(directoryPath, "temp.txt"));
        }

        [Fact]
        public void BackupFileSystemWatcherTest3()
        {
            const int waitInSeconds = 4;
            var monitoringPath1 = Path.Combine(Path.GetTempPath(), "MonitoringFolder1");
            var monitoringPath2 = Path.Combine(Path.GetTempPath(), "MonitoringFolder2");
            var monitoringPath3DeletedAfterABit = Path.Combine(Path.GetTempPath(), "MonitoringFolder3");
            EnsureFoldersForDirectoryPath(monitoringPath1);
            EnsureFoldersForDirectoryPath(monitoringPath2);
            EnsureFoldersForDirectoryPath(monitoringPath3DeletedAfterABit);

            var watcher = new BackupFileSystemWatcher
            {
                Filter = "*",
                IncludeSubdirectories = true,
                ScanTimer = 1,
                FoldersToMonitor = new[] { monitoringPath1, monitoringPath2, monitoringPath3DeletedAfterABit },
                ProcessChangesTimer = 1
            };
            BackupFileSystemWatcher.MinimumAgeBeforeScanning = 2;
            BackupFileSystemWatcher.ReadyToScan += BackupFileSystemWatcher_ReadyToScan3;
            BackupFileSystemWatcher.Error += BackupFileSystemWatcher_ErrorTest3;
            watcher.Start();
            _test3ExpectedEventFolderCount = 4;
            CreateFile(Path.Combine(monitoringPath1, "test1.txt"));
            CreateFile(Path.Combine(monitoringPath2, "test2.txt"));
            CreateFile(Path.Combine(monitoringPath2, "subFolder", "test3.txt"));
            CreateFile(Path.Combine(monitoringPath3DeletedAfterABit, "test4.txt"));
            Wait(waitInSeconds);
            Assert.True(_test3EventsCounter == 1);
            Assert.True(BackupFileSystemWatcher.FileOrFolderChanges.Count == 0, nameof(BackupFileSystemWatcher.FileOrFolderChanges.Count));
            Assert.True(BackupFileSystemWatcher.FoldersToScan.Count == 0, nameof(BackupFileSystemWatcher.FoldersToScan.Count));
            watcher.Stop();

            // Now delete a folder we are monitoring after we've stopped
            if (Directory.Exists(monitoringPath3DeletedAfterABit)) Directory.Delete(monitoringPath3DeletedAfterABit, true);

            // should fail to restart because a folder is missing now
            Assert.Throws<ArgumentException>(() => watcher.Start());

            // now create the folder again and start
            EnsureFoldersForDirectoryPath(monitoringPath3DeletedAfterABit);
            watcher.Start();
            _test3ExpectedEventFolderCount = 4;
            CreateFile(Path.Combine(monitoringPath1, "test1.txt"));
            CreateFile(Path.Combine(monitoringPath2, "test2.txt"));
            CreateFile(Path.Combine(monitoringPath2, "subFolder", "test3.txt"));
            CreateFile(Path.Combine(monitoringPath3DeletedAfterABit, "test4.txt"));
            Wait(waitInSeconds);
            Assert.True(_test3EventsCounter == 2);

            //delete a folder while we're monitoring it
            if (Directory.Exists(monitoringPath3DeletedAfterABit)) Directory.Delete(monitoringPath3DeletedAfterABit, true);
            _test3ExpectedEventFolderCount = 4;

            // now create the folder and file again
            CreateFile(Path.Combine(monitoringPath1, "test1.txt"));
            CreateFile(Path.Combine(monitoringPath2, "test2.txt"));
            CreateFile(Path.Combine(monitoringPath2, "subFolder", "test3.txt"));
            Wait(waitInSeconds);
            Assert.True(_test3EventsErrorCounter == 1);

            // should fail to restart because a folder is missing now
            Assert.Throws<ArgumentException>(() => watcher.Start());

            // now create the folder again and start
            EnsureFoldersForDirectoryPath(monitoringPath3DeletedAfterABit);
            watcher.Start();
            _test3ExpectedEventFolderCount = 4;
            CreateFile(Path.Combine(monitoringPath1, "test1.txt"));
            CreateFile(Path.Combine(monitoringPath2, "test2.txt"));
            CreateFile(Path.Combine(monitoringPath2, "subFolder", "test3.txt"));
            CreateFile(Path.Combine(monitoringPath3DeletedAfterABit, "test4.txt"));
            Wait(waitInSeconds);
            Assert.True(_test3EventsCounter == 3);
            Assert.True(_test3EventsErrorCounter == 1);
            watcher.Stop();

            //Unhook event handlers
            BackupFileSystemWatcher.ReadyToScan -= BackupFileSystemWatcher_ReadyToScan3;
            BackupFileSystemWatcher.Error -= BackupFileSystemWatcher_ErrorTest3;

            // Delete the folders we created
            if (Directory.Exists(monitoringPath1)) Directory.Delete(monitoringPath1, true);
            if (Directory.Exists(monitoringPath2)) Directory.Delete(monitoringPath2, true);
            if (Directory.Exists(monitoringPath3DeletedAfterABit)) Directory.Delete(monitoringPath3DeletedAfterABit, true);
        }

        private static void BackupFileSystemWatcher_ErrorTest3(object? sender, ErrorEventArgs e)
        {
            Assert.Contains("MonitoringFolder3 not found.", e.GetException().Message);
            _test3EventsErrorCounter++;
        }

        private static void Wait(int howManySecondsToWait)
        {
            var howLongToWait = new TimeSpan(0, 0, howManySecondsToWait);
            var sw = Stopwatch.StartNew();

            while (sw.Elapsed < howLongToWait) { }
        }

        private static void BackupFileSystemWatcher_ReadyToScan1(object? sender, BackupFileSystemWatcherEventArgs e)
        {
            foreach (var folder in e.Folders)
            {
                Utils.Trace($"{folder.Path} at {folder.ModifiedDateTime}");
            }
            Assert.True(e.Folders.Length == 3, nameof(e.Folders.Length));
            Assert.True(BackupFileSystemWatcher.FileOrFolderChanges.Count == 0, nameof(BackupFileSystemWatcher.FileOrFolderChanges.Count));
            Assert.True(BackupFileSystemWatcher.FoldersToScan.Count == 0, nameof(BackupFileSystemWatcher.FoldersToScan.Count));
            _test1EventsCounter += 1;
        }

        private static void BackupFileSystemWatcher_ReadyToScan2(object? sender, BackupFileSystemWatcherEventArgs e)
        {
            foreach (var folder in e.Folders)
            {
                Utils.Trace($"{folder.Path} at {folder.ModifiedDateTime}");
            }
            Assert.True(e.Folders.Length == 3, nameof(e.Folders.Length));
            Assert.True(BackupFileSystemWatcher.FileOrFolderChanges.Count == 0, nameof(BackupFileSystemWatcher.FileOrFolderChanges.Count));
            Assert.True(BackupFileSystemWatcher.FoldersToScan.Count == 0, nameof(BackupFileSystemWatcher.FoldersToScan.Count));
        }

        private static void BackupFileSystemWatcher_ReadyToScan3(object? sender, BackupFileSystemWatcherEventArgs e)
        {
            foreach (var folder in e.Folders)
            {
                Utils.Trace($"{folder.Path} at {folder.ModifiedDateTime}");
            }
            Assert.True(e.Folders.Length == _test3ExpectedEventFolderCount, nameof(e.Folders.Length));
            Assert.True(BackupFileSystemWatcher.FileOrFolderChanges.Count == 0, nameof(BackupFileSystemWatcher.FileOrFolderChanges.Count));
            Assert.True(BackupFileSystemWatcher.FoldersToScan.Count == 0, nameof(BackupFileSystemWatcher.FoldersToScan.Count));
            _test3EventsCounter++;
        }
    }
}
#endif