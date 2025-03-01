﻿// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="UtilsUnitTests.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

global using Xunit;

using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;

using BackupManager;
using BackupManager.Entities;

namespace TestProject;

[SuppressMessage("ReSharper", "MemberCanBeFileLocal")]
[SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "<Pending>")]
public sealed class UtilsUnitTests
{
    static UtilsUnitTests()
    {
        var mediaBackup = MediaBackup.Load(Path.Combine(Utils.GetProjectPath(typeof(UtilsUnitTests)), "..\\BackupManager\\MediaBackup.xml"));
        Utils.Config = mediaBackup.Config;
    }

    [InlineData(1)]
    [InlineData(14)]
    [Theory]
    public void TimeLeftTests(int hours)
    {
        var startTime = DateTime.Now;
        var targetTime = DateTime.Now.AddHours(hours).TimeOfDay;
        var timeLeft = Utils.TimeLeft(startTime, targetTime);
        Assert.True(timeLeft.Hours <= hours);
    }

    [Fact]
    public void DeleteBrokenSymbolicLinksTests()
    {
        var testDataPath = Path.Combine(Utils.GetProjectPath(typeof(UtilsUnitTests)), "TestData");
        var directoryParent = Path.Combine(testDataPath, @"BrokenLinksDirectory");
        var directoryToCheck = Path.Combine(testDataPath, @"BrokenLinksDirectory\DirectoryToCheck");
        var directoryToTarget = Path.Combine(testDataPath, @"BrokenLinksDirectory\Target");
        if (Directory.Exists(directoryParent)) _ = Utils.Directory.Delete(directoryParent);
        _ = Directory.CreateDirectory(directoryToCheck);
        _ = Directory.CreateDirectory(directoryToTarget);
        _ = Directory.CreateSymbolicLink(Path.Combine(directoryToCheck, "TestLink"), directoryToTarget);
        var linksDeleted = Utils.DeleteBrokenSymbolicLinks(directoryToCheck, false).ToArray();
        Assert.Empty(linksDeleted);
        Directory.Delete(directoryToTarget);
        linksDeleted = Utils.DeleteBrokenSymbolicLinks(directoryToCheck, false).ToArray();
        _ = Assert.Single(linksDeleted);
        if (Directory.Exists(directoryToCheck)) _ = Utils.Directory.Delete(directoryToCheck);
        if (Directory.Exists(directoryParent)) _ = Utils.Directory.Delete(directoryParent);
    }

    [InlineData(60_000, "1m")]
    [InlineData(35_000, "1m")]
    [InlineData(350, "1s")]
    [InlineData(142_000, "2m")]
    [InlineData(0, "0s")]
    [InlineData(1_000, "1s")]
    [Theory]
    public void FormatTimeSpanMinutesOnlyTests(int input, string expected)
    {
        Assert.Equal(expected, Utils.FormatTimeSpanMinutesOnly(TimeSpan.FromMilliseconds(input)));
    }

    [InlineData(60_000_000_000_000, "54.6 TB/s")]
    [InlineData(60_000_000_000, "55 GB/s")]
    [InlineData(60_000_000, "57 MB/s")]
    [InlineData(35_000, "34 KB/s")]
    [InlineData(350, "350 bytes/s")]
    [InlineData(142_000, "138 KB/s")]
    [InlineData(0, "0 bytes/s")]
    [InlineData(1_000, "1,000 bytes/s")]
    [Theory]
    public void FormatSpeedTests(long input, string expected)
    {
        Assert.Equal(expected, Utils.FormatSpeed(input));
    }

    [Fact]
    public void VersionNumber()
    {
        const string version1 = "1.2.3.4";
        const string version2 = "5.6.7.8";
        const string badVersion = "1";
        Assert.True(Utils.VersionIsNewer(version1, version2));
        Assert.False(Utils.VersionIsNewer(version1, badVersion));
    }

    [Fact]
    public void SharedFolder()
    {
        string? path1 = null;

        try
        {
            path1 = Path.Combine(Path.GetTempPath(), "SharedFolder");
            var file = Path.Combine(path1, "test1.txt");
            if (Directory.Exists(path1)) _ = Utils.Directory.Delete(path1, true);
            Utils.Directory.EnsurePath(path1);
            Utils.File.Create(file);
            const string shareName = "TestShare";
            var tempShare2 = Win32Share.GetNamedShare(shareName);
            _ = tempShare2?.Delete();
            Assert.True(Utils.ShareFolder(path1, shareName, "Test"));
            var domain = Environment.UserDomainName;
            Utils.AddPermissions(shareName, domain, "Everyone");
            var tempShare = Win32Share.GetNamedShare(shareName);
            Assert.Equal(Win32Share.MethodStatus.Success, tempShare.Delete());
        }
        finally
        {
            if (path1 != null)
            {
                if (Directory.Exists(path1))
                {
                    // Tidy up directories we created
                    _ = Utils.Directory.Delete(path1, true);
                }
            }
        }
    }

    [Fact]
    public void IsDirectoryWritable()
    {
        Assert.True(Utils.Directory.IsWritable(Utils.Config.DirectoriesToBackup[0]));
        Assert.False(Utils.Directory.IsWritable(Utils.Config.DirectoriesToBackup[0] + "2"));
    }

    [Fact]
    public void RenameFileToRemoveFixedSpaces()
    {
        var path1 = Path.Combine(Path.GetTempPath(), "RenameFileToRemoveFixedSpaces");
        var filePathWithFixedSpaces = Path.Combine(path1, "test" + Convert.ToChar(160) + "1.txt");
        Utils.Directory.EnsurePath(path1);
        Utils.File.Create(filePathWithFixedSpaces);
        Assert.True(Utils.StringContainsFixedSpace(filePathWithFixedSpaces));
        var filePathWithFixedSpacesRemoved = Utils.ReplaceFixedSpace(filePathWithFixedSpaces);
        var newFilePath = Utils.RenameFileToRemoveFixedSpaces(filePathWithFixedSpaces);
        Assert.Equal(filePathWithFixedSpacesRemoved, newFilePath);
        Assert.True(File.Exists(newFilePath));
        Assert.False(File.Exists(filePathWithFixedSpaces));
        _ = Utils.File.Delete(newFilePath);
        Assert.False(File.Exists(newFilePath));
        if (Directory.Exists(path1)) _ = Utils.Directory.Delete(path1, true);
    }

    [Fact]
    public void Config()
    {
        Utils.Config.LogParameters();
    }

    [Fact]
    [SuppressMessage("ReSharper", "StringLiteralTypo")]
    public void StringContainsFixedSpace()
    {
        const string workingString = @"\\nas1\assets1\_TV\Pennyworth The Origin of Batman's Butler {tvdb-363008}\Season 1";
        var brokenString = @"\\nas1\assets1\_TV\Pennyworth The" + Convert.ToChar(160) + @"Origin of Batman's Butler {tvdb-363008}\Season 1"; // - broken
        Assert.True(Utils.StringContainsFixedSpace(brokenString));
        Assert.False(Utils.StringContainsFixedSpace(workingString));
    }

    [Fact]
    public void FileMove()
    {
        var path1 = Path.Combine(Path.GetTempPath(), "FileMove");
        if (Directory.Exists(path1)) _ = Utils.Directory.Delete(path1, true);
        var file1 = Path.Combine(path1, "test1.txt");
        var file2 = Path.Combine(path1, "test2.txt");
        Utils.Directory.EnsurePath(path1);
        Utils.File.Create(file1);
        _ = Utils.File.Move(file1, file2);
        Assert.False(File.Exists(file1));
        Assert.True(File.Exists(file2));

        // Delete the folders we created
        if (Directory.Exists(path1)) _ = Utils.Directory.Delete(path1, true);
    }

    [Fact]
    public void ConvertMBtoByte()
    {
        Assert.Equal(23 * 1024 * 1024, Utils.ConvertMBtoBytes(23));
    }

    [Fact]
    public void IsDirectoryEmpty()
    {
        var path1 = Path.Combine(Path.GetTempPath(), "IsDirectoryEmpty");
        var path2 = Path.Combine(Path.GetTempPath(), "IsDirectoryEmpty2");
        if (Directory.Exists(path1)) _ = Utils.Directory.Delete(path1, true);
        if (Directory.Exists(path2)) _ = Utils.Directory.Delete(path2, true);
        var file1 = Path.Combine(path1, "test1.txt");
        Utils.Directory.EnsurePath(path1);
        Assert.True(Utils.Directory.IsEmpty(path1));
        Assert.False(Utils.Directory.IsEmpty(path1 + "bob"));
        Utils.File.Create(file1);
        _ = Directory.CreateSymbolicLink(path2, path1);
        Assert.False(Utils.Directory.IsEmpty(path1));
        Assert.False(Utils.Directory.IsEmpty(path2));

        // Delete the folders we created
        if (Directory.Exists(path1)) _ = Utils.Directory.Delete(path1, true);
        Assert.True(Utils.Directory.IsEmpty(path2));
        if (Directory.Exists(path2)) _ = Utils.Directory.Delete(path2, true);
    }

    [Fact]
    public void ConvertGBtoByte()
    {
        Assert.Equal(24_696_061_952, Utils.ConvertGBtoBytes(23));
    }

    [Fact]
    public void IsRunningAsAdmin()
    {
        Assert.True(Utils.IsRunningAsAdmin());
    }

    [Fact]
    public void GetRemoteFileByteArray()
    {
        var path1 = Path.Combine(Path.GetTempPath(), "GetRemoteFileByteArray");
        if (Directory.Exists(path1)) _ = Utils.Directory.Delete(path1, true);
        var file1 = Path.Combine(path1, "test1.txt");
        Utils.Directory.EnsurePath(path1);
        Utils.File.Create(file1);

        using (BufferedStream stream = new(File.OpenRead(file1), Utils.BYTES_IN_ONE_MEGABYTE))
        {
            var byteArray = Utils.File.GetByteArray(stream, Utils.BYTES_IN_ONE_MEGABYTE);
            Assert.Equal(9, byteArray.Length);
        }

        // Delete the folders we created
        if (Directory.Exists(path1)) _ = Utils.Directory.Delete(path1, true);
    }

    [Fact]
    public void GetShortMd5HashFromFile()
    {
        var path1 = Path.Combine(Path.GetTempPath(), "GetShortMd5HashFromFile");
        if (Directory.Exists(path1)) _ = Utils.Directory.Delete(path1, true);
        var file1 = Path.Combine(path1, "test1.txt");
        Utils.Directory.EnsurePath(path1);
        Utils.File.Create(file1);

        using (var stream = File.OpenRead(file1))
        {
            Assert.Equal("b3d5cf638ed2f6a94d6b3c628f946196", Utils.File.GetShortMd5HashFromFile(stream, Utils.BYTES_IN_ONE_MEGABYTE));
        }
        Assert.Equal("b3d5cf638ed2f6a94d6b3c628f946196", Utils.File.GetShortMd5Hash(file1));

        // Delete the folders we created
        if (Directory.Exists(path1)) _ = Utils.Directory.Delete(path1, true);
    }

    [Fact]
    public void GetHashFromFile()
    {
        var path1 = Path.Combine(Path.GetTempPath(), "GetHashFromFile");
        var file1 = Path.Combine(path1, "test1.txt");

        // Delete the folders we create
        if (Directory.Exists(path1)) _ = Utils.Directory.Delete(path1, true);
        Utils.Directory.EnsurePath(path1);
        Utils.File.Create(file1);
        var md5 = MD5.Create();
        var hash = Utils.File.GetHash(file1, md5);
        Assert.Equal("b3d5cf638ed2f6a94d6b3c628f946196", hash);

        // Delete the folders we created
        if (Directory.Exists(path1)) _ = Utils.Directory.Delete(path1, true);
    }

    [Fact]
    public void GetFiles()
    {
        var cancellationTokenSource = new CancellationTokenSource();
        var path1 = Path.Combine(Path.GetTempPath(), "TestGetFilesFolder");
        var file1 = Path.Combine(path1, "test1.txt");
        Utils.File.Create(Path.Combine(path1, "test1.txt"));
        var files = Utils.File.GetFiles(path1, cancellationTokenSource.Token);
        Assert.Single(files, file1);
        files = Utils.File.GetFiles(path1, "*.txt", cancellationTokenSource.Token);
        Assert.Single(files, file1);
        files = Utils.File.GetFiles(path1, "*.txt", SearchOption.AllDirectories, cancellationTokenSource.Token);
        Assert.Single(files, file1);
        files = Utils.File.GetFiles(path1, "*.txt", SearchOption.AllDirectories, FileAttributes.Hidden, cancellationTokenSource.Token);
        Assert.Single(files, file1);

        // Delete the folders we created
        if (Directory.Exists(path1)) _ = Utils.Directory.Delete(path1, true);
        files = Utils.File.GetFiles(path1 + "bob", "*.txt", SearchOption.AllDirectories, FileAttributes.Hidden, cancellationTokenSource.Token);
        Assert.Empty(files);
    }

    [InlineData("Config.xml", "ConfigSchema.xsd")]
    [InlineData("MediaBackup.xml", "MediaBackupSchema.xsd")]
    [InlineData("Rules.xml", "RulesSchema.xsd")]
    [Theory]
    public void ValidateXml(string xml, string xsd)
    {
        var path = Path.Combine(Utils.GetProjectPath(typeof(FileRulesUnitTest)), "..\\BackupManager\\");
        var xmlPath = Path.Combine(path, xml);
        var xsdPath = Path.Combine(path, xsd);
        Utils.ValidateXml(xmlPath, xsdPath);
    }

    [Fact]
    public void FormatTimeSpanFromSeconds()
    {
        var a = Utils.FormatTimeSpan(new TimeSpan(0, 0, 300));
        Assert.Equal("5 minutes", a);
        a = Utils.FormatTimeSpan(new TimeSpan(0, 0, 90000));
        Assert.Equal("a day or more", a);
    }

    [Fact]
    [SuppressMessage("ReSharper", "StringLiteralTypo")]
    public void GetRootPath()
    {
        var a = Utils.GetRootPath(@"\\nas2\assets1\_TV");
        Assert.NotNull(a);
        Assert.Equal(@"\\nas2\assets1", a);
        a = Utils.GetRootPath(@"\\nas2\assets1\_TV\Show1\Season 1\Episode1.mkv");
        Assert.NotNull(a);
        Assert.Equal(@"\\nas2\assets1", a);
        var path = Path.Combine(Path.GetTempPath(), "Folder1");
        Utils.Directory.EnsurePath(path);
        var file1 = Path.Combine(path, "test.tmp");
        Utils.File.Create(file1);
        a = Utils.GetRootPath(path);
        Assert.NotNull(a);
        Assert.Equal(@"C:\", a);
        a = Utils.GetRootPath(file1);
        Assert.NotNull(a);
        Assert.Equal(@"C:\", a);
        _ = Utils.File.Delete(file1);
        _ = Utils.Directory.Delete(path);
    }

    [Fact]
    [SuppressMessage("ReSharper", "StringLiteralTypo")]
    public void GetIndexFolder()
    {
        var a = Utils.GetIndexFolder(@"\\nas1\assets1\_TV");
        Assert.NotNull(a);
        Assert.Equal("_TV", a);
    }

    [Fact]
    public void FormatTimeFromSeconds()
    {
        var a = Utils.FormatTimeFromSeconds(0);
        Assert.Equal("less than 1 minute", a);
        a = Utils.FormatTimeFromSeconds(1);
        Assert.Equal("less than 1 minute", a);
        a = Utils.FormatTimeFromSeconds(42);
        Assert.Equal("less than 1 minute", a);
        a = Utils.FormatTimeFromSeconds(60);
        Assert.Equal("1 minute", a);
        a = Utils.FormatTimeFromSeconds(61);
        Assert.Equal("1-2 minutes", a);
        a = Utils.FormatTimeFromSeconds(100);
        Assert.Equal("1-2 minutes", a);
        a = Utils.FormatTimeFromSeconds(110);
        Assert.Equal("1-2 minutes", a);
        a = Utils.FormatTimeFromSeconds(120);
        Assert.Equal("2 minutes", a);
        a = Utils.FormatTimeFromSeconds(300);
        Assert.Equal("5 minutes", a);
        a = Utils.FormatTimeFromSeconds(306);
        Assert.Equal("5-6 minutes", a);
        a = Utils.FormatTimeFromSeconds(3600);
        Assert.Equal("1 hour", a);
        a = Utils.FormatTimeFromSeconds(60 * 60 + 1);
        Assert.Equal("60-61 minutes", a);
        a = Utils.FormatTimeFromSeconds(3901);
        Assert.Equal("65-66 minutes", a);
        a = Utils.FormatTimeFromSeconds(89 * 60 + 2);
        Assert.Equal("89-90 minutes", a);
        a = Utils.FormatTimeFromSeconds(90 * 60);
        Assert.Equal("1-2 hours", a);
        a = Utils.FormatTimeFromSeconds(60 * 60 * 2);
        Assert.Equal("2-3 hours", a);
        a = Utils.FormatTimeFromSeconds(60 * 60 * 3);
        Assert.Equal("3-4 hours", a);
        a = Utils.FormatTimeFromSeconds(60 * 60 * 4);
        Assert.Equal("4-5 hours", a);
        a = Utils.FormatTimeFromSeconds(60 * 60 * 5);
        Assert.Equal("5-6 hours", a);
        a = Utils.FormatTimeFromSeconds(60 * 60 * 6);
        Assert.Equal("6-7 hours", a);
        a = Utils.FormatTimeFromSeconds(60 * 60 * 7);
        Assert.Equal("7-8 hours", a);
        a = Utils.FormatTimeFromSeconds(60 * 60 * 10);
        Assert.Equal("10-11 hours", a);
        a = Utils.FormatTimeFromSeconds(60 * 60 * 15);
        Assert.Equal("15-16 hours", a);
        a = Utils.FormatTimeFromSeconds(60 * 60 * 24);
        Assert.Equal("a day or more", a);
        a = Utils.FormatTimeFromSeconds(90000);
        Assert.Equal("a day or more", a);
    }

    [Fact]
    public void TraceOut()
    {
        var result = Utils.TraceOut(true);
        Assert.True(result);
        var result2 = Utils.TraceOut("Test string");
        Assert.Equal("Test string", result2);
        var result3 = Utils.TraceOut(this);
        Assert.True(result3 == this);
        var result4 = Utils.TraceOut(this, "Test");
        Assert.True(result4 == this);
        var a = new[] { "a", "b", "c" };
        var result5 = Utils.TraceOut(a, "Test");
        Assert.True(result5 == a);
        var result6 = Utils.TraceOut(a);
        Assert.True(result6 == a);
        var b = new[] { 1, 2, 3 };
        var result7 = Utils.TraceOut(b, "Test");
        Assert.True(result7 == b);
        a = [];
        var result8 = Utils.TraceOut(a, "Test");
        Assert.True(result8 == a);
        a = [];
        result8 = Utils.TraceOut(a);
        Assert.True(result8 == a);
        var result9 = Utils.TraceOut(32);
        Assert.Equal(32, result9);
        var result10 = Utils.TraceOut<BackupFile>("Test");
        Assert.Null(result10);
        var c = new BackupFile();
        var result11 = Utils.TraceOut(c, "Test");
        Assert.True(result11.Equals(c));
        Utils.TraceOut();
    }

    [Fact]
    public void GetDiskInfo()
    {
        var result = Utils.GetDiskInfo(@$"\\{Environment.MachineName}\Admin$", out var availableSpace, out var totalBytes);
        Assert.True(result);
        Assert.True(availableSpace > 0);
        Assert.True(totalBytes > 0);
    }

    [InlineData(@"\\nas1\assets1", @"\\nas1\assets1")]
    [InlineData(@"\\nas1\assets1\_TV", @"\\nas1\assets1")]
    [InlineData(@"\\nas1\assets1\_TV\", @"\\nas1\assets1")]
    [InlineData(@"\\nas1", @"\\nas1")]
    [InlineData(@"\\nas1\", @"\\nas1")]
    [InlineData(@"c:\assets1", @"c:\assets1")]
    [InlineData(@"c:\", @"c:\")]
    [InlineData(@"c:\assets1\_tv", @"c:\assets1")]
    [InlineData(@"c:\assets1\_tv\", @"c:\assets1")]
    [Theory]
    public void GetDiskAndFirstDirectory(string directory, string expectedResult)
    {
        string[] directories = [directory];
        var results = Utils.GetDiskAndFirstDirectory(directories);
        var result = Assert.Single(results);
        Assert.Equal(expectedResult, result);
    }
}