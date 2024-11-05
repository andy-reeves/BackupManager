// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="SubtitlesBackupFile.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

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
        OriginalPath = path;
        Extension = Path.GetExtension(path);
        var fileName = Path.GetFileName(path);
        SubtitlesExtension = Utils.GetSubtitlesExtension(path);
        FullDirectory = Path.GetDirectoryName(path);
        IsValidFileName = new Regex(FileNameRegex).IsMatch(fileName);
        if (IsValidFileName) IsValidFileName = ParseInfoFromFileName(fileName);
        if (!FullDirectory.HasValue()) return;

        // ReSharper disable once AssignNullToNotNullAttribute
        IsValidDirectoryName = new Regex(DirectoryRegex).IsMatch(FullDirectory);
        if (IsValidDirectoryName) IsValidDirectoryName = ParseInfoFromDirectory(FullDirectory);
    }

    protected override string FileNameRegex => @"^(.*)\.(e[ns])(?:(?:\.)(hi))?\.srt$";

    protected override string DirectoryRegex => @"^.*\\_(?:Movies|Comedy|Concerts|TV)(?:\s\(non-t[mv]db\))?\\(.*)((\((\d{4})\)(-other)?)|(\s{t(m|v)db-\d{1,7}?}\\(Season\s\d+|Specials))).*$";

    public string Language { get; private set; }

    public bool HearingImpaired { get; private set; }

    public object SubtitlesExtension { get; }

    private bool ParseInfoFromDirectory(string directoryPath)
    {
        FullDirectory = directoryPath;
        return Regex.Match(directoryPath, DirectoryRegex).Success;
    }

    public override string GetFileName()
    {
        var hearingImpairedText = HearingImpaired ? ".hi" : string.Empty;
        return $"{Title}.{Language}{hearingImpairedText}{Extension}";
    }

    public bool RefreshMediaInfo(ExtendedBackupFileBase video)
    {
        if (video == null) return false;

        Title = video.GetFileNameWithoutExtension();
        return true;
    }

    private bool ParseInfoFromFileName(string filename)
    {
        const int languageGroup = 2;
        const int hearingImpairedGroup = 3;
        const int title = 1;
        var match = Regex.Match(filename, FileNameRegex);
        if (!match.Success) return false;

        Title = match.Groups[title].Value;
        Language = match.Groups[languageGroup].Value;
        HearingImpaired = match.Groups[hearingImpairedGroup].Value == "hi";
        Extension = ".srt";
        return true;
    }

    public override string GetFullName()
    {
        return FullDirectory.HasValue() ? Path.Combine(FullDirectory, GetFileName()) : GetFileName();
    }

    public override bool RefreshMediaInfo()
    {
        if (FullDirectory.HasNoValue()) return false;

        var files = Utils.File.GetFiles(FullDirectory, new CancellationToken());
        var videoFiles = files.Where(static f => Utils.File.IsVideo(f) && !Utils.File.IsSpecialFeature(f)).ToArray();

        switch (videoFiles.Length)
        {
            case 0:
                Utils.Trace("Inside RefreshMediaInfo but no video file");
                break;
            case > 1:
            {
                Utils.Trace("Inside RefreshMediaInfo more than 1 video file");
                var shortTitle = Title.SubstringBeforeIgnoreCase("[").Trim();

                foreach (var video in videoFiles.Where(videoFile => Path.GetFileName(videoFile).StartsWithIgnoreCase(shortTitle)).Select(static videoFileToUse => Utils.MediaHelper.ExtendedBackupFileBase(videoFileToUse)))
                {
                    _ = RefreshMediaInfo(video);
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