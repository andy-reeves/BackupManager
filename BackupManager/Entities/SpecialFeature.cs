using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;

namespace BackupManager.Entities;

[SuppressMessage("ReSharper", "UnusedMember.Global")]
internal enum SpecialFeature
{
    [EnumMember(Value = "")] None = 0,

    [EnumMember(Value = "featurette")] Featurette,

    [EnumMember(Value = "other")] Other,

    [EnumMember(Value = "interview")] Interview,

    [EnumMember(Value = "scene")] Scene,

    [EnumMember(Value = "short")] Short,

    [EnumMember(Value = "deleted")] Deleted,

    // ReSharper disable once StringLiteralTypo
    [EnumMember(Value = "behindthescenes")]
    BehindTheScenes,

    [EnumMember(Value = "trailer")] Trailer
}
