// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="MovieBackupFile.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.RegularExpressions;

using BackupManager.Extensions;

namespace BackupManager.Entities;

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
internal sealed class MovieBackupFile : VideoBackupFileBase
{
    public MovieBackupFile(string path)
    {
        OriginalPath = path;
        Extension = Path.GetExtension(path);
        var fileName = Path.GetFileName(path);
        DirectoryName = Path.GetDirectoryName(path);
        IsValidFileName = new Regex(FileNameRegex).IsMatch(fileName);
        if (IsValidFileName) IsValidFileName = ParseMediaInfoFromFileName(fileName);
        if (DirectoryName.HasNoValue()) return;

        // ReSharper disable once AssignNullToNotNullAttribute
        IsValidDirectoryName = new Regex(DirectoryRegex).IsMatch(DirectoryName);
        if (IsValidDirectoryName) IsValidDirectoryName = ParseMediaInfoFromDirectory(DirectoryName);
    }

    [SuppressMessage("ReSharper", "StringLiteralTypo")]
    protected override string FileNameRegex =>
        @"^(?:(.*)\((\d{4})\)(?:-other)?(?:\s{(?:tmdb-(\d{1,7}))?})?\s(?:{edition-((?:[1-7][05]TH\sANNIVERSARY)|4K|BLURAY|CHRONOLOGICAL|COLLECTORS|(?:CRITERION|KL\sSTUDIO)\sCOLLECTION|DIAMOND|DVD|IMAX|REDUX|REMASTERED|RESTORED|SPECIAL|(?:THE\sCOMPLETE\s)?EXTENDED|THE\sGODFATHER\sCODA|(?:THE\sRICHARD\sDONNER|DIRECTORS|ASSEMBLY|FINAL)\sCUT|THEATRICAL|ULTIMATE|UNCUT|UNRATED)}\s)?\[(DVD|SDTV|WEB(?:Rip|DL)|Bluray|HDTV|Remux)(?:-((?:480|576|720|1080|2160)p)(?:\sProper)?)?\](?:\[(3D)])?(?:\[((?:DV)?(?:(?:\s)?HDR10(?:Plus)?)?|HLG|PQ)\])?\[(DTS(?:\sHD|-(?:X|ES|HD\s(?:M|HR)A))?|(?:TrueHD|EAC3)(?:\sAtmos)?|AC3|FLAC|PCM|MP3|A[AV]C|Opus)\s([1-8]\.[01])\]\[([hx]26[45]|MPEG[24]|DivX|AVC|HEVC|XviD|V(?:C1|P9))\]|(.*)-(featurette|other|interview|scene|short|deleted|behindthescenes|trailer))\.(m(?:kv|p(?:4|e?g))|ts|avi)$";

    [SuppressMessage("ReSharper", "StringLiteralTypo")]
    protected override string DirectoryRegex => @"^.*\\_(?:Movies|Comedy|Concerts)(?:\s\(non-tmdb\))?\\(.*)\((\d{4})\)(-other)?.*$";

    public string AlternateMovieFolder { get; set; }

    public Edition Edition { get; set; }

    public string ReleaseYear { get; set; }

    // ReSharper disable once IdentifierTypo
    public string TmdbId { get; set; }

    public override string QualityFull
    {
        get
        {
            var qualityFull = IsRemux ? Utils.REMUX : $"{VideoQuality.ToEnumMember()}";
            if (VideoResolution != VideoResolution.Unknown) qualityFull += $"-{VideoResolution.ToEnumMember()}";
            return qualityFull.WrapInSquareBrackets();
        }
    }

    private bool ParseMediaInfoFromDirectory(string directoryPath)
    {
        DirectoryName = directoryPath;
        var match = Regex.Match(directoryPath, DirectoryRegex);
        if (!match.Success) return false;

        const int titleGroup = 1;
        const int releaseYearGroup = 2;
        const int otherMovieFileGroup = 3;
        var title = match.Groups[titleGroup].Value.Trim();
        var releaseYear = match.Groups[releaseYearGroup].Value;
        AlternateMovieFolder = match.Groups[otherMovieFileGroup].Value;
        if (SpecialFeature != SpecialFeature.None) return true;
        if (title != Title) return false;

        return releaseYear == ReleaseYear;
    }

    public override string GetFileName()
    {
        string s;

        if (SpecialFeature == SpecialFeature.None)
        {
            s = $"{Title} ({ReleaseYear}){AlternateMovieFolder} ";

            // ReSharper disable once StringLiteralTypo
            if (TmdbId.HasValue()) s += $"{{tmdb-{TmdbId}}} ";
            if (Edition != Edition.Unknown) s += $"{{edition-{Edition.ToEnumMember().ToUpperInvariant()}}} ";
            s += $"{QualityFull}";
            if (MediaInfoVideo3D) s += "[3D]";
            if (MediaInfoVideoDynamicRangeType != MediaInfoVideoDynamicRangeType.Unknown) s += $"[{MediaInfoVideoDynamicRangeType.ToEnumMember()}]";
            s += $"[{MediaInfoAudioCodec.ToEnumMember()} {MediaInfoAudioChannels.ToEnumMember()}][{MediaInfoVideoCodec.ToEnumMember()}]";
        }
        else
            s = $"{Title}-{SpecialFeature.ToEnumMember()}";
        s += $"{Extension}";
        return s;
    }

    private bool ParseMediaInfoFromFileName(string filename)
    {
        const int titleGroup = 1;
        const int releaseYearGroup = 2;

        // ReSharper disable once IdentifierTypo
        const int tmdbIdGroup = 3;
        const int editionGroup = 4;
        const int videoQualityGroup = 5;
        const int videoResolutionGroup = 6;
        const int video3DGroup = 7;
        const int videoDynamicRangeTypeGroup = 8;
        const int audioCodecGroup = 9;
        const int audioChannelsGroup = 10;
        const int videoCodecGroup = 11;
        const int specialFeatureTitleGroup = 12;
        const int specialFeatureGroup = 13;
        const int extensionGroup = 14;
        var match = Regex.Match(filename, FileNameRegex);
        var videoCodec = match.Groups[videoCodecGroup].Value;
        var audioCodec = match.Groups[audioCodecGroup].Value;
        var audioChannels = match.Groups[audioChannelsGroup].Value;
        var videoDynamicRangeType = match.Groups[videoDynamicRangeTypeGroup].Value;
        var videoResolution = match.Groups[videoResolutionGroup].Value;
        var videoQuality = match.Groups[videoQualityGroup].Value;
        var edition = match.Groups[editionGroup].Value;
        var id = match.Groups[tmdbIdGroup].Value;
        var title = match.Groups[titleGroup].Value.Trim();
        var releaseYear = match.Groups[releaseYearGroup].Value;
        var specialFeatureTitle = match.Groups[specialFeatureTitleGroup].Value;
        var specialFeature = match.Groups[specialFeatureGroup].Value;
        var extension = match.Groups[extensionGroup].Value.Trim();
        var video3D = match.Groups[video3DGroup].Value;
        MediaInfoVideoCodec = Utils.GetEnumFromAttributeValue<MediaInfoVideoCodec>(videoCodec);
        MediaInfoAudioCodec = Utils.GetEnumFromAttributeValue<MediaInfoAudioCodec>(audioCodec);
        MediaInfoAudioChannels = Utils.GetEnumFromAttributeValue<MediaInfoAudioChannels>(audioChannels);
        MediaInfoVideoDynamicRangeType = Utils.GetEnumFromAttributeValue<MediaInfoVideoDynamicRangeType>(videoDynamicRangeType);

        if (videoQuality.Contains(Utils.REMUX))
        {
            IsRemux = true;
            videoQuality = Utils.BLURAY;
        }
        VideoResolution = Utils.GetEnumFromAttributeValue<VideoResolution>(videoResolution);
        VideoQuality = Utils.GetEnumFromAttributeValue<VideoQuality>(videoQuality);
        Edition = Utils.GetEnumFromAttributeValue<Edition>(edition);
        TmdbId = id.HasValue() ? id : string.Empty;
        Title = title.Trim();
        ReleaseYear = releaseYear;
        SpecialFeature = Utils.GetEnumFromAttributeValue<SpecialFeature>(specialFeature);
        if (SpecialFeature != SpecialFeature.None) Title = specialFeatureTitle.Trim();
        Extension = "." + extension;
        MediaInfoVideo3D = video3D == "3D";
        return true;
    }
}