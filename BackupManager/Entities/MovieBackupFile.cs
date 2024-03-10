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
    protected override string FileNameRegex =>
        @"^(?:(.*)\((\d{4})\)(?:-other)?(?:\s{(?:tmdb-(\d{1,7}))?})?\s(?:{edition-((?:[1-7][05]TH\sANNIVERSARY)|4K|BLURAY|CHRONOLOGICAL|COLLECTORS|(?:CRITERION|KL\sSTUDIO)\sCOLLECTION|DIAMOND|DVD|IMAX|REDUX|REMASTERED|RESTORED|SPECIAL|(?:THE\sCOMPLETE\s)?EXTENDED|THE\sGODFATHER\sCODA|(?:THE\sRICHARD\sDONNER|DIRECTORS|FINAL)\sCUT|THEATRICAL|ULTIMATE|UNCUT|UNRATED)}\s)?\[(DVD|SDTV|WEB(?:Rip|DL)|Bluray|HDTV|Remux)(?:-((?:48|72|108|216)0p))?\](?:\[((?:DV)?(?:(?:\s)?HDR10(?:Plus)?)?|HLG|PQ)\])?\[(DTS(?:\sHD|-(?:X|ES|HD\s(?:M|HR)A))?|(?:TrueHD|EAC3)(?:\sAtmos)?|AC3|FLAC|PCM|MP3|A[AV]C|Opus)\s([1-8]\.[01])\]\[(h26[45]|MPEG[24]|DivX|XviD|V(?:C1|P9))\]|(.*)-(featurette|other|interview|scene|short|deleted|behindthescenes|trailer))\.(m(?:kv|p(?:4|e?g))|ts|avi)$";

    protected override string DirectoryRegex => @"^.*\\_(?:Movies|Comedy|Concerts)(?:\s\(non-tmdb\))?\\(.*)\((\d{4})\)(-other)?.*$";

    public MovieBackupFile(string path)
    {
        string fileName;
        string directoryPath;
        Extension = Path.GetExtension(path);
        OriginalPath = path;

        // check if we have a path to the file or just the filename
        if (path.Contains('\\'))
        {
            directoryPath = path.SubstringBeforeLastIgnoreCase(@"\");
            fileName = path.SubstringAfterLastIgnoreCase(@"\");
        }
        else
        {
            fileName = path;
            directoryPath = string.Empty;
        }
        var regex = new Regex(FileNameRegex);
        if (!regex.IsMatch(fileName)) return;

        IsValid = ParseMediaInfoFromFileName(fileName);
        if (IsValid && directoryPath.HasValue()) IsValid = ParseMediaInfoFromDirectory(directoryPath);
    }

    public string AlternateMovieFolder { get; set; }

    public SpecialFeature SpecialFeature { get; set; }

    public Edition Edition { get; set; }

    public string ReleaseYear { get; set; }

    // ReSharper disable once IdentifierTypo
    public string TmdbId { get; set; }

    public MovieVideoResolution VideoResolution { get; set; }

    public override string QualityFull
    {
        get
        {
            var s = $"[{VideoQuality.ToEnumMember()}";
            if (VideoResolution != MovieVideoResolution.Unknown) s += $"-{VideoResolution.ToEnumMember()}";
            s += "]";
            return s;
        }
    }

    private bool ParseMediaInfoFromDirectory(string directoryPath)
    {
        FullDirectory = directoryPath;
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

        if (SpecialFeature != SpecialFeature.None)
            s = $"{Title}-{SpecialFeature.ToEnumMember()}";
        else
        {
            s = $"{Title} ({ReleaseYear}){AlternateMovieFolder} ";

            // ReSharper disable once StringLiteralTypo
            if (TmdbId.HasValue()) s += $"{{tmdb-{TmdbId}}} ";
            if (Edition != Edition.Unknown) s += $"{{edition-{Edition.ToEnumMember().ToUpperInvariant()}}} ";
            s += $"{QualityFull}";

            if (MediaInfoVideoDynamicRangeType != MediaInfoVideoDynamicRangeType.Unknown)
                s += $"[{MediaInfoVideoDynamicRangeType.ToEnumMember()}]";
            s += $"[{MediaInfoAudioCodec.ToEnumMember()} {MediaInfoAudioChannels.ToEnumMember()}][{MediaInfoVideoCodec.ToEnumMember()}]";
        }
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
        const int videoDynamicRangeTypeGroup = 7;
        const int audioCodecGroup = 8;
        const int audioChannelsGroup = 9;
        const int videoCodecGroup = 10;
        const int specialFeatureTitleGroup = 11;
        const int specialFeatureGroup = 12;
        const int extensionGroup = 13;
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
        MediaInfoVideoCodec = Utils.GetEnumFromAttributeValue<MediaInfoVideoCodec>(videoCodec);
        MediaInfoAudioCodec = Utils.GetEnumFromAttributeValue<MediaInfoAudioCodec>(audioCodec);
        MediaInfoAudioChannels = Utils.GetEnumFromAttributeValue<MediaInfoAudioChannels>(audioChannels);
        MediaInfoVideoDynamicRangeType = Utils.GetEnumFromAttributeValue<MediaInfoVideoDynamicRangeType>(videoDynamicRangeType);
        VideoResolution = Utils.GetEnumFromAttributeValue<MovieVideoResolution>(videoResolution);
        VideoQuality = Utils.GetEnumFromAttributeValue<VideoQuality>(videoQuality);
        Edition = Utils.GetEnumFromAttributeValue<Edition>(edition);
        TmdbId = id.HasValue() ? id : string.Empty;
        Title = title.Trim();
        ReleaseYear = releaseYear;
        SpecialFeature = Utils.GetEnumFromAttributeValue<SpecialFeature>(specialFeature);
        if (SpecialFeature != SpecialFeature.None) Title = specialFeatureTitle.Trim();
        Extension = "." + extension;
        return true;
    }
}