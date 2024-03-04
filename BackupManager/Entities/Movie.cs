// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="Movie.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;

using BackupManager.Extensions;

namespace BackupManager.Entities;

internal sealed class Movie
{
    private const string FILE_NAME_PATTERN =
        @"^(?:(.*)\((\d{4})\)(?:-other)?(?:\s{(?:tmdb-(\d{1,7}))?})?\s(?:{edition-((?:[1-7][05]TH\sANNIVERSARY)|4K|BLURAY|CHRONOLOGICAL|COLLECTORS|(?:CRITERION|KL\sSTUDIO)\sCOLLECTION|DIAMOND|DVD|IMAX|REDUX|REMASTERED|RESTORED|SPECIAL|(?:THE\sCOMPLETE\s)?EXTENDED|THE\sGODFATHER\sCODA|(?:THE\sRICHARD\sDONNER|DIRECTORS|FINAL)\sCUT|THEATRICAL|ULTIMATE|UNCUT|UNRATED)}\s)?\[(DVD|SDTV|WEB(?:Rip|DL)|Bluray|HDTV|Remux)(?:-((?:48|72|108|216)0p))?\](?:\[((?:DV)?(?:(?:\s)?HDR10(?:Plus)?)?|HLG|PQ)\])?\[(DTS(?:\sHD|-(?:X|ES|HD\s(?:M|HR)A))?|(?:TrueHD|EAC3)(?:\sAtmos)?|AC3|FLAC|PCM|MP3|A[AV]C|Opus)\s([1-8]\.[01])\]\[(h26[45]|MPEG[24]|DivX|XviD|V(?:C1|P9))\]|.*TdarrCacheFile-.*|(.*)-(featurette|other|interview|scene|short|deleted|behindthescenes|trailer))\.(m(?:kv|p(?:4|e?g))|ts|avi|(?:e(?:n|s)(?:\.hi)?\.)srt)$";

    private const string FULL_PATTERN =
        @"^(?:.*\\_(?:Movies|Comedy|Concerts)(?:\s\(non-t[mv]db\))?\\(.*\(\d{4}\)(-other)?)\\(\1)(?:\s{tmdb-\d{1,7}?})?\s({edition-(([1-7][05]TH\sANNIVERSARY)|4K|BLURAY|CHRONOLOGICAL|COLLECTORS|(CRITERION|KL\sSTUDIO)\sCOLLECTION|DIAMOND|DVD|IMAX|REDUX|REMASTERED|RESTORED|SPECIAL|(THE\sCOMPLETE\s)?EXTENDED|THE\sGODFATHER\sCODA|(THE\sRICHARD\sDONNER|DIRECTORS|FINAL)\sCUT|THEATRICAL|ULTIMATE|UNCUT|UNRATED)}\s)?\[(DVD|SDTV|(WEB(Rip|DL)|Bluray|HDTV|Remux)-(48|72|108|216)0p)\](\[((DV)?((\s)?HDR10(Plus)?)?|HLG|PQ)\])?\[(DTS(\sHD|-(X|ES|HD\s(M|HR)A))?|(TrueHD|EAC3)(\sAtmos)?|AC3|FLAC|PCM|MP3|A[AV]C|Opus)\s([1-8]\.[01])\]\[(h26[45]|MPEG[24]|DivX|XviD|V(C1|P9))\]|.*TdarrCacheFile-.*|.*-(featurette|other|interview|scene|short|deleted|behindthescenes|trailer))\.(m(kv|p(4|e?g))|ts|avi|(e(n|s)(\.hi)?\.)srt)$";

    private const string DIRECTORY_ONLY_PATTERN = @"^.*\\_(?:Movies|Comedy|Concerts)(?:\s\(non-t[mv]db\))?\\(.*)\((\d{4})\)(-other)?.*$";

    public Movie(string path)
    {
        string fileName;
        string directoryPath;
        FileExtension = Path.GetExtension(path);

        // check if we have a path to the file or just the filename
        if (path.Contains(@"\"))
        {
            var movieFullRegex = new Regex(FULL_PATTERN);
            if (!movieFullRegex.IsMatch(path)) return;

            // we have a path
            directoryPath = path.SubstringBeforeLastIgnoreCase(@"\");
            fileName = path.SubstringAfterLastIgnoreCase(@"\");
        }
        else
        {
            fileName = path;
            directoryPath = string.Empty;
        }
        var movieFileNameRegex = new Regex(FILE_NAME_PATTERN);
        if (!movieFileNameRegex.IsMatch(fileName)) return;

        Valid = ParseMovieMediaInfoFromFileName(fileName);
        if (Valid && directoryPath.HasValue()) Valid = ParseDirectory(directoryPath);
    }

    private bool ParseDirectory(string directoryPath)
    {
        var match = Regex.Match(directoryPath, DIRECTORY_ONLY_PATTERN);
        const int titleGroup = 1;
        const int releaseYearGroup = 2;
        const int otherMovieFileGroup = 3;
        var title = match.Groups[titleGroup].Value.Trim();
        var releaseYear = match.Groups[releaseYearGroup].Value;
        AlternateMovieFolder = match.Groups[otherMovieFileGroup].Value;

        if (SpecialFeature == SpecialFeature.None)
        {
            if (title != Title) return false;
            if (releaseYear != ReleaseYear) return false;
        }
        Directory = directoryPath;
        return true;
    }

    public string AlternateMovieFolder { get; set; }

    public string Directory { get; set; }

    public bool Valid { get; }

    public string Extension { get; set; }

    public string FileExtension { get; set; }

    public string GetFileName()
    {
        string s;

        if (SpecialFeature != SpecialFeature.None)
            s = $"{Title}-{SpecialFeature.ToEnumMember()}";
        else
        {
            s = $"{Title} ({ReleaseYear}){AlternateMovieFolder} ";
            if (TmdbId.HasValue()) s += $"{{tmdb-{TmdbId}}} ";
            if (Edition != Edition.Unknown) s += $"{{edition-{Edition.ToEnumMember().ToUpperInvariant()}}} ";
            s += $"[{MediaInfoVideoQuality.ToEnumMember()}";
            if (MediaInfoVideoResolution != VideoResolution.Unknown) s += $"-{MediaInfoVideoResolution.ToEnumMember()}";
            s += "]";

            if (MediaInfoVideoDynamicRangeType != MediaInfoVideoDynamicRangeType.Unknown)
                s += $"[{MediaInfoVideoDynamicRangeType.ToEnumMember()}]";
            s += $"[{MediaInfoAudioCodec.ToEnumMember()} {MediaInfoAudioChannels.ToEnumMember()}][{MediaInfoVideoCodec.ToEnumMember()}]";
        }
        s += $"{Extension}";
        return s;
    }

    private bool ParseMovieMediaInfoFromFileName(string filename)
    {
        const int titleGroup = 1;
        const int releaseYearGroup = 2;
        const int tmdbIdGroup = 3;
        const int editionGroup = 4;
        const int videoQualityGroup = 5;
        const int videoResolutionGroup = 6;
        const int videoDynamicRangeTypeGroup = 7;
        const int audioCodecGroup = 8;
        const int audioChannelsGroup = 9;
        const int videoCodecGroup = 10;
        const int specialFeatureTitle = 11;
        const int specialFeatureGroup = 12;
        const int extensionGroup = 13;
        var match = Regex.Match(filename, FILE_NAME_PATTERN);
        if (GetMediaInfoVideoCodec(match.Groups[videoCodecGroup].Value, out var codec2)) MediaInfoVideoCodec = codec2;
        if (GetMediaInfoAudioCodec(match.Groups[audioCodecGroup].Value, out var audioCodec)) MediaInfoAudioCodec = audioCodec;
        if (GetMediaInfoAudioChannels(match.Groups[audioChannelsGroup].Value, out var audioChannels)) MediaInfoAudioChannels = audioChannels;

        if (GetMediaInfoVideoDynamicRangeType(match.Groups[videoDynamicRangeTypeGroup].Value, out var dynamicRangeType))
            MediaInfoVideoDynamicRangeType = dynamicRangeType;

        if (GetMediaInfoVideoResolution(match.Groups[videoResolutionGroup].Value, out var videoResolution))
            MediaInfoVideoResolution = videoResolution;
        if (GetMediaInfoVideoQuality(match.Groups[videoQualityGroup].Value, out var videoQuality)) MediaInfoVideoQuality = videoQuality;
        if (GetVideoEdition(match.Groups[editionGroup].Value, out var edition)) Edition = edition;
        if (GetTmdbId(match.Groups[tmdbIdGroup].Value, out var tmdbId)) TmdbId = tmdbId;
        Title = match.Groups[titleGroup].Value.Trim();
        ReleaseYear = match.Groups[releaseYearGroup].Value;

        if (GetSpecialFeature(match.Groups[specialFeatureGroup].Value, out var specialFeature))
        {
            SpecialFeature = specialFeature;
            if (specialFeature != SpecialFeature.None) Title = match.Groups[specialFeatureTitle].Value.Trim();
        }
        Extension = "." + match.Groups[extensionGroup].Value.Trim();
        return true;
    }

    public SpecialFeature SpecialFeature { get; set; }

    public Edition Edition { get; set; }

    private bool GetTmdbId(string value, out string tmdbId)
    {
        if (value.HasValue())
        {
            tmdbId = value;
            return true;
        }
        tmdbId = string.Empty;
        return false;
    }

    private bool GetVideoEdition(string value, out Edition edition)
    {
        foreach (var bob in typeof(Edition).GetTypeInfo().DeclaredMembers)
        {
            if (bob.GetCustomAttribute<EnumMemberAttribute>(false)?.Value?.ToUpperInvariant() == value.ToUpperInvariant())
            {
                edition = (Edition)Enum.Parse(typeof(Edition), bob.Name);
                return true;
            }
        }
        edition = Edition.Unknown;
        return false;
    }

    private bool GetMediaInfoVideoQuality(string value, out VideoQuality videoQuality)
    {
        foreach (var bob in typeof(VideoQuality).GetTypeInfo().DeclaredMembers)
        {
            if (bob.GetCustomAttribute<EnumMemberAttribute>(false)?.Value == value)
            {
                videoQuality = (VideoQuality)Enum.Parse(typeof(VideoQuality), bob.Name);
                return true;
            }
        }
        videoQuality = VideoQuality.Unknown;
        return false;
    }

    private bool GetMediaInfoVideoResolution(string value, out VideoResolution videoResolution)
    {
        foreach (var bob in typeof(VideoResolution).GetTypeInfo().DeclaredMembers)
        {
            if (bob.GetCustomAttribute<EnumMemberAttribute>(false)?.Value == value)
            {
                videoResolution = (VideoResolution)Enum.Parse(typeof(VideoResolution), bob.Name);
                return true;
            }
        }
        videoResolution = VideoResolution.Unknown;
        return false;
    }

    private bool GetMediaInfoVideoDynamicRangeType(string value, out MediaInfoVideoDynamicRangeType mediaInfoVideoDynamicRangeType)
    {
        foreach (var bob in typeof(MediaInfoVideoDynamicRangeType).GetTypeInfo().DeclaredMembers)
        {
            if (bob.GetCustomAttribute<EnumMemberAttribute>(false)?.Value == value)
            {
                mediaInfoVideoDynamicRangeType = (MediaInfoVideoDynamicRangeType)Enum.Parse(typeof(MediaInfoVideoDynamicRangeType), bob.Name);
                return true;
            }
        }
        mediaInfoVideoDynamicRangeType = MediaInfoVideoDynamicRangeType.Unknown;
        return false;
    }

    private bool GetMediaInfoVideoCodec(string value, out MediaInfoVideoCodec codec)
    {
        foreach (var bob in typeof(MediaInfoVideoCodec).GetTypeInfo().DeclaredMembers)
        {
            if (bob.GetCustomAttribute<EnumMemberAttribute>(false)?.Value == value)
            {
                codec = (MediaInfoVideoCodec)Enum.Parse(typeof(MediaInfoVideoCodec), bob.Name);
                return true;
            }
        }
        codec = MediaInfoVideoCodec.Unknown;
        return false;
    }

    private bool GetMediaInfoAudioCodec(string value, out MediaInfoAudioCodec audioCodec)
    {
        foreach (var bob in typeof(MediaInfoAudioCodec).GetTypeInfo().DeclaredMembers)
        {
            if (bob.GetCustomAttribute<EnumMemberAttribute>(false)?.Value == value)
            {
                audioCodec = (MediaInfoAudioCodec)Enum.Parse(typeof(MediaInfoAudioCodec), bob.Name);
                return true;
            }
        }
        audioCodec = MediaInfoAudioCodec.Unknown;
        return false;
    }

    private bool GetSpecialFeature(string value, out SpecialFeature specialFeature)
    {
        foreach (var bob in typeof(SpecialFeature).GetTypeInfo().DeclaredMembers)
        {
            if (bob.GetCustomAttribute<EnumMemberAttribute>(false)?.Value == value)
            {
                specialFeature = (SpecialFeature)Enum.Parse(typeof(SpecialFeature), bob.Name);
                return true;
            }
        }
        specialFeature = SpecialFeature.None;
        return false;
    }

    private bool GetMediaInfoAudioChannels(string value, out MediaInfoAudioChannels mediaInfoAudioChannels)
    {
        foreach (var bob in typeof(MediaInfoAudioChannels).GetTypeInfo().DeclaredMembers)
        {
            if (bob.GetCustomAttribute<EnumMemberAttribute>(false)?.Value == value)
            {
                mediaInfoAudioChannels = (MediaInfoAudioChannels)Enum.Parse(typeof(MediaInfoAudioChannels), bob.Name);
                return true;
            }
        }
        mediaInfoAudioChannels = MediaInfoAudioChannels.Unknown;
        return false;
    }

    public VideoResolution MediaInfoVideoResolution { get; set; }

    public VideoQuality MediaInfoVideoQuality { get; set; }

    public string Title { get; set; }

    public string ReleaseYear { get; set; }

    // ReSharper disable once IdentifierTypo
    public string TmdbId { get; set; }

    public string QualityFull => MediaInfoVideoQuality.ToEnumMember() + "-" + MediaInfoVideoResolution.ToEnumMember();

    public MediaInfoVideoDynamicRangeType MediaInfoVideoDynamicRangeType { get; set; }

    public MediaInfoAudioCodec MediaInfoAudioCodec { get; set; }

    public MediaInfoAudioChannels MediaInfoAudioChannels { get; set; }

    public MediaInfoVideoCodec MediaInfoVideoCodec { get; set; }

    public string GetFullName()
    {
        return Directory.HasValue() ? Path.Combine(Directory, GetFileName()) : GetFileName();
    }
}

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

[SuppressMessage("ReSharper", "InconsistentNaming")]
[SuppressMessage("ReSharper", "IdentifierTypo")]
[SuppressMessage("ReSharper", "StringLiteralTypo")]
internal enum Edition
{
    [EnumMember(Value = "")] Unknown = 0,

    [EnumMember(Value = "10th Anniversary")]
    Anniversary10th,

    [EnumMember(Value = "20th Anniversary")]
    Anniversary20th,

    [EnumMember(Value = "25th Anniversary")]
    Anniversary25th,

    [EnumMember(Value = "30th Anniversary")]
    Anniversary30th,

    [EnumMember(Value = "35th Anniversary")]
    Anniversary35th,

    [EnumMember(Value = "40th Anniversary")]
    Anniversary40th,

    [EnumMember(Value = "45th Anniversary")]
    Anniversary45th,

    [EnumMember(Value = "50th Anniversary")]
    Anniversary50th,

    [EnumMember(Value = "60th Anniversary")]
    Anniversary60th,

    [EnumMember(Value = "70th Anniversary")]
    Anniversary70th,

    [EnumMember(Value = "4K")] FourK,

    [EnumMember(Value = "Bluray")] Bluray,

    [EnumMember(Value = "Chronological")] Chronological,

    [EnumMember(Value = "Collectors")] Collectors,

    [EnumMember(Value = "Criterion Collection")]
    CriterionCollection,

    [EnumMember(Value = "Diamond")] Diamond,

    [EnumMember(Value = "Directors Cut")] DirectorsCut,

    [EnumMember(Value = "DVD")] DVD,

    [EnumMember(Value = "Extended")] Extended,

    [EnumMember(Value = "Final Cut")] FinalCut,

    [EnumMember(Value = "IMAX")] Imax,

    [EnumMember(Value = "KL Studio Collection")]
    KLStudioCollection,

    [EnumMember(Value = "Redux")] Redux,

    [EnumMember(Value = "Remastered")] Remastered,

    [EnumMember(Value = "Restored")] Restored,

    [EnumMember(Value = "Special")] Special,

    [EnumMember(Value = "The Complete Extended")]
    TheCompleteExtended,

    [EnumMember(Value = "The Godfather Coda")]
    TheGodfatherCoda,

    [EnumMember(Value = "The Richard Donner Cut")]
    TheRichardDonnerCut,

    [EnumMember(Value = "Theatrical")] Theatrical,

    [EnumMember(Value = "Ultimate")] Ultimate,

    [EnumMember(Value = "Uncut")] Uncut,

    [EnumMember(Value = "Unrated")] Unrated
}

internal enum VideoResolution
{
    [EnumMember(Value = "")] Unknown = 0,

    [EnumMember(Value = "480p")] P480,

    [EnumMember(Value = "720p")] P720,

    [EnumMember(Value = "1080p")] P1080,

    [EnumMember(Value = "2160p")] P2160
}

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

[SuppressMessage("ReSharper", "InconsistentNaming")]
internal enum MediaInfoVideoDynamicRangeType
{
    [EnumMember(Value = "")] Unknown = 0,

    [EnumMember(Value = "DV HDR10")] DV_HDR10,

    [EnumMember(Value = "HDR10Plus")] HDR10Plus,

    [EnumMember(Value = "HDR10")] HDR10,

    [EnumMember(Value = "HLG")] HLG,

    [EnumMember(Value = "PQ")] PQ
}

[SuppressMessage("ReSharper", "StringLiteralTypo")]
[SuppressMessage("ReSharper", "UnusedMember.Global")]
[SuppressMessage("ReSharper", "IdentifierTypo")]
[SuppressMessage("ReSharper", "InconsistentNaming")]
internal enum VideoQuality
{
    [EnumMember(Value = "")] Unknown = 0,

    [EnumMember(Value = "WEBDL")] WEBDL,

    [EnumMember(Value = "HDTV")] HDTV,

    [EnumMember(Value = "SDTV")] SDTV,

    [EnumMember(Value = "WEBRip")] WEBRip,

    [EnumMember(Value = "DVD")] DVD,

    [EnumMember(Value = "Bluray")] Bluray,

    [EnumMember(Value = "Remux")] Remux
}
