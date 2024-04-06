// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="MovieBackupFileNameTests.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;

using BackupManager.Entities;

namespace TestProject;

[SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "<Pending>")]
[SuppressMessage("ReSharper", "MemberCanBeFileLocal")]
[SuppressMessage("ReSharper", "StringLiteralTypo")]
public sealed class MovieBackupFileNameTests
{
    [Theory]
    [InlineData(@"\\nas2\assets3\_Movies (non-tmdb)\Aliens (1986)\Aliens (1986) [Remux-2160p][HDR10][AC3 5.1][h265].mkv", true)]
    [InlineData(@"\\nas1\assets4\_Movies\Aliens (1986)-other\Aliens (1986)-other {tmdb-679} [Remux-2160p][HDR10][AC3 5.1][h265].mkv", true)]
    [InlineData(@"\\nas2\assets3\_Movies\Aliens (1986)\Aliens (1986) {tmdb-679} [Bluray-2160p][HDR10][AC3 5.1][h265].mkv", true)]
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
    [SuppressMessage("ReSharper", "StringLiteralTypo")]
    public void MovieNameOnlyTests(string fileName, bool isValidFileName)
    {
        var file = new MovieBackupFile(fileName);
        Assert.Equal(isValidFileName, file.IsValidFileName);
        if (file.IsValidFileName) Assert.Equal(Path.GetFileName(fileName), file.GetFileName());
        if (file.IsValidDirectoryName) Assert.Equal(fileName, file.GetFullName());
    }
}