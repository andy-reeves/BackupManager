// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="FileRulesUnitTest.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;
using System.Xml;

using BackupManager;
using BackupManager.Entities;
using BackupManager.Extensions;

namespace TestProject;

[SuppressMessage("ReSharper", "MemberCanBeFileLocal")]
[SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "<Pending>")]
public sealed class FileRulesUnitTest
{
    public enum TestRegexType
    {
        Discovery = 0,

        Test = 1
    }

    private static readonly MediaBackup _mediaBackup;

    static FileRulesUnitTest()
    {
        _mediaBackup = MediaBackup.Load(Path.Combine(Utils.GetProjectPath(typeof(FileRulesUnitTest)), "..\\BackupManager\\MediaBackup.xml"));
        Utils.Config = _mediaBackup.Config;
    }

    [InlineData(1, 1, TestRegexType.Test, true, @"X:\_TV\Chernobyl {tvdb-360893}\Season 1\Chernobyl s01e01 12345 [Bluray-2160p Remux].mkv")]
    [InlineData(1, 2, TestRegexType.Test, false, @"X:\_TV\Chernobyl {tdb-360893}\Season 1\Chernobyl s01e01 12345 [Bluray-2160p Remux].mkv")]
    [InlineData(1, 3, TestRegexType.Discovery, false, @"X:\_TV (tmdb-123456})\Chernobyl {tdb-360893}\Season 1\Chernobyl s01e01 12345 [Bluray-2160p Remux].mkv")]
    [InlineData(1, 4, TestRegexType.Discovery, true, @"X:\_TV\Chernobyl {tdb-360893}\Season 1\Chernobyl s01e01 12345 [Bluray-2160p Remux].mkv")]
    [InlineData(2, 1, TestRegexType.Test, true, @"X:\_TV\Chernobyl {tvdb-360893}\Season 1\Chernobyl s01e01 12345 [Bluray-2160p Remux].mkv")]
    [InlineData(2, 2, TestRegexType.Test, false, @"X:\_TV\Chernobyl {tvdb-360893}\Season 1\Chernobyl s01e01 TBA [Bluray-2160p Remux].mkv")]
    [InlineData(2, 3, TestRegexType.Discovery, true, @"X:\_TV (non-tvdb)\Chernobyl {tvdb-360893}\Season 1\Chernobyl s01e01 TBA [Bluray-2160p Remux].mkv")]
    [InlineData(2, 4, TestRegexType.Discovery, false, @"X:\_Movies\Chernobyl {tvdb-360893}\Season 1\Chernobyl s01e01 TBA [Bluray-2160p Remux].mkv")]
    [InlineData(2, 5, TestRegexType.Test, false, @"X:\_TV\Chernobyl {tvdb-360893}\Season 1\Chernobyl s01e01 TBD [Bluray-2160p Remux].mkv")]
    [InlineData(2, 6, TestRegexType.Test, true, @"X:\_TV\Chernobyl {tvdb-360893}\Season 1\Chernobyl s01e01 TBDa [Bluray-2160p Remux].mkv")]
    [InlineData(2, 7, TestRegexType.Test, true, @"X:\_TV\Chernobyl {tvdb-360893}\Season 1\Chernobyl s01e01 TBAa [Bluray-2160p Remux].mkv")]
    [InlineData(3, 1, TestRegexType.Discovery, true, @"\\nas2\assets3\_Music\ABBA\More ABBA Gold\01-01- Summer Night City.mp3")]
    [InlineData(3, 2, TestRegexType.Discovery, false,
        @"\\nas5\assets4\_TV\3rd Rock from the Sun {tvdb-72389}\Specials\3rd Rock from the Sun s00e02 Behind the Scenes [DVD].ts")]
    [InlineData(3, 3, TestRegexType.Test, true, @"\\nas2\assets3\_Music\ABBA\More ABBA Gold\01-01- Summer Night City.mp3")]
    [InlineData(3, 4, TestRegexType.Test, false, @"\\nas2\assets3\_Music\ABBA\More ABBA Gold\Folder.jpg")]
    [InlineData(3, 5, TestRegexType.Test, true, @"\\nas2\assets3\_Music\ABBA\More ABBA Gold\folder.jpg")]
    [InlineData(3, 6, TestRegexType.Test, false, @"\\nas2\assets3\_Music\ABBA\More ABBA Gold\01-01 Summer Night City.mp3")]
    [InlineData(3, 7, TestRegexType.Test, true, @"\\nas2\assets3\_Music\ABBA\More ABBA Gold\01-101- Summer Night City.mp3")]
    [InlineData(3, 8, TestRegexType.Test, true, @"\\nas2\assets3\_Music\ABBA\More ABBA Gold\01-101- Summer Night City.m4a")]
    [InlineData(3, 9, TestRegexType.Test, false, @"\\nas2\assets3\_Music\ABBA\More ABBA Gold\01-101- Summer Night City.flaca")]
    [InlineData(3, 10, TestRegexType.Test, true, @"\\nas2\assets3\_Music\ABBA\More ABBA Gold\01-101- Summer Night City.flac")]
    [InlineData(4, 1, TestRegexType.Discovery, true, @"Z:\_TV\Tom and Jerry {tvdb-72860}\Season 1940\Tom and Jerry s1940e01 Puss Gets The Boot [SDTV].mkv")]
    [InlineData(4, 2, TestRegexType.Test, true, @"X:\_TV\Chernobyl {tvdb-360893}\Season 1\Chernobyl s01e01 12345 [Bluray-2160p Remux].mkv")]
    [InlineData(4, 3, TestRegexType.Test, true, @"X:\_Movies (non-tmdb)\Blood Brothers (1989)\Example 1-featurette.mkv")]
    [InlineData(4, 4, TestRegexType.Test, true, @"X\_TV (non-tvdb)\Das Boot (1985) {tmdb-156249}\Season 1\Das Boot (1985) s01e01 [Bluray-1080p Remux].mkv")]
    [InlineData(4, 5, TestRegexType.Test, false, @"X:\_TV\Chernobyl {tvdb-360893}\Chernobyl s01e01 12345 [Bluray-2160p Remux].mkv")]
    [InlineData(4, 6, TestRegexType.Test, true, @"Z:\_TV\Tom and Jerry {tvdb-72860}\Season 1940\Tom and Jerry s1940e01 Puss Gets The Boot [SDTV].mkv")]
    [InlineData(4, 7, TestRegexType.Discovery, false, @"X:\_Movies\Chernobyl {tvdb-360893}\Season 1\Chernobyl s01e01 TBA [Bluray-2160p Remux].mkv")]
    [InlineData(4, 8, TestRegexType.Test, true,
        @"Z:\_TV\Tom and Jerry {tvdb-72860} {edition-DVD}\Season 1940\Tom and Jerry s1940e01 Puss Gets The Boot [DVD-576p Remux].mkv")]
    [InlineData(5, 1, TestRegexType.Test, true, @"X:\_Movies\12 Angry Men (1957)\12 Angry Men (1957) {tmdb-389} {edition-BLURAY} [Remux-1080p][DTS-HD MA 1.0][h264].mkv")]
    [InlineData(5, 2, TestRegexType.Test, true, @"X:\_Movies (non-tmdb)\Blood Brothers (1989)\Example 1-scene.mkv")]
    [InlineData(5, 3, TestRegexType.Test, false, @"X:\_Movies\12 Angry Men (1957)\12 Angry Men (1957) {edition-BLURAY} [Remux-1080p][DTS-HD MA 1.0][h264].mkv")]
    [InlineData(5, 4, TestRegexType.Discovery, false, @"X:\_TV\Chernobyl {tvdb-360893}\Chernobyl s01e01 12345 [Bluray-2160p Remux].mkv")]
    [InlineData(5, 5, TestRegexType.Discovery, false, @"Z:\_TV\Tom and Jerry {tvdb-72860}\Season 1940\Tom and Jerry s1940e01 Puss Gets The Boot [SDTV].mkv")]
    [InlineData(5, 6, TestRegexType.Discovery, true,
        @"X:\_Movies\12 Angry Men (1957)\12 Angry Men (1957) {tmdb-389} {edition-BLURAY} [Remux-1080p][DTS-HD MA 1.0][h264].mkv")]
    [InlineData(5, 7, TestRegexType.Discovery, false, @"X:\_Movies (non-tmdb)\Blood Brothers (1989)\Example 1-scene.mkv")]
    [InlineData(6, 1, TestRegexType.Test, true, @"X:\_Movies (non-tmdb)\Blood Brothers (1989)\Blood Brothers (1989).mkv")]
    [InlineData(6, 2, TestRegexType.Test, true, @"X:\_Movies (non-tmdb)\Blood Brothers (1989)\Example 1-featurette.mkv")]
    [InlineData(6, 3, TestRegexType.Test, true, @"X:\_Movies (non-tmdb)\Blood Brothers (1989)\Example 1-other.mkv")]
    [InlineData(6, 4, TestRegexType.Test, true, @"X:\_Movies (non-tmdb)\Blood Brothers (1989)\Example 1-interview.mkv")]
    [InlineData(6, 5, TestRegexType.Test, true, @"X:\_Movies (non-tmdb)\Blood Brothers (1989)\Example 1-scene.mkv")]
    [InlineData(6, 6, TestRegexType.Test, true, @"X:\_Movies (non-tmdb)\Blood Brothers (1989)\Example 1-short.mkv")]
    [InlineData(6, 7, TestRegexType.Test, true, @"X:\_Movies (non-tmdb)\Blood Brothers (1989)\Example 1-deleted.mkv")]
    [InlineData(6, 8, TestRegexType.Test, true, @"X:\_Movies (non-tmdb)\Blood Brothers (1989)\Example 1-behindthescenes.mkv")]
    [InlineData(6, 9, TestRegexType.Test, true, @"X:\_Movies (non-tmdb)\Blood Brothers (1989)\Example 1-trailer.mkv")]
    [InlineData(6, 10, TestRegexType.Test, true,
        @"X:\_Movies (non-tmdb)\The Lord of the Rings (2003)\The Lord of the Rings (2003) {tmdb-120} {edition-THE COMPLETE EXTENDED} [Remux-2160p][DV HDR10][TrueHD Atmos 7.1][h265].mkv")]
    [InlineData(6, 11, TestRegexType.Test, false, @"X:\_Movies\12 Angry Men (1957)\12 Angry Men {edition-BLURAY} [Remux-1080p][DTS-HD MA 1.0][h264].mkv")]
    [InlineData(6, 12, TestRegexType.Test, false, @"X:\_Movies (non-tmdb)\Blood Brothers (1989)\Example 1-behindscenes.mkv")]
    [InlineData(6, 13, TestRegexType.Test, false, @"X:\_Movies (non-tmdb)\Blood Brothers (1989)\Example 1-trailertest.mkv")]
    [InlineData(6, 14, TestRegexType.Discovery, false, @"Z:\_TV\Tom and Jerry {tvdb-72860}\Season 1940\Tom and Jerry s1940e01 Puss Gets The Boot [SDTV].mkv")]
    [InlineData(6, 15, TestRegexType.Discovery, true,
        @"X:\_Movies\12 Angry Men (1957)\12 Angry Men (1957) {tmdb-389} {edition-BLURAY} [Remux-1080p][DTS-HD MA 1.0][h264].mkv")]
    [InlineData(6, 16, TestRegexType.Discovery, true, @"X:\_Movies (non-tmdb)\Blood Brothers (1989)\Example 1-scene.mkv")]
    [InlineData(7, 1, TestRegexType.Discovery, true, @"X:\_Movies\12 Angry Men (1957)\12 Angry Men {edition-BLURAY} [Remux-1080p][DTS-HD MA 1.0][h264].mkv")]
    [InlineData(7, 2, TestRegexType.Discovery, false, @"X:\_Movies\12 Angry Men (1957)\12 Angry Men [Remux-1080p][DTS-HD MA 1.0][h264].mkv")]
    [InlineData(7, 3, TestRegexType.Test, true, @"X:\_Movies\12 Angry Men (1957)\12 Angry Men {edition-BLURAY} [Remux-1080p][DTS-HD MA 1.0][h264].mkv")]
    [InlineData(7, 4, TestRegexType.Test, false, @"X:\_Movies\12 Angry Men (1957)\12 Angry Men {edition-MYFAVOURITE} [Remux-1080p][DTS-HD MA 1.0][h264].mkv")]
    [InlineData(7, 5, TestRegexType.Discovery, true, @"X:\_Movies\12 Angry Men (1957)\12 Angry Men {edition-MYFAVOURITE} [Remux-1080p][DTS-HD MA 1.0][h264].mkv")]
    [InlineData(7, 6, TestRegexType.Test, false, @"X:\_Movies\12 Angry Men (1957)\12 Angry Men {edition-36TH ANNIVERSARY} [Remux-1080p][DTS-HD MA 1.0][h264].mkv")]
    [InlineData(7, 7, TestRegexType.Test, true, @"X:\_Movies\12 Angry Men (1957)\12 Angry Men {edition-75TH ANNIVERSARY} [Remux-1080p][DTS-HD MA 1.0][h264].mkv")]
    [InlineData(7, 8, TestRegexType.Test, true, @"X:\_Movies\12 Angry Men (1957)\12 Angry Men {edition-ASSEMBLY CUT} [Remux-1080p][DTS-HD MA 1.0][h264].mkv")]
    [InlineData(8, 1, TestRegexType.Test, true, @"X:\_Concerts\12 Angry Men (1957)\12 Angry Men {edition-BLURAY} [Remux-1080p][DTS-HD MA 1.0][h264].mkv")]
    [InlineData(8, 2, TestRegexType.Test, false, @"X:\_Movies\12 Angry Men (1957)\12 Angry Men.subtitles.srt")]
    [InlineData(8, 3, TestRegexType.Discovery, true, @"X:\_Movies\12 Angry Men (1957)\12 Angry Men {edition-BLURAY} [Remux-1080p][DTS-HD MA 1.0][h264].mkv")]
    [InlineData(8, 4, TestRegexType.Discovery, true, @"X:\_Movies (non-tmdb)\Blood Brothers (1989)\Example 1-behindscenes.mkv")]
    [InlineData(9, 1, TestRegexType.Test, true, @"X:\_Concerts\12 Angry Men (1957)\12 Angry Men {edition-BLURAY} [Remux-1080p][DTS-HD MA 1.0][h264].mkv")]
    [InlineData(9, 2, TestRegexType.Test, false, @"X:\_Movies\12 Angry Men ()\12 Angry Men.mkv")]
    [InlineData(9, 3, TestRegexType.Discovery, true, @"X:\_Concerts\12 Angry Men (1957)\12 Angry Men {edition-BLURAY} [Remux-1080p][DTS-HD MA 1.0][h264].mkv")]
    [InlineData(9, 4, TestRegexType.Discovery, true, @"X:\_TV (non-tvdb)\Blood Brothers (1989)\Example 1-behindscenes.mkv")]
    [InlineData(9, 5, TestRegexType.Discovery, false, @"X:\_Music\Blood Brothers (1989)\Example 1-behindscenes.mkv")]
    [InlineData(10, 1, TestRegexType.Test, true, @"X:\_Concerts\12 Angry Men (1957)\12 Angry Men {edition-BLURAY} [Remux-1080p][DTS-HD MA 1.0][h264].mkv")]
    [InlineData(10, 2, TestRegexType.Test, false, @"X:\_Movies\12 Angry Men (0)\12 Angry Men.mkv")]
    [InlineData(10, 3, TestRegexType.Discovery, true, @"X:\_Concerts\12 Angry Men (1957)\12 Angry Men {edition-BLURAY} [Remux-1080p][DTS-HD MA 1.0][h264].mkv")]
    [InlineData(10, 4, TestRegexType.Discovery, true, @"X:\_TV (non-tvdb)\Blood Brothers (1989)\Example 1-behindscenes.mkv")]
    [InlineData(10, 5, TestRegexType.Discovery, false, @"X:\_Music\Blood Brothers (1989)\Example 1-behindscenes.mkv")]
    [InlineData(11, 1, TestRegexType.Test, true, @"X:\_Concerts\12 Angry Men (1957)\12 Angry Men {edition-BLURAY} [Remux-1080p][DTS-HD MA 1.0][h264].mkv")]
    [InlineData(11, 2, TestRegexType.Test, false, @"X:\_Movies\12 Angry Men (0)\12 Angry Men [Remux-1080p Proper].mkv")]
    [InlineData(11, 3, TestRegexType.Discovery, true, @"X:\_Concerts\12 Angry Men (1957)\12 Angry Men {edition-BLURAY} [Remux-1080p][DTS-HD MA 1.0][h264].mkv")]
    [InlineData(11, 4, TestRegexType.Discovery, true, @"X:\_TV (non-tvdb)\Blood Brothers (1989)\Example 1-behindscenes.mkv")]
    [InlineData(11, 5, TestRegexType.Discovery, false, @"X:\_Music\Blood Brothers (1989)\Example 1-behindscenes.mkv")]
    [InlineData(12, 1, TestRegexType.Test, true, @"X:\_Concerts\12 Angry Men (1957)\12 Angry Men {edition-BLURAY} [Remux-1080p][DTS-HD MA 1.0][h264].mkv")]
    [InlineData(12, 2, TestRegexType.Test, false, @"X:\_Movies\12 Angry Men (0)\12 Angry Men [Remux-1080p Proper REAL].mkv")]
    [InlineData(12, 3, TestRegexType.Discovery, true, @"X:\_Concerts\12 Angry Men (1957)\12 Angry Men {edition-BLURAY} [Remux-1080p][DTS-HD MA 1.0][h264].mkv")]
    [InlineData(12, 4, TestRegexType.Discovery, true, @"X:\_TV (non-tvdb)\Blood Brothers (1989)\Example 1-behindscenes.mkv")]
    [InlineData(12, 5, TestRegexType.Discovery, false, @"X:\_Music\Blood Brothers (1989)\Example 1-behindscenes.mkv")]
    [InlineData(13, 1, TestRegexType.Test, true, @"X:\_Concerts\12 Angry Men (1957)\12 Angry Men {edition-BLURAY} [Remux-1080p][DTS-HD MA 1.0][h264].mkv")]
    [InlineData(13, 2, TestRegexType.Test, false, @"X:\_Movies\12 Angry Men (0)\12 Angry Men [Remux-1080p].bob")]
    [InlineData(13, 3, TestRegexType.Discovery, true, @"X:\_Concerts\12 Angry Men (1957)\12 Angry Men {edition-BLURAY} [Remux-1080p][DTS-HD MA 1.0][h264].mkv")]
    [InlineData(13, 4, TestRegexType.Discovery, true, @"X:\_Movies (non-tvdb)\Blood Brothers (1989)\Example 1-behindscenes.mkv")]
    [InlineData(13, 5, TestRegexType.Discovery, false, @"X:\_Music\Blood Brothers (1989)\Example 1-behindscenes.mkv")]
    [InlineData(14, 1, TestRegexType.Discovery, true, @"X:\_TV\Chernobyl {tdb-360893}\Season 1\Chernobyl s01e01 12345 [Bluray-2160p Remux]-other.mkv")]
    [InlineData(14, 2, TestRegexType.Discovery, true, @"X:\_TV\Chernobyl {tdb-360893}\Chernobyl s01e01 12345 [Bluray-2160p Remux]-other.mkv")]
    [InlineData(14, 3, TestRegexType.Discovery, false, @"X:\_TV\Chernobyl {tdb-360893}\Season 1\Chernobyl s01e01 12345 [Bluray-2160p Remux]-bobby.mkv")]
    [InlineData(14, 4, TestRegexType.Discovery, false, @"X:\_TV\Chernobyl {tdb-360893}\Chernobyl s01e01 12345 [Bluray-2160p Remux]-bobby.mkv")]
    [InlineData(14, 5, TestRegexType.Discovery, false, @"K:\_TV\American Horror Story {tvdb-250487}\Season 1\American Horror Story s01e01 Pilot [HDTV-720p].mkv")]
    [InlineData(14, 6, TestRegexType.Discovery, false,
        @"Z:\_TV\MasterChef Australia {tvdb-92091}\Season 7\MasterChef Australia s07e34 Off-site Challenge Tokyo Tina vs. Saigon Sally [SDTV].mp4")]
    [InlineData(14, 7, TestRegexType.Discovery, true,
        @"Z:\_TV\MasterChef Australia {tvdb-92091}\Season 7\MasterChef Australia s07e34 Off-site Challenge Tokyo Tina vs. Saigon Sally [SDTV]-other.mp4")]
    [InlineData(14, 8, TestRegexType.Discovery, false, @"K:\_TV\Westworld {tvdb-296762}\Season 2\Westworld s02e50 - An Evocative Location.mkv")]
    [InlineData(14, 9, TestRegexType.Test, true, @"X:\_TV\Game of Thrones {tvdb-121361}\Reunion Special-featurette.mkv")]
    [InlineData(14, 10, TestRegexType.Test, false, @"X:\_TV\Game of Thrones {tvdb-121361}\Season 1\Reunion Special-featurette.mkv")]
    [InlineData(14, 11, TestRegexType.Test, false,
        @"Z:\_TV\MasterChef Australia {tvdb-92091}\Season 7\MasterChef Australia s07e34 Off-site Challenge Tokyo Tina vs. Saigon Sally [SDTV].mp4")]
    [InlineData(14, 12, TestRegexType.Test, false,
        @"Z:\_TV\MasterChef Australia {tvdb-92091}\Season 7\MasterChef Australia s07e34 Off-site Challenge Tokyo Tina vs. Saigon Sally [SDTV]-other.mp4")]
    [InlineData(14, 13, TestRegexType.Discovery, false, @"X:\_Concerts\12 Angry Men (1957)\12 Angry Men {edition-BLURAY} [Remux-1080p][DTS-HD MA 1.0][h264].mkv")]
    [InlineData(14, 14, TestRegexType.Discovery, true, @"X:\_TV (non-tvdb)\Blood Brothers (1989)\Example 1-behindthescenes.mkv")]
    [InlineData(14, 15, TestRegexType.Discovery, false, @"X:\_Music\Blood Brothers (1989)\Example 1-behindscenes.mkv")]
    [InlineData(14, 16, TestRegexType.Discovery, false,
        @"\\nas5\assets2\_TV\The Bridge (2013) {tvdb-264085}\Season 1\The Bridge (2013) s01e13 The Crazy Place [Bluray-1080p][DTS 5.1][h264]-TdarrCacheFile-GWLejgTOOR.mkv")]
    [InlineData(14, 17, TestRegexType.Test, true, @"\\nas5\assets2\_TV\_TV\Friends {tvdb-79168} {edition-DVD}\Behind The Scenes Season 5-featurette.mkv")]
    [InlineData(15, 1, TestRegexType.Discovery, true, @"X:\_Movies\12 Angry Men (1957)\12 Angry Men {edition-BLURAY} [Remux-1080p][DTS-HD MA 1.0][h264]-other.mkv")]
    [InlineData(15, 2, TestRegexType.Discovery, true, @"X:\_Concerts\12 Angry Men (1957)\12 Angry Men {edition-BLURAY} [Remux-1080p][DTS-HD MA 1.0][h264]-bobby.mkv")]
    [InlineData(15, 3, TestRegexType.Discovery, true, @"X:\_Movies\12 Angry Men (1957)\Other\12 Angry Men {edition-BLURAY} [Remux-1080p][DTS-HD MA 1.0][h264]-other.mkv")]
    [InlineData(15, 4, TestRegexType.Discovery, true, @"X:\_Movies\12 Angry Men (1957)\Other\12 Angry Men {edition-BLURAY} [Remux-1080p][DTS-HD MA 1.0][h264]-bobby.mkv")]
    [InlineData(15, 5, TestRegexType.Test, true, @"X:\_Movies\12 Angry Men (1957)\12 Angry Men {edition-BLURAY} [Remux-1080p][DTS-HD MA 1.0][h264]-featurette.mkv")]
    [InlineData(15, 6, TestRegexType.Test, true,
        @"X:\_Movies (non-tmdb)\The Lord of the Rings (2003)\The Lord of the Rings (2003) {tmdb-120} {edition-THE COMPLETE EXTENDED} [Remux-2160p][DV HDR10][TrueHD Atmos 7.1][h265]-other.mkv")]
    [InlineData(15, 7, TestRegexType.Test, false, @"X:\_Movies\12 Angry Men (1957)\12 Angry Men {edition-BLURAY} [Remux-1080p][DTS-HD MA 1.0][h264]-feature.mkv")]
    [InlineData(15, 8, TestRegexType.Test, false, @"X:\_Movies\12 Angry Men (1957)\Special Features\12 Angry Men Making Of-featurette.mkv")]
    [InlineData(15, 9, TestRegexType.Test, false,
        @"X:\_Movies (non-tmdb)\The Lord of the Rings (2003)\Other\The Lord of the Rings (2003) {tmdb-120} {edition-THE COMPLETE EXTENDED} [Remux-2160p][DV HDR10][TrueHD Atmos 7.1][h265]-featurette.mkv")]
    [InlineData(15, 10, TestRegexType.Test, true, @"X:\_Movies\12 Angry Men (1957)\12 Angry Men {edition-BLURAY} [Remux-1080p][DTS-HD MA 1.0][h264]-other.mkv")]
    [InlineData(15, 11, TestRegexType.Test, true, @"Q:\_Comedy\Rhod Gilbert and the Award-Winning Mince Pie (2009)\The Audience-short.mkv")]
    [InlineData(15, 12, TestRegexType.Test, false, @"Q:\_Comedy\Rhod Gilbert and the Award-Winning Mince Pie (2009)\The Audience-short..mkv")]
    [InlineData(15, 13, TestRegexType.Discovery, false, @"X:\_TV (non-tvdb)\Blood Brothers (1989)\Example 1-behindscenes.mkv")]
    [InlineData(15, 14, TestRegexType.Discovery, false, @"X:\_Music\Blood Brothers (1989)\Example 1-behindscenes.mkv")]
    [InlineData(15, 15, TestRegexType.Test, true, @"\\nas5\assets3\_Movies\Withnail & I (1987)\Withnail on the Pier-featurette.mkv")]
    [InlineData(16, 1, TestRegexType.Test, false, @"X:\_Movies\12 Angry Men (1957)\12 Angry Men {edition-BLURAY} [Remux-1080p][DTS-HD MA 1.0][h264]..mkv")]
    [InlineData(16, 2, TestRegexType.Test, false, @"X:\_Movies\12 Angry Men (1957)\12 Angry Men [Remux-1080p]..mkv")]
    [InlineData(16, 3, TestRegexType.Test, false, @"X:\_Movies\12 Angry Men (1957)\12 Angry Men [Remux-1080p]-other..mkv")]
    [InlineData(16, 4, TestRegexType.Test, true, @"X:\_Movies\12 Angry Men (1957)\12 Angry Men [Remux-1080p]-other.mkv")]
    [InlineData(16, 5, TestRegexType.Test, true, @"X:\_Movies\12 Angry Men (1957)\12 Angry Men [Remux-1080p].mkv")]
    [InlineData(16, 6, TestRegexType.Test, false, @"X:\_Movies\12 Angry Men (1957)\12 Angry Men {edition-BLURAY} [Remux-1080p][DTS-HD MA 1.0][h264]..mkv")]
    [InlineData(16, 7, TestRegexType.Test, false, @"X:\_Movies\12 Angry Men (1957)\12 Angry Men {edition-BLURAY} [Remux-1080p][DTS-HD MA 1.0][h264]..")]
    [InlineData(16, 8, TestRegexType.Test, false, @"X:\_Movies\12 Angry Men (1957)\12 Angry Men {edition-BLURAY} [Remux-1080p][DTS-HD MA 1..0][h264].mkv")]
    [InlineData(16, 9, TestRegexType.Test, true, @"\\nas1\assets1\_TV\Mr Benn {tvdb-72516}\As If By Magic...The Story Behind Mr Benn-featurette.mkv")]
    [InlineData(16, 10, TestRegexType.Test, true, @"X:\_Movies\Beck 12 The Loner (2002)\Beck 12 The Loner (2002) {tmdb-263120} [WEBRip-1080p][AAC 2.0][h264].es.srt")]
    [InlineData(16, 11, TestRegexType.Discovery, true, @"X:\_Concerts\12 Angry Men (1957)\12 Angry Men {edition-BLURAY} [Remux-1080p][DTS-HD MA 1.0][h264].mkv")]
    [InlineData(16, 12, TestRegexType.Discovery, true, @"X:\_TV (non-tvdb)\Blood Brothers (1989)\Example 1-behindscenes.mkv")]
    [InlineData(16, 13, TestRegexType.Discovery, false, @"X:\_Music\Blood Brothers (1989)\Example 1-behindscenes.mkv")]
    [InlineData(17, 1, TestRegexType.Test, true,
        @"X:\_Movies (non-tmdb)\The Lord of the Rings (2003)\The Lord of the Rings (2003) {tmdb-120} {edition-THE COMPLETE EXTENDED} [Remux-2160p][TrueHD Atmos 7.1][h265].mkv")]
    [InlineData(17, 2, TestRegexType.Test, true, @"X:\_Movies (non-tmdb)\Blood Brothers (1989)\Example 1-behindthescenes.mkv")]
    [InlineData(17, 3, TestRegexType.Test, false, @"X:\_Movies (non-tmdb)\Blood Brothers (1989)\Example 1-trailertest.mkv")]
    [InlineData(17, 4, TestRegexType.Discovery, false, @"Z:\_TV\Tom and Jerry {tvdb-72860}\Season 1940\Tom and Jerry s1940e01 Puss Gets The Boot [SDTV].mkv")]
    [InlineData(17, 5, TestRegexType.Discovery, true,
        @"X:\_Movies\12 Angry Men (1957)\12 Angry Men (1957) {tmdb-389} {edition-BLURAY} [Remux-1080p][DTS-HD MA 1.0][h264].mkv")]
    [InlineData(17, 6, TestRegexType.Discovery, true, @"X:\_Movies (non-tmdb)\Blood Brothers (1989)\Example 1-scene.mkv")]
    [InlineData(17, 7, TestRegexType.Test, true,
        @"X:\_Movies (non-tmdb)\The Lord of the Rings (2003)\The Lord of the Rings (2003) {tmdb-120} {edition-THE COMPLETE EXTENDED} [Remux-2160p][DV HDR10][TrueHD Atmos 7.1][h265].mkv")]
    [InlineData(17, 8, TestRegexType.Test, false,
        @"X:\_Movies (non-tmdb)\The Lord of the Rings (2003)\The Lord of the Rings (2003) {tmdb-120} {edition-THE COMPLETE EXTENDED} [Remux-2160p][DV HDR10][TrueHD Atmos 7.1][bob].mkv")]
    [InlineData(17, 9, TestRegexType.Test, true,
        @"X:\_Movies (non-tmdb)\The Lord of the Rings (2003)\The Lord of the Rings (2003) {tmdb-120} {edition-30TH ANNIVERSARY} [Remux-2160p][DV HDR10][FLAC 1.0][VC1].mkv")]
    [InlineData(17, 10, TestRegexType.Test, true,
        @"X:\_Movies (non-tmdb)\The Lord of the Rings (2003)\The Lord of the Rings (2003) {tmdb-120} {edition-THE COMPLETE EXTENDED} [Remux-2160p][EAC3 Atmos 7.1][VC1].mkv")]
    [InlineData(17, 11, TestRegexType.Test, false,
        @"\\nas3\assets4\_Movies (non-tmdb)\Blood Brothers (1989)\Blood Brothers (1989) [Remux-1080p][FLAC 1.0][x264].en.hi.srt")]
    [InlineData(17, 12, TestRegexType.Test, true, @"\\nas1\assets4\_Movies\Aliens (1986)\Aliens (1986) {tmdb-679} [Remux-2160p][HDR10][AC3 5.1][h265].mkv")]
    [InlineData(17, 13, TestRegexType.Test, true,
        @"\\nas1\assets4\_Movies\Guy Ritchies The Covenant (2023)\Guy Ritchies The Covenant (2023) {tmdb-882569} [WEBDL-2160p][HDR10Plus][EAC3 Atmos 5.1][h265].es.srt")]
    [InlineData(17, 14, TestRegexType.Test, true,
        @"\\nas1\assets4\_Movies\Guy Ritchies The Covenant (2023)\Guy Ritchies The Covenant (2023) {tmdb-882569} [WEBDL-2160p][HDR10Plus][EAC3 Atmos 5.1][h265].mkv")]
    [InlineData(17, 15, TestRegexType.Test, false,
        @"\\nas1\assets4\_Movies\Extraction 2 (2023)\Extraction 2 (2023) {tmdb-697843} [WEBDL-1080p][DV HDR10Plus][EAC3 Atmos 5.1][HEVC].mkv")]
    [InlineData(17, 16, TestRegexType.Test, true,
        @"\\nas1\assets4\_Movies\Extraction 2 (2023)\Extraction 2 (2023) {tmdb-697843} [WEBDL-1080p][DV][EAC3 Atmos 4.0][h265].mkv")]
    [InlineData(17, 17, TestRegexType.Test, true, @"\\nas5\assets3\_Movies\Tootsie (1982)\Tootsie (1982) {tmdb-9576} [Remux-1080p][DTS-X 8.0][h264].mkv")]
    [InlineData(17, 18, TestRegexType.Test, false, @"\\nas1\assets4\_Movies\The 4th Man (1983)\The 4th Man (1983) {tmdb-29140} [DVD][AC3 2.0][].en.srt")]
    [InlineData(17, 19, TestRegexType.Test, false,
        @"\\nas1\assets4\_Movies\Angelique The Road To Versailles (1965)\Angelique The Road To Versailles (1965) {tmdb-44453} [Bluray-1080p]][x264].en.srt")]
    [InlineData(17, 20, TestRegexType.Test, false, @"\\nas2\assets2\_Movies\A Murder of Crows (1999)\A Murder of Crows (1999) {tmdb-17263} [HDTV-1080p][MP3 2.0][].avi")]
    [InlineData(17, 21, TestRegexType.Test, false,
        @"\\nas3\assets2\_Movies\Creature from the Black Lagoon (1954)\Creature from the Black Lagoon (1954) {tmdb-10973} [Remux-1080p][3D][DTS-HD MA 2.0][AVC].mkv")]
    [InlineData(17, 22, TestRegexType.Test, true,
        @"\\nas4\assets1\_Movies\LEGO Marvel Avengers Time Twisted (2022)\LEGO Marvel Avengers Time Twisted (2022) {tmdb-940543} [WEBDL-1080p][Opus 2.0][VP9].mkv")]
    [InlineData(17, 23, TestRegexType.Test, false,
        @"\\nas3\assets4\_Movies\Apocalypse Now (1979)\Apocalypse Now (1979) {tmdb-28} {edition-FINAL CUT} [Remux-2160p][DV HDR10][TrueHD Atmos 7.1][h265].1.es.srt")]
    [InlineData(17, 24, TestRegexType.Test, false,
        @"\\nas3\assets4\_Movies (non-tmdb)\12 Angry Men (1957)\12 Angry Men (1957) {tmdb-389} {edition-CRITERION COLLECTION} [Remux-1080p].es.srt")]
    [InlineData(17, 25, TestRegexType.Test, false, @"\\nas3\assets4\_Movies (non-tmdb)\Blood Brothers (1989)\Blood Brothers (1989).mp4")]
    [InlineData(17, 26, TestRegexType.Test, false,
        @"\\nas4\assets1\_Movies\What Becomes of the Broken Hearted (1999)\What Becomes of the Broken Hearted (1999) {tmdb-13893} [DVD][AC3 2.0][].es.srt")]
    [InlineData(17, 27, TestRegexType.Test, false,
        @"\\nas4\assets1\_Movies\Full Tilt Boogie (1998)\Full Tilt Boogie (1998) {tmdb-36606} [Bluray-1080p][DTS 2.0][x264].hi.srt")]
    [InlineData(17, 28, TestRegexType.Test, true, @"\\nas4\assets1\_Movies\A (2022)\A (2022) {tmdb-1} [DVD-576p][DTS 1.0][h264].mkv")]
    [InlineData(17, 29, TestRegexType.Test, true, @"\\nas4\assets1\_Movies\A (2022)\A (2022) {tmdb-1} [DVD-576p][DTS-X 2.0][h265].mkv")]
    [InlineData(17, 30, TestRegexType.Test, true, @"\\nas4\assets1\_Movies\A (2022)\A (2022) {tmdb-1} [DVD-576p][DTS-ES 3.0][VP9].mkv")]
    [InlineData(17, 31, TestRegexType.Test, true, @"\\nas4\assets1\_Movies\A (2022)\A (2022) {tmdb-1} [DVD-576p][DTS HD 3.1][VC1].mkv")]
    [InlineData(17, 32, TestRegexType.Test, true, @"\\nas4\assets1\_Movies\A (2022)\A (2022) {tmdb-1} [DVD-576p][DTS-HD MA 4.0][MPEG2].mkv")]
    [InlineData(17, 33, TestRegexType.Test, true, @"\\nas4\assets1\_Movies\A (2022)\A (2022) {tmdb-1} [DVD-576p][DTS-HD HRA 5.0][VP9].mkv")]
    [InlineData(17, 34, TestRegexType.Test, true, @"\\nas4\assets1\_Movies\A (2022)\A (2022) {tmdb-1} [DVD-576p][TrueHD Atmos 5.1][VP9].mkv")]
    [InlineData(17, 35, TestRegexType.Test, true, @"\\nas4\assets1\_Movies\A (2022)\A (2022) {tmdb-1} [DVD-576p][EAC3 6.0][VP9].mkv")]
    [InlineData(17, 36, TestRegexType.Test, true, @"\\nas4\assets1\_Movies\A (2022)\A (2022) {tmdb-1} [DVD-576p][EAC3 Atmos 6.1][VP9].mkv")]
    [InlineData(17, 37, TestRegexType.Test, true, @"\\nas4\assets1\_Movies\A (2022)\A (2022) {tmdb-1} [DVD-576p][AC3 7.1][VP9].mkv")]
    [InlineData(17, 38, TestRegexType.Test, true, @"\\nas4\assets1\_Movies\A (2022)\A (2022) {tmdb-1} [DVD-576p][FLAC 8.0][VP9].mkv")]
    [InlineData(17, 39, TestRegexType.Test, true, @"\\nas4\assets1\_Movies\A (2022)\A (2022) {tmdb-1} [DVD-576p][PCM 2.0][VP9].mkv")]
    [InlineData(17, 40, TestRegexType.Test, true, @"\\nas4\assets1\_Movies\A (2022)\A (2022) {tmdb-1} [DVD-576p][MP3 2.0][VP9].mkv")]
    [InlineData(17, 41, TestRegexType.Test, true, @"\\nas4\assets1\_Movies\A (2022)\A (2022) {tmdb-1} [DVD-576p][AAC 2.0][VP9].mkv")]
    [InlineData(17, 42, TestRegexType.Test, true, @"\\nas4\assets1\_Movies\A (2022)\A (2022) {tmdb-1} [DVD-576p][TrueHD 2.0][VP9].mkv")]
    [InlineData(17, 43, TestRegexType.Test, true, @"\\nas4\assets1\_Movies\A (2022)\A (2022) {tmdb-1} [DVD-576p][AVC 2.0][VP9].mkv")]
    [InlineData(17, 44, TestRegexType.Test, true, @"\\nas4\assets1\_Movies\A (2022)\A (2022) {tmdb-1} [DVD-576p][Opus 2.0][VP9].mkv")]
    [InlineData(17, 45, TestRegexType.Test, false, @"\\nas4\assets1\_Movies\A (2022)\A (2022) {tmdb-1} [DVD-576p][DT 2.0][VP9].mkv")]
    [InlineData(17, 46, TestRegexType.Test, false, @"\\nas4\assets1\_Movies\A (2022)\A (2022) {tmdb-1} [DVD-576p][DTS- 2.0][VP9].mkv")]
    [InlineData(17, 48, TestRegexType.Test, false, @"\\nas4\assets1\_Movies\A (2022)\A (2022) {tmdb-1} [DVD-576p][DTSES 2.0][VP9].mkv")]
    [InlineData(17, 49, TestRegexType.Test, false, @"\\nas4\assets1\_Movies\A (2022)\A (2022) {tmdb-1} [DVD-576p][DTSHD 2.0][VP9].mkv")]
    [InlineData(17, 50, TestRegexType.Test, false, @"\\nas4\assets1\_Movies\A (2022)\A (2022) {tmdb-1} [DVD-576p][DTS-HDMA 2.0][VP9].mkv")]
    [InlineData(17, 51, TestRegexType.Test, false, @"\\nas4\assets1\_Movies\A (2022)\A (2022) {tmdb-1} [DVD-576p][DTSHD-HRA 2.0][VP9].mkv")]
    [InlineData(17, 52, TestRegexType.Test, false, @"\\nas4\assets1\_Movies\A (2022)\A (2022) {tmdb-1} [DVD-576p][TrueHD-Atmos 2.0][VP9].mkv")]
    [InlineData(17, 53, TestRegexType.Test, false, @"\\nas4\assets1\_Movies\A (2022)\A (2022) {tmdb-1} [DVD-576p][EAC 2.0][VP9].mkv")]
    [InlineData(17, 54, TestRegexType.Test, false, @"\\nas4\assets1\_Movies\A (2022)\A (2022) {tmdb-1} [DVD-576p][EAC3-Atmos 2.0][VP9].mkv")]
    [InlineData(17, 55, TestRegexType.Test, false, @"\\nas4\assets1\_Movies\A (2022)\A (2022) {tmdb-1} [DVD-576p][AC3- 2.0][VP9].mkv")]
    [InlineData(17, 56, TestRegexType.Test, false, @"\\nas4\assets1\_Movies\A (2022)\A (2022) {tmdb-1} [DVD-576p][FLAC+ 2.0][VP9].mkv")]
    [InlineData(17, 57, TestRegexType.Test, false, @"\\nas4\assets1\_Movies\A (2022)\A (2022) {tmdb-1} [DVD-576p][PCM2.0][VP9].mkv")]
    [InlineData(17, 58, TestRegexType.Test, false, @"\\nas4\assets1\_Movies\A (2022)\A (2022) {tmdb-1} [DVD-576p][MP4 2.0][VP9].mkv")]
    [InlineData(17, 59, TestRegexType.Test, false, @"\\nas4\assets1\_Movies\A (2022)\A (2022) {tmdb-1} [DVD-576p][AAC2 2.0][VP9].mkv")]
    [InlineData(17, 60, TestRegexType.Test, false, @"\\nas4\assets1\_Movies\A (2022)\A (2022) {tmdb-1} [DVD-576p][TrueHD2 2.0][VP9].mkv")]
    [InlineData(17, 61, TestRegexType.Test, false, @"\\nas4\assets1\_Movies\A (2022)\A (2022) {tmdb-1} [DVD-576p][AVC6 2.0][VP9].mkv")]
    [InlineData(17, 62, TestRegexType.Test, false, @"\\nas4\assets1\_Movies\A (2022)\A (2022) {tmdb-1} [DVD-576p][Opus9 2.0][VP9].mkv")]
    [InlineData(17, 63, TestRegexType.Test, false, @"\\nas4\assets1\_Movies\A (2023)\A (2022) {tmdb-1} [DVD-576p][EAC3 6.0][VP9].mkv")]
    [InlineData(17, 64, TestRegexType.Test, true, @"\\nas4\assets1\_Movies\Blood Brothers (1989)\Blood Brothers (1989) [SDTV-576p][AAC 2.0][h264].mp4")]
    [InlineData(17, 65, TestRegexType.Test, true,
        @"\\nas2\assets2\_Movies\A Murder of Crows (1999)\A Murder of Crows (1999) {tmdb-17263} [HDTV-1080p][MP3 2.0][MPEG4].avi")]
    [InlineData(17, 66, TestRegexType.Test, true,
        @"\\nas2\assets2\_Movies\12 Angry Men (1957)-other\12 Angry Men (1957)-other {tmdb-389} {edition-CRITERION COLLECTION} [Remux-1080p][PCM 1.0][h264].mkv")]
    [InlineData(17, 67, TestRegexType.Test, true, @"\\nas5\assets3\_Movies\Seven Samurai (1954)\Seven Samurai (1954) {tmdb-346} [Remux-2160p][HLG][FLAC 1.0][h265].mkv")]
    [InlineData(17, 68, TestRegexType.Test, true, @"\\nas1\assets4\_Movies\Aliens (1986)\Aliens (1986) {tmdb-679} [Remux-2160p][DV][AC3 5.1][h265].mkv")]
    [InlineData(17, 69, TestRegexType.Test, true,
        @"X:\_Movies (non-tmdb)\The Lord of the Rings (2003)\The Lord of the Rings (2003) {tmdb-120} {edition-THE COMPLETE EXTENDED} [Remux-2160p][TrueHD Atmos 7.1][h265].mkv")]
    [InlineData(17, 70, TestRegexType.Test, true, @"X:\_Movies (non-tmdb)\Blood Brothers (1989)\Example 1-behindthescenes.mkv")]
    [InlineData(17, 71, TestRegexType.Test, false, @"X:\_Movies (non-tmdb)\Blood Brothers (1989)\Example 1-trailertest.mkv")]
    [InlineData(17, 72, TestRegexType.Discovery, false, @"Z:\_TV\Tom and Jerry {tvdb-72860}\Season 1940\Tom and Jerry s1940e01 Puss Gets The Boot [SDTV].mkv")]
    [InlineData(17, 73, TestRegexType.Discovery, true,
        @"X:\_Movies\12 Angry Men (1957)\12 Angry Men (1957) {tmdb-389} {edition-BLURAY} [Remux-1080p][DTS-HD MA 1.0][h264].mkv")]
    [InlineData(17, 74, TestRegexType.Discovery, true, @"X:\_Movies (non-tmdb)\Blood Brothers (1989)\Example 1-scene.mkv")]
    [InlineData(17, 75, TestRegexType.Test, true,
        @"X:\_Movies (non-tmdb)\The Lord of the Rings (2003)\The Lord of the Rings (2003) {tmdb-120} {edition-THE COMPLETE EXTENDED} [Remux-2160p][DV HDR10][TrueHD Atmos 7.1][h265].mkv")]
    [InlineData(17, 76, TestRegexType.Test, false,
        @"X:\_Movies (non-tmdb)\The (2003)\The (2003) {tmdb-120}  [Remux-2160p][DV HDR10][TrueHD Atmos 7.1][h265]-TdarrCacheFile-fwefefwe.mkv")]
    [InlineData(18, 1, TestRegexType.Discovery, true, @"Z:\_TV\Tom and Jerry {tvdb-72860}\Season 1940\Tom and Jerry s1940e01 Puss Gets The Boot [SDTV].mkv")]
    [InlineData(18, 2, TestRegexType.Discovery, true,
        @"Z:\_TV (non-tvdb)\The Queen's Christmas Broadcast\Season 1\The Queen's Christmas Broadcast s01e2015 2015 [HDTV-720p].mp4")]
    [InlineData(18, 3, TestRegexType.Discovery, true, @"X:\_Movies\Cheers {tvdb-77623}\Season 11\Cheers s11e26-e28 One for the Road [SDTV][MP3 2.0][XviD].avi")]
    [InlineData(18, 4, TestRegexType.Test, false,
        @"\\nas4\assets4\_TV\Paw Patrol {tvdb-272472}\Season 7\Paw Patrol s07e01-e04 This is a long long long long long long long long long long long  long long long long path but is it long enough yet I think so but only if we make it this far [HDTV-1080p][AAC 2.0][x264].mkv")]
    [InlineData(18, 5, TestRegexType.Test, true, @"\\nas4\assets1\_TV\Curfew {tvdb-79556}\Season 1\Curfew s01e01 Episode 1 [HDTV-720p][DV][Vorbis 6.0][x264].mkv")]
    [InlineData(19, 1, TestRegexType.Discovery, true, @"Z:\_TV\Tom and Jerry {tvdb-72860}\Season 1940\Tom and Jerry s1940e01 Puss Gets The Boot [SDTV].mkv")]
    [InlineData(19, 2, TestRegexType.Discovery, true,
        @"Z:\_TV (non-tvdb)\The Queen's Christmas Broadcast\Season 1\The Queen's Christmas Broadcast s01e2015 2015 [HDTV-720p].mp4")]
    [InlineData(19, 3, TestRegexType.Test, true, @"X:\_TV\Cheers {tvdb-77623}\Season 11\Cheers s11e26-e28 One for the Road [SDTV-576p][MP3 2.0][XviD].avi")]
    [InlineData(19, 4, TestRegexType.Test, true, @"X:\_TV\Cheers {tvdb-77623}\Season 11\Cheers s11e26e27 One for the Road [SDTV-576p][MP3 2.0][XviD].avi")]
    [InlineData(19, 5, TestRegexType.Test, false, @"X:\_TV\Cheers {tvdb-77623}\Season 11\Cheers s11.e26-e28 One for the Road [SDTV-576p][MP3 2.0][XviD].avi")]
    [InlineData(19, 6, TestRegexType.Test, false, @"X:\_TV\Cheers {tvdb-77623}\Season 11\Cheers s11e26f27 One for the Road [SDTV-576p][MP3 2.0][XviD].avi")]
    [InlineData(19, 7, TestRegexType.Test, true,
        @"X:\_TV\The Late Late Show with James Corden {tvdb-292421}\Season 1\The Late Late Show with James Corden 2015-03-23 [HDTV-720p][MP3 2.0][XviD].mkv")]
    [InlineData(19, 8, TestRegexType.Test, true, @"X:\_TV (non-tmdb)\Blood Brothers\Example 1-behindthescenes.mkv")]
    [InlineData(19, 9, TestRegexType.Test, false,
        @"X:\_TV\The Late Late Show with James Corden {tvdb-292421}\Season 1\The Late Late Show with James Corden 20150323 [HDTV-720p][MP3 2.0][XviD].mkv")]
    [InlineData(19, 10, TestRegexType.Test, true,
        @"Z:\_TV\Tom and Jerry {tvdb-72860}\Season 1940\Tom and Jerry s1940e01 Puss Gets The Boot [SDTV-576p][MP3 2.0][XviD].mkv")]
    [InlineData(19, 11, TestRegexType.Test, true, @"X:\_TV\Long Way Round {tvdb-77623}\Season 11\Long Way Round s01e08 [DVD-576p][MP3 2.0][XviD].mkv")]
    [InlineData(19, 12, TestRegexType.Test, true, @"X:\_TV\Long Way Round {tvdb-77623}\Season 11\Long Way Round s01e108e109 [DVD-576p][MP3 2.0][XviD].mkv")]
    [InlineData(19, 13, TestRegexType.Test, true, @"X:\_TV\Long Way Round {tvdb-77623}\Season 11\Long Way Round s01e08-e12 [DVD-576p][MP3 2.0][XviD].mkv")]
    [InlineData(19, 14, TestRegexType.Test, true,
        @"Z:\_TV\The Queen's Christmas Broadcast {tvdb-359422}\Season 1\The Queen's Christmas Broadcast s01e2015 2015 [HDTV-720p][MP3 2.0][XviD].mp4")]
    [InlineData(19, 15, TestRegexType.Test, true,
        @"Z:\_TV\Tom and Jerry {tvdb-72860}\Season 1940\Tom and Jerry s1940e01 Puss Gets The Boot [SDTV-576p][MP3 2.0][XviD].mkv")]
    [InlineData(19, 16, TestRegexType.Test, false, @"Z:\_TV\Tom and Jerry {tvdb-72860}\Season 1940\Tom and Jerry s1940e2015 Puss Gets The Boot [SDTV-576p][DV][XviD].mkv")]
    [InlineData(19, 17, TestRegexType.Discovery, false, @"X:\_Movies\Chernobyl {tvdb-360893}\Season 1\Chernobyl s01e01 TBA [Bluray-2160p Remux][MP3 2.0][XviD].mkv")]
    [InlineData(19, 18, TestRegexType.Test, true,
        @"\\nas1\assets1\_TV\Charlie's Angels {tvdb-77170}\Season 5\Charlie's Angels s05e12 Chorus Line Angels [Bluray-1080p Remux][MP3 2.0][XviD].mkv")]
    [InlineData(19, 19, TestRegexType.Test, true,
        @"\\nas5\assets4\_TV\3rd Rock from the Sun {tvdb-72389}\Specials\3rd Rock from the Sun s00e02 Behind the Scenes [DVD-576p][MP3 2.0][XviD].avi")]
    [InlineData(19, 20, TestRegexType.Test, false, @"\\nas2\assets4\_TV\Episodes {tvdb-123581}\Season 3\Episodes s03e08 Episode 308 [Raw-HD][MP3 2.0][XviD].ts")]
    [InlineData(19, 21, TestRegexType.Test, false,
        @"\\nas2\assets1\_TV\The Grand Tour {tvdb-314087}\Season 5\The Grand Tour (2016) s05e01 A Scandi Flick [WEBDL-720p][MP3 2.0][XviD].mkv")]
    [InlineData(19, 22, TestRegexType.Test, false,
        @"\\nas5\assets4\_TV\3rd Rock from the Sun {tvdb-72389}\Specials\3rd Rock from the Sun s00e02 Behind the Scenes [DVD-576p][MP3 2.0][XviD].ts")]
    [InlineData(19, 23, TestRegexType.Test, false,
        @"K:\_TV\American Horror Story {tvdb-250487}\Season 1\American Horror Story s01e02 Home Invasion [HDTV-720p][DTS 5.1][x264].mkv")]
    [InlineData(19, 24, TestRegexType.Test, true,
        @"K:\_TV\American Horror Story {tvdb-250487}\Season 1\American Horror Story s01e02 Home Invasion [HDTV-720p][DTS 5.1][h264].mkv")]
    [InlineData(19, 25, TestRegexType.Test, true,
        @"K:\_TV\American Horror Story {tvdb-250487}\Season 1\American Horror Story s01e02 Home Invasion [HDTV-720p][DTS 5.1][h264].mkv")]
    [InlineData(19, 26, TestRegexType.Test, true,
        @"K:\_TV\American Horror Story {tvdb-250487}\Season 1\American Horror Story s01e02 Home Invasion [HDTV-720p][DV HDR10][DTS HD 5.1][h264].mkv")]
    [InlineData(19, 27, TestRegexType.Test, true,
        @"K:\_TV\American Horror Story {tvdb-250487}\Season 1\American Horror Story s01e02 Home Invasion [HDTV-720p][DV][DTS HD 5.1][h264].mkv")]
    [InlineData(19, 28, TestRegexType.Test, false,
        @"\\nas1\assets1\_TV\Ben and Holly's Little Kingdom {tvdb-136941}\Season 2\Ben and Holly's Little Kingdom s02e15 Gaston To The Rescue [DVD][AAC 5.1][].mp4")]
    [InlineData(19, 29, TestRegexType.Test, true,
        @"\\nas4\assets1\_TV\8 Out of 10 Cats {tvdb-79556}\Season 11\8 Out of 10 Cats s11e02 Patsy Palmer, Jack Whitehall, Krishnan Guru-Murthy, Joe Wilkinson [HDTV-720p][MP2 2.0][h264].mkv")]
    [InlineData(19, 30, TestRegexType.Test, true, @"\\nas4\assets1\_TV\Curfew {tvdb-79556}\Season 1\Curfew s01e01 Episode 1 [HDTV-720p][Vorbis 6.0][h264].mkv")]
    [InlineData(19, 31, TestRegexType.Test, false,
        @"\\nas4\assets1\_TV\Curfew {tvdb-79556}\Season 1\Curfew s01e01 Episode 1 [HDTV-720p][Vorbis 6.0][h264]-TdarrCacheFile-fwefefwf.mkv")]
    [InlineData(19, 32, TestRegexType.Test, true,
        @"\\nas5\assets4\_TV\Prehistoric Planet (2022) {tvdb-418505}\Season 2\Prehistoric Planet (2022) s02e01 Islands [WEBDL-2160p][DV HDR10Plus][EAC3 Atmos 5.1][h265].mkv")]
    [InlineData(19, 33, TestRegexType.Test, true,
        @"\\nas5\assets4\_TV\Prehistoric Planet (2022) {tvdb-418505} {edition-DVD}\Season 2\Prehistoric Planet (2022) s02e01 Islands [WEBDL-2160p][DV HDR10Plus][EAC3 Atmos 5.1][h265].mkv")]
    [InlineData(20, 1, TestRegexType.Discovery, true, @"Z:\_TV\Tom and Jerry {tvdb-72860}\Season 1940\Tom and Jerry s1940e01 Puss Gets The Boot [SDTV].mkv")]
    [InlineData(20, 2, TestRegexType.Discovery, true,
        @"Z:\_TV (non-tvdb)\The Queen's Christmas Broadcast\Season 1\The Queen's Christmas Broadcast s01e2015 2015 [HDTV-720p].mp4")]
    [InlineData(20, 3, TestRegexType.Discovery, true, @"X:\_Movies\Cheers {tvdb-77623}\Season 11\Cheers s11e26-e28 One for the Road [SDTV][MP3 2.0][XviD].avi")]
    [InlineData(20, 4, TestRegexType.Test, true, @"\\nas4\assets1\_TV\Curfew {tvdb-79556}\Season 1\Curfew s01e01 Episode 1 [HDTV-720p][Vorbis 6.0][x264].mkv")]
    [InlineData(20, 5, TestRegexType.Test, false, @"\\nas4\assets1\_TV\Curfew {tvdb-79556}\Season 1\Curfew s01e01 Episode 1 [HDTV-720p][DV][Vorbis 6.0][x264].mkv")]
    [InlineData(20, 6, TestRegexType.Test, true, @"\\nas4\assets2\_Movies\Accused (2023)\Accused (2023) {tmdb-912974} [WEBDL-1080p][EAC3 5.1][x264].es.srt")]
    [InlineData(21, 1, TestRegexType.Discovery, true, @"Z:\_TV\Tom and Jerry {tvdb-72860}\Season 1940\Tom and Jerry s1940e01 Puss Gets The Boot [SDTV].en.srt")]
    [InlineData(21, 2, TestRegexType.Discovery, true,
        @"Z:\_TV (non-tvdb)\The Queen's Christmas Broadcast\Season 1\The Queen's Christmas Broadcast s01e2015 2015 [HDTV-720p].es.srt")]
    [InlineData(21, 3, TestRegexType.Discovery, true, @"X:\_Movies\Cheers {tvdb-77623}\Season 11\Cheers s11e26-e28 One for the Road [SDTV][MP3 2.0][XviD].en.srt")]
    [InlineData(21, 4, TestRegexType.Test, true, @"X:\_Movies\Cheers {tvdb-77623}\Season 11\Cheers s11e26-e28 One for the Road [SDTV][MP3 2.0][XviD].en.srt")]
    [InlineData(21, 5, TestRegexType.Test, false, @"X:\_Movies\Cheers {tvdb-77623}\Season 11\Cheers s11e26-e28 One for the Road [SDTV][MP3 2.0][XviD]._eng.srt")]
    [InlineData(21, 6, TestRegexType.Test, false, @"X:\_Movies\Cheers {tvdb-77623}\Season 11\Cheers s11e26-e28 One for the Road [SDTV][MP3 2.0][XviD]._dut.srt")]
    [InlineData(21, 7, TestRegexType.Test, false, @"X:\_Movies\Cheers {tvdb-77623}\Season 11\Cheers s11e26-e28 One for the Road [SDTV][MP3 2.0][XviD]._2.srt")]
    [Theory]
    [SuppressMessage("ReSharper", "StringLiteralTypo")]
    public void FileRuleMainTests(int ruleNumber, int testNumber, TestRegexType regexTestType, bool expectedResult, string testPath)
    {
        var rule = _mediaBackup.Config.FileRules.SingleOrDefault(p => p.Number == ruleNumber);
        Assert.NotNull(rule);
        var regEx = regexTestType == TestRegexType.Test ? rule.FileTestRegEx : rule.FileDiscoveryRegEx;

        if (expectedResult)
            Assert.True(testPath.IsMatch(regEx), $"Test {testNumber} of Rule {ruleNumber} {rule.Message} for {testPath}");
        else
            Assert.False(testPath.IsMatch(regEx), $"Test {testNumber} of Rule {ruleNumber} {rule.Message} for {testPath}");
    }

    [Fact]
    public void FileRuleTests3()
    {
        var rule1 = _mediaBackup.Config.FileRules.SingleOrDefault(static p => p.Number == 1);
        var rule2 = _mediaBackup.Config.FileRules.SingleOrDefault(static p => p.Number == 2);
        Assert.NotNull(rule1);
        Assert.NotNull(rule2);
        Assert.NotEqual(rule1, rule2);
        Assert.False(rule1.Equals(null));
        object obj = rule1;
        Assert.False(obj.Equals(rule2));
        Assert.StartsWith("Rule 1 TV files must contain {t", rule1.ToString());
        Assert.NotEqual(0, rule1.GetHashCode());
    }

    [Fact]
    public void FileRuleTests2()
    {
        var path1 = Path.Combine(Path.GetTempPath(), "FileMove");
        if (Directory.Exists(path1)) _ = Utils.Directory.Delete(path1, true);
        var file1 = Path.Combine(path1, "test1.txt");
        Utils.Directory.EnsurePath(path1);
        Utils.File.Create(file1);
        _ = Assert.Throws<ArgumentNullException>(static () => Rules.Load(null));
        _ = Assert.Throws<XmlException>(() => Rules.Load(file1));

        // Delete the folders we created
        if (Directory.Exists(path1)) _ = Utils.Directory.Delete(path1, true);
    }
}