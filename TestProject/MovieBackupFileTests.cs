// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="EntityTests.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;

using BackupManager;
using BackupManager.Entities;

namespace TestProject;

[SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "<Pending>")]
[SuppressMessage("ReSharper", "MemberCanBeFileLocal")]
[SuppressMessage("ReSharper", "StringLiteralTypo")]
public sealed class MovieBackupFileTests
{
    [Theory]
    [InlineData("File16 (2014) {tmdb-261103} [Remux-1080p][3D][DTS-HD MA 5.1][h264].mkv", false, true, "File16 (2014) {tmdb-261103} [Remux-1080p][3D][DTS-HD MA 5.1][h264].mkv")]
    [InlineData("File13 (2024) [Remux-1080p][DTS-HD MA 5.1][h264].mkv", false, true, "File13 (2024) [Remux-1080p][DTS-HD MA 5.1][h264].mkv")]
    [InlineData("File14 (2024) [WEBDL-1080p][EAC3 5.1][h264].mkv", false, true, "File14 (2024) [WEBDL-1080p][EAC3 5.1][h265].mkv")]
    [InlineData("Avengers Infinity War (2018) {tmdb-299536} [Remux-2160p][HDR10][TrueHD Atmos 7.1][h265].mkv", false, true, "Avengers Infinity War (2018) {tmdb-299536} [Remux-2160p][HDR10][TrueHD Atmos 7.1][h265].mkv")]
    [InlineData(@"\\nas3\assets3\_Movies\Joker (2019)\Joker (2019) {tmdb-475557} [Remux-2160p][HDR10][TrueHD Atmos 7.1][h265].mkv", true, true, "Joker (2019) {tmdb-475557} [Remux-2160p][HDR10][TrueHD Atmos 7.1][h265].mkv")]
    public void MovieRefreshInfoTests(string sourceFileName, bool validDirectoryName, bool validFileName, string expectedFileName)
    {
        string fileName;

        if (File.Exists(sourceFileName))
            fileName = sourceFileName;
        else
        {
            var testDataPath = Path.Combine(Utils.GetProjectPath(typeof(MediaHelperTests)), "TestData");
            fileName = Path.Combine(testDataPath, sourceFileName);
        }
        var movie = new MovieBackupFile(fileName);
        Assert.Equal(validDirectoryName, movie.IsValidDirectoryName);
        Assert.Equal(validFileName, movie.IsValidFileName);
        if (movie.IsValidFileName) Assert.Equal(Path.GetFileName(fileName), movie.GetFileName());
        Assert.True(movie.RefreshMediaInfo());
        Assert.Equal(expectedFileName, movie.GetFileName());
    }
}