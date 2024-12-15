// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="EntityTests.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Xml.Serialization;

using BackupManager;
using BackupManager.Entities;
using BackupManager.Extensions;

namespace TestProject;

[SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "<Pending>")]
[SuppressMessage("ReSharper", "MemberCanBeFileLocal")]
[SuppressMessage("ReSharper", "StringLiteralTypo")]
public sealed class EntityTests
{
    static EntityTests()
    {
        Utils.Config = BackupManager.Entities.MediaBackup.Load(Path.Combine(Utils.GetProjectPath(typeof(EntityTests)), "..\\BackupManager\\MediaBackup.xml")).Config;
    }

    [Fact]
    public void DirectoryScan()
    {
        var dateTime = DateTime.Now;
        var a = new DirectoryScan(DirectoryScanType.ProcessingFiles, @"c:\testPath1", dateTime, "1");
        Assert.Equal(@"c:\testPath1", a.Path);
        Assert.Equal(dateTime, a.StartDateTime);
        Assert.Equal(DateTime.MinValue, a.EndDateTime);
        Assert.Equal(TimeSpan.Zero, a.ScanDuration);
        var endDate = dateTime.AddDays(1);
        a.EndDateTime = endDate;
        Assert.Equal(dateTime, a.StartDateTime);
        Assert.Equal(endDate, a.EndDateTime);
        Assert.Equal(new TimeSpan(1, 0, 0, 0), a.ScanDuration);
        var b = new DirectoryScan(DirectoryScanType.ProcessingFiles, @"c:\testPath2", DateTime.Now, "1");
        b.EndDateTime = b.StartDateTime.AddDays(2).AddHours(12);
        var list = new[] { a, b };
        Assert.True(BackupManager.Entities.DirectoryScan.LapsedTime(list) > new TimeSpan(2, 12, 0, 0));
        Assert.True(BackupManager.Entities.DirectoryScan.LapsedTime(list) < new TimeSpan(2, 12, 0, 1));
        Assert.False(a.Equals(b));
        object obj = a;
        Assert.False(obj.Equals(b));
        Assert.Equal(@"c:\testPath1", a.ToString());
        Assert.Equal(@"c:\testPath1", obj.ToString());
    }

    [Fact]
    public void MediaBackup()
    {
        var pathToFiles = Path.Combine(Path.GetTempPath(), "BackupFile1");
        var pathToBackupFile2 = Path.Combine(Path.GetTempPath(), "BackupFile2");
        var pathToBackupShare = Path.Combine(Path.GetTempPath(), "BackupFile3");
        var pathToBackupDisk = Path.Combine(pathToBackupShare, "backup 1000");
        var pathToMovies = Path.Combine(pathToFiles, "_Movies");
        var pathToMovies2 = Path.Combine(pathToBackupFile2, "_Movies");
        var pathToTv = Path.Combine(pathToBackupFile2, "_TV");
        if (Directory.Exists(pathToFiles)) _ = Utils.Directory.Delete(pathToFiles, true);
        if (Directory.Exists(pathToBackupFile2)) _ = Utils.Directory.Delete(pathToBackupFile2, true);
        if (Directory.Exists(pathToBackupShare)) _ = Utils.Directory.Delete(pathToBackupShare, true);
        var pathToFile1 = Path.Combine(pathToMovies, "test1.txt");
        var pathToFile2 = Path.Combine(pathToTv, "test2.txt");
        var pathToFile3 = Path.Combine(pathToMovies2, "test1.txt");
        Utils.Directory.EnsurePath(pathToMovies);
        Utils.Directory.EnsurePath(pathToTv);
        Utils.Directory.EnsurePath(pathToBackupDisk);
        Utils.File.Create(pathToFile1);
        Utils.File.Create(pathToFile2);
        Utils.File.Create(pathToFile3);
        var mediaBackup = BackupManager.Entities.MediaBackup.Load(Path.Combine(Utils.GetProjectPath(typeof(FileRulesUnitTest)), "..\\BackupManager\\MediaBackup.xml"));
        mediaBackup.BackupMediaFile(new CancellationToken());
        mediaBackup.Config.DirectoriesToBackup.Add(pathToMovies);
        mediaBackup.Config.DirectoriesToBackup.Add(pathToTv);
        mediaBackup.Config.DirectoriesToBackup.Add(pathToMovies2);

        // GetFoldersForPath
        Assert.True(mediaBackup.GetFoldersForPath(pathToFile1, out var directory, out var relativePath));
        Assert.Equal(pathToMovies, directory);
        Assert.Equal("test1.txt", relativePath);

        // GetBackupFile
        var backupFile = mediaBackup.GetBackupFile(pathToFile1);
        Assert.Equal("test1.txt", backupFile.RelativePath);
        var f1 = mediaBackup.GetBackupFileFromContentsHashcode("b3d5cf638ed2f6a94d6b3c628f946196");
        Assert.Equal("test1.txt", f1.RelativePath);
        var backupFile2 = mediaBackup.GetBackupFile(pathToFile1);
        Assert.Equal("test1.txt", backupFile2.RelativePath);

        // GetBackupFileFromContentsHashcode
        Assert.Null(mediaBackup.GetBackupFileFromContentsHashcode("test"));

        // EnsureFile
        Assert.True(mediaBackup.EnsureFile(pathToFile2));

        // GetParentPath
        Assert.Null(mediaBackup.GetParentPath(pathToFile1));

        // GetFilters
        if (Utils.Config.PlexToken.HasNoValue()) Assert.Equal("!*.bup,!*-TdarrCacheFile-*.*", mediaBackup.GetFilters());

        // GetBackupDisk
        var disk = mediaBackup.GetBackupDisk(pathToBackupShare);
        Assert.Equal("backup 1000", disk.Name);
        disk = mediaBackup.GetBackupDisk(pathToBackupShare);
        Assert.Equal("backup 1000", disk.Name);

        // GetBackupFileFromHashKey
        Assert.NotNull(mediaBackup.GetBackupFileFromHashKey(@"_Movies\test1.txt"));

        // GetBackupFilesOnBackupDisk
        IEnumerable<BackupFile> backupFiles = mediaBackup.GetBackupFilesOnBackupDisk(disk.Name, false);
        Assert.NotNull(backupFiles);
        Assert.Empty(backupFiles);
        backupFile.Disk = "backup 1000";
        backupFiles = mediaBackup.GetBackupFilesOnBackupDisk(disk.Name, false);
        Assert.NotNull(backupFiles);
        _ = Assert.Single(backupFiles);

        if (Utils.Config.PlexToken.HasNoValue())
        {
            // GetOldestFile
            Assert.Null(mediaBackup.GetOldestFile());
            backupFile.UpdateDiskChecked("backup 1000");
            Assert.NotNull(mediaBackup.GetOldestFile());
        }

        // GetBackupFilesInDirectory
        IEnumerable<BackupFile> a = mediaBackup.GetBackupFilesInDirectory(pathToMovies, true);
        Assert.NotNull(a);
        _ = Assert.Single(a);

        // GetBackupFilesNotMarkedAsDeleted
        IEnumerable<BackupFile> c = mediaBackup.GetBackupFiles(false);
        Assert.NotNull(c);

        if (Utils.Config.PlexToken.HasNoValue())
        {
            Assert.Equal(2, c.Count());
            backupFile.Deleted = true;

            // GetBackupFilesMarkedAsDeleted
            IEnumerable<BackupFile> b = mediaBackup.GetBackupFilesMarkedAsDeleted(false);
            Assert.NotNull(b);
            _ = Assert.Single(b);

            // GetBackupFilesWithDiskEmpty
            backupFile.Deleted = false;
            IEnumerable<BackupFile> d = mediaBackup.GetBackupFilesWithDiskEmpty();
            Assert.NotNull(d);
            var collection = d as BackupFile[] ?? d.ToArray();
            _ = Assert.Single(collection);

            foreach (var file in collection)
            {
                Assert.Equal("test2.txt", file.RelativePath);
            }
        }

        // Contains
        Assert.True(mediaBackup.Contains(@"_Movies\test1.txt"));

        // ClearFlags
        backupFile.Flag = true;
        mediaBackup.ClearFlags();

        foreach (var file in mediaBackup.BackupFiles)
        {
            Assert.False(file.Flag);
        }

        if (Utils.Config.PlexToken.HasNoValue())
        {
            // DirectoriesLastFullScan
            Assert.Equal("2023-01-01", mediaBackup.DirectoriesLastFullScan);
            mediaBackup.UpdateLastFullScan();
            Assert.NotEqual("2023-01-01", mediaBackup.DirectoriesLastFullScan);
        }

        // GetBackFile (with files with the same hash in different locations)
        _ = mediaBackup.EnsureFile(pathToFile3);
        var j = mediaBackup.GetBackupFile(pathToFile3);
        Assert.NotNull(j);
        Assert.Equal("test1.txt", j.RelativePath);
        var k = mediaBackup.GetBackupFile(pathToFile1);
        Assert.NotNull(k);
        Assert.Equal("test1.txt", k.RelativePath);

        if (Utils.Config.PlexToken.HasNoValue())
        {
            // Remove
            mediaBackup.RemoveFile(backupFile);
            _ = Assert.Single(mediaBackup.BackupFiles);
            mediaBackup.RemoveFilesWithFlag(false, false);
            Assert.Empty(mediaBackup.BackupFiles);
        }

        // Tidy up folders
        if (Directory.Exists(pathToFiles)) _ = Utils.Directory.Delete(pathToFiles, true);
        if (Directory.Exists(pathToBackupFile2)) _ = Utils.Directory.Delete(pathToBackupFile2, true);
        if (Directory.Exists(pathToBackupShare)) _ = Utils.Directory.Delete(pathToBackupShare, true);
    }

    [Fact]
    public void BackupFile()
    {
        var pathToFiles = Path.Combine(Path.GetTempPath(), "BackupFile1");
        var pathToBackupFile2 = Path.Combine(Path.GetTempPath(), "BackupFile2");
        var pathToBackupShare = Path.Combine(Path.GetTempPath(), "BackupFile3");
        var pathToBackupDisk = Path.Combine(pathToBackupShare, "backup 1000");
        var pathToMovies = Path.Combine(pathToFiles, "_Movies");
        var pathToTv = Path.Combine(pathToBackupFile2, "_TV");
        var pathToMoviesOnBackupDisk = Path.Combine(pathToBackupDisk, "_Movies");
        if (Directory.Exists(pathToFiles)) _ = Utils.Directory.Delete(pathToFiles, true);
        if (Directory.Exists(pathToBackupFile2)) _ = Utils.Directory.Delete(pathToBackupFile2, true);
        if (Directory.Exists(pathToBackupShare)) _ = Utils.Directory.Delete(pathToBackupShare, true);
        var pathToFile1 = Path.Combine(pathToMovies, "test1.txt");
        var pathToFile2 = Path.Combine(pathToTv, "test2.txt");
        var pathToFile1OnBackupDisk = Path.Combine(pathToMoviesOnBackupDisk, "test1.txt");
        Utils.Directory.EnsurePath(pathToMovies);
        Utils.Directory.EnsurePath(pathToTv);
        Utils.File.Create(pathToFile1);
        Utils.File.Create(pathToFile2);
        var backupFile1 = new BackupFile(pathToFile1, pathToMovies);
        Assert.Equal("test1.txt", backupFile1.RelativePath);
        Assert.Equal(9, backupFile1.Length);
        Assert.Equal(".txt", backupFile1.Extension);
        var backupFile2 = new BackupFile(pathToFile2, pathToTv);
        Assert.NotEqual(backupFile2, backupFile1);
        Assert.False(backupFile1.Equals(null));
        object obj = backupFile1;
        Assert.False(obj.Equals(backupFile2));
        Assert.Equal("b3d5cf638ed2f6a94d6b3c628f946196", backupFile1.ContentsHash);
        Assert.Equal("c91e47329777637e2370464651ba47aa", backupFile2.ContentsHash);
        backupFile1.ContentsHash = null;
        Assert.Equal("b3d5cf638ed2f6a94d6b3c628f946196", backupFile1.ContentsHash);
        backupFile1.Deleted = true;
        Assert.True(backupFile1.Deleted);
        backupFile1.Flag = true;
        Assert.True(backupFile1.Flag);
        Assert.Equal(0, backupFile1.BackupDiskNumber);
        Assert.Equal("_Movies\\test1.txt", backupFile1.Hash);
        Assert.False(backupFile1.DiskCheckedTime.HasValue);
        Assert.Equal("", backupFile1.Disk);
        backupFile1.Disk = "backup 45";
        Assert.Equal("backup 45", backupFile1.Disk);
        Assert.Equal(45, backupFile1.BackupDiskNumber);
        backupFile1.Disk = "backup ";
        Assert.Equal("backup ", backupFile1.Disk);
        Assert.Equal(0, backupFile1.BackupDiskNumber);
        backupFile1.DiskCheckedTime = null;
        backupFile1.UpdateDiskChecked("backup 45");
        Assert.True(backupFile1.DiskCheckedTime.HasValue);
        Assert.Equal("test1.txt", backupFile1.FileName);
        Assert.Equal(pathToFile1OnBackupDisk, backupFile1.BackupDiskFullPath(pathToBackupDisk));
        backupFile1.ClearDiskChecked();
        Assert.False(backupFile1.DiskCheckedTime.HasValue);
        Assert.NotEqual(0, backupFile1.GetHashCode());
        var ct = new CancellationToken();
        _ = Utils.File.Copy(pathToFile1, pathToFile1OnBackupDisk, ct);
        var backupDisk = new BackupDisk("backup 1000", pathToBackupShare);
        Assert.True(backupFile1.CheckContentHashes(backupDisk));
        Assert.False(backupFile1.Deleted);
        File.AppendAllText(pathToFile1OnBackupDisk, "test");
        Assert.False(backupFile1.CheckContentHashes(backupDisk));
        if (Directory.Exists(pathToFiles)) _ = Utils.Directory.Delete(pathToFiles, true);
        if (Directory.Exists(pathToBackupFile2)) _ = Utils.Directory.Delete(pathToBackupFile2, true);
        if (Directory.Exists(pathToBackupShare)) _ = Utils.Directory.Delete(pathToBackupShare, true);
    }

    [Fact]
    public void BackupDisk()
    {
        const string backupShareName = @"d:\";
        const string backupDiskName = "backup 45";
        var backupDisk = new BackupDisk(backupDiskName, backupShareName);
        Assert.Equal(Path.Combine(backupShareName, backupDiskName), backupDisk.BackupPath);
        Assert.Null(backupDisk.CheckedTime);
        Assert.Equal(45, backupDisk.Number);
        backupDisk.UpdateDiskChecked();
        Assert.NotNull(backupDisk.CheckedTime);
        Assert.Equal(45, backupDisk.GetHashCode());
        Assert.Equal(backupDiskName, backupDisk.ToString());
        _ = backupDisk.Update(null);

        if (Utils.Config.PlexToken.HasNoValue())
        {
            Assert.NotEqual(0, backupDisk.Capacity);
            Assert.NotEqual(string.Empty, backupDisk.CapacityFormatted);
            Assert.NotEqual(0, backupDisk.Free);
            Assert.NotEqual(string.Empty, backupDisk.FreeFormatted);
            Assert.NotEqual(string.Empty, backupDisk.LastReadSpeed);
            Assert.NotEqual(string.Empty, backupDisk.LastWriteSpeed);
        }
        var backupDisk2 = new BackupDisk(backupDiskName, backupShareName);
        Assert.Equal(backupDisk2, backupDisk);
        Assert.False(backupDisk.Equals(null));
        object obj = backupDisk;
        Assert.True(obj.Equals(backupDisk2));
        backupDisk.UpdateSpeeds(25, 30);
        Assert.Equal("25 bytes/s", backupDisk.LastReadSpeed);
        Assert.Equal("30 bytes/s", backupDisk.LastWriteSpeed);
    }

    [Fact]
    public void Folder()
    {
        var path = Path.GetTempFileName();
        var folder1 = new FileSystemEntry(@"c:\bob", DateTime.Now);
        XmlSerializer xmlSerializer = new(typeof(FileSystemEntry));
        StreamWriter streamWriter = new(path);
        xmlSerializer.Serialize(streamWriter, folder1);
        streamWriter.Close();
        FileSystemEntry? folder2;
        XmlSerializer serializer = new(typeof(FileSystemEntry));

        using (FileStream stream = new(path, FileMode.Open, FileAccess.Read))
        {
            folder2 = serializer.Deserialize(stream) as FileSystemEntry;
        }
        Assert.True(folder1.Equals(folder2));
        var collection1 = new Collection<FileSystemEntry> { folder1, new(@"barry") };
        xmlSerializer = new XmlSerializer(typeof(Collection<FileSystemEntry>));
        streamWriter = new StreamWriter(path);
        xmlSerializer.Serialize(streamWriter, collection1);
        streamWriter.Close();
        Collection<FileSystemEntry>? collection2;
        XmlSerializer serializer2 = new(typeof(Collection<FileSystemEntry>));

        using (FileStream stream = new(path, FileMode.Open, FileAccess.Read))
        {
            collection2 = serializer2.Deserialize(stream) as Collection<FileSystemEntry>;
        }
        Assert.True(collection2 != null && collection1.SequenceEqual(collection2));
        _ = Utils.File.Delete(path);
    }
}