// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="EntityTests.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

#if DEBUG
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Xml.Serialization;

using BackupManager;
using BackupManager.Entities;

namespace TestProject;

[SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "<Pending>")]
[SuppressMessage("ReSharper", "MemberCanBeFileLocal")]
public sealed class EntityTests
{
    /// <summary>
    ///     This file has a hash of 098f6bcd4621d373cade4e832627b4f6 and length of 4
    /// </summary>
    /// <param name="filePath"></param>
    private static void CreateFile(string filePath)
    {
        Utils.EnsureDirectoriesForFilePath(filePath);
        File.AppendAllText(filePath, "test");
    }

    [Fact]
    public void MediaBackup()
    {
        var pathToFiles = Path.Combine(Path.GetTempPath(), "BackupFile1");
        var pathToBackupFile2 = Path.Combine(Path.GetTempPath(), "BackupFile2");
        var pathToBackupShare = Path.Combine(Path.GetTempPath(), "BackupFile3");
        var pathToBackupDisk = Path.Combine(pathToBackupShare, "backup 1000");
        var pathToMovies = Path.Combine(pathToFiles, "_Movies");
        var pathToTv = Path.Combine(pathToBackupFile2, "_TV");
        if (Directory.Exists(pathToFiles)) Directory.Delete(pathToFiles, true);
        if (Directory.Exists(pathToBackupFile2)) Directory.Delete(pathToBackupFile2, true);
        if (Directory.Exists(pathToBackupShare)) Directory.Delete(pathToBackupShare, true);
        var pathToFile1 = Path.Combine(pathToMovies, "test1.txt");
        var pathToFile2 = Path.Combine(pathToTv, "test2.txt");
        Utils.EnsureDirectoriesForDirectoryPath(pathToMovies);
        Utils.EnsureDirectoriesForDirectoryPath(pathToTv);
        Utils.EnsureDirectoriesForDirectoryPath(pathToBackupDisk);
        CreateFile(pathToFile1);
        CreateFile(pathToFile2);

        var mediaBackup =
            BackupManager.Entities.MediaBackup.Load(Path.Combine(Utils.GetProjectPath(typeof(FileRulesUnitTest)),
                "..\\BackupManager\\MediaBackup.xml"));
        mediaBackup.BackupMediaFile();
        mediaBackup.Config.Directories.Add(pathToMovies);
        mediaBackup.Config.Directories.Add(pathToTv);
        Assert.True(mediaBackup.GetFoldersForPath(pathToFile1, out var directory, out var relativePath));
        Assert.Equal(pathToMovies, directory);
        Assert.Equal("test1.txt", relativePath);
        var backupFile = mediaBackup.GetBackupFile(pathToFile1);
        Assert.Equal("test1.txt", backupFile.RelativePath);
        var f1 = mediaBackup.GetBackupFileFromContentsHashcode("098f6bcd4621d373cade4e832627b4f6");
        Assert.Equal("test1.txt", f1.RelativePath);
        var backupFile2 = mediaBackup.GetBackupFile(pathToFile1);
        Assert.Equal("test1.txt", backupFile2.RelativePath);
        backupFile2.Directory = pathToFile2;
        var backupFile3 = mediaBackup.GetBackupFile(pathToFile2);
        Assert.Equal("test2.txt", backupFile3.RelativePath);
        Assert.True(mediaBackup.EnsureFile(pathToFile2));
        Assert.Null(mediaBackup.GetParentPath(pathToFile1));
        Assert.Equal("!*.bup", mediaBackup.GetFilters());
        var disk = mediaBackup.GetBackupDisk(pathToBackupShare);
        Assert.Equal("backup 1000", disk.Name);
        disk = mediaBackup.GetBackupDisk(pathToBackupShare);
        Assert.Equal("backup 1000", disk.Name);
        Assert.NotNull(mediaBackup.GetBackupFileFromHashKey(@"_Movies\test1.txt"));
        var backupFiles = mediaBackup.GetBackupFilesOnBackupDisk(disk.Name, false);
        Assert.NotNull(backupFiles);
        Assert.Empty(backupFiles);
        backupFile.Disk = "backup 1000";
        backupFiles = mediaBackup.GetBackupFilesOnBackupDisk(disk.Name, false);
        Assert.NotNull(backupFiles);
        Assert.Single(backupFiles);
        var b = mediaBackup.GetOldestFile();
        Assert.Null(b);
        backupFile.UpdateDiskChecked("backup 1000");
        b = mediaBackup.GetOldestFile();
        Assert.NotNull(b);
        mediaBackup.RemoveFile(backupFile);
        Assert.Single(mediaBackup.BackupFiles);
        mediaBackup.RemoveFilesWithFlag(true, false);
        Assert.Empty(mediaBackup.BackupFiles);

        // Tidy up folders
        if (Directory.Exists(pathToFiles)) Directory.Delete(pathToFiles, true);
        if (Directory.Exists(pathToBackupFile2)) Directory.Delete(pathToBackupFile2, true);
        if (Directory.Exists(pathToBackupShare)) Directory.Delete(pathToBackupShare, true);
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
        if (Directory.Exists(pathToFiles)) Directory.Delete(pathToFiles, true);
        if (Directory.Exists(pathToBackupFile2)) Directory.Delete(pathToBackupFile2, true);
        if (Directory.Exists(pathToBackupShare)) Directory.Delete(pathToBackupShare, true);
        var pathToFile1 = Path.Combine(pathToMovies, "test1.txt");
        var pathToFile2 = Path.Combine(pathToTv, "test2.txt");
        var pathToFile1OnBackupDisk = Path.Combine(pathToMoviesOnBackupDisk, "test1.txt");
        Utils.EnsureDirectoriesForDirectoryPath(pathToMovies);
        Utils.EnsureDirectoriesForDirectoryPath(pathToTv);
        CreateFile(pathToFile1);
        CreateFile(pathToFile2);
        var backupFile1 = new BackupFile(pathToFile1, pathToMovies);
        Assert.Equal("test1.txt", backupFile1.RelativePath);
        Assert.Equal(4, backupFile1.Length);
        var backupFile2 = new BackupFile(pathToFile2, pathToTv);
        Assert.NotEqual(backupFile2, backupFile1);
        Assert.False(backupFile1.Equals(null));
        object obj = backupFile1;
        Assert.False(obj.Equals(backupFile2));
        Assert.Equal("098f6bcd4621d373cade4e832627b4f6", backupFile1.ContentsHash);
        Assert.Equal("098f6bcd4621d373cade4e832627b4f6", backupFile1.ContentsHash);
        backupFile1.ContentsHash = null;
        Assert.Equal("098f6bcd4621d373cade4e832627b4f6", backupFile1.ContentsHash);
        backupFile1.Deleted = true;
        Assert.True(backupFile1.Deleted);
        backupFile1.Flag = true;
        Assert.True(backupFile1.Flag);
        Assert.Equal(0, backupFile1.BackupDiskNumber);
        Assert.Equal("_Movies\\test1.txt", backupFile1.Hash);
        Assert.Equal("", backupFile1.DiskChecked);
        Assert.Equal("", backupFile1.Disk);
        backupFile1.Disk = "backup 45";
        Assert.Equal("backup 45", backupFile1.Disk);
        Assert.Equal(45, backupFile1.BackupDiskNumber);
        backupFile1.Disk = "backup ";
        Assert.Equal("backup ", backupFile1.Disk);
        Assert.Equal(0, backupFile1.BackupDiskNumber);
        backupFile1.DiskChecked = null;
        backupFile1.UpdateDiskChecked("backup 45");
        Assert.NotEqual("", backupFile1.DiskChecked);
        Assert.Equal("test1.txt", backupFile1.FileName);
        Assert.Equal(pathToFile1OnBackupDisk, backupFile1.BackupDiskFullPath(pathToBackupDisk));
        backupFile1.ClearDiskChecked();
        Assert.Equal("", backupFile1.DiskChecked);
        Assert.NotEqual(0, backupFile1.GetHashCode());
        Utils.FileCopy(pathToFile1, pathToFile1OnBackupDisk);
        var backupDisk = new BackupDisk("backup 1000", pathToBackupShare);
        Assert.True(backupFile1.CheckContentHashes(backupDisk));
        File.AppendAllText(pathToFile1OnBackupDisk, "test");
        Assert.False(backupFile1.CheckContentHashes(backupDisk));
        if (Directory.Exists(pathToFiles)) Directory.Delete(pathToFiles, true);
        if (Directory.Exists(pathToBackupFile2)) Directory.Delete(pathToBackupFile2, true);
        if (Directory.Exists(pathToBackupShare)) Directory.Delete(pathToBackupShare, true);
    }

    [Fact]
    public void BackupDisk()
    {
        var backupShareName = Path.Combine(@"\\", Environment.MachineName, "backup");
        const string backupDiskName = "backup 45";
        var backupDisk = new BackupDisk(backupDiskName, backupShareName);
        Assert.Equal(Path.Combine(backupShareName, backupDiskName), backupDisk.BackupPath);
        Assert.Null(backupDisk.Checked);
        Assert.Equal(45, backupDisk.Number);
        backupDisk.UpdateDiskChecked();
        Assert.NotNull(backupDisk.Checked);
        Assert.Equal(45, backupDisk.GetHashCode());
        Assert.Equal(backupDiskName, backupDisk.ToString());
        backupDisk.Update(null);
        Assert.NotEqual(0, backupDisk.Capacity);
        Assert.NotEqual(string.Empty, backupDisk.CapacityFormatted);
        Assert.NotEqual(0, backupDisk.Free);
        Assert.NotEqual(string.Empty, backupDisk.FreeFormatted);
        Assert.NotEqual(string.Empty, backupDisk.LastReadSpeed);
        Assert.NotEqual(string.Empty, backupDisk.LastWriteSpeed);
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
        File.Delete(path);
    }
}
#endif