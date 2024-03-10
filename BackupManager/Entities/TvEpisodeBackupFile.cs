// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="TvEpisodeBackupFile.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.RegularExpressions;

using BackupManager.Extensions;

namespace BackupManager.Entities;

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
internal sealed class TvEpisodeBackupFile : VideoBackupFileBase
{
    private const string FILE_NAME_PATTERN =
        @"^(?:(.*)\s(?:s?(\d{1,4})?(e\d{2,4}-?(?:e\d{2,4})?|\d{4}-\d\d-\d\d))(\s.*)?(?:\[(DVD|SDTV|WEB(?:Rip|DL)|Bluray|HDTV)(?:-((?:48|72|108|216)0p(?:\sRemux)?))?\](?:\[((?:DV)?(?:(?:\s)?HDR10(?:Plus)?)?|PQ|HLG)\])?\[(DTS(?:\sHD|-(?:X|ES|HD\s(?:M|HR)A))?|(?:TrueHD|EAC3)(?:\sAtmos)?|AC3|FLAC|PCM|MP[23]|A[AV]C|Opus|Vorbis|WMA)\s([1-8]\.[01])\]\[([hx]26[45]|MPEG(?:[24])?|XviD|V(?:C1|P9)|DivX|HEVC|AVC|RGB)\])|(.*)-(featurette|other|interview|scene|short|deleted|behindthescenes|trailer))\.(m(?:kv|p(?:4|e?g))|avi)$";

    private const string DIRECTORY_ONLY_PATTERN = @"^.*\\_TV(?:\s\(non-tvdb\))?\\(.*)\s{t[mv]db-(\d{1,7}?)}(?:\\(?:Season\s(\d+)|(Specials)))?$";

    public TvEpisodeBackupFile(string path)
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
        var regex = new Regex(FILE_NAME_PATTERN);
        if (!regex.IsMatch(fileName)) return;

        IsValid = ParseMediaInfoFromFileName(fileName);
        if (IsValid && directoryPath.HasValue()) IsValid = ParseMediaInfoFromDirectory(directoryPath);
    }

    public string EpisodeTitle { get; private set; }

    public TvVideoResolution VideoResolution { get; set; }

    public SpecialFeature SpecialFeature { get; set; }

    // ReSharper disable once IdentifierTypo
    // ReSharper disable once UnusedAutoPropertyAccessor.Global
    public string TvdbId { get; set; }

    public override string QualityFull
    {
        get
        {
            var s = $"[{VideoQuality.ToEnumMember()}";
            if (VideoResolution != TvVideoResolution.Unknown) s += $"-{VideoResolution.ToEnumMember()}";
            s += "]";
            return s;
        }
    }

    protected override bool Validate()
    {
        var fileName = GetFileName();
        var regex = new Regex(FILE_NAME_PATTERN);
        IsValid = regex.IsMatch(fileName);
        return IsValid;
    }

    private bool ParseMediaInfoFromDirectory(string directoryPath)
    {
        FullDirectory = directoryPath;
        var match = Regex.Match(directoryPath, DIRECTORY_ONLY_PATTERN);
        if (!match.Success) return false;

        const int titleGroup = 1;
        const int idGroup = 2;
        const int seasonGroup = 3;
        const int specialsGroup = 4;
        var id = match.Groups[idGroup].Value;
        TvdbId = id.HasValue() ? id : string.Empty;
        var title = match.Groups[titleGroup].Value.Trim();
        var season = match.Groups[seasonGroup].Value.Trim();
        var specials = match.Groups[specialsGroup].Value.Trim();
        if (SpecialFeature != SpecialFeature.None) return true;
        if (title != Title) return false;

        if (specials.HasValue()) season = "0";
        return Convert.ToInt32(season) == Convert.ToInt32(Season);
    }

    public override string GetFileName()
    {
        string s;

        if (SpecialFeature != SpecialFeature.None)
            s = $"{Title}-{SpecialFeature.ToEnumMember()}";
        else
        {
            s = $"{Title} ";
            if (Season.HasValue()) s += $"s{Season}";
            s += $"{Episode} ";
            if (EpisodeTitle.HasValue()) s += $"{EpisodeTitle} ";
            s += $"{QualityFull}";

            if (MediaInfoVideoDynamicRangeType != MediaInfoVideoDynamicRangeType.Unknown)
                s += $"[{MediaInfoVideoDynamicRangeType.ToEnumMember()}]";
            var audioChannels = MediaInfoAudioChannels.ToEnumMember();
            s += $"[{MediaInfoAudioCodec.ToEnumMember()} {audioChannels}][{MediaInfoVideoCodec.ToEnumMember()}]";
        }
        s += $"{Extension}";
        return s;
    }

    private bool ParseMediaInfoFromFileName(string filename)
    {
        const int showTitleGroup = 1;
        const int seasonGroup = 2;
        const int episodeGroup = 3;

        // ReSharper disable once IdentifierTypo
        const int episodeTitleGroup = 4;
        const int videoQualityGroup = 5;
        const int videoResolutionGroup = 6;
        const int videoDynamicRangeTypeGroup = 7;
        const int audioCodecGroup = 8;
        const int audioChannelsGroup = 9;
        const int videoCodecGroup = 10;
        const int specialFeatureTitleGroup = 11;
        const int specialFeatureGroup = 12;
        const int extensionGroup = 13;
        var match = Regex.Match(filename, FILE_NAME_PATTERN);
        var videoCodec = match.Groups[videoCodecGroup].Value;
        var audioCodec = match.Groups[audioCodecGroup].Value;
        var audioChannels = match.Groups[audioChannelsGroup].Value;
        var videoDynamicRangeType = match.Groups[videoDynamicRangeTypeGroup].Value;
        var videoResolution = match.Groups[videoResolutionGroup].Value;
        var videoQuality = match.Groups[videoQualityGroup].Value;
        var showTitle = match.Groups[showTitleGroup].Value.Trim();
        var episodeTitle = match.Groups[episodeTitleGroup].Value;
        var season = match.Groups[seasonGroup].Value;
        var episode = match.Groups[episodeGroup].Value;
        var specialFeatureTitle = match.Groups[specialFeatureTitleGroup].Value;
        var specialFeature = match.Groups[specialFeatureGroup].Value;
        var extension = match.Groups[extensionGroup].Value.Trim();
        MediaInfoVideoCodec = Utils.GetEnumFromAttributeValue<MediaInfoVideoCodec>(videoCodec);
        MediaInfoAudioCodec = Utils.GetEnumFromAttributeValue<MediaInfoAudioCodec>(audioCodec);
        MediaInfoAudioChannels = Utils.GetEnumFromAttributeValue<MediaInfoAudioChannels>(audioChannels);
        MediaInfoVideoDynamicRangeType = Utils.GetEnumFromAttributeValue<MediaInfoVideoDynamicRangeType>(videoDynamicRangeType);
        VideoResolution = Utils.GetEnumFromAttributeValue<TvVideoResolution>(videoResolution);
        VideoQuality = Utils.GetEnumFromAttributeValue<VideoQuality>(videoQuality);
        Title = showTitle.Trim();
        EpisodeTitle = episodeTitle.Trim();
        Season = season.Trim();
        Episode = episode.Trim();
        SpecialFeature = Utils.GetEnumFromAttributeValue<SpecialFeature>(specialFeature);
        if (SpecialFeature != SpecialFeature.None) Title = specialFeatureTitle.Trim();
        Extension = "." + extension;
        return true;
    }

    public string Episode { get; set; }

    public string Season { get; set; }
}