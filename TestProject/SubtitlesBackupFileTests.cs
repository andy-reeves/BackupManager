// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="SubtitlesBackupFileTests.cs" company="Andy Reeves">
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
public sealed class SubtitlesBackupFileTests
{
    [Theory]
    [InlineData(@"_TV\File15 {tvdb-1}\Season 1\File15 s01e03 Kid in the Park [WEBDL-2160p][DV HDR10Plus][EAC3 Atmos 5.1][h264].en.hi.srt", true,
        @"_TV\File15 {tvdb-1}\Season 1\File15 s01e03 Kid in the Park [WEBDL-2160p][DV HDR10Plus][EAC3 Atmos 5.1][h264].en.hi.srt")]
    [InlineData(@"_TV\TV Show {tvdb-2}\Season 1\TV Show s01e01 Episode 1 [HDTV-1080p][undefined 2.0][h265].en.srt", true,
        @"_TV\TV Show {tvdb-2}\Season 1\TV Show s01e01 Episode 1 [HDTV-1080p][AAC 2.0][h265].en.srt")]
    [InlineData(@"_TV\TV Show {tvdb-2}\Season 1\TV Show s01e02 Episode 2 [HDTV-1080p][undefined 2.0][h265].en.srt", true,
        @"_TV\TV Show {tvdb-2}\Season 1\TV Show s01e02 Episode 2 [HDTV-1080p][AAC 2.0][h265].en.srt")]
    public void SubtitlesTestsWithRefreshMediaInfo(string subtitlesFullName, bool isValidSubtitleFullName, string newSubtitlesFullName)
    {
        var testDataPath = Path.Combine(Utils.GetProjectPath(typeof(SubtitlesBackupFileTests)), "TestData");
        var fileName = Path.Combine(testDataPath, subtitlesFullName);
        var newFileName = Path.Combine(testDataPath, newSubtitlesFullName);
        var file = new SubtitlesBackupFile(fileName);
        Assert.Equal(isValidSubtitleFullName, file.IsValid);

        if (file.IsValid)
        {
            Assert.Equal(fileName, file.DirectoryName.HasValue() ? file.GetFullName() : file.GetFileName());
            Assert.Equal("." + subtitlesFullName.SubstringAfterIgnoreCase("]."), file.SubtitlesExtension);
        }
        Assert.True(file.RefreshMediaInfo());
        Assert.Equal(isValidSubtitleFullName, file.IsValid);
        if (!file.IsValid) return;

        Assert.True(file.FullPathToVideoFile.StartsWithIgnoreCase(Path.Combine(file.DirectoryName, file.Title)));
        Assert.Equal(newFileName, file.DirectoryName.HasValue() ? file.GetFullName() : file.GetFileName());
    }

    [Theory]
    [InlineData(
        @"\\nas2\assets1\_TV\James Martin's Saturday Morning {tvdb-334389}\Season 8\James Martin's Saturday Morning s08e01 Aug 31, 2024 Louise Minchin, Levi Roots, Atul Kochhar, Alysia Vasey [HDTV-1080p][AAC 2.0][h265].en.srt",
        true,
        @"\\nas2\assets1\_TV\James Martin's Saturday Morning {tvdb-334389}\Season 8\James Martin's Saturday Morning s08e01 Aug 31, 2024 Louise Minchin, Levi Roots, Atul Kochhar, Alysia Vasey [HDTV-1080p][AAC 2.0][h265].en.srt")]
    [InlineData(
        @"\\nas2\assets1\_TV\James Martin's Saturday Morning {tvdb-334389}\Season 8\James Martin's Saturday Morning s08e09 Oct 26, 2024 Lulu, Fred Sirieix, Cyrus Todiwala Sally Abe [HDTV-1080p][AAC 2.0][h264].en.srt",
        true,
        @"\\nas2\assets1\_TV\James Martin's Saturday Morning {tvdb-334389}\Season 8\James Martin's Saturday Morning s08e09 Oct 26, 2024 Lulu, Fred Sirieix, Cyrus Todiwala Sally Abe [HDTV-1080p][AAC 2.0][h264].en.srt")]
    [InlineData(@"\\nas4\assets2\_Movies\Misery (1990)\Misery (1990) {tmdb-1700} [Remux-2160p][DV HDR10][DTS-HD MA 5.1][h265].es.srt", true,
        @"\\nas4\assets2\_Movies\Misery (1990)\Misery (1990) {tmdb-1700} [Remux-2160p][DV HDR10][DTS-HD MA 5.1][h265].es.srt")]
    public void SubtitlesTestsWithRefreshMediaInfoRealFiles(string subtitlesFullName, bool isValidSubtitleFullName, string newSubtitlesFullName)
    {
        var file = new SubtitlesBackupFile(subtitlesFullName);
        Assert.Equal(isValidSubtitleFullName, file.IsValid);

        if (file.IsValid)
        {
            Assert.Equal(subtitlesFullName, file.DirectoryName.HasValue() ? file.GetFullName() : file.GetFileName());
            Assert.Equal("." + subtitlesFullName.SubstringAfterIgnoreCase("]."), file.SubtitlesExtension);
        }
        Assert.True(file.RefreshMediaInfo());
        Assert.Equal(isValidSubtitleFullName, file.IsValid);
        if (file.IsValid) Assert.Equal(newSubtitlesFullName, file.DirectoryName.HasValue() ? file.GetFullName() : file.GetFileName());
    }
}