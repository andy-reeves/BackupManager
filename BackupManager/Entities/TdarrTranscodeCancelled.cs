// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="TdarrTranscodeCancelled.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;

using CsvHelper.Configuration.Attributes;

namespace BackupManager.Entities;

[SuppressMessage("ReSharper", "UnusedMember.Global")]
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
internal class TdarrTranscodeCancelled
{
    [Name("_id")] public string Id { get; set; }

    [Name("file")] public string File { get; set; }

    [Name("DB")] public string Db { get; set; }

    // 
    [Name("footprintId")] public string FootprintId { get; set; }

    [Name("hasClosedCaptions")] public string HasClosedCaptions { get; set; }

    [Name("container")] public string Container { get; set; }

    [Name("scannerReads")] public string ScannerReads { get; set; }

    [Name("ffProbeData")] public string FfProbeData { get; set; }

    [Name("file_size")] public string FileSize { get; set; }

    [Name("video_resolution")] public string VideoResolution { get; set; }

    [Name("fileMedium")] public string FileMedium { get; set; }

    [Name("video_codec_name")] public string VideoCodecName { get; set; }

    [Name("audio_codec_name")] public string AudioCodecName { get; set; }

    [Name("lastPluginDetails")] public string LastPluginDetails { get; set; }

    [Name("createdAt")] public string CreatedAt { get; set; }

    [Name("bit_rate")] public string BitRate { get; set; }

    [Name("duration")] public string Duration { get; set; }

    [Name("statSync")] public string StatSync { get; set; }

    [Name("HealthCheck")] public string HealthCheck { get; set; }

    [Name("TranscodeDecisionMaker")] public string TranscodeDecisionMaker { get; set; }

    [Name("lastHealthCheckDate")] public string LastHealthCheckDate { get; set; }

    [Name("holdUntil")] public string HoldUntil { get; set; }

    [Name("lastTranscodeDate")] public string LastTranscodeDate { get; set; }

    [Name("bumped")] public string Bumped { get; set; }

    [Name("history")] public string History { get; set; }

    [Name("oldSize")] public string OldSize { get; set; }

    [Name("newSize")] public string NewSize { get; set; }

    [Name("newVsOldRatio")] public string NewVsOldRatio { get; set; }

    [Name("videoStreamIndex")] public string VideoStreamIndex { get; set; }

    [Name("lastUpdate")] public string LastUpdate { get; set; }
}