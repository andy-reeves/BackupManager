// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="MediaInfoTests.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

#if DEBUG
using System.Diagnostics.CodeAnalysis;

using BackupManager;

namespace TestProject;

[SuppressMessage("ReSharper", "MemberCanBeFileLocal")]
[SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "<Pending>")]
public sealed class MediaInfoTests
{
    [Fact]
    public void MediaInfo()
    {
        var mediaFileName = Path.Combine(Utils.GetProjectPath(typeof(UtilsUnitTests)), @"TestData\File1 [DV].mkv");
        Assert.True(Utils.FileIsDolbyVisionProfile5(mediaFileName));
        mediaFileName = Path.Combine(Utils.GetProjectPath(typeof(UtilsUnitTests)), @"TestData\File2 [DV].mkv");
        Assert.False(Utils.FileIsDolbyVisionProfile5(mediaFileName));
        mediaFileName = Path.Combine(Utils.GetProjectPath(typeof(UtilsUnitTests)), @"TestData\File3 [DV].mkv");
        Assert.True(Utils.FileIsDolbyVisionProfile5(mediaFileName));
        mediaFileName = Path.Combine(Utils.GetProjectPath(typeof(UtilsUnitTests)), @"TestData\File4 [DV] profile8.mkv");
        Assert.False(Utils.FileIsDolbyVisionProfile5(mediaFileName));
    }

    [Fact]
    public void RenameVideoFiles()
    {
        var projectPath = Utils.GetProjectPath(typeof(UtilsUnitTests));
        var mediaFileName = Path.Combine(projectPath, @"TestData\File5 [WEBDL-1080p][EAC3 5.1][h264].mkv");
        var mediaFileNameOutputIfRenamed = Path.Combine(projectPath, @"TestData\File5 [WEBDL-1080p][EAC3 5.1][h264].mkv");
        Assert.True(File.Exists(mediaFileName));
        Utils.RenameVideoCodec(mediaFileName);
        Assert.True(File.Exists(mediaFileNameOutputIfRenamed));
        mediaFileName = Path.Combine(projectPath, @"TestData\File6 [WEBDL-1080p][EAC3 5.1][h264].mkv");
        mediaFileNameOutputIfRenamed = Path.Combine(projectPath, @"TestData\File6 [WEBDL-1080p][EAC3 5.1][x265].mkv");
        Assert.True(File.Exists(mediaFileName));
        Utils.RenameVideoCodec(mediaFileName);
        Assert.False(File.Exists(mediaFileName));
        Assert.True(File.Exists(mediaFileNameOutputIfRenamed));
        Utils.FileMove(mediaFileNameOutputIfRenamed, mediaFileName);
        Assert.False(File.Exists(mediaFileNameOutputIfRenamed));
        Assert.True(File.Exists(mediaFileName));
        mediaFileName = Path.Combine(projectPath, @"TestData\File7 [WEBDL-1080p][EAC3 5.1].mkv");
        mediaFileNameOutputIfRenamed = Path.Combine(projectPath, @"TestData\File7 [WEBDL-1080p][EAC3 5.1].mkv");
        Assert.True(File.Exists(mediaFileName));
        Utils.RenameVideoCodec(mediaFileName);
        Assert.True(File.Exists(mediaFileNameOutputIfRenamed));
    }
}
#endif