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

    [Theory]
    [InlineData("File1 [DV].mkv", 5)]
    [InlineData("File2 [DV].mkv", 0)]
    [InlineData("File3 [DV].mkv", 5)]
    [InlineData("File4 [DV] profile8.mkv", 8)]
    public void MediaInfo(string fileName, int dvProfile)
    {
        var testDataPath = Path.Combine(Utils.GetProjectPath(typeof(MediaHelperTests)), "TestData");
        var mediaFileName = Path.Combine(testDataPath, fileName);
        var file = new MovieBackupFile(mediaFileName);
        Assert.True(file.RefreshMediaInfo());

        if (dvProfile > 0)
            Assert.Equal(dvProfile, file.MediaInfoModel.DoviConfigurationRecord.DvProfile);
        else
            Assert.Null(file.MediaInfoModel.DoviConfigurationRecord);
    }
}