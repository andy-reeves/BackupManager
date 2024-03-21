// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="MediaInfoAudioChannels.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;

namespace BackupManager.Entities;

[SuppressMessage("ReSharper", "UnusedMember.Global")]
internal enum MediaInfoAudioChannels
{
    [EnumMember(Value = "")] Unknown = 0,

    [EnumMember(Value = "1.0")] OnePointZero,

    [EnumMember(Value = "2.0")] TwoPointZero,

    [EnumMember(Value = "2.1")] TwoPointOne,

    [EnumMember(Value = "3.0")] ThreePointZero,

    [EnumMember(Value = "3.1")] ThreePointOne,

    [EnumMember(Value = "4.0")] FourPointZero,

    [EnumMember(Value = "5.0")] FivePointZero,

    [EnumMember(Value = "5.1")] FivePointOne,

    [EnumMember(Value = "6.0")] SixPointZero,

    [EnumMember(Value = "6.1")] SixPointOne,

    [EnumMember(Value = "7.1")] SevenPointOne,

    [EnumMember(Value = "8.0")] EightPointZero
}