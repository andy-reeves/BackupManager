﻿// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="MediaHelper.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

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
        ///     Returns True if the path contains [DV (without the closing ']')
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>

        // ReSharper disable once UnusedMember.Global
        internal static bool VideoFileIsDolbyVision(string path)
        {
            ArgumentException.ThrowIfNullOrEmpty(path);
            return path.HasValue() && path.ContainsIgnoreCase("[DV");
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
        internal static string FormatAudioCodec(MediaInfoModel mediaInfo)
        {
            if (mediaInfo?.AudioFormat == null)
            {
                LogWithPushover(BackupAction.General, PushoverPriority.High, $"About to return null for {mediaInfo?.Title}");
                return null;
            }
            var audioFormat = mediaInfo.AudioFormat.Trim();
            var audioCodecId = mediaInfo.AudioCodecId ?? string.Empty;
            var audioProfile = mediaInfo.AudioProfile ?? string.Empty;

            if (audioFormat.Empty())
            {
                LogWithPushover(BackupAction.General, PushoverPriority.High, $"About to return string.Empty for {mediaInfo.Title}");
                return string.Empty;
            }
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
                    LogWithPushover(BackupAction.General, PushoverPriority.High,
                        $"{mediaInfo.Title}. Unknown video format: '{audioFormat}'. Streams: {mediaInfo.RawStreamData}");
                    return mediaInfo.AudioFormat;
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="mediaInfo"></param>
        /// <returns>
        ///     The following values are possibly returned: null, string.Empty, h264, h265, XviD, DivX, MPEG4, VP6, MPEG2,
        ///     MPEG, VC1, AV1, VP7, VP8, VP9, WMV, RGB
        /// </returns>
        [SuppressMessage("ReSharper", "StringLiteralTypo")]
        internal static string FormatVideoCodec(MediaInfoModel mediaInfo)
        {
            if (mediaInfo?.VideoFormat == null)
            {
                LogWithPushover(BackupAction.General, PushoverPriority.High, $"About to return null for {mediaInfo?.Title}");
                return null;
            }
            var videoFormat = mediaInfo.VideoFormat.Trim();
            var videoCodecId = mediaInfo.VideoCodecId ?? string.Empty;

            if (videoFormat.Empty())
            {
                LogWithPushover(BackupAction.General, PushoverPriority.High, $"About to return string.Empty for {mediaInfo.Title}");
                return videoFormat;
            }
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
                    LogWithPushover(BackupAction.General, PushoverPriority.High, $"About to return string.Empty for {mediaInfo.Title}");
                    return string.Empty;
            }
            LogWithPushover(BackupAction.General, PushoverPriority.High, $"{mediaInfo.Title}. Unknown video format: '{videoFormat}'. Streams: {mediaInfo.RawStreamData}");
            return videoFormat;
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
        ///     Checks if the path is a video file or subtitles file and renames the file with extracted media info if required
        /// </summary>
        /// <param name="path"></param>
        /// <returns>
        ///     False if the rename was required but failed or if the refresh of the mediaInfo failed (maybe file locked)
        ///     otherwise True
        /// </returns>
        internal static bool CheckVideoFileAndRenameIfRequired(ref string path)
        {
            var file = ExtendedBackupFileBase(path);
            if (file == null) return true;

            if (!file.IsValidFileName)
            {
                LogWithPushover(BackupAction.Error, $"{path} does not have a valid file name. Name not checked");
                return true;
            }

            if (!file.RefreshMediaInfo())
            {
                LogWithPushover(BackupAction.General, $"Refreshing media info for {path} failed");
                return false;
            }
            var newFullPath = file.GetFullName();

            if (file is SubtitlesBackupFile backupFile)
            {
                if (backupFile.FullPathToVideoFile.HasNoValue()) LogWithPushover(BackupAction.Error, $"{path} is Subtitles file with no TV episode or Movie named");

                if (newFullPath.ContainsIgnoreCase("-tdarrcachefile-"))
                {
                    LogWithPushover(BackupAction.ScanDirectory, $"{newFullPath} is Subtitles file with '-tdarrCacheFile-' in the path so not renaming");
                    return true;
                }

                if (newFullPath.ContainsIgnoreCase("[]"))
                {
                    LogWithPushover(BackupAction.ScanDirectory, $"{newFullPath} is Subtitles file with '[]' in the path so not renaming");
                    return true;
                }
            }
            else
            {
                if (file.MediaInfoModel?.DoviConfigurationRecord?.DvProfile == 5)
                {
                    LogWithPushover(BackupAction.Error, $"{path} is [DV] Profile 5");
                    return true;
                }
            }

            // Path is the same so do not rename
            if (newFullPath == path) return true;

            if (File.Exists(newFullPath))
            {
                LogWithPushover(BackupAction.Error, $"Renaming {path} failed as {newFullPath} already exists");
                return false;
            }

            if (Path.GetDirectoryName(path) != Path.GetDirectoryName(newFullPath))
            {
                LogWithPushover(BackupAction.Error, PushoverPriority.High, $"Renaming {path} to {newFullPath} and directories do not match anymore so not renaming");
                return true;
            }
            LogWithPushover(BackupAction.General, $"Renaming {path} to {newFullPath}");
            _ = File.Move(path, newFullPath);
            path = newFullPath;
            return true;
        }

        /// <summary>
        ///     Returns the TmdbId for the movie from the path provided
        /// </summary>
        /// <param name="path">The path to the movie file</param>
        /// <param name="edition"></param>
        /// <returns>-1 if the file is not found or has no TmdbId or is a Special Feature otherwise returns the TmdbId</returns>
        internal static int GetTmdbId(string path, out string edition)
        {
            var file = ExtendedBackupFileBase(path);
            edition = string.Empty;
            if (file is not MovieBackupFile movieFile) return -1;
            if (movieFile.SpecialFeature != SpecialFeature.None) return -1;
            if (movieFile.TmdbId.HasNoValue()) return -1;

            if (movieFile.Edition != Edition.Unknown) edition = movieFile.Edition.ToString();
            return Convert.ToInt32(movieFile.TmdbId);
        }

        /// <summary>
        ///     Returns the TvdbId for the TV series from the path provided
        /// </summary>
        /// <param name="path">The path to the TV episode file</param>
        /// <param name="edition"></param>
        /// <param name="seasonNumber"></param>
        /// <param name="episodeNumber"></param>
        /// <returns>
        ///     -1 if the file is not found or has no TvdbId or is a Special Feature otherwise returns the TvdbId. If  the
        ///     episode is a range of episodes like e01-e03 then we return -1
        /// </returns>
        internal static int GetTvdbInfo(string path, out string edition, out int seasonNumber, out int episodeNumber)
        {
            edition = string.Empty;
            seasonNumber = -1;
            episodeNumber = -1;
            var file = ExtendedBackupFileBase(path);
            if (file is not TvEpisodeBackupFile tvEpisodeBackupFile) return -1;
            if (tvEpisodeBackupFile.SpecialFeature != SpecialFeature.None) return -1;
            if (tvEpisodeBackupFile.TvdbId.HasNoValue() || !tvEpisodeBackupFile.IsValidFileName) return -1;

            edition = tvEpisodeBackupFile.Edition;
            seasonNumber = Convert.ToInt32(tvEpisodeBackupFile.Season);
            var result = int.TryParse(tvEpisodeBackupFile.Episode[1..], out var parsedResult);

            if (result)
                episodeNumber = parsedResult;
            else
                return -1;

            return Convert.ToInt32(tvEpisodeBackupFile.TvdbId);
        }

        internal static int GetMovieRuntimeFromTmdbApi(int tmdbId)
        {
            try
            {
                var httpsApiThemoviedbOrgMovieLanguageEnUs = $"https://api.themoviedb.org/3/movie/{tmdbId}?language=en-US";
                HttpClient client = new();
                client.DefaultRequestHeaders.Add("accept", "application/json");
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {Config.TmdbApiReadAccessToken}");
                var task = Task.Run(() => client.GetStringAsync(httpsApiThemoviedbOrgMovieLanguageEnUs));
                task.Wait();
                var response = task.Result;
                var node = JsonNode.Parse(response);
                var runtime = node?["runtime"]?.ToString();
                return Convert.ToInt32(runtime);
            }
            catch (AggregateException)
            {
                return -1;
            }
        }

        internal static decimal FormatAudioChannels(MediaInfoModel mediaInfo)
        {
            var audioChannels = FormatAudioChannelsFromAudioChannelPositions(mediaInfo);
            if (audioChannels is null or 0.0m) audioChannels = mediaInfo.AudioChannels;
            return audioChannels.Value;
        }

        internal static bool ExtractSubtitleFiles(string path)
        {
            TraceIn(path);
            ArgumentException.ThrowIfNullOrEmpty(path);
            if (!File.Exists(path)) throw new FileNotFoundException(Resources.FileNotFound, path);
            if (!File.IsVideo(path)) throw new NotSupportedException("file is not video");

            var result = VideoFileInfoReader.ExtractSubtitleFiles(path);
            if (!result) throw new IOException($"Unable to extract subtitles for {path}");

            return TraceOut(true);
        }

        internal static int AudioStreamCount(string path)
        {
            TraceIn(path);
            ArgumentException.ThrowIfNullOrEmpty(path);
            if (!File.Exists(path)) throw new FileNotFoundException(Resources.FileNotFound, path);
            if (!File.IsVideo(path)) throw new NotSupportedException("file is not video");

            return VideoFileInfoReader.AudioStreamCount(path);
        }

        internal static int VideoStreamCount(string path)
        {
            TraceIn(path);
            ArgumentException.ThrowIfNullOrEmpty(path);
            if (!File.Exists(path)) throw new FileNotFoundException(Resources.FileNotFound, path);
            if (!File.IsVideo(path)) throw new NotSupportedException("file is not video");

            return VideoFileInfoReader.VideoStreamCount(path);
        }

        internal static int SubtitlesStreamCount(string path)
        {
            TraceIn(path);
            ArgumentException.ThrowIfNullOrEmpty(path);
            if (!File.Exists(path)) throw new FileNotFoundException(Resources.FileNotFound, path);
            if (!File.IsVideo(path)) throw new NotSupportedException("file is not video");

            return VideoFileInfoReader.SubtitlesStreamCount(path);
        }

        internal static int ChaptersStreamCount(string path)
        {
            TraceIn(path);
            ArgumentException.ThrowIfNullOrEmpty(path);
            if (!File.Exists(path)) throw new FileNotFoundException(Resources.FileNotFound, path);
            if (!File.IsVideo(path)) throw new NotSupportedException("file is not video");

            return VideoFileInfoReader.ChaptersStreamCount(path);
        }

        internal static bool HasMetadata(string path)
        {
            TraceIn(path);
            ArgumentException.ThrowIfNullOrEmpty(path);
            if (!File.Exists(path)) throw new FileNotFoundException(Resources.FileNotFound, path);
            if (!File.IsVideo(path)) throw new NotSupportedException("file is not video");

            return VideoFileInfoReader.HasMetadata(path);
        }

        internal static bool HasChapters(string path)
        {
            TraceIn(path);
            ArgumentException.ThrowIfNullOrEmpty(path);
            if (!File.Exists(path)) throw new FileNotFoundException(Resources.FileNotFound, path);
            if (!File.IsVideo(path)) throw new NotSupportedException("file is not video");

            return VideoFileInfoReader.HasChapters(path);
        }

        internal static bool HasSubtitles(string path)
        {
            TraceIn(path);
            ArgumentException.ThrowIfNullOrEmpty(path);
            if (!File.Exists(path)) throw new FileNotFoundException(Resources.FileNotFound, path);
            if (!File.IsVideo(path)) throw new NotSupportedException("file is not video");

            return VideoFileInfoReader.SubtitlesStreamCount(path) > 0;
        }

        internal static bool ExtractChapters(string path, string outputFilename)
        {
            TraceIn(path);
            ArgumentException.ThrowIfNullOrEmpty(path);
            if (!File.Exists(path)) throw new FileNotFoundException(Resources.FileNotFound, path);
            if (!File.IsVideo(path)) throw new NotSupportedException("file is not video");

            var result = VideoFileInfoReader.ExtractChapters(path, outputFilename);
            if (!result) throw new IOException($"Unable to extract chapters for {path}");

            return TraceOut(true);
        }

        internal static bool AddChaptersToFile(string path, string chaptersFilename, string outputFilename)
        {
            TraceIn(path);
            ArgumentException.ThrowIfNullOrEmpty(path);
            if (!File.Exists(path)) throw new FileNotFoundException(Resources.FileNotFound, path);
            if (!File.IsVideo(path)) throw new NotSupportedException("file is not video");

            var result = VideoFileInfoReader.AddChaptersToFile(path, chaptersFilename, outputFilename);
            if (!result) throw new IOException($"Unable to add chapters {chaptersFilename} to {path}");

            return TraceOut(true);
        }

        internal static bool RemoveSubtitlesFromFile(string inputFilename, string outputFilename)
        {
            TraceIn(inputFilename);
            ArgumentException.ThrowIfNullOrEmpty(inputFilename);
            if (!File.Exists(inputFilename)) throw new FileNotFoundException(Resources.FileNotFound, inputFilename);
            if (!File.IsVideo(inputFilename)) throw new NotSupportedException("file is not video");
            if (File.Exists(outputFilename)) throw new ArgumentException(string.Format(Resources.FileExists, outputFilename), outputFilename);

            var result = VideoFileInfoReader.RemoveSubtitlesFromFile(inputFilename, outputFilename);
            if (result) return TraceOut(true);

            Log($"Unable to remove subtitles for {inputFilename}");
            return false;
        }

        internal static bool RemoveChaptersFromFile(string inputFilename, string outputFilename)
        {
            TraceIn(inputFilename);
            ArgumentException.ThrowIfNullOrEmpty(inputFilename);
            if (!File.Exists(inputFilename)) throw new FileNotFoundException(Resources.FileNotFound, inputFilename);
            if (!File.IsVideo(inputFilename)) throw new NotSupportedException("file is not video");
            if (File.Exists(outputFilename)) throw new ArgumentException(string.Format(Resources.FileExists, outputFilename), outputFilename);

            var result = VideoFileInfoReader.RemoveChaptersFromFile(inputFilename, outputFilename);
            if (!result) throw new IOException($"Unable to remove chapters for {inputFilename}");

            return TraceOut(true);
        }

        internal static bool RemoveMetadataFromFile(string inputFilename, string outputFilename)
        {
            TraceIn(inputFilename);
            ArgumentException.ThrowIfNullOrEmpty(inputFilename);
            if (!File.Exists(inputFilename)) throw new FileNotFoundException(Resources.FileNotFound, inputFilename);
            if (!File.IsVideo(inputFilename)) throw new NotSupportedException("file is not video");
            if (File.Exists(outputFilename)) throw new ArgumentException(string.Format(Resources.FileExists, outputFilename), outputFilename);

            var result = VideoFileInfoReader.RemoveMetadataFromFile(inputFilename, outputFilename);
            if (!result) throw new IOException($"Unable to remove metadata for {inputFilename}");

            return TraceOut(true);
        }

        internal static MediaInfoModel GetMediaInfoModel(string path)
        {
            TraceIn(path);
            ArgumentException.ThrowIfNullOrEmpty(path);
            if (!File.Exists(path)) throw new FileNotFoundException(Resources.FileNotFound, path);
            if (!File.IsVideo(path)) throw new NotSupportedException("file is not video");

            var info = new VideoFileInfoReader().GetMediaInfo(path) ?? throw new IOException(string.Format(Resources.UnableToLoadFFProbe, path));
            return TraceOut(info);
        }

        /// <summary>
        ///     Returns a MovieBackupFile, a TvEpisodeFile, or a SubtitlesBackupFile or null. Does not check the File exists.
        /// </summary>
        /// <param name="path">The full path to the file</param>
        /// <returns>Null if the file isn't a movie, TV episode or subtitles file.</returns>
        internal static ExtendedBackupFileBase ExtendedBackupFileBase(string path)
        {
            ArgumentException.ThrowIfNullOrEmpty(path, nameof(path));
            if (File.IsSubtitles(path)) return new SubtitlesBackupFile(path);
            if (File.IsMovieComedyOrConcert(path)) return new MovieBackupFile(path);

            return File.IsTv(path) ? new TvEpisodeBackupFile(path) : null;
        }

        private static decimal? FormatAudioChannelsFromAudioChannelPositions(MediaInfoModel mediaInfo)
        {
            if (mediaInfo.AudioChannelPositions == null) return 0;

            var match = _positionRegex.Match(mediaInfo.AudioChannelPositions);
            return match.Success ? decimal.Parse(match.Groups["position"].Value, NumberStyles.Number, CultureInfo.InvariantCulture) : 0;
        }

        [SuppressMessage("ReSharper", "StringLiteralTypo")]
        internal static void CheckRuntimeForMovieOrTvEpisode(string path, int runtimeFromCache, int minimum, int maximum, bool logToPushover = false)
        {
            if (!File.IsVideo(path)) return;

            var file = ExtendedBackupFileBase(path);
            if (file is not VideoBackupFileBase videoFile) return;
            if (videoFile.SpecialFeature != SpecialFeature.None) return;

            if (!videoFile.RefreshMediaInfo())
            {
                LogWithPushover(BackupAction.ProcessFiles, PushoverPriority.High, $"Unable to refresh the MediaInfo for {path}");
                return;
            }
            var fileRuntime = videoFile.MediaInfoModel.RunTime.TotalMinutes;

            if (runtimeFromCache <= 0)
            {
                Log(BackupAction.ProcessFiles, $"{fileRuntime:N0} mins from file but unable to get the runtime for {path} from cache or Tmdb Api");
                return;
            }
            var percentage = Convert.ToInt32(fileRuntime * 100 / runtimeFromCache);

            if (percentage < minimum || percentage > maximum)
            {
                if (logToPushover)
                {
                    LogWithPushover(BackupAction.ProcessFiles, PushoverPriority.High,
                        $"{percentage:N0}% - File = {fileRuntime:N0} mins, Cache = {runtimeFromCache:N0} mins. Runtime is incorrect for {path}");
                }
                else
                    Log(BackupAction.ProcessFiles, $"{percentage:N0}% - File = {fileRuntime:N0} mins, Cache = {runtimeFromCache:N0} mins. Runtime is incorrect for {path}");
            }
            else
                Trace($"{percentage:N0}% - File = {fileRuntime:N0} mins, Cache = {runtimeFromCache:N0} mins for {path}");
        }

        internal static int GetTvEpisodeRuntimeFromTmdbApi(int tvdbId, int seasonNumber, int episodeNumber)
        {
            try
            {
                var findApi = $"https://api.themoviedb.org/3/find/{tvdbId}?external_source=tvdb_id";
                HttpClient client = new();
                client.DefaultRequestHeaders.Add("accept", "application/json");
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {Config.TmdbApiReadAccessToken}");
                var task = Task.Run(() => client.GetStringAsync(findApi));
                task.Wait();
                var response = task.Result;
                var node = JsonNode.Parse(response);
                var id = node?["tv_results"]?[0]?["id"]?.ToString();
                var url = $"https://api.themoviedb.org/3/tv/{id}/season/{seasonNumber}/episode/{episodeNumber}?language=en-US";
                task = Task.Run(() => client.GetStringAsync(url));
                task.Wait();
                response = task.Result;
                node = JsonNode.Parse(response);
                var runtime = node?["runtime"]?.ToString();
                return Convert.ToInt32(runtime);
            }
            catch (AggregateException)
            {
                return -1;
            }
            catch (ArgumentOutOfRangeException)
            {
                return -1;
            }
        }
    }
}