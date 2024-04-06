// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="TvEpisodeBackupFileNameTests.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;

using BackupManager;
using BackupManager.Entities;
using BackupManager.Extensions;

namespace TestProject;

[SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "<Pending>")]
[SuppressMessage("ReSharper", "MemberCanBeFileLocal")]
[SuppressMessage("ReSharper", "StringLiteralTypo")]
public sealed class TvEpisodeBackupFileNameTests
{
    [Theory]
    [InlineData(@"Z:\_TV\Tom and Jerry {tvdb-72860}\Season 1940\Tom and Jerry s1940e01 Puss Gets The Boot [Bluray-1080p Remux Proper][MP3 2.0][XviD].mkv", true, "Tom and Jerry s1940e01 Puss Gets The Boot [Bluray-1080p Remux][MP3 2.0][XviD].mkv")]
    [InlineData(@"Z:\_TV\Tom and Jerry {tvdb-72860}\Season 1940\Tom and Jerry s1940e01 Puss Gets The Boot [Bluray-1080p Remux][MP3 2.0][XviD].mkv", true)]
    [InlineData(@"Z:\_TV (non-tvdb)\Tom and Jerry {tmdb-72860}\Season 1940\Tom and Jerry s1940e01 Puss Gets The Boot [SDTV][MP3 2.0][XviD].mkv", true)]
    [InlineData(@"Z:\_TV (non-tvdb)\Tom and Jerry {tvdb-72860}\Season 1940\Tom and Jerry s1940e01 Puss Gets The Boot [SDTV][MP3 2.0][XviD].mkv", true)]
    [InlineData(@"K:\_TV\Westworld {tvdb-296762}\Westworld s02e64 - The Delos Experiment-other.mkv", true)]
    [InlineData(@"\\nas1\assets1\_TV\Charlie's Angels {tvdb-77170}\Season 5\Charlie's Angels s05e01e03 Angel in Hiding (1) [Bluray-1080p Remux][DTS-HD MA 2.0][h264].mkv", true)]
    [InlineData(@"\\nas2\assets3\_TV\Lost {tvdb-73739}\Season 2\Lost s02e21 [HDTV-720p][DTS 5.1][h264].mkv", true)]
    [InlineData(@"\\nas5\assets4\_TV\Automan {tvdb-72589}\Season 1\Automan s01e01 Automan [SDTV][MP2 2.0][MPEG].mpg", true)]
    [InlineData(@"\\nas2\assets4\_TV\Criminal Record {tvdb-421495}\Season 1\Criminal Record s01e03 Kid in the Park [WEBDL-2160p][DV HDR10Plus][EAC3 Atmos 5.1][h265].mkv", true)]
    [InlineData(@"Tom and Jerry 2023-01-23 Puss Gets The Boot [SDTV][MP3 2.0][XviD].mkv", true)]
    [InlineData(@"Z:\_TV\Tom and Jerry {tvdb-72860}\Season 1940\File8 s01e01 [Bluray-1080p Remux][DTS-HD MA 5.1][AVC].mkv", true)]
    [InlineData(@"Z:\_TV (non-tvdb)\Tom and Jerry {tvdb-72860}\Season 1940\Tom and Jerry s1940e01 Puss Gets The Boot [SDTV][MP3 2.0][AVC].mkv", true)]
    [SuppressMessage("ReSharper", "StringLiteralTypo")]
    public void TvEpisodeTests(string fileName, bool isValidFileName, string expectedFileName = "")
    {
        var file = new TvEpisodeBackupFile(fileName);
        Assert.Equal(isValidFileName, file.IsValidFileName);
        if (expectedFileName.HasNoValue()) expectedFileName = Path.GetFileName(fileName);
        if (file.IsValidFileName) Assert.Equal(expectedFileName, file.GetFileName());
        if (file.IsValidDirectoryName) Assert.Equal(Path.Combine(Path.GetDirectoryName(fileName) ?? string.Empty, expectedFileName), file.GetFullName());
    }

    [Theory]
    [InlineData("File15 s01e03 Kid in the Park [WEBDL-2160p][DV HDR10Plus][EAC3 Atmos 5.1][h264].mkv", true, "File15 s01e03 Kid in the Park [WEBDL-1080p][EAC3 5.1][h265].mkv")]
    [InlineData(@"File8 s01e01 [Bluray-1080p Remux][DTS-HD MA 5.1][AVC].mkv", true, "File8 s01e01 [Bluray-1080p Remux][DTS-HD MA 5.1][h264].mkv")]
    [InlineData(@"Percy Jackson and the Olympians s01e01 I Accidentally Vaporize My Pre-Algebra Teacher [SDTV][MP3 2.0][].avi", false, "Percy Jackson and the Olympians s01e01 I Accidentally Vaporize My Pre-Algebra Teacher [SDTV][MP3 2.0][].avi")]
    public void TvTests2(string param1, bool refreshReturnValue, string mediaFileNameOutputIfRenamed)
    {
        var testDataPath = Path.Combine(Utils.GetProjectPath(typeof(MediaHelperTests)), "TestData");
        var mediaFileName = Path.Combine(testDataPath, param1);
        var tvEpisodeBackupFile = new TvEpisodeBackupFile(mediaFileName);
        if (tvEpisodeBackupFile.IsValidFileName) Assert.Equal(Path.GetFileName(mediaFileName), tvEpisodeBackupFile.GetFileName());
        Assert.Equal(refreshReturnValue, tvEpisodeBackupFile.RefreshMediaInfo());
        Assert.Equal(refreshReturnValue, tvEpisodeBackupFile.IsValidFileName);
        if (refreshReturnValue) Assert.Equal(mediaFileNameOutputIfRenamed, tvEpisodeBackupFile.GetFileName());
    }
}