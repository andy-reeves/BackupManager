// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="Movie.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

using BackupManager.Extensions;

namespace BackupManager.Entities;

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
internal sealed partial class Movie
{
    // private const string FILE_NAME_PATTERN =
    //    @"^(?:(.*)\((\d{4})\)(?:-other)?(?:\s{(?:tmdb-(\d{1,7}))?})?\s(?:{edition-((?:[1-7][05]TH\sANNIVERSARY)|4K|BLURAY|CHRONOLOGICAL|COLLECTORS|(?:CRITERION|KL\sSTUDIO)\sCOLLECTION|DIAMOND|DVD|IMAX|REDUX|REMASTERED|RESTORED|SPECIAL|(?:THE\sCOMPLETE\s)?EXTENDED|THE\sGODFATHER\sCODA|(?:THE\sRICHARD\sDONNER|DIRECTORS|FINAL)\sCUT|THEATRICAL|ULTIMATE|UNCUT|UNRATED)}\s)?\[(DVD|SDTV|WEB(?:Rip|DL)|Bluray|HDTV|Remux)(?:-((?:48|72|108|216)0p))?\](?:\[((?:DV)?(?:(?:\s)?HDR10(?:Plus)?)?|HLG|PQ)\])?\[(DTS(?:\sHD|-(?:X|ES|HD\s(?:M|HR)A))?|(?:TrueHD|EAC3)(?:\sAtmos)?|AC3|FLAC|PCM|MP3|A[AV]C|Opus)\s([1-8]\.[01])\]\[(h26[45]|MPEG[24]|DivX|XviD|V(?:C1|P9))\]|.*TdarrCacheFile-.*|(.*)-(featurette|other|interview|scene|short|deleted|behindthescenes|trailer))\.(m(?:kv|p(?:4|e?g))|ts|avi|(?:e(?:n|s)(?:\.hi)?\.)srt)$";

    // ReSharper disable once UnusedMember.Local
    //private const string FULL_PATTERN =
    //   @"^(?:.*\\_(?:Movies|Comedy|Concerts)(?:\s\(non-t[mv]db\))?\\(.*\(\d{4}\)(-other)?)\\(\1)(?:\s{tmdb-\d{1,7}?})?\s({edition-(([1-7][05]TH\sANNIVERSARY)|4K|BLURAY|CHRONOLOGICAL|COLLECTORS|(CRITERION|KL\sSTUDIO)\sCOLLECTION|DIAMOND|DVD|IMAX|REDUX|REMASTERED|RESTORED|SPECIAL|(THE\sCOMPLETE\s)?EXTENDED|THE\sGODFATHER\sCODA|(THE\sRICHARD\sDONNER|DIRECTORS|FINAL)\sCUT|THEATRICAL|ULTIMATE|UNCUT|UNRATED)}\s)?\[(DVD|SDTV|(WEB(Rip|DL)|Bluray|HDTV|Remux)-(48|72|108|216)0p)\](\[((DV)?((\s)?HDR10(Plus)?)?|HLG|PQ)\])?\[(DTS(\sHD|-(X|ES|HD\s(M|HR)A))?|(TrueHD|EAC3)(\sAtmos)?|AC3|FLAC|PCM|MP3|A[AV]C|Opus)\s([1-8]\.[01])\]\[(h26[45]|MPEG[24]|DivX|XviD|V(C1|P9))\]|.*TdarrCacheFile-.*|.*-(featurette|other|interview|scene|short|deleted|behindthescenes|trailer))\.(m(kv|p(4|e?g))|ts|avi|(e(n|s)(\.hi)?\.)srt)$";

    // private const string DIRECTORY_ONLY_PATTERN = @"^.*\\_(?:Movies|Comedy|Concerts)(?:\s\(non-t[mv]db\))?\\(.*)\((\d{4})\)(-other)?.*$";

    public bool RefreshMediaInfo()
    {
        var model = Utils.GetMediaInfoModel(OriginalPath);
        if (model == null) return false;

        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(OriginalPath);
        var videoCodec = Utils.FormatVideoCodec(model, fileNameWithoutExtension);
        var audioCodec = Utils.FormatAudioCodec(model, fileNameWithoutExtension);
        var audioChannels = Utils.FormatAudioChannels(model);
        var dynamicRangeType = Utils.FormatVideoDynamicRangeType(model);
        MediaInfoVideoCodec = Utils.GetEnumFromAttributeValue<MediaInfoVideoCodec>(videoCodec);
        MediaInfoAudioCodec = Utils.GetEnumFromAttributeValue<MediaInfoAudioCodec>(audioCodec);
        MediaInfoAudioChannels = Utils.GetEnumFromAttributeValue<MediaInfoAudioChannels>(audioChannels.ToString(CultureInfo.InvariantCulture));
        MediaInfoVideoDynamicRangeType = Utils.GetEnumFromAttributeValue<MediaInfoVideoDynamicRangeType>(dynamicRangeType);
        return true;
    }

    public Movie(string path)
    {
        string fileName;
        string directoryPath;
        FileExtension = Path.GetExtension(path);
        OriginalPath = path;

        // check if we have a path to the file or just the filename
        if (path.Contains('\\'))
        {
            // var movieFullRegex = new Regex(FULL_PATTERN);
            //if (!movieFullRegex.IsMatch(path)) return;

            // we have a path
            directoryPath = path.SubstringBeforeLastIgnoreCase(@"\");
            fileName = path.SubstringAfterLastIgnoreCase(@"\");
        }
        else
        {
            fileName = path;
            directoryPath = string.Empty;
        }
        var movieFileNameRegex = FileNameRegex();
        if (!movieFileNameRegex.IsMatch(fileName)) return;

        Valid = ParseMovieMediaInfoFromFileName(fileName);
        if (Valid && directoryPath.HasValue()) Valid = ParseDirectory(directoryPath);
    }

    public string OriginalPath { get; }

    private bool ParseDirectory(string directoryPath)
    {
        var match = DirectoryRegex().Match(directoryPath);
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

    // ReSharper disable once UnusedAutoPropertyAccessor.Global
    public string FileExtension { get; }

    public string GetFileName()
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

        // ReSharper disable once IdentifierTypo
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
        var match = FileNameRegex().Match(filename);
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
        var specialFeatureTitle1 = match.Groups[specialFeatureTitle].Value;
        var specialFeature = match.Groups[specialFeatureGroup].Value;
        var extension = match.Groups[extensionGroup].Value.Trim();
        MediaInfoVideoCodec = Utils.GetEnumFromAttributeValue<MediaInfoVideoCodec>(videoCodec);
        MediaInfoAudioCodec = Utils.GetEnumFromAttributeValue<MediaInfoAudioCodec>(audioCodec);
        MediaInfoAudioChannels = Utils.GetEnumFromAttributeValue<MediaInfoAudioChannels>(audioChannels);
        MediaInfoVideoDynamicRangeType = Utils.GetEnumFromAttributeValue<MediaInfoVideoDynamicRangeType>(videoDynamicRangeType);
        MediaInfoVideoResolution = Utils.GetEnumFromAttributeValue<VideoResolution>(videoResolution);
        MediaInfoVideoQuality = Utils.GetEnumFromAttributeValue<VideoQuality>(videoQuality);
        Edition = Utils.GetEnumFromAttributeValue<Edition>(edition);
        TmdbId = id.HasValue() ? id : string.Empty;
        Title = title.Trim();
        ReleaseYear = releaseYear;
        SpecialFeature = Utils.GetEnumFromAttributeValue<SpecialFeature>(specialFeature);
        if (SpecialFeature != SpecialFeature.None) Title = specialFeatureTitle1.Trim();
        Extension = "." + extension;
        return true;
    }

    public SpecialFeature SpecialFeature { get; set; }

    public Edition Edition { get; set; }

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

    [GeneratedRegex(@"^.*\\_(?:Movies|Comedy|Concerts)(?:\s\(non-t[mv]db\))?\\(.*)\((\d{4})\)(-other)?.*$")]
    private static partial Regex DirectoryRegex();

    [GeneratedRegex(
        @"^(?:(.*)\((\d{4})\)(?:-other)?(?:\s{(?:tmdb-(\d{1,7}))?})?\s(?:{edition-((?:[1-7][05]TH\sANNIVERSARY)|4K|BLURAY|CHRONOLOGICAL|COLLECTORS|(?:CRITERION|KL\sSTUDIO)\sCOLLECTION|DIAMOND|DVD|IMAX|REDUX|REMASTERED|RESTORED|SPECIAL|(?:THE\sCOMPLETE\s)?EXTENDED|THE\sGODFATHER\sCODA|(?:THE\sRICHARD\sDONNER|DIRECTORS|FINAL)\sCUT|THEATRICAL|ULTIMATE|UNCUT|UNRATED)}\s)?\[(DVD|SDTV|WEB(?:Rip|DL)|Bluray|HDTV|Remux)(?:-((?:48|72|108|216)0p))?\](?:\[((?:DV)?(?:(?:\s)?HDR10(?:Plus)?)?|HLG|PQ)\])?\[(DTS(?:\sHD|-(?:X|ES|HD\s(?:M|HR)A))?|(?:TrueHD|EAC3)(?:\sAtmos)?|AC3|FLAC|PCM|MP3|A[AV]C|Opus)\s([1-8]\.[01])\]\[(h26[45]|MPEG[24]|DivX|XviD|V(?:C1|P9))\]|.*TdarrCacheFile-.*|(.*)-(featurette|other|interview|scene|short|deleted|behindthescenes|trailer))\.(m(?:kv|p(?:4|e?g))|ts|avi|(?:e(?:n|s)(?:\.hi)?\.)srt)$")]
    [SuppressMessage("ReSharper", "StringLiteralTypo")]
    private static partial Regex FileNameRegex();
}
