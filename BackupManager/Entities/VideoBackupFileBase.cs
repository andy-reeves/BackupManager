// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="VideoBackupFileBase.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace BackupManager.Entities;

internal abstract class VideoBackupFileBase : ExtendedBackupFileBase
{
    public abstract string QualityFull { get; }

    public MediaInfoVideoDynamicRangeType MediaInfoVideoDynamicRangeType { get; set; }

    public MediaInfoAudioCodec MediaInfoAudioCodec { get; set; }

    public MediaInfoAudioChannels MediaInfoAudioChannels { get; set; }

    public VideoResolution VideoResolution { get; set; }

    public VideoQuality VideoQuality { get; set; }

    public MediaInfoVideoCodec MediaInfoVideoCodec { get; set; }
}