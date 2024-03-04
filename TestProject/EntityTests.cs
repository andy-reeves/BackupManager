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
public sealed class EntityTests
{
    // ReSharper disable once PrivateFieldCanBeConvertedToLocalVariable
    private static readonly MediaBackup _mediaBackup;

    static EntityTests()
    {
        _mediaBackup = BackupManager.Entities.MediaBackup.Load(Path.Combine(Utils.GetProjectPath(typeof(EntityTests)),
            "..\\BackupManager\\MediaBackup.xml"));
        Utils.Config = _mediaBackup.Config;
    }

    [Fact]
    public void DirectoryScan()
    {
        var dateTime = DateTime.Now;
        var a = new DirectoryScan(DirectoryScanType.ProcessingFiles, @"c:\testPath", dateTime, "1");
        Assert.Equal(@"c:\testPath", a.Path);
        Assert.Equal(dateTime, a.StartDateTime);
        Assert.Equal(DateTime.MinValue, a.EndDateTime);
        Assert.Equal(TimeSpan.Zero, a.ScanDuration);
        var endDate = dateTime.AddDays(1);
        a.EndDateTime = endDate;
        Assert.Equal(dateTime, a.StartDateTime);
        Assert.Equal(endDate, a.EndDateTime);
        Assert.Equal(new TimeSpan(1, 0, 0, 0), a.ScanDuration);
    }

    [Theory]
    [InlineData(@"\\nas1\assets4\_Movies\Aliens (1986)-other\Aliens (1986)-other {tmdb-679} [Remux-2160p][HDR10][AC3 5.1][h265].mkv", true)]
    [InlineData(@"\\nas1\assets4\_Movies\Aliens (1986)\Aliens (1986) {tmdb-679} [Remux-2160p][HDR10][AC3 5.1][h265].mkv", true)]
    [InlineData("Battlestar Galactica (1978) {tmdb-148980} {edition-EXTENDED} [Remux-2160p][HDR10][DTS-HD MA 5.1][h265].mkv", true)]
    [InlineData("Battlestar Galactica (1978) {tmdb-148980} [Remux-2160p][DTS-HD MA 5.1][h265].mkv", true)]
    [InlineData("Battlestar Galactica (1978) {tmdb-148980} [Remux-1080p][DTS-HD MA 5.1][h264].mkv", true)]
    [InlineData("Battlestar Galactica (1978) [Remux-1080p][DTS-HD MA 5.1][h264].mkv", true)]
    [InlineData("Battlestar Galactica (1978) [Remux-1080p][audio][h264].mkv", false)]
    [InlineData("A (2022) {tmdb-1} [DVD][DTS 1.0][h264].mkv", true)]
    [InlineData("A (2022) {tmdb-1} [DVD][DTS-X 2.0][h265].mkv", true)]
    [InlineData("A (2022) {tmdb-1} [DVD][DTS-ES 3.0][VP9].mkv", true)]
    [InlineData("A (2022) {tmdb-1} [DVD][DTS HD 3.1][VC1].mkv", true)]
    [InlineData("A (2022) {tmdb-1} [DVD][DTS-HD MA 4.0][MPEG2].mkv", true)]
    [InlineData("A (2022) {tmdb-1} [DVD][DTS-HD HRA 5.0][VP9].mkv", true)]
    [InlineData("A (2022) {tmdb-1} [DVD][TrueHD Atmos 5.1][VP9].mkv", true)]
    [InlineData("A (2022) {tmdb-1} [DVD][EAC3 6.0][VP9].mkv", true)]
    [InlineData("A (2022) {tmdb-1} [DVD][EAC3 Atmos 6.1][VP9].mkv", true)]
    [InlineData("A (2022) {tmdb-1} [DVD][AC3 7.1][VP9].mkv", true)]
    [InlineData("A (2022) {tmdb-1} [DVD][FLAC 8.0][VP9].mkv", true)]
    [InlineData("A (2022) {tmdb-1} [DVD][PCM 2.0][VP9].mkv", true)]
    [InlineData("A (2022) {tmdb-1} [DVD][MP3 2.0][VP9].mkv", true)]
    [InlineData("A (2022) {tmdb-1} [DVD][AAC 2.0][VP9].mkv", true)]
    [InlineData("A (2022) {tmdb-1} [DVD][TrueHD 2.0][VP9].mkv", true)]
    [InlineData("A (2022) {tmdb-1} [DVD][AVC 2.0][VP9].mkv", true)]
    [InlineData("A (2022) {tmdb-1} [DVD][Opus 2.0][VP9].mkv", true)]
    [InlineData("Asterix and Obelix The Middle Kingdom (2023) {tmdb-643215} [Remux-1080p][DTS-HD MA 5.1][h264].en.srt", true)]
    [InlineData("Special video-featurette.mkv", true)]
    [SuppressMessage("ReSharper", "StringLiteralTypo")]
    public void MovieTests(string fileName, bool isValidFileName)
    {
        var movie = new Movie(fileName);
        Assert.Equal(isValidFileName, movie.Valid);
        if (movie.Valid) Assert.Equal(fileName, movie.Directory.HasValue() ? movie.GetFullName() : movie.GetFileName());
        /* foreach (var file in _mediaBackup.BackupFiles.Where(static f => !f.Deleted &&
                                                                         (f.FullPath.Contains(@"_Concerts") || f.FullPath.Contains(@"_Comedy") ||
                                                                          f.FullPath.Contains(@"_Movies"))))
         {
             movie = new Movie(file.FullPath);
             Assert.True(movie.Valid);
             if (movie.Valid) Assert.Equal(file.FullPath, movie.Directory.HasValue() ? movie.GetFullName() : movie.GetFileName());
         }*/
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
        if (Directory.Exists(pathToFiles)) _ = Utils.DirectoryDelete(pathToFiles, true);
        if (Directory.Exists(pathToBackupFile2)) _ = Utils.DirectoryDelete(pathToBackupFile2, true);
        if (Directory.Exists(pathToBackupShare)) _ = Utils.DirectoryDelete(pathToBackupShare, true);
        var pathToFile1 = Path.Combine(pathToMovies, "test1.txt");
        var pathToFile2 = Path.Combine(pathToTv, "test2.txt");
        var pathToFile3 = Path.Combine(pathToMovies2, "test1.txt");
        Utils.EnsureDirectoriesForDirectoryPath(pathToMovies);
        Utils.EnsureDirectoriesForDirectoryPath(pathToTv);
        Utils.EnsureDirectoriesForDirectoryPath(pathToBackupDisk);
        Utils.CreateFile(pathToFile1);
        Utils.CreateFile(pathToFile2);
        Utils.CreateFile(pathToFile3);

        var mediaBackup = BackupManager.Entities.MediaBackup.Load(Path.Combine(Utils.GetProjectPath(typeof(FileRulesUnitTest)),
            "..\\BackupManager\\MediaBackup.xml"));
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
        var f1 = mediaBackup.GetBackupFileFromContentsHashcode("098f6bcd4621d373cade4e832627b4f6");
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
        if (Utils.Config.PlexToken.HasNoValue()) Assert.Equal("!*.bup", mediaBackup.GetFilters());

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
        if (Directory.Exists(pathToFiles)) _ = Utils.DirectoryDelete(pathToFiles, true);
        if (Directory.Exists(pathToBackupFile2)) _ = Utils.DirectoryDelete(pathToBackupFile2, true);
        if (Directory.Exists(pathToBackupShare)) _ = Utils.DirectoryDelete(pathToBackupShare, true);
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
        if (Directory.Exists(pathToFiles)) _ = Utils.DirectoryDelete(pathToFiles, true);
        if (Directory.Exists(pathToBackupFile2)) _ = Utils.DirectoryDelete(pathToBackupFile2, true);
        if (Directory.Exists(pathToBackupShare)) _ = Utils.DirectoryDelete(pathToBackupShare, true);
        var pathToFile1 = Path.Combine(pathToMovies, "test1.txt");
        var pathToFile2 = Path.Combine(pathToTv, "test2.txt");
        var pathToFile1OnBackupDisk = Path.Combine(pathToMoviesOnBackupDisk, "test1.txt");
        Utils.EnsureDirectoriesForDirectoryPath(pathToMovies);
        Utils.EnsureDirectoriesForDirectoryPath(pathToTv);
        Utils.CreateFile(pathToFile1);
        Utils.CreateFile(pathToFile2);
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
        _ = Utils.FileCopy(pathToFile1, pathToFile1OnBackupDisk, new CancellationToken());
        var backupDisk = new BackupDisk("backup 1000", pathToBackupShare);
        Assert.True(backupFile1.CheckContentHashes(backupDisk));
        File.AppendAllText(pathToFile1OnBackupDisk, "test");
        Assert.False(backupFile1.CheckContentHashes(backupDisk));
        if (Directory.Exists(pathToFiles)) _ = Utils.DirectoryDelete(pathToFiles, true);
        if (Directory.Exists(pathToBackupFile2)) _ = Utils.DirectoryDelete(pathToBackupFile2, true);
        if (Directory.Exists(pathToBackupShare)) _ = Utils.DirectoryDelete(pathToBackupShare, true);
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
        _ = Utils.FileDelete(path);
    }
}
