﻿// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="ExtensionsTests.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;
using System.Windows.Forms;

using BackupManager;
using BackupManager.Extensions;

namespace TestProject;

[SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "<Pending>")]
[SuppressMessage("ReSharper", "MemberCanBeFileLocal")]
public sealed class ExtensionsTests
{
    [InlineData("32nd", 32)]
    [InlineData("0", 0)]
    [InlineData("20th", 20)]
    [Theory]
    public void Integer(string expectedValue, int testValue)
    {
        Assert.Equal(expectedValue, testValue.ToOrdinalString());
    }

    [Fact]
    public void Control()
    {
        const string text = "test";
        var a = new TextBox();
        a.Invoke(static c => c.Text = text);
        Assert.Equal(text, a.Text);
    }

    [InlineData("2nd", "2nd", "2nd")]
    [InlineData("3rd", "3Rd", "3Rd")]
    [InlineData("10th", "10TH", "10TH")]
    [InlineData("Test", "Test", "test")]
    [Theory]
    public void String(string expectedValueOrdinal, string expectedValueCapitalize, string testValue)
    {
        Assert.Equal(expectedValueOrdinal, testValue.ToTitleCaseIgnoreOrdinals());
        Assert.Equal(expectedValueCapitalize, testValue.Capitalize());
    }

    [InlineData("1727737200", 2024, 10, 1)]
    [InlineData("55119600", 1971, 10, 1)]
    [Theory]
    public void DateTimeTests(string expectedResult, int year, int month, int day)
    {
        var dateTime1 = new DateTime(year, month, day);
        Assert.Equal(expectedResult, dateTime1.ToUnixTime());
        Assert.Equal(expectedResult + "000", dateTime1.ToUnixTimeMilliseconds());
    }

    [InlineData(true, 45, 44, 46)]
    [InlineData(true, 44, 44, 45)]
    [InlineData(false, 44, 46, 48)]
    [Theory]
    public void ObjectExtensions(bool expectedValue, int value, int minimum, int maximum)
    {
        Assert.Equal(expectedValue, value.IsInRange(minimum, maximum));
    }

    [InlineData(0, "0 bytes")]
    [InlineData(-23, "-23 bytes")]
    [InlineData((long)Utils.BYTES_IN_ONE_KILOBYTE - 10, "1,014 bytes")]
    [InlineData(1023, "1,023 bytes")]
    [InlineData((long)Utils.BYTES_IN_ONE_KILOBYTE, "1 KB")]
    [InlineData(3056, "3 KB")]
    [InlineData((long)Utils.BYTES_IN_ONE_MEGABYTE + 1, "1 MB")]
    [InlineData((long)Utils.BYTES_IN_ONE_MEGABYTE + Utils.BYTES_IN_ONE_MEGABYTE / 10, "1.1 MB")]
    [InlineData((long)Utils.BYTES_IN_ONE_MEGABYTE + (Utils.BYTES_IN_ONE_MEGABYTE / 10 - 55000), "1 MB")]
    [InlineData((long)Utils.BYTES_IN_ONE_MEGABYTE + (Utils.BYTES_IN_ONE_MEGABYTE / 2 - 100), "1.5 MB")]
    [InlineData(23424234, "22.3 MB")]
    [InlineData(25 * (long)Utils.BYTES_IN_ONE_MEGABYTE + 1, "25 MB")]
    [InlineData(304353456, "290.3 MB")]
    [InlineData((long)Utils.BYTES_IN_ONE_GIGABYTE + 1, "1 GB")]
    [InlineData(25 * (long)Utils.BYTES_IN_ONE_GIGABYTE + 1, "25 GB")]
    [InlineData(304753353456, "283.8 GB")]
    [InlineData(445242304353456, "404.9 TB")]
    [InlineData(2342423232323234, "2.1 PB")]
    [InlineData(234445242304353456, "208.2 PB")]
    [Theory]
    public void Int64Extensions(long value, string expectedResult)
    {
        var result = value.SizeSuffix();
        Assert.Equal(expectedResult, result);
    }
}