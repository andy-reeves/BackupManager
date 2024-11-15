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
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;

using BackupManager.Extensions;

using FFMpegCore;
using FFMpegCore.Exceptions;

// ReSharper disable once IdentifierTypo
namespace BackupManager.Radarr;

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
    internal bool RemoveSubtitlesFromFile(string inputFilename, string outputFilename)
    {
        try
        {
            _ = FFMpegArguments.FromFileInput(inputFilename).OutputToFile(outputFilename, false, options => { _ = options.WithCustomArgument("-c copy -map 0 -sn"); }).ProcessSynchronously();
        }
        catch (Exception)
        {
            return false;
        }
        return true;
    }

    [SuppressMessage("ReSharper", "IdentifierTypo")]
    [SuppressMessage("ReSharper", "StringLiteralTypo")]
    [SuppressMessage("ReSharper", "ConstantConditionalAccessQualifier")]
    internal bool RemoveChaptersFromFile(string inputFilename, string outputFilename)
    {
        try
        {
            var subs = string.Empty;
            if (SubtitlesStreamCount(inputFilename) > 0) subs = "-map 0:s";
            _ = FFMpegArguments.FromFileInput(inputFilename).OutputToFile(outputFilename, false, options => { _ = options.WithCustomArgument($"-map 0:a -map 0:v {subs} -map_metadata -1 -map_chapters -1 -c copy"); }).ProcessSynchronously();
        }
        catch (Exception)
        {
            return false;
        }
        return true;
    }

    [SuppressMessage("ReSharper", "IdentifierTypo")]
    [SuppressMessage("ReSharper", "StringLiteralTypo")]
    [SuppressMessage("ReSharper", "ConstantConditionalAccessQualifier")]
    internal bool RemoveMetadataFromFile(string inputFilename, string outputFilename)
    {
        if (!HasMetadata(inputFilename)) return true;

        try
        {
            var subs = string.Empty;
            if (SubtitlesStreamCount(inputFilename) > 0) subs = "-map 0:s";
            _ = FFMpegArguments.FromFileInput(inputFilename).OutputToFile(outputFilename, false, options => { _ = options.WithCustomArgument($"-map 0:a -map 0:v {subs} -map_metadata -1 -c copy"); }).ProcessSynchronously();
        }
        catch (Exception)
        {
            return false;
        }
        return true;
    }

    [SuppressMessage("ReSharper", "IdentifierTypo")]
    [SuppressMessage("ReSharper", "StringLiteralTypo")]
    [SuppressMessage("ReSharper", "ConstantConditionalAccessQualifier")]
    public bool ExtractChapters(string inputFilename, string outputFilename)
    {
        try
        {
            _ = FFMpegArguments.FromFileInput(inputFilename).OutputToFile(outputFilename, false, options => { _ = options.WithCustomArgument("-f ffmetadata"); }).ProcessSynchronously();
        }
        catch (Exception)
        {
            return false;
        }
        return true;
    }

    [SuppressMessage("ReSharper", "IdentifierTypo")]
    [SuppressMessage("ReSharper", "StringLiteralTypo")]
    [SuppressMessage("ReSharper", "ConstantConditionalAccessQualifier")]
    public bool HasChapters(string filename)
    {
        try
        {
            var ffprobeOutput = FFProbe.GetStreamJson(filename, ffOptions: new FFOptions { ExtraArguments = "-show_chapters" });
            return !ffprobeOutput.Contains("\"chapters\": [    ],") && ffprobeOutput.Contains("\"chapters\": [");
        }
        catch (Exception)
        {
            return false;
        }
    }

    [SuppressMessage("ReSharper", "IdentifierTypo")]
    [SuppressMessage("ReSharper", "StringLiteralTypo")]
    [SuppressMessage("ReSharper", "ConstantConditionalAccessQualifier")]
    public bool HasMetadata(string filename)
    {
        try
        {
            // check for   "nb_streams": 4,  then minus the video, audio, subtitles and chapters
            var ffprobeOutput = FFProbe.GetStreamJson(filename, ffOptions: new FFOptions { ExtraArguments = "-show_streams" });
            var node = JsonNode.Parse(ffprobeOutput);

            // var audio = node.Root["streams"].AsArray(); //["?codec_type=='data']"]; //;["?codec_type == 'audio'"]; // streams[?codec_type=='data'] ; //Phone[type='mobile'] [codec_type='audio']
            if (node != null)
            {
                var streams = node["streams"]?.AsArray(); //["?codec_type=='data']"]; //;["?codec_type == 'audio'"]; // streams[?codec_type=='data'] ; //Phone[type='mobile'] [codec_type='audio']

                if (streams != null)
                {
                    foreach (var streamNode in streams)
                    {
                        if (streamNode["codec_type"]?.ToString() == "data") return true;
                    }
                }
            }
            return false;
        }
        catch (Exception)
        {
            return false;
        }
    }

    [SuppressMessage("ReSharper", "IdentifierTypo")]
    [SuppressMessage("ReSharper", "StringLiteralTypo")]
    [SuppressMessage("ReSharper", "ConstantConditionalAccessQualifier")]
    public int ChaptersStreamCount(string filename)
    {
        try
        {
            return HasChapters(filename) ? 1 : 0;
        }
        catch (Exception)
        {
            return -1;
        }
    }

    [SuppressMessage("ReSharper", "IdentifierTypo")]
    [SuppressMessage("ReSharper", "StringLiteralTypo")]
    [SuppressMessage("ReSharper", "ConstantConditionalAccessQualifier")]
    public int SubtitlesStreamCount(string filename)
    {
        try
        {
            var ffprobeOutput = FFProbe.GetStreamJson(filename, ffOptions: new FFOptions { ExtraArguments = "-probesize 50000000" });
            var analysis = FFProbe.AnalyseStreamJson(ffprobeOutput);

            if (analysis.PrimaryAudioStream?.ChannelLayout.IsNullOrWhiteSpace() ?? true)
            {
                ffprobeOutput = FFProbe.GetStreamJson(filename, ffOptions: new FFOptions { ExtraArguments = "-probesize 150000000 -analyzeduration 150000000" });
                analysis = FFProbe.AnalyseStreamJson(ffprobeOutput);
            }
            Utils.Log($"Checking {filename} for subtitles");
            return analysis.SubtitleStreams.Count;
        }
        catch (Exception)
        {
            return -1;
        }
    }

    [SuppressMessage("ReSharper", "IdentifierTypo")]
    [SuppressMessage("ReSharper", "StringLiteralTypo")]
    [SuppressMessage("ReSharper", "ConstantConditionalAccessQualifier")]
    public int AudioStreamCount(string filename)
    {
        try
        {
            var ffprobeOutput = FFProbe.GetStreamJson(filename, ffOptions: new FFOptions { ExtraArguments = "-probesize 50000000" });
            var analysis = FFProbe.AnalyseStreamJson(ffprobeOutput);

            if (analysis.PrimaryAudioStream?.ChannelLayout.IsNullOrWhiteSpace() ?? true)
            {
                ffprobeOutput = FFProbe.GetStreamJson(filename, ffOptions: new FFOptions { ExtraArguments = "-probesize 150000000 -analyzeduration 150000000" });
                analysis = FFProbe.AnalyseStreamJson(ffprobeOutput);
            }
            return analysis.AudioStreams.Count;
        }
        catch (Exception)
        {
            return -1;
        }
    }

    [SuppressMessage("ReSharper", "IdentifierTypo")]
    [SuppressMessage("ReSharper", "StringLiteralTypo")]
    [SuppressMessage("ReSharper", "ConstantConditionalAccessQualifier")]
    public int VideoStreamCount(string filename)
    {
        try
        {
            var ffprobeOutput = FFProbe.GetStreamJson(filename, ffOptions: new FFOptions { ExtraArguments = "-probesize 50000000" });
            var analysis = FFProbe.AnalyseStreamJson(ffprobeOutput);

            if (analysis.PrimaryAudioStream?.ChannelLayout.IsNullOrWhiteSpace() ?? true)
            {
                ffprobeOutput = FFProbe.GetStreamJson(filename, ffOptions: new FFOptions { ExtraArguments = "-probesize 150000000 -analyzeduration 150000000" });
                analysis = FFProbe.AnalyseStreamJson(ffprobeOutput);
            }
            return analysis.VideoStreams.Count;
        }
        catch (Exception)
        {
            return -1;
        }
    }

    [SuppressMessage("ReSharper", "IdentifierTypo")]
    [SuppressMessage("ReSharper", "StringLiteralTypo")]
    [SuppressMessage("ReSharper", "ConstantConditionalAccessQualifier")]
    public bool AddChaptersToFile(string inputFilename, string chaptersFilename, string outputFilename)
    {
        try
        {
            _ = FFMpegArguments.FromFileInput(inputFilename).OutputToFile(outputFilename, false, options => { _ = options.WithCustomArgument($" -f ffmetadata -i {chaptersFilename} -map_metadata 1 -map_chapters 1 -c copy"); }).ProcessSynchronously();
        }
        catch (Exception)
        {
            return false;
        }
        return true;
    }

    [SuppressMessage("ReSharper", "IdentifierTypo")]
    [SuppressMessage("ReSharper", "StringLiteralTypo")]
    [SuppressMessage("ReSharper", "ConstantConditionalAccessQualifier")]
    public bool ExtractSubtitleFiles(string filename)
    {
        var englishDone = false;

        try
        {
            var ffprobeOutput = FFProbe.GetStreamJson(filename, ffOptions: new FFOptions { ExtraArguments = "-probesize 50000000" });
            var analysis = FFProbe.AnalyseStreamJson(ffprobeOutput);

            if (analysis.PrimaryAudioStream?.ChannelLayout.IsNullOrWhiteSpace() ?? true)
            {
                ffprobeOutput = FFProbe.GetStreamJson(filename, ffOptions: new FFOptions { ExtraArguments = "-probesize 150000000 -analyzeduration 150000000" });
                analysis = FFProbe.AnalyseStreamJson(ffprobeOutput);
            }
            Utils.Log($"Checking {filename} for subtitles to extract");

            for (var i = 0; i < analysis.SubtitleStreams.Count; i++)
            {
                var subStream = analysis.SubtitleStreams[i];
                if (subStream.CodecName.HasNoValue() || (subStream.CodecName.HasValue() && subStream.CodecName.ContainsIgnoreCase("_pgs_"))) continue;

                if (subStream.Tags != null && subStream.Tags.TryGetValue("title", out var value))
                    if (value.ContainsIgnoreCase("webvtt"))
                        continue;

                var hearingImpaired = false;
                var forced = false;
                Utils.Log($"Language is {subStream.Language}");

                var subFileName = subStream.Language switch
                {
                    "eng" or "und" => ".en",
                    "esp" or "spa" => ".es",
                    _ => "."
                };
                if (subFileName == ".") continue;

                ProcessSubtitleTags(subStream, ref hearingImpaired, ref forced);
                if (subStream.Language.ContainsIgnoreCase("forced")) forced = true;
                if (hearingImpaired) subFileName += ".hi";
                if (forced) subFileName += ".forced";
                subFileName += ".srt";
                var newFullPath = Path.GetDirectoryName(filename) + @"\" + Path.GetFileNameWithoutExtension(filename) + subFileName;
                if (File.Exists(newFullPath)) newFullPath = newFullPath.Replace(".srt", $".{i}.srt");
                var index = i;
                _ = FFMpegArguments.FromFileInput(filename).OutputToFile(newFullPath, false, options => { _ = options.WithCustomArgument($"-map 0:s:{index}"); }).ProcessSynchronously();
                if (subFileName.StartsWithIgnoreCase(".en")) englishDone = true;
            }
        }
        catch (FFMpegException ex)
        {
            Utils.Log($"{ex}");
            return englishDone;
        }
        return true;
    }

    private static void ProcessSubtitleTags(SubtitleStream subStream, ref bool hearingImpaired, ref bool forced)
    {
        if (subStream.Tags != null)
        {
            foreach (var t in subStream.Tags)
            {
                Utils.Log($"{t.Key} = {t.Value}");
            }

            if (subStream.Tags.TryGetValue("handler_name", out var value))
            {
                if (value.Contains("SDH")) hearingImpaired = true;
            }

            if (subStream.Tags.TryGetValue("title", out value))
            {
                if (value.Contains("SDH")) hearingImpaired = true;
                if (value.Contains("forced")) forced = true;
            }

            if (subStream.Tags.TryGetValue("forced", out var value2))
            {
                if (value2.Contains("true")) forced = true;
            }
        }
        if (subStream.Disposition == null) return;

        if (subStream.Disposition.TryGetValue("forced", out var value3))
            if (value3)
                forced = true;
        if (!subStream.Disposition.TryGetValue("hearing_impaired", out var value4)) return;

        if (value4) hearingImpaired = true;
    }

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
                ffprobeOutput = FFProbe.GetStreamJson(filename, ffOptions: new FFOptions { ExtraArguments = "-probesize 150000000 -analyzeduration 150000000" });
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
                VideoMultiViewCount = primaryVideoStream?.Tags?.ContainsKey("stereo_mode") ?? false ? 2 : 1,

                // ReSharper disable once ConstantConditionalAccessQualifier
                DoviConfigurationRecord = primaryVideoStream?.SideDataList?.Find(static x => x.GetType().Name == nameof(DoviConfigurationRecordSideData)) as DoviConfigurationRecordSideData,
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

                // ReSharper disable once ConstantConditionalAccessQualifier
                AudioLanguages = analysis.AudioStreams?.Select(static x => x.Language).Where(static l => l.IsNotNullOrWhiteSpace()).ToList(),

                // ReSharper disable once ConstantConditionalAccessQualifier
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
                // Andy get more than 1 frame side data not just the first one
                //var frameOutput = FFProbe.GetFrameJson(filename,
                //    ffOptions: new FFOptions { ExtraArguments = $"-read_intervals \"%+#5\" -select_streams v:{primaryVideoStream?.Index ?? 0}" });
                // The {primaryVideoStream?.Index ?? 0} above does not work for all movies 
                var frameOutput = FFProbe.GetFrameJson(filename, ffOptions: new FFOptions { ExtraArguments = "-read_intervals \"%+#5\" -select_streams v" });
                mediaInfoModel.RawFrameData = frameOutput;
                frames = FFProbe.AnalyseFrameJson(frameOutput);
            }
            var streamSideData = primaryVideoStream?.SideDataList ?? new List<SideData>();

            // Andy - check all the frames we retrieved for SideData
            /* var framesSideData = frames?.Frames?.Count > 0

                 // ReSharper disable once ConstantNullCoalescingCondition
                 ? frames?.Frames[0]?.SideDataList ?? new List<SideData>()
                 : new List<SideData>();*/
            List<SideData> framesSideData = new();

            // ReSharper disable once ConstantConditionalAccessQualifier
            if (frames?.Frames?.Count > 0)
            {
                for (var i = 0; i < frames.Frames.Count; i++)
                {
                    // ReSharper disable once ConstantConditionalAccessQualifier
                    var f = frames?.Frames[i];
                    if (f.SideDataList is { Count: > 0 }) framesSideData.AddRange(f.SideDataList);
                }
            }
            var sideData = streamSideData.Concat(framesSideData).ToList();
            mediaInfoModel.VideoHdrFormat = GetHdrFormat(mediaInfoModel.VideoBitDepth, mediaInfoModel.VideoColourPrimaries, mediaInfoModel.VideoTransferCharacteristics, sideData);
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
        if (TryGetSideData<MasteringDisplayMetadata>(sideData, out _) || TryGetSideData<ContentLightLevelMetadata>(sideData, out _)) return HdrFormat.Hdr10;

        return HdrFormat.Pq10;
    }

    private static bool TryGetSideData<T>(IEnumerable<SideData> list, out T result) where T : SideData
    {
        result = (T)list?.FirstOrDefault(static x => x.GetType().Name == typeof(T).Name);
        return result != null;
    }
}