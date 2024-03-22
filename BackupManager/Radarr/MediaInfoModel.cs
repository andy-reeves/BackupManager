// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="MediaInfoModel.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

using FFMpegCore;

// ReSharper disable once IdentifierTypo
namespace BackupManager.Radarr;

file interface IEmbeddedDocument { }

internal enum HdrFormat
{
    None,

    Pq10,

    Hdr10,

    Hdr10Plus,

    Hlg10,

    DolbyVision,

    DolbyVisionHdr10,

    DolbyVisionSdr,

    DolbyVisionHlg,

    DolbyVisionHdr10Plus
}

[SuppressMessage("ReSharper", "IdentifierTypo")]
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
[SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global")]
internal sealed class MediaInfoModel : IEmbeddedDocument
{
    public string RawStreamData { get; set; }

    public string RawFrameData { get; set; }

    public int SchemaRevision { get; set; }

    public string ContainerFormat { get; set; }

    public string VideoFormat { get; set; }

    public string VideoCodecId { get; set; }

    public string VideoProfile { get; set; }

    public long VideoBitrate { get; set; }

    public int VideoBitDepth { get; set; }

    public string VideoColourPrimaries { get; set; }

    public string VideoTransferCharacteristics { get; set; }

    public DoviConfigurationRecordSideData DoviConfigurationRecord { get; set; }

    public HdrFormat VideoHdrFormat { get; set; }

    public int Height { get; set; }

    public int Width { get; set; }

    public string AudioFormat { get; set; }

    public string AudioCodecId { get; set; }

    public string AudioProfile { get; set; }

    public long AudioBitrate { get; set; }

    public TimeSpan RunTime { get; set; }

    public int AudioStreamCount { get; set; }

    public int AudioChannels { get; set; }

    public string AudioChannelPositions { get; set; }

    public decimal VideoFps { get; set; }

    public List<string> AudioLanguages { get; set; }

    public List<string> Subtitles { get; set; }

    public string ScanType { get; set; }

    [JsonIgnore] public string Title { get; set; }

    public int VideoMultiViewCount { get; internal set; }
}