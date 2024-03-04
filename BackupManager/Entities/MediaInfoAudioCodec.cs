using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;

namespace BackupManager.Entities;

[SuppressMessage("ReSharper", "UnusedMember.Global")]
[SuppressMessage("ReSharper", "StringLiteralTypo")]
[SuppressMessage("ReSharper", "IdentifierTypo")]
[SuppressMessage("ReSharper", "InconsistentNaming")]
internal enum MediaInfoAudioCodec
{
    [EnumMember(Value = "")] Unknown = 0,

    [EnumMember(Value = "AAC")] AAC,

    [EnumMember(Value = "AC3")] AC3,

    [EnumMember(Value = "AVC")] AVC,

    [EnumMember(Value = "DTS")] DTS,

    [EnumMember(Value = "DTS-ES")] DTS_ES,

    [EnumMember(Value = "DTS HD")] DTS_HD,

    [EnumMember(Value = "DTS-HD HRA")] DTS_HD_HRA,

    [EnumMember(Value = "DTS-HD MA")] DTS_HD_MA,

    [EnumMember(Value = "DTS-X")] DTS_X,

    [EnumMember(Value = "EAC3")] EAC3,

    [EnumMember(Value = "EAC3 Atmos")] EAC3_Atmos,

    [EnumMember(Value = "FLAC")] FLAC,

    [EnumMember(Value = "MP3")] MP3,

    [EnumMember(Value = "Opus")] Opus,

    [EnumMember(Value = "PCM")] PCM,

    [EnumMember(Value = "TrueHD")] TrueHD,

    [EnumMember(Value = "TrueHD Atmos")] TrueHD_Atmos
}
