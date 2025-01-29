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
    public TvEpisodeBackupFile(string path)
    {
        OriginalPath = path;
        Extension = Path.GetExtension(path);
        var fileName = Path.GetFileName(path);
        DirectoryName = Path.GetDirectoryName(path);
        IsValidFileName = new Regex(FileNameRegex).IsMatch(fileName);
        if (IsValidFileName) IsValidFileName = ParseMediaInfoFromFileName(fileName);
        if (!DirectoryName.HasValue()) return;

        // ReSharper disable once AssignNullToNotNullAttribute
        IsValidDirectoryName = new Regex(DirectoryRegex).IsMatch(DirectoryName);
        if (IsValidDirectoryName) IsValidDirectoryName = ParseMediaInfoFromDirectory(DirectoryName);
    }

    [SuppressMessage("ReSharper", "StringLiteralTypo")]
    protected override string FileNameRegex =>
        @"^(?:(.*)-(featurette|other|interview|scene|short|deleted|behindthescenes|trailer)|(.*)\s(?:s?(\d{1,4})?(e\d{2,4}-?(?:e\d{2,4})?|\d{4}-\d\d-\d\d))(.*?)(?:\[(DVD|SDTV|WEB(?:Rip|DL)|Bluray|HDTV)(?:-((?:480|576|720|1080|2160)p(?:\sRemux)??)??(?:\sProper)??)?\])??(?:\[((?:DV)??(?:(?:\s)??HDR10(?:Plus)??)??|PQ|HLG)\])??(?:\[(DTS(?:\sHD|-(?:X|ES|HD\s(?:M|HR)A))??|(?:TrueHD|EAC3)(?:\sAtmos)??|AC3|FLAC|PCM|MP[23]|A[AV]C|Opus|Vorbis|WMA)\s([1-8]\.[01])\])??(?:\[([hx]26[45]|MPEG(?:[24])?|XviD|V(?:C1|P9)|DivX|HEVC|AVC|RGB)\])??)\.(m(?:kv|p(?:4|e?g))|avi)$";

    [SuppressMessage("ReSharper", "StringLiteralTypo")]
    protected override string DirectoryRegex => @"^.*\\_TV(?:\s\(non-tvdb\))?\\(.*)\s{t[mv]db-(\d{1,7}?)}(?:\\(?:Season\s(\d+)|(Specials)))?$";

    public string EpisodeTitle { get; private set; }

    // ReSharper disable once IdentifierTypo
    // ReSharper disable once UnusedAutoPropertyAccessor.Global
    public string TvdbId { get; set; }

    public override string QualityFull
    {
        get
        {
            if (VideoQuality == VideoQuality.Unknown) return string.Empty;

            var s = $"[{VideoQuality.ToEnumMember()}";
            if (VideoResolution != VideoResolution.Unknown) s += $"-{VideoResolution.ToEnumMember()}";
            if (IsRemux) s += " " + Utils.REMUX;
            s += "]";
            return s;
        }
    }

    /// <summary>
    ///     The episode number like 'e04'. To preserve any leading '0' we keep as a string. It could be e01, e01e03, or e14-e15
    /// </summary>
    public string Episode { get; set; }

    public string Season { get; set; }

    private bool ParseMediaInfoFromDirectory(string directoryPath)
    {
        DirectoryName = directoryPath;
        var match = Regex.Match(directoryPath, DirectoryRegex);
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
            s += $"{Episode}";
            if (EpisodeTitle.HasValue()) s += $" {EpisodeTitle}";
            if (QualityFull != string.Empty) s += $" {QualityFull}";
            if (MediaInfoVideoDynamicRangeType != MediaInfoVideoDynamicRangeType.Unknown) s += $"[{MediaInfoVideoDynamicRangeType.ToEnumMember()}]";

            if (MediaInfoAudioChannels != MediaInfoAudioChannels.Unknown)
            {
                var audioChannels = MediaInfoAudioChannels.ToEnumMember();
                if (!s.EndsWithIgnoreCase("]")) s += " ";
                s += $"[{MediaInfoAudioCodec.ToEnumMember()} {audioChannels}]";
                if (MediaInfoVideoCodec != MediaInfoVideoCodec.Unknown) s += $"[{MediaInfoVideoCodec.ToEnumMember()}]";
            }
        }
        s += $"{Extension}";
        return s;
    }

    private bool ParseMediaInfoFromFileName(string filename)
    {
        const int specialFeatureTitleGroup = 1;
        const int specialFeatureGroup = 2;
        const int showTitleGroup = 3;
        const int seasonGroup = 4;
        const int episodeGroup = 5;
        const int episodeTitleGroup = 6;
        const int videoQualityGroup = 7;
        const int videoResolutionGroup = 8;
        const int videoDynamicRangeTypeGroup = 9;
        const int audioCodecGroup = 10;
        const int audioChannelsGroup = 11;
        const int videoCodecGroup = 12;
        const int extensionGroup = 13;
        var match = Regex.Match(filename, FileNameRegex);
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

        if (videoResolution.Contains(Utils.REMUX))
        {
            IsRemux = true;
            videoResolution = videoResolution.SubstringBeforeLastIgnoreCase(Utils.REMUX).Trim();
        }
        VideoResolution = Utils.GetEnumFromAttributeValue<VideoResolution>(videoResolution);
        VideoQuality = Utils.GetEnumFromAttributeValue<VideoQuality>(videoQuality);
        Title = showTitle.Trim();

        // Trim the episode title before any metadata in the title
        // sometimes double metadata  may appear in the filename
        // this will remove it
        EpisodeTitle = episodeTitle.Contains("][") ? episodeTitle.SubstringBeforeLastIgnoreCase(" [").Trim() : episodeTitle.Trim();
        Season = season.Trim();
        Episode = episode.Trim();
        SpecialFeature = Utils.GetEnumFromAttributeValue<SpecialFeature>(specialFeature);
        if (SpecialFeature != SpecialFeature.None) Title = specialFeatureTitle.Trim();
        Extension = "." + extension;
        return true;
    }
}