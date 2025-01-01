// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="MediaHelperTests.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;

using BackupManager;

namespace TestProject;

[SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "<Pending>")]
[SuppressMessage("ReSharper", "MemberCanBeFileLocal")]
[SuppressMessage("ReSharper", "StringLiteralTypo")]
public sealed class MediaHelperTests
{
    [Theory]
    [InlineData("File18.nochapters.master.mkv", "File18.master.chap", "File18.chaptersAdded.mkv", 1, 1, 0, 1, false)]
    public void AddChaptersToFile(string inputFilename, string chaptersFilename, string outputFilename, int videoStreamCount, int audioStreamCount,
        int subtitlesStreamCount, int chaptersStreamCount, bool hasMetadata)
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
    [InlineData("File23.mkv", 1)]
    public void HasSubtitles(string inputFilename, int expectedResult)
    {
        var testDataPath = Path.Combine(Utils.GetProjectPath(typeof(MediaHelperTests)), "TestData");
        var fileName = Path.Combine(testDataPath, inputFilename);
        if (expectedResult > 0) Assert.True(Utils.MediaHelper.HasSubtitles(fileName));
        Assert.Equal(expectedResult, Utils.MediaHelper.SubtitlesStreamCount(fileName));
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
    public void RemoveSubtitlesFromFile(string inputFilename, string outputFilename, int videoStreamCount, int audioStreamCount, int subtitlesStreamCount,
        int chaptersStreamCount, bool hasMetadata)
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
    public void RemoveMetadataFromFile(string inputFilename, bool hasMetadataInput, string outputFilename, int videoStreamCount, int audioStreamCount,
        int subtitlesStreamCount, int chaptersStreamCount)
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
    public void RemoveChaptersFromFile(string inputFilename, string outputFilename, int videoStreamCount, int audioStreamCount, int subtitlesStreamCount,
        int chaptersStreamCount, bool hasMetadataOutput)
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
}