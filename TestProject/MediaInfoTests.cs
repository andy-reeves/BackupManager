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
}
#endif