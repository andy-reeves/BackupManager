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
public sealed class MediaInfoTests
{
    static MediaInfoTests()
    {
        var mediaBackup = MediaBackup.Load(Path.Combine(Utils.GetProjectPath(typeof(MediaInfoTests)), "..\\BackupManager\\MediaBackup.xml"));
        Utils.Config = mediaBackup.Config;
    }

    [Fact]
    public void MediaInfo()
    {
        var testDataPath = Path.Combine(Utils.GetProjectPath(typeof(MediaInfoTests)), "TestData");
        var mediaFileName = Path.Combine(testDataPath, "File1 [DV].mkv");
        Assert.True(Utils.FileIsDolbyVisionProfile5(mediaFileName));
        mediaFileName = Path.Combine(testDataPath, "File2 [DV].mkv");
        Assert.False(Utils.FileIsDolbyVisionProfile5(mediaFileName));
        mediaFileName = Path.Combine(testDataPath, "File3 [DV].mkv");
        Assert.True(Utils.FileIsDolbyVisionProfile5(mediaFileName));
        mediaFileName = Path.Combine(testDataPath, "File4 [DV] profile8.mkv");
        Assert.False(Utils.FileIsDolbyVisionProfile5(mediaFileName));
    }

    [Fact]
    [SuppressMessage("ReSharper", "StringLiteralTypo")]
    [SuppressMessage("ReSharper", "CommentTypo")]
    public void RenameVideoFiles()
    {
        var testDataPath = Path.Combine(Utils.GetProjectPath(typeof(UtilsUnitTests)), "TestData");

        // ID                                       : 1
        // Format                                   : RGB
        // Codec ID                                 : V_MS/VFW/FOURCC
        var mediaFileName = Path.Combine(testDataPath, "File11 Episode 1 [HDTV-720p][AAC 2.0][RGB].mkv");
        var mediaFileNameOutputIfRenamed = mediaFileName;
        Assert.True(File.Exists(mediaFileName));
        Assert.False(Utils.RenameVideoCodec(mediaFileName, out _));
        Assert.True(File.Exists(mediaFileNameOutputIfRenamed));

        // ID                                       : 1
        // Format                                   : MPEG-4 Visual
        // Format profile                           : Simple@L3
        // Format settings, BVOP                    : No
        // Format settings, QPel                    : No
        // Format settings, GMC                     : No warppoints
        // Format settings, Matrix                  : Default (H.263)
        // Codec ID                                 : V_MPEG4/ISO/ASP
        // Codec ID/Info                            : Advanced Simple Profile
        mediaFileName = Path.Combine(testDataPath, "File10 (1983) {tmdb-29140} [DVD][AC3 2.0][MPEG4].mkv");
        mediaFileNameOutputIfRenamed = mediaFileName;
        Assert.True(File.Exists(mediaFileName));
        Assert.False(Utils.RenameVideoCodec(mediaFileName, out var newPath));
        Assert.Equal(mediaFileName, newPath);
        Assert.True(File.Exists(mediaFileNameOutputIfRenamed));

        // ID                                       : 1
        // Format                                   : V_MS/VFW/FOURCC / xvid
        // Codec ID                                 : V_MS/VFW/FOURCC / xvid
        mediaFileName = Path.Combine(testDataPath, "File9 [SDTV][MP2 2.0][XviD].mkv");
        mediaFileNameOutputIfRenamed = mediaFileName;
        Assert.True(File.Exists(mediaFileName));
        Assert.False(Utils.RenameVideoCodec(mediaFileName, out _));
        Assert.True(File.Exists(mediaFileNameOutputIfRenamed));

        // ID                                       : 1
        // Format                                   : AVC
        // Format/Info                              : Advanced Video Codec
        // Format profile                           : High@L4.1
        // Format settings                          : CABAC / 2 Ref Frames
        // Format settings, CABAC                   : Yes
        // Format settings, Reference frames        : 2 frames
        // Codec ID                                 : V_MPEG4/ISO/AVC
        mediaFileName = Path.Combine(testDataPath, "File8 [Bluray-1080p Remux][DTS-HD MA 5.1][h264].mkv");
        mediaFileNameOutputIfRenamed = mediaFileName;
        Assert.True(File.Exists(mediaFileName));
        Assert.False(Utils.RenameVideoCodec(mediaFileName, out _));
        Assert.True(File.Exists(mediaFileNameOutputIfRenamed));

        // ID                                       : 1
        // Format                                   : AVC
        // Format/Info                              : Advanced Video Codec
        // Format profile                           : High@L4
        // Format settings                          : CABAC / 4 Ref Frames
        // Format settings, CABAC                   : Yes
        // Format settings, Reference frames        : 4 frames
        // Codec ID                                 : V_MPEG4/ISO/AVC
        mediaFileName = Path.Combine(testDataPath, "File5 [WEBDL-1080p][EAC3 5.1][h264].mkv");
        mediaFileNameOutputIfRenamed = mediaFileName;
        Assert.True(File.Exists(mediaFileName));
        Assert.False(Utils.RenameVideoCodec(mediaFileName, out _));
        Assert.True(File.Exists(mediaFileNameOutputIfRenamed));

        // This one does need renaming
        // ID                                       : 1
        // Format                                   : HEVC
        // Format/Info                              : High Efficiency Video Coding
        // Format profile                           : Main@L4@Main
        // Codec ID                                 : V_MPEGH/ISO/HEVC
        mediaFileName = Path.Combine(testDataPath, "File6 [WEBDL-1080p][EAC3 5.1][h264].mkv");
        mediaFileNameOutputIfRenamed = Path.Combine(testDataPath, "File6 [WEBDL-1080p][EAC3 5.1][h265].mkv");
        Assert.True(File.Exists(mediaFileName));
        Assert.True(Utils.RenameVideoCodec(mediaFileName, out newPath));
        Assert.False(File.Exists(mediaFileName));
        Assert.True(File.Exists(mediaFileNameOutputIfRenamed));
        Assert.Equal(mediaFileNameOutputIfRenamed, newPath);

        // reset the file
        Utils.FileMove(mediaFileNameOutputIfRenamed, mediaFileName);
        Assert.False(File.Exists(mediaFileNameOutputIfRenamed));
        Assert.True(File.Exists(mediaFileName));

        // ID                                       : 1
        // Format                                   : HEVC
        // Format/Info                              : High Efficiency Video Coding
        // Format profile                           : Main@L4@Main
        // Codec ID                                 : V_MPEGH/ISO/HEVC
        mediaFileName = Path.Combine(testDataPath, "File7 [WEBDL-1080p][EAC3 5.1][h265].mkv");
        mediaFileNameOutputIfRenamed = mediaFileName;
        Assert.True(File.Exists(mediaFileName));
        Assert.False(Utils.RenameVideoCodec(mediaFileName, out _));
        Assert.True(File.Exists(mediaFileNameOutputIfRenamed));
    }
}