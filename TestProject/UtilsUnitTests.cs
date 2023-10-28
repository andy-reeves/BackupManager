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
    [Fact]
    [SuppressMessage("ReSharper", "IdentifierTypo")]
    public void GetVersionNumber()
    {
        const string bazarrVersion = "1.3.1";
        Assert.Equal(bazarrVersion, Utils.GetApplicationVersionNumber(Utils.Application.Bazarr));
        const string plexVersion = "1.32.7.7571";
        Assert.Equal(plexVersion, Utils.GetApplicationVersionNumber(Utils.Application.Plex));
        const string sonarrVersion = "3.0.10.1567";
        Assert.Equal(sonarrVersion, Utils.GetApplicationVersionNumber(Utils.Application.Sonarr));
        const string radarrVersion = "5.1.1.8195";
        Assert.Equal(radarrVersion, Utils.GetApplicationVersionNumber(Utils.Application.Radarr));
        const string prowlarrVersion = "1.10.0.4047";
        Assert.Equal(prowlarrVersion, Utils.GetApplicationVersionNumber(Utils.Application.Prowlarr));
        const string sabnzbVersion = "4.1.0";
        Assert.Equal(sabnzbVersion, Utils.GetApplicationVersionNumber(Utils.Application.SABnzbd));
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
        Assert.True(a == "5 minutes");
        a = Utils.FormatTimeSpan(new TimeSpan(0, 0, 90000));
        Assert.True(a == "a day or so");
    }

    [Fact]
    [SuppressMessage("ReSharper", "StringLiteralTypo")]
    public void GetRootPath()
    {
        var a = Utils.GetRootPath(@"\\nas1\assets1\_TV");
        Assert.NotNull(a);
        Assert.True(a == @"\\nas1\assets1");
        a = Utils.GetRootPath(@"\\nas1\assets1\_TV\Show1\Season 1\Episode1.mkv");
        Assert.NotNull(a);
        Assert.True(a == @"\\nas1\assets1");
        var path = Path.Combine(Path.GetTempPath(), "Folder1");
        Utils.EnsureDirectoriesForDirectoryPath(path);
        var file1 = Path.Combine(path, "test.tmp");
        CreateFile(file1);
        a = Utils.GetRootPath(path);
        Assert.NotNull(a);
        Assert.True(a == @"C:\");
        a = Utils.GetRootPath(file1);
        Assert.NotNull(a);
        Assert.True(a == @"C:\");
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
        Assert.True(a == "_TV");
    }

    [Fact]
    public void FormatTimeFromSeconds()
    {
        var a = Utils.FormatTimeFromSeconds(300);
        Assert.True(a == "5 minutes");
        a = Utils.FormatTimeFromSeconds(100);
        Assert.True(a == "100 seconds");
        a = Utils.FormatTimeFromSeconds(306);
        Assert.True(a == "5 minutes");
        a = Utils.FormatTimeFromSeconds(3900);
        Assert.True(a == "1 hour");
        a = Utils.FormatTimeFromSeconds(90000);
        Assert.True(a == "a day or so");
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