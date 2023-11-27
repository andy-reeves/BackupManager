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
    public void BackupFile()
    {
        var path1 = Path.Combine(Path.GetTempPath(), "BackupFile1");
        var path2 = Path.Combine(Path.GetTempPath(), "BackupFile2");
        var path3 = Path.Combine(path1, "_Movies");
        var path4 = Path.Combine(path2, "_TV");
        if (Directory.Exists(path1)) Directory.Delete(path1, true);
        if (Directory.Exists(path2)) Directory.Delete(path2, true);
        var file1 = Path.Combine(path3, "test1.txt");
        var file2 = Path.Combine(path4, "test2.txt");
        Utils.EnsureDirectoriesForDirectoryPath(path3);
        Utils.EnsureDirectoriesForDirectoryPath(path4);
        CreateFile(file1);
        CreateFile(file2);
        var backupFile1 = new BackupFile(file1, path1);
        Assert.Equal("_Movies\\test1.txt", backupFile1.RelativePath);
        Assert.Equal(4, backupFile1.Length);
        var backupFile2 = new BackupFile(file2, path2);
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
        Assert.Equal("BackupFile1\\_Movies\\test1.txt", backupFile1.Hash);
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
        Assert.Equal("\\\\media\\Backup\\BackupFile1\\_Movies\\test1.txt", backupFile1.BackupDiskFullPath(@"\\media\Backup"));
        backupFile1.ClearDiskChecked();
        Assert.Equal("", backupFile1.DiskChecked);
        Assert.NotEqual(0, backupFile1.GetHashCode());
        if (Directory.Exists(path1)) Directory.Delete(path1, true);
        if (Directory.Exists(path2)) Directory.Delete(path2, true);
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