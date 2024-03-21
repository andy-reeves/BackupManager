// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="MediaInfoVideoCodec.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;

namespace BackupManager.Entities;

[SuppressMessage("ReSharper", "InconsistentNaming")]
[SuppressMessage("ReSharper", "UnusedMember.Global")]
internal enum MediaInfoVideoCodec
{
    [EnumMember(Value = "")] Unknown = 0,

    [EnumMember(Value = "AVC")] AVC,

    [EnumMember(Value = "DivX")] DivX,

    [EnumMember(Value = "h264")] h264,

    [EnumMember(Value = "h265")] h265,

    [EnumMember(Value = "HEVC")] HEVC,

    [EnumMember(Value = "MPEG")] MPEG,

    [EnumMember(Value = "MPEG2")] MPEG2,

    [EnumMember(Value = "MPEG4")] MPEG4,

    [EnumMember(Value = "VC1")] VC1,

    [EnumMember(Value = "VP9")] VP9,

    [EnumMember(Value = "x264")] x264,

    [EnumMember(Value = "x265")] x265,

    [EnumMember(Value = "XviD")] XviD
}