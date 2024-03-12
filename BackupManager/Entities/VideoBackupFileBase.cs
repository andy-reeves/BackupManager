// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="VideoBackupFileBase.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System.IO;

using BackupManager.Extensions;

namespace BackupManager.Entities;

internal abstract class VideoBackupFileBase : ExtendedBackupFileBase
{
    // ReSharper disable once IdentifierTypo
    protected const string REMUX = "Remux";

    protected VideoResolution VideoResolution { get; set; }

    // ReSharper disable once UnusedMemberInSuper.Global
    public abstract string QualityFull { get; }

    protected MediaInfoVideoDynamicRangeType MediaInfoVideoDynamicRangeType { get; set; }

    protected MediaInfoAudioCodec MediaInfoAudioCodec { get; set; }

    protected MediaInfoAudioChannels MediaInfoAudioChannels { get; set; }

    protected VideoQuality VideoQuality { get; set; }

    protected MediaInfoVideoCodec MediaInfoVideoCodec { get; set; }

    // ReSharper disable once IdentifierTypo
    protected bool IsRemux { get; set; }

    public override string GetFullName()
    {
        return FullDirectory.HasValue() ? Path.Combine(FullDirectory, GetFileName()) : GetFileName();
    }

    private static VideoResolution GetResolutionFromMediaInfo(MediaInfoModel model)
    {
        if (model == null) return VideoResolution.Unknown;

        var height = model.Height;
        var width = model.Width;
        if (width > 3200 || height > 2100) return VideoResolution.R2160p;
        if (width > 1800 || height > 1000) return VideoResolution.R1080p;
        if (width > 1200 || height > 700) return VideoResolution.R720p;
        if (width > 1000 || height > 560) return VideoResolution.R576p;
        if (width > 0 || height > 0) return VideoResolution.R480p;

        return VideoResolution.Unknown;
    }

    public override bool RefreshMediaInfo()
    {
        try
        {
            var model = Utils.GetMediaInfoModel(OriginalPath);
            if (model == null) return false;

            var resolutionFromMediaInfo = GetResolutionFromMediaInfo(model);

            if (resolutionFromMediaInfo != VideoResolution)
            {
                Utils.LogWithPushover(BackupAction.Error,
                    $"Video resolution for {OriginalPath} was {VideoResolution} from the file name but {resolutionFromMediaInfo} from the MediaInfo");
                VideoResolution = resolutionFromMediaInfo;
            }
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(OriginalPath);
            var videoCodec = Utils.FormatVideoCodec(model, fileNameWithoutExtension);
            var audioCodec = Utils.FormatAudioCodec(model, fileNameWithoutExtension);
            var audioChannels = Utils.FormatAudioChannels(model);
            var dynamicRangeType = Utils.FormatVideoDynamicRangeType(model);
            MediaInfoVideoCodec = Utils.GetEnumFromAttributeValue<MediaInfoVideoCodec>(videoCodec);
            if (audioCodec != null) MediaInfoAudioCodec = Utils.GetEnumFromAttributeValue<MediaInfoAudioCodec>(audioCodec);
            if (audioChannels > 0) MediaInfoAudioChannels = Utils.GetEnumFromAttributeValue<MediaInfoAudioChannels>($"{audioChannels:0.0}");
            MediaInfoVideoDynamicRangeType = Utils.GetEnumFromAttributeValue<MediaInfoVideoDynamicRangeType>(dynamicRangeType);

            // Check if we're valid now
            _ = Validate();
            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }
}