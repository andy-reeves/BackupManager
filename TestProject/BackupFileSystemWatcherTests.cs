

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
        [Fact]
        public void BackupFileSystemWatcherTest1()
        {
            try
            {
                var monitoringPath1 = Path.Combine(Path.GetTempPath(), "MonitoringFolder1");
                var monitoringPath2 = Path.Combine(Path.GetTempPath(), "MonitoringFolder2");
                Utils.EnsureDirectories(Path.Combine(monitoringPath1, "temp.txt"));
                Utils.EnsureDirectories(Path.Combine(monitoringPath2, "temp.txt"));
                var howLongToWait = new TimeSpan(0, 0, 5);

                var fileSystemWatcher = new BackupFileSystemWatcher
                {
                    Filter = "*",
                    IncludeSubdirectories = true,
                    EnableRaisingEvents = true,
                    ScanTimer = 2,
                    MinimumAgeBeforeScanning = 2,
                    FoldersToMonitor = new[] { monitoringPath1, monitoringPath2 },
                    NotifyFilter = NotifyFilters.Attributes | NotifyFilters.CreationTime | NotifyFilters.DirectoryName | NotifyFilters.FileName |
                                   NotifyFilters.LastAccess | NotifyFilters.LastWrite | NotifyFilters.Security | NotifyFilters.Size,
                    ProcessChangesTimer = 1
                };
                BackupFileSystemWatcher.ReadyToScan += BackupFileSystemWatcher_ReadyToScan;
                fileSystemWatcher.Start();
                File.AppendAllText(Path.Combine(monitoringPath1, "test1.txt"), "test");

                // Task.Delay(30000).Wait();
                File.AppendAllText(Path.Combine(monitoringPath2, "test2.txt"), "test");

                //Task.Delay(30000).Wait();
                var filePath = Path.Combine(monitoringPath2, "subFolder", "test3.txt");
                Utils.EnsureDirectories(filePath);
                File.AppendAllText(filePath, "test");
                var sw = Stopwatch.StartNew();

                while (sw.Elapsed < howLongToWait) { }
                Assert.True(BackupFileSystemWatcher.FileOrFolderChanges.Count == 0);
                Assert.True(BackupFileSystemWatcher.FoldersToScan.Count == 0);
                fileSystemWatcher.Stop();
                if (Directory.Exists(monitoringPath1)) Directory.Delete(monitoringPath1, true);
                if (Directory.Exists(monitoringPath2)) Directory.Delete(monitoringPath2, true);
            }
            catch (ApplicationException ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        private static void BackupFileSystemWatcher_ReadyToScan(object? sender, BackupFileSystemWatcherEventArgs e)
        {
            foreach (var folder in e.Folders)
            {
                Utils.Trace($"{folder.Path} at {folder.ModifiedDateTime}");
            }
            Assert.True(e.Folders.Length == 3);
            Assert.True(BackupFileSystemWatcher.FileOrFolderChanges.Count == 0);
            Assert.True(BackupFileSystemWatcher.FoldersToScan.Count == 0);
        }
    }
}
#endif
