// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="UtilsFileTests.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;

using BackupManager;

namespace TestProject;

[SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "<Pending>")]
[SuppressMessage("ReSharper", "MemberCanBeFileLocal")]
[SuppressMessage("ReSharper", "StringLiteralTypo")]
public sealed class UtilsFileTests
{
    [InlineData(@"c:\bobby\subtitles.srt", true)]
    [InlineData(@"c:\bobby\subtitles.en.hi.srt", true)]
    [InlineData(@"c:\bobby\subtitles.jp.srt", false)]
    [InlineData(@"c:\bobby\subtitles.jp.hi.srt", false)]
    [InlineData(@"c:\bobby\subtitles.es.srt", true)]
    [Theory]
    public void IsSubtitlesTests(string path, bool expectedResult)
    {
        Assert.Equal(expectedResult, Utils.File.IsSubtitles(path));
    }

    [InlineData(@"c:\bobby\file1-other.mkv", true)]
    [InlineData(@"c:\bobby\file2.mkv", false)]
    [InlineData(@"c:\bobby\file3-bobby", false)]
    [InlineData(@"c:\bobby\file4-behindthescenes.mkv", true)]
    [Theory]
    public void IsSpecialFeatureTests(string path, bool expectedResult)
    {
        Assert.Equal(expectedResult, Utils.File.IsSpecialFeature(path));
    }

    [InlineData(@"c:\bobby\file1-other.mkv", true)]
    [InlineData(@"c:\bobby\file2.mkv", true)]
    [InlineData(@"c:\bobby\file3-bobby", false)]
    [InlineData(@"c:\bobby\file4-behindthescenes.mkv", true)]
    [InlineData(@"c:\bobby\file4-behindthescenes.mp4", true)]
    [Theory]
    public void IsVideoTests(string path, bool expectedResult)
    {
        Assert.Equal(expectedResult, Utils.File.IsVideo(path));
    }
}