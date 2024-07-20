// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="VideoBackupFileBase.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;

using BackupManager.Extensions;

namespace BackupManager.Entities;

internal abstract class VideoBackupFileBase : ExtendedBackupFileBase
{
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

    protected SpecialFeature SpecialFeature { get; set; }

    protected bool MediaInfoVideo3D { get; set; }

    public override string GetFullName()
    {
        return FullDirectory.HasValue() ? Path.Combine(FullDirectory, GetFileName()) : GetFileName();
    }

    // ReSharper disable once FunctionComplexityOverflow
    [SuppressMessage("ReSharper", "StringLiteralTypo")]
    public override bool RefreshMediaInfo()
    {
        try
        {
            if (SpecialFeature == SpecialFeature.None)
            {
                var model = Utils.MediaHelper.GetMediaInfoModel(OriginalPath);
                if (model == null) return false;

                var resolutionFromMediaInfo = Utils.MediaHelper.GetResolutionFromMediaInfo(model);
                VideoResolution = resolutionFromMediaInfo;

                VideoQuality = VideoResolution switch
                {
                    // ReSharper disable once CommentTypo
                    // if the resolution is actually 576 or 480 then the Quality must be SDTV and not anything else 
                    VideoResolution.R576p or VideoResolution.R480p when VideoQuality is VideoQuality.HDTV or VideoQuality.Unknown => VideoQuality.SDTV,
                    VideoResolution.R720p or VideoResolution.R1080p or VideoResolution.R2160p when VideoQuality is VideoQuality.SDTV or VideoQuality.DVD or VideoQuality.Unknown => VideoQuality.HDTV,
                    _ => VideoQuality
                };

                switch (VideoResolution)
                {
                    case VideoResolution.R1080p:
                    case VideoResolution.R2160p:
                        if (VideoQuality is VideoQuality.SDTV or VideoQuality.DVD) Utils.LogWithPushover(BackupAction.Error, $"{OriginalPath} is {VideoQuality} and so can't be {VideoResolution}");
                        break;
                    case VideoResolution.R720p:
                        if (VideoQuality == VideoQuality.SDTV || VideoQuality == VideoQuality.DVD || IsRemux) Utils.LogWithPushover(BackupAction.Error, $"{OriginalPath} is {VideoQuality} and so can't be {VideoResolution} or Remux");
                        break;
                    case VideoResolution.R480p:
                    case VideoResolution.R576p:
                        if (VideoQuality == VideoQuality.HDTV) Utils.LogWithPushover(BackupAction.Error, $"{OriginalPath} is {VideoResolution} and so can't be {VideoQuality}");
                        break;
                    case VideoResolution.Unknown:
                        Utils.LogWithPushover(BackupAction.Error, $"{OriginalPath} can't be {VideoResolution}");
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
                var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(OriginalPath);
                var videoCodec = Utils.MediaHelper.FormatVideoCodec(model, fileNameWithoutExtension);
                var audioCodec = Utils.MediaHelper.FormatAudioCodec(model, fileNameWithoutExtension);
                var audioChannels = Utils.MediaHelper.FormatAudioChannels(model);
                var dynamicRangeType = Utils.MediaHelper.FormatVideoDynamicRangeType(model);
                MediaInfoVideoCodec = Utils.GetEnumFromAttributeValue<MediaInfoVideoCodec>(videoCodec);
                MediaInfoVideo3D = model.VideoMultiViewCount > 1;
                if (audioCodec != null) MediaInfoAudioCodec = Utils.GetEnumFromAttributeValue<MediaInfoAudioCodec>(audioCodec);
                if (audioChannels > 0) MediaInfoAudioChannels = Utils.GetEnumFromAttributeValue<MediaInfoAudioChannels>($"{audioChannels:0.0}");
                MediaInfoVideoDynamicRangeType = Utils.GetEnumFromAttributeValue<MediaInfoVideoDynamicRangeType>(dynamicRangeType);
                MediaInfoModel = model;
            }

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