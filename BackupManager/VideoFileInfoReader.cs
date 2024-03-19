// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="VideoFileInfoReader.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

// Notes
// This is from Radarr 5.3.6.8612 at https://github.com/Radarr/Radarr/releases/tag/v5.3.6.8612
// It uses the packages: Servarr.FFMpegCore (4.7.0-26) and Servarr.FFprobe (5.1.4.112)
// With a few fixes and changes
// you also need:
// MediaInfoModel.cs
// These use ffprobe.exe (which needs libcrypto-3-x64.dll, libcurl.dll, libmediainfo.dll and libssl-3-x64.dll
// These were last copied from Radarr on 19.03.24 
// main changes/fixes are: check the first 10 frames to determine [HDR10] instead of [PQ]
//

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

using BackupManager.Extensions;

using FFMpegCore;

namespace BackupManager;

internal sealed class VideoFileInfoReader
{
    private const int CURRENT_MEDIA_INFO_SCHEMA_REVISION = 9;

    private static readonly string[] _validHdrColourPrimaries = { "bt2020" };

    // ReSharper disable once StringLiteralTypo
    private static readonly string[] _hlgTransferFunctions = { "bt2020-10", "arib-std-b67" };

    // ReSharper disable once StringLiteralTypo
    private static readonly string[] _pqTransferFunctions = { "smpte2084" };

    private static readonly string[] _validHdrTransferFunctions = _hlgTransferFunctions.Concat(_pqTransferFunctions).ToArray();

    private readonly List<FFProbePixelFormat> pixelFormats;

    internal VideoFileInfoReader()
    {
        try
        {
            pixelFormats = FFProbe.GetPixelFormats();
        }
        catch
        {
            pixelFormats = new List<FFProbePixelFormat>();
        }
    }

    [SuppressMessage("ReSharper", "IdentifierTypo")]
    [SuppressMessage("ReSharper", "StringLiteralTypo")]
    [SuppressMessage("ReSharper", "ConstantConditionalAccessQualifier")]

    // ReSharper disable once FunctionComplexityOverflow
    public MediaInfoModel GetMediaInfo(string filename)
    {
        try
        {
            var ffprobeOutput = FFProbe.GetStreamJson(filename, ffOptions: new FFOptions { ExtraArguments = "-probesize 50000000" });
            var analysis = FFProbe.AnalyseStreamJson(ffprobeOutput);
            var primaryVideoStream = GetPrimaryVideoStream(analysis);

            if (analysis.PrimaryAudioStream?.ChannelLayout.IsNullOrWhiteSpace() ?? true)
            {
                ffprobeOutput = FFProbe.GetStreamJson(filename,
                    ffOptions: new FFOptions { ExtraArguments = "-probesize 150000000 -analyzeduration 150000000" });
                analysis = FFProbe.AnalyseStreamJson(ffprobeOutput);
            }

            var mediaInfoModel = new MediaInfoModel
            {
                ContainerFormat = analysis.Format.FormatName,
                VideoFormat = primaryVideoStream?.CodecName,
                VideoCodecId = primaryVideoStream?.CodecTagString,
                VideoProfile = primaryVideoStream?.Profile,
                VideoBitrate = primaryVideoStream?.BitRate ?? 0,
                VideoBitDepth = GetPixelFormat(primaryVideoStream?.PixelFormat)?.Components.Min(static x => x.BitDepth) ?? 8,
                VideoColourPrimaries = primaryVideoStream?.ColorPrimaries,
                VideoTransferCharacteristics = primaryVideoStream?.ColorTransfer,
                DoviConfigurationRecord =
                    primaryVideoStream?.SideDataList?.Find(static x => x.GetType().Name == nameof(DoviConfigurationRecordSideData)) as
                        DoviConfigurationRecordSideData,
                Height = primaryVideoStream?.Height ?? 0,
                Width = primaryVideoStream?.Width ?? 0,
                AudioFormat = analysis.PrimaryAudioStream?.CodecName,
                AudioCodecId = analysis.PrimaryAudioStream?.CodecTagString,
                AudioProfile = analysis.PrimaryAudioStream?.Profile,
                AudioBitrate = analysis.PrimaryAudioStream?.BitRate ?? 0,
                RunTime = GetBestRuntime(analysis.PrimaryAudioStream?.Duration, primaryVideoStream?.Duration, analysis.Format.Duration),
                AudioStreamCount = analysis.AudioStreams.Count,
                AudioChannels = analysis.PrimaryAudioStream?.Channels ?? 0,
                AudioChannelPositions = analysis.PrimaryAudioStream?.ChannelLayout,
                VideoFps = primaryVideoStream?.FrameRate ?? 0,
                AudioLanguages = analysis.AudioStreams?.Select(static x => x.Language).Where(static l => l.IsNotNullOrWhiteSpace()).ToList(),
                Subtitles = analysis.SubtitleStreams?.Select(static x => x.Language).Where(static l => l.IsNotNullOrWhiteSpace()).ToList(),
                ScanType = "Progressive",
                RawStreamData = ffprobeOutput,
                SchemaRevision = CURRENT_MEDIA_INFO_SCHEMA_REVISION
            };
            if (analysis.Format.Tags?.TryGetValue("title", out var title) ?? false) mediaInfoModel.Title = title;
            FFProbeFrames frames = null;

            // if it looks like PQ10 or similar HDR, do a frame analysis to figure out which type it is
            if (_pqTransferFunctions.Contains(mediaInfoModel.VideoTransferCharacteristics))
            {
                // var frameOutput = FFProbe.GetFrameJson(filename,
                //    ffOptions: new FFOptions { ExtraArguments = "-read_intervals \"%+#10\" -select_streams v" });

                // Andy get 10 frames side data not just the first one
                var frameOutput = FFProbe.GetFrameJson(filename,
                    ffOptions: new FFOptions { ExtraArguments = $"-read_intervals \"%+#10\" -select_streams v:{primaryVideoStream?.Index ?? 0}" });
                mediaInfoModel.RawFrameData = frameOutput;
                frames = FFProbe.AnalyseFrameJson(frameOutput);
            }
            var streamSideData = primaryVideoStream?.SideDataList ?? new List<SideData>();
            /* var framesSideData = frames?.Frames?.Count > 0

                 // ReSharper disable once ConstantNullCoalescingCondition
                 ? frames?.Frames[0]?.SideDataList ?? new List<SideData>()
                 : new List<SideData>();*/
            List<SideData> framesSideData = new();

            if (frames?.Frames?.Count > 0)
            {
                for (var i = 0; i < frames.Frames.Count; i++)
                {
                    var f = frames?.Frames[i];
                    if (f.SideDataList is { Count: > 0 }) framesSideData.AddRange(f.SideDataList);
                }
            }
            var sideData = streamSideData.Concat(framesSideData).ToList();

            mediaInfoModel.VideoHdrFormat = GetHdrFormat(mediaInfoModel.VideoBitDepth, mediaInfoModel.VideoColourPrimaries,
                mediaInfoModel.VideoTransferCharacteristics, sideData);
            return mediaInfoModel;
        }
        catch (Exception)
        {
            // ignored
        }
        return null;
    }

    private static TimeSpan GetBestRuntime(TimeSpan? audio, TimeSpan? video, TimeSpan general)
    {
        if (video.HasValue && video.Value.TotalMilliseconds != 0) return video.Value;
        if (!audio.HasValue || audio.Value.TotalMilliseconds == 0) return general;

        return audio.Value;
    }

    private static VideoStream GetPrimaryVideoStream(IMediaAnalysis mediaAnalysis)
    {
        if (mediaAnalysis.VideoStreams.Count <= 1) return mediaAnalysis.PrimaryVideoStream;

        // motion image codec streams are often in front of the main video stream
        // ReSharper disable once StringLiteralTypo
        var codecFilter = new[] { "mjpeg", "png" };
        return mediaAnalysis.VideoStreams.FirstOrDefault(s => !codecFilter.Contains(s.CodecName)) ?? mediaAnalysis.PrimaryVideoStream;
    }

    private FFProbePixelFormat GetPixelFormat(string format)
    {
        return pixelFormats.Find(x => x.Name == format);
    }

    private static HdrFormat GetHdrFormat(int bitDepth, string colorPrimaries, string transferFunction, IReadOnlyCollection<SideData> sideData)
    {
        if (bitDepth < 10) return HdrFormat.None;

        // ReSharper disable once IdentifierTypo
        if (TryGetSideData<DoviConfigurationRecordSideData>(sideData, out var dovi))
        {
            var hasHdr10Plus = TryGetSideData<HdrDynamicMetadataSpmte2094>(sideData, out _);

            return dovi.DvBlSignalCompatibilityId switch
            {
                1 => hasHdr10Plus ? HdrFormat.DolbyVisionHdr10Plus : HdrFormat.DolbyVisionHdr10,
                2 => HdrFormat.DolbyVisionSdr,
                4 => HdrFormat.DolbyVisionHlg,
                6 => hasHdr10Plus ? HdrFormat.DolbyVisionHdr10Plus : HdrFormat.DolbyVisionHdr10,
                _ => HdrFormat.DolbyVision
            };
        }
        if (!_validHdrColourPrimaries.Contains(colorPrimaries) || !_validHdrTransferFunctions.Contains(transferFunction)) return HdrFormat.None;
        if (_hlgTransferFunctions.Contains(transferFunction)) return HdrFormat.Hlg10;
        if (!_pqTransferFunctions.Contains(transferFunction)) return HdrFormat.None;
        if (TryGetSideData<HdrDynamicMetadataSpmte2094>(sideData, out _)) return HdrFormat.Hdr10Plus;

        if (TryGetSideData<MasteringDisplayMetadata>(sideData, out _) || TryGetSideData<ContentLightLevelMetadata>(sideData, out _))
            return HdrFormat.Hdr10;

        return HdrFormat.Pq10;
    }

    private static bool TryGetSideData<T>(IEnumerable<SideData> list, out T result) where T : SideData
    {
        result = (T)list?.FirstOrDefault(static x => x.GetType().Name == typeof(T).Name);
        return result != null;
    }
}