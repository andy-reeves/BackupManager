// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="MovieBackupFileTests.cs" company="Andy Reeves">
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
public sealed class MovieBackupFileTests
{
    [Theory]
    [InlineData(@"Novocaine (2025) {tmdb-1195506} [WEBDL-1080p][AAC 2.0][AV1].mkv", true)]
    [InlineData(@"\\nas2\assets3\_Movies (non-tmdb)\Aliens (1986)\Aliens (1986) [Remux-2160p Proper][HDR10][AC3 5.1][h265].mkv", true,
        "Aliens (1986) [Remux-2160p][HDR10][AC3 5.1][h265].mkv")]
    [InlineData(@"\\nas2\assets3\_Movies (non-tmdb)\Aliens (1986)\Aliens (1986) [Remux-2160p][HDR10][AC3 5.1][h265].mkv", true)]
    [InlineData(@"\\nas1\assets4\_Movies\Aliens (1986)-other\Aliens (1986)-other {tmdb-679} [Remux-2160p][HDR10][AC3 5.1][h265].mkv", true)]
    [InlineData(@"\\nas2\assets3\_Movies\Aliens (1986)\Aliens (1986) {tmdb-679} [Bluray-2160p][HDR10][AC3 5.1][h265].mkv", true)]
    [InlineData("Battlestar Galactica (1978) {tmdb-148980} {edition-EXTENDEDBOB} [Remux-2160p][HDR10][DTS-HD MA 5.1][h265].mkv", false)]
    [InlineData("Battlestar Galactica (1978) {tmdb-148980} {edition-ASSEMBLY CUT} [Remux-2160p][HDR10][DTS-HD MA 5.1][h265].mkv", true)]
    [InlineData("Battlestar Galactica (1978) {tmdb-148980} {edition-EXTENDED} [Remux-2160p][HDR10][DTS-HD MA 5.1][h265].mkv", true)]
    [InlineData("Battlestar Galactica (1978) {tmdb-148980} [Remux-2160p][PQ][DTS-HD MA 5.1][h265].mkv", true)]
    [InlineData("Battlestar Galactica (1978) {tmdb-148980} [Remux-1080p][DTS-HD MA 5.1][h264].mkv", true)]
    [InlineData("Battlestar Galactica (1978) [Remux-1080p][DTS-HD MA 5.1][h264].mkv", true)]
    [InlineData("Battlestar Galactica (1978) [Remux-1080p][audio][h264].mkv", false)]
    [InlineData("A (2022) {tmdb-1} [DVD][DTS 1.0][h264].mkv", true)]
    [InlineData("A (2022) {tmdb-1} [DVD][DTS-X 2.0][h265].mkv", true)]
    [InlineData("A (2022) {tmdb-1} [DVD][DTS-ES 3.0][VP9].mkv", true)]
    [InlineData("A (2022) {tmdb-1} [DVD][DTS HD 3.1][VC1].mkv", true)]
    [InlineData("A (2022) {tmdb-1} [DVD][DTS-HD MA 4.0][MPEG2].mkv", true)]
    [InlineData("A (2022) {tmdb-1} [DVD][DTS-HD HRA 5.0][VP9].mkv", true)]
    [InlineData("A (2022) {tmdb-1} [DVD][TrueHD Atmos 5.1][VP9].mkv", true)]
    [InlineData("A (2022) {tmdb-1} [DVD][EAC3 6.0][VP9].mkv", true)]
    [InlineData("A (2022) {tmdb-1} [DVD][EAC3 Atmos 6.1][VP9].mkv", true)]
    [InlineData("A (2022) {tmdb-1} [DVD][AC3 7.1][VP9].mkv", true)]
    [InlineData("A (2022) {tmdb-1} [DVD][FLAC 8.0][VP9].mkv", true)]
    [InlineData("A (2022) {tmdb-1} [DVD][PCM 2.0][VP9].mkv", true)]
    [InlineData("A (2022) {tmdb-1} [DVD][MP3 2.0][VP9].mkv", true)]
    [InlineData("A (2022) {tmdb-1} [DVD][AAC 2.0][VP9].mkv", true)]
    [InlineData("A (2022) {tmdb-1} [DVD][TrueHD 2.0][VP9].mkv", true)]
    [InlineData("A (2022) {tmdb-1} [DVD][AVC 2.0][VP9].mkv", true)]
    [InlineData("A (2022) {tmdb-1} [DVD][Opus 2.0][VP9].mkv", true)]
    [InlineData("Asterix and Obelix The Middle Kingdom (2023) {tmdb-643215} [Remux-1080p][DTS-HD MA 5.1][h264].en.srt", false)]
    [InlineData("Special video-featurette.mkv", true)]
    [InlineData(@"\\nas2\assets3\_Movies (non-tmdb)\Aliens (1986)\Aliens (1986) [Remux-2160p][HDR10][AC3 5.1].mkv", true)]
    [InlineData(@"Gangster Story (1959) {tmdb-81720} [DVD][MP2 2.0][MPEG2].mkv", true)]
    [SuppressMessage("ReSharper", "StringLiteralTypo")]
    public void MovieNameOnlyTests(string fileName, bool isValidFileName, string expectedFileName = "")
    {
        var file = new MovieBackupFile(fileName);
        Assert.Equal(isValidFileName, file.IsValidFileName);
        if (expectedFileName.HasNoValue()) expectedFileName = Path.GetFileName(fileName);
        if (file.IsValidFileName) Assert.Equal(expectedFileName, file.GetFileName());
        if (file.IsValidDirectoryName) Assert.Equal(Path.Combine(Path.GetDirectoryName(fileName) ?? string.Empty, expectedFileName), file.GetFullName());
    }

    [Theory]
    [InlineData("File16 (2014) {tmdb-261103} [Remux-1080p][3D][DTS-HD MA 5.1][h264].mkv", true, true, true,
        "File16 (2014) {tmdb-261103} [Remux-1080p][3D][DTS-HD MA 5.1][h264].mkv")]
    [InlineData("File13 (2024) [Remux-1080p][DTS-HD MA 5.1][h264].mkv", true, true, true, "File13 (2024) [Remux-1080p][DTS-HD MA 5.1][h264].mkv")]
    [InlineData("File14 (2024) [WEBDL-1080p][EAC3 5.1][h264].mkv", true, true, false, "File14 (2024) [WEBDL-1080p][EAC3 5.1][h265].mkv")]
    [InlineData("Avengers Infinity War (2018) {tmdb-299536} [Remux-2160p][HDR10][TrueHD Atmos 7.1][h265].mkv", true, true, true,
        "Avengers Infinity War (2018) {tmdb-299536} [Remux-2160p][HDR10][TrueHD Atmos 7.1][h265].mkv")]
    [InlineData("Avengers Infinity War (2018) {tmdb-299536}.mkv", true, true, false,
        "Avengers Infinity War (2018) {tmdb-299536} [HDTV-2160p][HDR10][TrueHD Atmos 7.1][h265].mkv")]
    [InlineData("Avengers Infinity War (2018).mkv", true, true, false, "Avengers Infinity War (2018) [HDTV-2160p][HDR10][TrueHD Atmos 7.1][h265].mkv")]
    public void MovieRefreshInfoTests(string sourceFileName, bool validFileName, bool refreshReturnValue, bool outputNameSameAsInput, string mediaFileNameOutputIfRenamed)
    {
        var testDataPath = Path.Combine(Utils.GetProjectPath(typeof(MediaHelperTests)), "TestData");
        var mediaFileName = File.Exists(sourceFileName) ? sourceFileName : Path.Combine(testDataPath, sourceFileName);
        var backupFile = new MovieBackupFile(mediaFileName);
        if (File.Exists(mediaFileName)) Assert.Equal(refreshReturnValue, backupFile.RefreshMediaInfo());
        Assert.Equal(validFileName, backupFile.IsValidFileName);
        if (backupFile.IsValidFileName && outputNameSameAsInput) Assert.Equal(Path.GetFileName(mediaFileName), backupFile.GetFileName());
        if (refreshReturnValue) Assert.Equal(mediaFileNameOutputIfRenamed, backupFile.GetFileName());
    }
}