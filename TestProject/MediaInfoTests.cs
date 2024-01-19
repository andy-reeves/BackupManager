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
        /*var mediaFileName = Path.Combine(Utils.GetProjectPath(typeof(UtilsUnitTests)), @"TestData\File1 [DV].mkv");
        Assert.True(Utils.FileIsDolbyVisionProfile5(mediaFileName));
        mediaFileName = Path.Combine(Utils.GetProjectPath(typeof(UtilsUnitTests)), @"TestData\File2 [DV].mkv");
        Assert.False(Utils.FileIsDolbyVisionProfile5(mediaFileName));
        mediaFileName = Path.Combine(Utils.GetProjectPath(typeof(UtilsUnitTests)), @"TestData\File3 [DV].mkv");
        Assert.True(Utils.FileIsDolbyVisionProfile5(mediaFileName));
        mediaFileName = Path.Combine(Utils.GetProjectPath(typeof(UtilsUnitTests)), @"TestData\File4 [DV] profile8.mkv");
        Assert.False(Utils.FileIsDolbyVisionProfile5(mediaFileName));
        */
        //var mediaFileName =
        //    @"I:\radarr_bin\Extraction 2 (2023)\Extraction 2 (2023) {tmdb-697843} [WEBDL-1080p][DV][EAC3 Atmos 5.1][HEVC].mkv";
        // Assert.False(Utils.FileIsDolbyVisionProfile5(mediaFileName));

        var mediaFileName =
            @"\\nas2\assets4\Test\File 4 - not working - DV Profile 5 - everything purple and green no playback\File 4 - not working - DV Profile 5 - everything purple and green no playback [WEBDL-2160p][DV][AC3 5.1][h265].mkv";
        Assert.False(Utils.FileIsDolbyVisionProfile5(mediaFileName));

        //R:\Test\File 4 - not working - DV Profile 5 - everything purple and green no playback\File 4 - not working - DV Profile 5 - everything purple and green no playback [WEBDL-2160p][DV][AC3 5.1][h265].mkv
    }

    [Fact]
    public void FFprobe()
    {
        // var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "Files", "Media", "H264_sample.mp4");
        //var info = Subject.GetMediaInfo(path);
        var mediaFileName = Path.Combine(Utils.GetProjectPath(typeof(UtilsUnitTests)), @"TestData\File1 [DV].mkv");
        var info = new VideoFileInfoReader().GetMediaInfo(mediaFileName);
        Console.WriteLine(info.ContainerFormat);
        Assert.True(Utils.FileIsDolbyVisionProfile5(mediaFileName));
    }
}
#endif