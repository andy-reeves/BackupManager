﻿// --------------------------------------------------------------------------------------------------------------------
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
    [InlineData("A(2023) {tmdb-1} [DVD][DTS 5.1][h264].en.srt", true, "A (2023) {tmdb-1} [DVD][DTS 5.1][h265].mkv", "A (2023) {tmdb-1} [DVD][DTS 5.1][h265].en.srt")]
    [InlineData("A(2023) {tmdb-1} [DVD][DTS 5.1][h264].es.hi.srt", true, "A (2023) {tmdb-1} [DVD][DTS 5.1][h265].mkv", "A (2023) {tmdb-1} [DVD][DTS 5.1][h265].es.hi.srt")]
    [InlineData("Special video-featurette.mkv", false, "", "")]
    [SuppressMessage("ReSharper", "StringLiteralTypo")]
    public void SubtitlesTests(string fileName, bool isValidFileName, string newMovieName, string newSubtitlesName)
    {
        var file = new SubtitlesBackupFile(fileName);
        Assert.Equal(isValidFileName, file.IsValidFileName);
        if (file.IsValidFileName) Assert.Equal(fileName, file.GetFileName());
        if (file.IsValidDirectoryName) Assert.Equal(fileName, file.GetFullName());
        var movie = new MovieBackupFile(newMovieName);
        _ = file.RefreshMediaInfo(movie);
        if (file.IsValidFileName) Assert.Equal(newSubtitlesName, file.GetFileName());
    }

    [Theory]
    [InlineData(@"_TV\File15 {tvdb-1}\Season 1\File15 s01e03 Kid in the Park [WEBDL-2160p][DV HDR10Plus][EAC3 Atmos 5.1][h264].en.hi.srt", true,
        @"_TV\File15 {tvdb-1}\Season 1\File15 s01e03 Kid in the Park [WEBDL-2160p][DV HDR10Plus][EAC3 Atmos 5.1][h264].en.hi.srt")]
    public void SubtitlesTestsWithRefreshMediaInfo(string subtitlesFullName, bool isValidSubtitleFullName, string newSubtitlesFullName)
    {
        var testDataPath = Path.Combine(Utils.GetProjectPath(typeof(MediaHelperTests)), "TestData");
        var fileName = Path.Combine(testDataPath, subtitlesFullName);
        var newFileName = Path.Combine(testDataPath, newSubtitlesFullName);
        var file = new SubtitlesBackupFile(fileName);
        Assert.Equal(isValidSubtitleFullName, file.IsValid);

        if (file.IsValid)
        {
            Assert.Equal(fileName, file.FullDirectory.HasValue() ? file.GetFullName() : file.GetFileName());
            Assert.Equal("." + subtitlesFullName.SubstringAfterIgnoreCase("]."), file.SubtitlesExtension);
        }
        Assert.True(file.RefreshMediaInfo());
        Assert.Equal(isValidSubtitleFullName, file.IsValid);
        if (file.IsValid) Assert.Equal(newFileName, file.FullDirectory.HasValue() ? file.GetFullName() : file.GetFileName());
    }
}