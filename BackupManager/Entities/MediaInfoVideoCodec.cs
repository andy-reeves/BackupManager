﻿using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;

namespace BackupManager.Entities;

[SuppressMessage("ReSharper", "InconsistentNaming")]
internal enum MediaInfoVideoCodec
{
    [EnumMember(Value = "")] Unknown = 0,

    [EnumMember(Value = "DivX")] DivX,

    [EnumMember(Value = "h264")] h264,

    [EnumMember(Value = "XviD")] XviD,

    [EnumMember(Value = "h265")] h265,

    [EnumMember(Value = "MPEG2")] MPEG2,

    [EnumMember(Value = "MPEG4")] MPEG4,

    [EnumMember(Value = "VP9")] VP9,

    [EnumMember(Value = "VC1")] VC1
}