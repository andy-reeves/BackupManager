// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="MediaInfoTests.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;

using BackupManager;
using BackupManager.Entities;

namespace TestProject;

[SuppressMessage("ReSharper", "MemberCanBeFileLocal")]
[SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "<Pending>")]
public sealed class MediaHelperTests
{
    static MediaHelperTests()
    {
        var mediaBackup = MediaBackup.Load(Path.Combine(Utils.GetProjectPath(typeof(MediaHelperTests)), "..\\BackupManager\\MediaBackup.xml"));
        Utils.Config = mediaBackup.Config;
    }

    [Fact]
    public void MediaInfo()
    {
        var testDataPath = Path.Combine(Utils.GetProjectPath(typeof(MediaHelperTests)), "TestData");
        var mediaFileName = Path.Combine(testDataPath, "File1 [DV].mkv");
        Assert.True(Utils.File.IsDolbyVisionProfile5(mediaFileName));
        var file = new MovieBackupFile(mediaFileName);
        Assert.True(file.RefreshMediaInfo());
        Assert.Equal(5, file.MediaInfoModel.DoviConfigurationRecord.DvProfile);
        mediaFileName = Path.Combine(testDataPath, "File2 [DV].mkv");
        Assert.False(Utils.File.IsDolbyVisionProfile5(mediaFileName));
        file = new MovieBackupFile(mediaFileName);
        Assert.True(file.RefreshMediaInfo());
        Assert.Null(file.MediaInfoModel.DoviConfigurationRecord);
        mediaFileName = Path.Combine(testDataPath, "File3 [DV].mkv");
        Assert.True(Utils.File.IsDolbyVisionProfile5(mediaFileName));
        mediaFileName = Path.Combine(testDataPath, "File4 [DV] profile8.mkv");
        Assert.False(Utils.File.IsDolbyVisionProfile5(mediaFileName));
        file = new MovieBackupFile(mediaFileName);
        Assert.True(file.RefreshMediaInfo());
        Assert.Equal(8, file.MediaInfoModel.DoviConfigurationRecord.DvProfile);
    }
}