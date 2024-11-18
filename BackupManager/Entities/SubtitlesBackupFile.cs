// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="SubtitlesBackupFile.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

using BackupManager.Extensions;

namespace BackupManager.Entities;

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
internal sealed class SubtitlesBackupFile : ExtendedBackupFileBase
{
    public SubtitlesBackupFile(string path)
    {
        if (!path.EndsWithIgnoreCase("srt")) throw new ArgumentException("Subtitles files must end with .srt");

        OriginalPath = path;
        var fileName = Path.GetFileName(path);
        DirectoryName = Path.GetDirectoryName(path);
        IsValidFileName = new Regex(FileNameRegex).IsMatch(fileName);
        if (IsValidFileName) IsValidFileName = ParseInfoFromFileName(fileName);
        if (!DirectoryName.HasValue()) return;

        // ReSharper disable once AssignNullToNotNullAttribute
        IsValidDirectoryName = new Regex(DirectoryRegex).IsMatch(DirectoryName);
        if (IsValidDirectoryName) IsValidDirectoryName = ParseInfoFromDirectory(DirectoryName);
    }

    protected override string FileNameRegex => @"^(.*)(?:(?:\.)(e[ns]))(?:(?:\.)(hi|cc|sdh))?(?:(?:\.)(forced))?\.srt$";

    protected override string DirectoryRegex => @"^.*\\_(?:Movies|Comedy|Concerts|TV)(?:\s\(non-t[mv]db\))?\\(.*)((\((\d{4})\)(-other)?)|(\s{t(m|v)db-\d{1,7}?}\\(Season\s\d+|Specials))).*$";

    public string Language { get; private set; }

    public bool HearingImpaired { get; private set; }

    public string FullPathToVideoFile { get; set; }

    public bool Forced { get; private set; }

    public object SubtitlesExtension { get; private set; }

    private bool ParseInfoFromDirectory(string directoryPath)
    {
        DirectoryName = directoryPath;
        return Regex.Match(directoryPath, DirectoryRegex).Success;
    }

    public override string GetFileName()
    {
        var hearingImpairedText = HearingImpaired ? ".hi" : string.Empty;
        var forcedText = Forced ? ".forced" : string.Empty;
        return $"{Title}.{Language}{hearingImpairedText}{forcedText}{Extension}";
    }

    public bool RefreshMediaInfo(ExtendedBackupFileBase video)
    {
        if (video == null) return false;

        Title = video.GetFileNameWithoutExtension();
        FullPathToVideoFile = video.GetFullName();
        return true;
    }

    private bool ParseInfoFromFileName(string filename)
    {
        const int languageGroup = 2;
        const int hearingImpairedGroup = 3;
        const int forcedGroup = 4;
        const int title = 1;
        var match = Regex.Match(filename, FileNameRegex);
        if (!match.Success) return false;

        Title = match.Groups[title].Value;
        Language = match.Groups[languageGroup].Value;
        HearingImpaired = match.Groups[hearingImpairedGroup].Value != string.Empty;
        Forced = match.Groups[forcedGroup].Value.EqualsIgnoreCase("forced");
        Extension = ".srt";
        SubtitlesExtension = filename.SubstringAfterIgnoreCase(Title);
        return true;
    }

    public override string GetFullName()
    {
        return DirectoryName.HasValue() ? Path.Combine(DirectoryName, GetFileName()) : GetFileName();
    }

    public override bool RefreshMediaInfo()
    {
        if (DirectoryName.HasNoValue()) return false;

        var files = Utils.File.GetFiles(DirectoryName, new CancellationToken());
        var videoFiles = files.Where(static f => Utils.File.IsVideo(f) && !Utils.File.IsSpecialFeature(f)).ToArray();

        switch (videoFiles.Length)
        {
            case 0:
                Utils.Trace("Inside RefreshMediaInfo but no video file");
                break;
            case > 1:
            {
                Utils.Trace("Inside RefreshMediaInfo more than 1 video file");

                // More than 1 video file so we do this:
                // Check for a video file that matches this name (apart from the subtitles extensions) - user that if found
                // If it is not found us the trimmed Title to check that
                var titleWithoutExt = Title;

                foreach (var video in videoFiles.Where(videoFile => Path.GetFileName(videoFile).StartsWithIgnoreCase(titleWithoutExt)).Select(static videoFileToUse => Utils.MediaHelper.ExtendedBackupFileBase(videoFileToUse)))
                {
                    _ = RefreshMediaInfo(video);
                    return Validate();
                }
                var shortTitle = Title.SubstringBeforeIgnoreCase("[").Trim();

                foreach (var video in videoFiles.Where(videoFile => Path.GetFileName(videoFile).StartsWithIgnoreCase(shortTitle)).Select(static videoFileToUse => Utils.MediaHelper.ExtendedBackupFileBase(videoFileToUse)))
                {
                    _ = RefreshMediaInfo(video);
                    return Validate();
                }
                break;
            }
            default:
                Utils.Trace("Inside RefreshMediaInfo 1 video file");
                _ = RefreshMediaInfo(Utils.MediaHelper.ExtendedBackupFileBase(videoFiles[0]));
                break;
        }
        return Validate();
    }
}