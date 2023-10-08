// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="UtilsUnitTests.cs" company="Andy Reeves">
//
//  </copyright>
//  --------------------------------------------------------------------------------------------------------------------

#if DEBUG
global using Xunit;

using BackupManager;
using BackupManager.Entities;

namespace TestProject;

public class UtilsUnitTests
{
    [Fact]
    public void FormatTimeSpanFromSeconds()
    {
        var a = Utils.FormatTimeSpan(new TimeSpan(0, 0, 300));
        Assert.True(a == "5 minutes");
        a = Utils.FormatTimeSpan(new TimeSpan(0, 0, 90000));
        Assert.True(a == "a day or so");
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