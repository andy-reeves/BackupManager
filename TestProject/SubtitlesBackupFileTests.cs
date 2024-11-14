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
    [InlineData("File18.nochapters.master.mkv", "File18.master.chap", "File18.chaptersAdded.mkv", 1, 1, 0, 1, false)]
    public void AddChaptersToFile(string inputFilename, string chaptersFilename, string outputFilename, int videoStreamCount, int audioStreamCount, int subtitlesStreamCount, int chaptersStreamCount, bool hasMetadata)
    {
        var testDataPath = Path.Combine(Utils.GetProjectPath(typeof(MediaHelperTests)), "TestData");
        var fileName = Path.Combine(testDataPath, inputFilename);
        var chapName = Path.Combine(testDataPath, chaptersFilename);
        var outName = Path.Combine(testDataPath, outputFilename);
        if (File.Exists(outName)) _ = Utils.File.Delete(outName);
        Assert.True(Utils.MediaHelper.AddChaptersToFile(fileName, chapName, outName));
        Assert.Equal(videoStreamCount, Utils.MediaHelper.VideoStreamCount(outName));
        Assert.Equal(audioStreamCount, Utils.MediaHelper.AudioStreamCount(outName));
        Assert.Equal(subtitlesStreamCount, Utils.MediaHelper.SubtitlesStreamCount(outName));
        Assert.Equal(chaptersStreamCount, Utils.MediaHelper.ChaptersStreamCount(outName));
        Assert.Equal(hasMetadata, Utils.MediaHelper.HasMetadata(outName));
        if (File.Exists(outName)) _ = Utils.File.Delete(outName);
    }

    [Theory]
    [InlineData("File18.withChapters.master.mkv", true)]
    [InlineData("File18.noChapters.master.mkv", false)]
    public void HasChapters(string inputFilename, bool expectedResult)
    {
        var testDataPath = Path.Combine(Utils.GetProjectPath(typeof(MediaHelperTests)), "TestData");
        var fileName = Path.Combine(testDataPath, inputFilename);
        Assert.Equal(expectedResult, Utils.MediaHelper.HasChapters(fileName));
    }

    [Theory]
    [InlineData("File18.withChapters.master.mkv", "File18.chap", "File18.master.chap")]
    public void ExtractChapters(string inputFilename, string outputFilename, string masterChapFile)
    {
        var testDataPath = Path.Combine(Utils.GetProjectPath(typeof(MediaHelperTests)), "TestData");
        var fileName = Path.Combine(testDataPath, inputFilename);
        var outName = Path.Combine(testDataPath, outputFilename);
        var chpMaster = Path.Combine(testDataPath, masterChapFile);
        if (File.Exists(outName)) _ = Utils.File.Delete(outName);
        Assert.True(Utils.MediaHelper.ExtractChapters(fileName, outName));
        var outHash = Utils.File.GetShortMd5Hash(outName);
        var outMasterHash = Utils.File.GetShortMd5Hash(chpMaster);
        Assert.Equal(outMasterHash, outHash);
        if (File.Exists(outName)) _ = Utils.File.Delete(outName);
    }

    [Theory]
    [InlineData("File17.withSubtitles.input.mkv", "File17.withSubtitles.expected.en.srt", "File17.withSubtitles.expected.en.hi.forced.srt")]
    [InlineData("File18.withChapters.master.mkv", "", "")]
    public void ExtractSubtitles(string inputFilename, string expectedEnglish, string expectedEnglishHearingImpairedForced)
    {
        var testDataPath = Path.Combine(Utils.GetProjectPath(typeof(MediaHelperTests)), "TestData");
        var fileName = Path.Combine(testDataPath, inputFilename);
        var withEnglishHearingImpaired = Path.Combine(testDataPath, Path.GetFileNameWithoutExtension(inputFilename) + ".en.hi.forced.srt");
        var withEnglish = Path.Combine(testDataPath, Path.GetFileNameWithoutExtension(inputFilename) + ".en.srt");
        var masEnFull = Path.Combine(testDataPath, expectedEnglish);
        var masEnHiFull = Path.Combine(testDataPath, expectedEnglishHearingImpairedForced);
        if (File.Exists(withEnglishHearingImpaired)) _ = Utils.File.Delete(withEnglishHearingImpaired);
        if (File.Exists(withEnglish)) _ = Utils.File.Delete(withEnglish);
        Assert.True(Utils.MediaHelper.ExtractSubtitleFiles(fileName));
        if (expectedEnglish == string.Empty) return;

        var outHash = Utils.File.GetShortMd5Hash(withEnglish);
        var outMasterHash = Utils.File.GetShortMd5Hash(masEnFull);
        Assert.Equal(outMasterHash, outHash);
        outHash = Utils.File.GetShortMd5Hash(withEnglishHearingImpaired);
        outMasterHash = Utils.File.GetShortMd5Hash(masEnHiFull);
        Assert.Equal(outMasterHash, outHash);
        if (File.Exists(withEnglishHearingImpaired)) _ = Utils.File.Delete(withEnglishHearingImpaired);
        if (File.Exists(withEnglish)) _ = Utils.File.Delete(withEnglish);
    }

    [Theory]
    [InlineData("File17.withSubtitles.input.mkv", "File17.nosubtitles.mkv", 1, 1, 0, 0, false)]
    [InlineData("File18.withChapters.master.mkv", "File18.nosubtitles.mkv", 1, 1, 0, 1, false)]
    [InlineData("File19.mkv", "File19.nosubs.mkv", 1, 12, 0, 0, false)]
    public void RemoveSubtitlesFromFile(string inputFilename, string outputFilename, int videoStreamCount, int audioStreamCount, int subtitlesStreamCount, int chaptersStreamCount, bool hasMetadata)
    {
        var testDataPath = Path.Combine(Utils.GetProjectPath(typeof(MediaHelperTests)), "TestData");
        var fileName = Path.Combine(testDataPath, inputFilename);
        var outName = Path.Combine(testDataPath, outputFilename);
        if (File.Exists(outName)) _ = Utils.File.Delete(outName);
        Assert.True(Utils.MediaHelper.RemoveSubtitlesFromFile(fileName, outName));
        Assert.Equal(videoStreamCount, Utils.MediaHelper.VideoStreamCount(outName));
        Assert.Equal(audioStreamCount, Utils.MediaHelper.AudioStreamCount(outName));
        Assert.Equal(subtitlesStreamCount, Utils.MediaHelper.SubtitlesStreamCount(outName));
        Assert.Equal(chaptersStreamCount, Utils.MediaHelper.ChaptersStreamCount(outName));
        Assert.Equal(hasMetadata, Utils.MediaHelper.HasMetadata(outName));
        if (File.Exists(outName)) _ = Utils.File.Delete(outName);
    }

    [Theory]
    [InlineData("File19.mkv", false, "", 0, 0, 0, 0)]
    [InlineData("File20.mkv", false, "", 0, 0, 0, 0)]
    public void RemoveMetadataFromFile(string inputFilename, bool hasMetadataInput, string outputFilename, int videoStreamCount, int audioStreamCount, int subtitlesStreamCount, int chaptersStreamCount)
    {
        var testDataPath = Path.Combine(Utils.GetProjectPath(typeof(MediaHelperTests)), "TestData");
        var fileName = Path.Combine(testDataPath, inputFilename);
        var outName = Path.Combine(testDataPath, outputFilename);
        if (File.Exists(outName)) _ = Utils.File.Delete(outName);

        if (hasMetadataInput)
        {
            Assert.True(Utils.MediaHelper.RemoveMetadataFromFile(fileName, outName));
            Assert.Equal(videoStreamCount, Utils.MediaHelper.VideoStreamCount(outName));
            Assert.Equal(audioStreamCount, Utils.MediaHelper.AudioStreamCount(outName));
            Assert.Equal(subtitlesStreamCount, Utils.MediaHelper.SubtitlesStreamCount(outName));
            Assert.Equal(chaptersStreamCount, Utils.MediaHelper.ChaptersStreamCount(outName));
            Assert.False(Utils.MediaHelper.HasMetadata(outName));
        }
        else
            Assert.False(Utils.MediaHelper.HasMetadata(fileName));
        if (File.Exists(outName)) _ = Utils.File.Delete(outName);
    }

    [Theory]
    [InlineData("File17.withSubtitles.input.mkv", "File17.nochapters.mkv", 1, 1, 2, 0, false)]
    [InlineData("File18.withChapters.master.mkv", "File18.nochapters.mkv", 1, 1, 0, 0, false)]
    [InlineData("File20.mkv", "File20.nochapters.mkv", 1, 12, 6, 0, false)]
    public void RemoveChaptersFromFile(string inputFilename, string outputFilename, int videoStreamCount, int audioStreamCount, int subtitlesStreamCount, int chaptersStreamCount, bool hasMetadataOutput)
    {
        var testDataPath = Path.Combine(Utils.GetProjectPath(typeof(MediaHelperTests)), "TestData");
        var fileName = Path.Combine(testDataPath, inputFilename);
        var outName = Path.Combine(testDataPath, outputFilename);
        if (File.Exists(outName)) _ = Utils.File.Delete(outName);
        Assert.True(Utils.MediaHelper.RemoveChaptersFromFile(fileName, outName));
        Assert.Equal(videoStreamCount, Utils.MediaHelper.VideoStreamCount(outName));
        Assert.Equal(audioStreamCount, Utils.MediaHelper.AudioStreamCount(outName));
        Assert.Equal(subtitlesStreamCount, Utils.MediaHelper.SubtitlesStreamCount(outName));
        Assert.Equal(chaptersStreamCount, Utils.MediaHelper.ChaptersStreamCount(outName));
        Assert.Equal(hasMetadataOutput, Utils.MediaHelper.HasMetadata(outName));
        Assert.False(Utils.MediaHelper.HasMetadata(outName));
        if (File.Exists(outName)) _ = Utils.File.Delete(outName);
    }

    [Theory]
    [InlineData("A(2023) {tmdb-1} [DVD][DTS 5.1][h264].en.srt", true, "en", false, false, "A (2023) {tmdb-1} [DVD][DTS 5.1][h265].mkv", "A (2023) {tmdb-1} [DVD][DTS 5.1][h265].en.srt")]
    [InlineData("A(2023) {tmdb-1} [DVD][DTS 5.1][h264].es.hi.srt", true, "es", true, false, "A (2023) {tmdb-1} [DVD][DTS 5.1][h265].mkv", "A (2023) {tmdb-1} [DVD][DTS 5.1][h265].es.hi.srt")]
    [InlineData("A(2023) {tmdb-1} [DVD][DTS 5.1][h264].es.cc.srt", true, "es", true, false, "A (2023) {tmdb-1} [DVD][DTS 5.1][h265].mkv", "A (2023) {tmdb-1} [DVD][DTS 5.1][h265].es.hi.srt")]
    [InlineData("A(2023) {tmdb-1} [DVD][DTS 5.1][h264].es.hi.forced.srt", true, "es", true, true, "A (2023) {tmdb-1} [DVD][DTS 5.1][h265].mkv", "A (2023) {tmdb-1} [DVD][DTS 5.1][h265].es.hi.forced.srt")]
    [SuppressMessage("ReSharper", "StringLiteralTypo")]
    public void SubtitlesTests(string fileName, bool isValidFileName, string languageCode, bool hearingImpaired, bool forced, string newMovieName, string newSubtitlesName)
    {
        var file = new SubtitlesBackupFile(fileName);
        Assert.Equal(isValidFileName, file.IsValidFileName);
        if (file.IsValidDirectoryName) Assert.Equal(fileName, file.GetFullName());

        if (isValidFileName)
        {
            Assert.Equal(languageCode, file.Language);
            Assert.Equal(hearingImpaired, file.HearingImpaired);
            Assert.Equal(forced, file.Forced);
        }
        var movie = new MovieBackupFile(newMovieName);
        _ = file.RefreshMediaInfo(movie);
        if (file.IsValidFileName) Assert.Equal(newSubtitlesName, file.GetFileName());
    }

    [Theory]
    [InlineData(@"_TV\File15 {tvdb-1}\Season 1\File15 s01e03 Kid in the Park [WEBDL-2160p][DV HDR10Plus][EAC3 Atmos 5.1][h264].en.hi.srt", true,
        @"_TV\File15 {tvdb-1}\Season 1\File15 s01e03 Kid in the Park [WEBDL-2160p][DV HDR10Plus][EAC3 Atmos 5.1][h264].en.hi.srt")]
    [InlineData(@"_TV\TV Show {tvdb-2}\Season 1\TV Show s01e01 Episode 1 [HDTV-1080p][undefined 2.0][h265].en.srt", true, @"_TV\TV Show {tvdb-2}\Season 1\TV Show s01e01 Episode 1 [HDTV-1080p][AAC 2.0][h265].en.srt")]
    [InlineData(@"_TV\TV Show {tvdb-2}\Season 1\TV Show s01e02 Episode 2 [HDTV-1080p][undefined 2.0][h265].en.srt", true, @"_TV\TV Show {tvdb-2}\Season 1\TV Show s01e02 Episode 2 [HDTV-1080p][AAC 2.0][h265].en.srt")]
    public void SubtitlesTestsWithRefreshMediaInfo(string subtitlesFullName, bool isValidSubtitleFullName, string newSubtitlesFullName)
    {
        var testDataPath = Path.Combine(Utils.GetProjectPath(typeof(MediaHelperTests)), "TestData");
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

        if (file.IsValid)
        {
            Assert.True(file.FullPathToVideoFile.StartsWithIgnoreCase(Path.Combine(file.DirectoryName, file.Title)));
            Assert.Equal(newFileName, file.DirectoryName.HasValue() ? file.GetFullName() : file.GetFileName());
        }
    }

    [Theory]
    [InlineData(@"\\nas2\assets1\_TV\James Martin's Saturday Morning {tvdb-334389}\Season 8\James Martin's Saturday Morning s08e01 Aug 31, 2024 Louise Minchin, Levi Roots, Atul Kochhar, Alysia Vasey [HDTV-1080p][AAC 2.0][h265].en.srt", true,
        @"\\nas2\assets1\_TV\James Martin's Saturday Morning {tvdb-334389}\Season 8\James Martin's Saturday Morning s08e01 Aug 31, 2024 Louise Minchin, Levi Roots, Atul Kochhar, Alysia Vasey [HDTV-1080p][AAC 2.0][h265].en.srt")]
    [InlineData(@"\\nas2\assets1\_TV\James Martin's Saturday Morning {tvdb-334389}\Season 8\James Martin's Saturday Morning s08e09 Oct 26, 2024 Lulu, Fred Sirieix, Cyrus Todiwala Sally Abe [HDTV-1080p][AAC 2.0][h264].en.srt", true,
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