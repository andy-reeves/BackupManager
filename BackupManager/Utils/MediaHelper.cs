﻿// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="MediaHelper.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;

using BackupManager.Entities;
using BackupManager.Extensions;
using BackupManager.Properties;
using BackupManager.Radarr;

// ReSharper disable once CheckNamespace
namespace BackupManager;

internal static partial class Utils
{
    internal static class MediaHelper
    {
        /// <summary>
        ///     Returns True if the path contains [DV]
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        internal static bool VideoFileIsDolbyVision(string path)
        {
            ArgumentException.ThrowIfNullOrEmpty(path);
            return path.HasValue() && path.ContainsIgnoreCase("[DV]");
        }

        public static string FormatVideoDynamicRangeType(MediaInfoModel mediaInfo)
        {
            return mediaInfo.VideoHdrFormat switch
            {
                HdrFormat.DolbyVision => "DV",
                HdrFormat.DolbyVisionHdr10 => "DV HDR10",
                HdrFormat.DolbyVisionHdr10Plus => "DV HDR10Plus",
                HdrFormat.DolbyVisionHlg => "DV HLG",
                HdrFormat.DolbyVisionSdr => "DV SDR",
                HdrFormat.Hdr10 => "HDR10",
                HdrFormat.Hdr10Plus => "HDR10Plus",
                HdrFormat.Hlg10 => "HLG",
                HdrFormat.Pq10 => "PQ",
                _ => ""
            };
        }

        [SuppressMessage("ReSharper", "StringLiteralTypo")]
        internal static string FormatAudioCodec(MediaInfoModel mediaInfo, string sceneName)
        {
            if (mediaInfo.AudioFormat == null) return null;

            var audioFormat = mediaInfo.AudioFormat;
            var audioCodecId = mediaInfo.AudioCodecId ?? string.Empty;
            var audioProfile = mediaInfo.AudioProfile ?? string.Empty;
            if (audioFormat.Empty()) return string.Empty;
            if (audioCodecId == "thd+") return "TrueHD Atmos";

            switch (audioFormat)
            {
                case "truehd":
                    return "TrueHD";
                case "flac":
                    return "FLAC";
                case "dts":
                    return audioProfile switch
                    {
                        "DTS:X" => "DTS-X",
                        "DTS-HD MA" => "DTS-HD MA",
                        "DTS-ES" => "DTS-ES",
                        "DTS-HD HRA" => "DTS-HD HRA",
                        "DTS Express" => "DTS Express",
                        "DTS 96/24" => "DTS 96/24",
                        _ => "DTS"
                    };
            }
            if (audioCodecId == "ec+3") return "EAC3 Atmos";

            switch (audioFormat)
            {
                case "eac3":
                    return "EAC3";
                case "ac3":
                    return "AC3";
                case "aac" when audioCodecId == "A_AAC/MPEG4/LC/SBR":
                    return "HE-AAC";
                case "aac":
                    return "AAC";
                case "mp3":
                    return "MP3";
                case "mp2":
                    return "MP2";
                case "opus":
                    return "Opus";
            }
            if (audioFormat.StartsWithIgnoreCase("pcm_") || audioFormat.StartsWithIgnoreCase("adpcm_")) return "PCM";

            switch (audioFormat)
            {
                case "vorbis":
                    return "Vorbis";
                case "wmav1" or "wmav2" or "wmapro":
                    return "WMA";
                default:
                    Trace($"Unknown audio format: '{audioFormat}' in '{sceneName}'. Streams: {mediaInfo.RawStreamData}");
                    return mediaInfo.AudioFormat;
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="mediaInfo"></param>
        /// <param name="fileName"></param>
        /// <returns>
        ///     The following values are possibly returned: null, string.Empty, h264, h265, XviD, DivX, MPEG4, VP6, MPEG2,
        ///     MPEG, VC1, AV1, VP7,VP8, VP9, WMV, RGB
        /// </returns>
        [SuppressMessage("ReSharper", "StringLiteralTypo")]
        internal static string FormatVideoCodec(MediaInfoModel mediaInfo, string fileName)
        {
            if (mediaInfo.VideoFormat == null) return null;

            var videoFormat = mediaInfo.VideoFormat;
            var videoCodecId = mediaInfo.VideoCodecId ?? string.Empty;
            var result = videoFormat.Trim();
            if (videoFormat.Empty()) return result;
            if (videoCodecId == "x264" || videoFormat == "h264") return "h264";
            if (videoCodecId == "x265") return "h265";

            if (videoFormat == "mpeg4" || videoFormat.Contains("msmpeg4"))
            {
                return videoCodecId.ToLowerInvariant() switch
                {
                    "xvid" => "XviD",
                    "div3" or "dx50" or "divx" => "DivX",
                    _ => "MPEG4"
                };
            }
            if (videoFormat.Contains("vp6")) return "VP6";

            switch (videoFormat)
            {
                case "hevc":
                    return "h265";
                case "mpeg2video":
                    return "MPEG2";
                case "mpeg1video":
                    return "MPEG";
                case "vc1":
                    return "VC1";
                case "av1":
                    return "AV1";
                case "vp7":
                case "vp8":
                case "vp9":
                    return videoFormat.ToUpperInvariant();
                case "wmv1":
                case "wmv2":
                case "wmv3":
                    return "WMV";
                case "rawvideo":
                    return "RGB";
                case "qtrle":
                case "rpza":
                case "rv10":
                case "rv20":
                case "rv30":
                case "rv40":
                case "cinepak":
                case "msvideo1":
                    LogWithPushover(BackupAction.General, PushoverPriority.High, $"About to return string.Empty for {fileName}");
                    return "";
            }
            Trace($"Unknown video format: '{videoFormat}'. Streams: {mediaInfo.RawStreamData}");
            return result;
        }

        internal static VideoResolution GetResolutionFromMediaInfo(MediaInfoModel model)
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

        /// <summary>
        ///     Checks if the path is a video file and renames the file with extracted media info if required
        /// </summary>
        /// <param name="path"></param>
        internal static void CheckVideoFileAndRenameIfRequired(ref string path)
        {
            var file = ExtendedBackupFileBase(path);
            if (file == null) return;

            if (!file.IsValidFileName)
            {
                LogWithPushover(BackupAction.Error, $"{path} does not have a valid file name. Name not checked");
                return;
            }

            if (file.RefreshMediaInfo())
            {
                var newFullPath = file.GetFullName();
                if (newFullPath == path) return;

                if (file.MediaInfoModel.DoviConfigurationRecord?.DvProfile == 5)
                {
                    LogWithPushover(BackupAction.Error, $"{path} is [DV] Profile 5");
                    return;
                }

                if (File.Exists(newFullPath))
                    LogWithPushover(BackupAction.Error, $"Renaming {path} failed as {newFullPath} already exists");
                else
                {
                    if (Path.GetDirectoryName(path) == Path.GetDirectoryName(newFullPath))
                    {
                        LogWithPushover(BackupAction.General, $"Renaming {path} to {newFullPath}");
                        _ = File.Move(path, newFullPath);
                        Trace($"Renamed {path} to {newFullPath}");
                        path = newFullPath;
                    }
                    else
                        LogWithPushover(BackupAction.General, PushoverPriority.High, $"Renaming {path} to {newFullPath} and directories do not match anymore");
                }
            }
            else
                LogWithPushover(BackupAction.Error, $"Refreshing media info for {path} failed");
        }

        internal static decimal FormatAudioChannels(MediaInfoModel mediaInfo)
        {
            var audioChannels = FormatAudioChannelsFromAudioChannelPositions(mediaInfo);
            if (audioChannels is null or 0.0m) audioChannels = mediaInfo.AudioChannels;
            return audioChannels.Value;
        }

        internal static MediaInfoModel GetMediaInfoModel(string path)
        {
            TraceIn(path);
            ArgumentException.ThrowIfNullOrEmpty(path);
            if (!File.Exists(path)) throw new FileNotFoundException(Resources.FileNotFound, path);

            ArgumentException.ThrowIfNullOrEmpty(path);
            if (!File.IsVideo(path)) throw new NotSupportedException("file is not video");

            var info = new VideoFileInfoReader().GetMediaInfo(path) ?? throw new IOException(string.Format(Resources.UnableToLoadFFProbe, path));
            return TraceOut(info);
        }

        /// <summary>
        ///     Returns a MovieBackupFile, a TvEpisodeFile, or a SubtitlesBackupFile or null
        /// </summary>
        /// <param name="path">The full path to the file</param>
        /// <returns>Null if the file isn't a movie, TV episode or subtitles file.</returns>
        internal static ExtendedBackupFileBase ExtendedBackupFileBase(string path)
        {
            // ReSharper disable once StringLiteralTypo
            if (path.Contains("TdarrCacheFile-")) return null;
            if (path.Contains(@"\_TV")) return path.EndsWithIgnoreCase(".srt") ? new SubtitlesBackupFile(path) : new TvEpisodeBackupFile(path);
            if (path.Contains(@"\_Movies") || path.Contains(@"\_Concerts") || path.Contains(@"\_Comedy")) return path.EndsWithIgnoreCase(".srt") ? new SubtitlesBackupFile(path) : new MovieBackupFile(path);

            return null;
        }

        private static decimal? FormatAudioChannelsFromAudioChannelPositions(MediaInfoModel mediaInfo)
        {
            if (mediaInfo.AudioChannelPositions == null) return 0;

            var match = _positionRegex.Match(mediaInfo.AudioChannelPositions);
            return match.Success ? decimal.Parse(match.Groups["position"].Value, NumberStyles.Number, CultureInfo.InvariantCulture) : 0;
        }
    }
}