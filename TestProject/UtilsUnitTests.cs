// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="UtilsUnitTests.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

#if DEBUG
global using Xunit;

using System.Diagnostics.CodeAnalysis;

using BackupManager;
using BackupManager.Entities;

namespace TestProject;

[SuppressMessage("ReSharper", "MemberCanBeFileLocal")]
[SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "<Pending>")]
public sealed class UtilsUnitTests
{
    static UtilsUnitTests()
    {
        var mediaBackup = MediaBackup.Load(Path.Combine(Utils.GetProjectPath(typeof(FileRulesUnitTest)), "..\\BackupManager\\MediaBackup.xml"));
        Utils.Config = mediaBackup.Config;
    }

    [Fact]
    [SuppressMessage("ReSharper", "IdentifierTypo")]
    public void GetVersionNumber()
    {
        Assert.Equal("1.3.1", Utils.GetApplicationVersionNumber(ApplicationType.Bazarr));
        Assert.Equal("1.32.8.7639", Utils.GetApplicationVersionNumber(ApplicationType.PlexPass));
        Assert.Equal("1.9.4.4039", Utils.GetApplicationVersionNumber(ApplicationType.Prowlarr));
        Assert.Equal("5.1.3.8246", Utils.GetApplicationVersionNumber(ApplicationType.Radarr));
        Assert.Equal("4.1.0", Utils.GetApplicationVersionNumber(ApplicationType.SABnzbd));
        Assert.Equal("3.0.10.1567", Utils.GetApplicationVersionNumber(ApplicationType.Sonarr));
    }

    [Fact]
    public void GetLatestApplicationVersionNumber()
    {
        Assert.Equal("1.3.1", Utils.GetLatestApplicationVersionNumber(ApplicationType.Bazarr));
        Assert.Equal("1.32.7.7621", Utils.GetLatestApplicationVersionNumber(ApplicationType.PlexPass));
        Assert.Equal("1.9.4", Utils.GetLatestApplicationVersionNumber(ApplicationType.Prowlarr));
        Assert.Equal("5.1.3", Utils.GetLatestApplicationVersionNumber(ApplicationType.Radarr));
        Assert.Equal("4.1.0", Utils.GetLatestApplicationVersionNumber(ApplicationType.SABnzbd));
        Assert.Equal("3.0.10", Utils.GetLatestApplicationVersionNumber(ApplicationType.Sonarr, "v3"));

        // These are the latest or develop branches
        Assert.Equal("1.32.7.7621", Utils.GetLatestApplicationVersionNumber(ApplicationType.Plex));
        Assert.Equal("3.0.9", Utils.GetLatestApplicationVersionNumber(ApplicationType.Sonarr, "develop"));
        Assert.Equal("3.0.9", Utils.GetLatestApplicationVersionNumber(ApplicationType.Sonarr));
        Assert.Equal("1.10.5", Utils.GetLatestApplicationVersionNumber(ApplicationType.Prowlarr, "develop"));
        Assert.Equal("5.2.3", Utils.GetLatestApplicationVersionNumber(ApplicationType.Radarr, "develop"));
    }

    [Fact]
    public void ValidateXml()
    {
        /*
            xs:int          int
            xs:integer      BigInteger
            xs:long         long
            xs:boolean      bool
            xs:double       double
            xs:float        float
            xs:short        short
            xs:string       string
            xs:date         string (but a date)
            xs:dateTime     DateTime
        */
        var path = Path.Combine(Utils.GetProjectPath(typeof(FileRulesUnitTest)), "..\\BackupManager\\");
        var xmlPath = Path.Combine(path, "Config.xml");
        var xsdPath = Path.Combine(path, "ConfigSchema.xsd");
        var result = Utils.ValidateXml(xmlPath, xsdPath);
        Assert.True(result, "Config.xml is not valid");
        xmlPath = Path.Combine(path, "MediaBackup.xml");
        xsdPath = Path.Combine(path, "MediaBackupSchema.xsd");
        result = Utils.ValidateXml(xmlPath, xsdPath);
        Assert.True(result, "MediaBackup.xml is not valid");
        xmlPath = Path.Combine(path, "Rules.xml");
        xsdPath = Path.Combine(path, "RulesSchema.xsd");
        result = Utils.ValidateXml(xmlPath, xsdPath);
        Assert.True(result, "Rules.xml is not valid");
    }

    [Fact]
    public void FormatTimeSpanFromSeconds()
    {
        var a = Utils.FormatTimeSpan(new TimeSpan(0, 0, 300));
        Assert.Equal("5-6 minutes", a);
        a = Utils.FormatTimeSpan(new TimeSpan(0, 0, 90000));
        Assert.Equal("a day or more", a);
    }

    [Fact]
    [SuppressMessage("ReSharper", "StringLiteralTypo")]
    public void GetRootPath()
    {
        var a = Utils.GetRootPath(@"\\nas1\assets1\_TV");
        Assert.NotNull(a);
        Assert.Equal(@"\\nas1\assets1", a);
        a = Utils.GetRootPath(@"\\nas1\assets1\_TV\Show1\Season 1\Episode1.mkv");
        Assert.NotNull(a);
        Assert.Equal(@"\\nas1\assets1", a);
        var path = Path.Combine(Path.GetTempPath(), "Folder1");
        Utils.EnsureDirectoriesForDirectoryPath(path);
        var file1 = Path.Combine(path, "test.tmp");
        CreateFile(file1);
        a = Utils.GetRootPath(path);
        Assert.NotNull(a);
        Assert.Equal(@"C:\", a);
        a = Utils.GetRootPath(file1);
        Assert.NotNull(a);
        Assert.Equal(@"C:\", a);
        File.Delete(file1);
        Directory.Delete(path);
    }

    private static void CreateFile(string filePath)
    {
        Utils.EnsureDirectoriesForFilePath(filePath);
        File.AppendAllText(filePath, "test");
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
        Assert.True(result2 == "Test string");
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
        a = Array.Empty<string>();
        var result8 = Utils.TraceOut(a, "Test");
        Assert.True(result8 == a);
        a = Array.Empty<string>();
        result8 = Utils.TraceOut(a);
        Assert.True(result8 == a);
        var result9 = Utils.TraceOut(32);
        Assert.True(result9 == 32);
        var result10 = Utils.TraceOut<BackupFile>("Test");
        Assert.True(result10 == null);
        var c = new BackupFile();
        var result11 = Utils.TraceOut(c, "Test");
        Assert.True(result11.Equals(c));
        Utils.TraceOut();
    }

    [Fact]
    public void CreateHashForByteArray()
    {
        var path = Path.Combine(Utils.GetProjectPath(typeof(UtilsUnitTests)), @"TestData\TestFile1");
        var size = new FileInfo(path).Length;
        var startDownloadPositionForEndBlock = size - Utils.EndBlockSize;
        var startDownloadPositionForMiddleBlock = size / 2;
        var firstByteArray = Utils.GetLocalFileByteArray(path, 0, Utils.StartBlockSize);
        var secondByteArray = Utils.GetLocalFileByteArray(path, startDownloadPositionForMiddleBlock, Utils.MiddleBlockSize);
        var thirdByteArray = Utils.GetLocalFileByteArray(path, startDownloadPositionForEndBlock, Utils.EndBlockSize);
        var result = Utils.CreateHashForByteArray(firstByteArray, secondByteArray, thirdByteArray);
        Assert.True(result == "1416d38415ac751620b97eab7f433723");
    }

    [Fact]
    public void GetDiskInfo()
    {
        var result = Utils.GetDiskInfo(@$"\\{Environment.MachineName}\Admin$", out var availableSpace, out var totalBytes);
        Assert.True(result);
        Assert.True(availableSpace > 0);
        Assert.True(totalBytes > 0);
    }
}
#endif