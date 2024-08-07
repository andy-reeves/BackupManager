﻿// --------------------------------------------------------------------------------------------------------------------
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

    protected override string FileNameRegex => @"^(.*)\.(e[ns](?:\.hi)?)\.srt$";

    protected override string DirectoryRegex => @"^.*\\_(?:Movies|Comedy|Concerts|TV)(?:\s\(non-t[mv]db\))?\\(.*)((\((\d{4})\)(-other)?)|(\s{t(m|v)db-\d{1,7}?}\\(Season\s\d+|Specials))).*$";

    public string Subtitles { get; private set; }

    public object SubtitlesExtension { get; }

    private bool ParseInfoFromDirectory(string directoryPath)
    {
        FullDirectory = directoryPath;
        return Regex.Match(directoryPath, DirectoryRegex).Success;
    }

    public override string GetFileName()
    {
        return $"{Title}.{Subtitles}{Extension}";
    }

    public bool RefreshMediaInfo(ExtendedBackupFileBase video)
    {
        if (video == null) return false;

        Title = video.GetFileNameWithoutExtension();
        return true;
    }

    private bool ParseInfoFromFileName(string filename)
    {
        const int subtitlesGroup = 2;
        const int title = 1;
        var match = Regex.Match(filename, FileNameRegex);
        if (!match.Success) return false;

        Title = match.Groups[title].Value;
        Subtitles = match.Groups[subtitlesGroup].Value;
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
        if (videoFiles.Any(file => Path.GetFileName(file).StartsWithIgnoreCase(Title))) return Validate();

        // if more than 1 movie file in this folder, we can't pick one
        if (videoFiles.Length is 0 or > 1) return Validate();

        var video = Utils.MediaHelper.ExtendedBackupFileBase(videoFiles[0]);
        _ = RefreshMediaInfo(video);
        return Validate();
    }
}