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
        var directoryPath = Path.GetDirectoryName(path);
        var regex = new Regex(FileNameRegex);
        if (!regex.IsMatch(fileName)) return;

        IsValid = ParseInfoFromFileName(fileName);
        if (IsValid && directoryPath.HasValue()) IsValid = ParseInfoFromDirectory(directoryPath);
    }

    protected override string FileNameRegex => @"^(.*)\.(e[ns](?:\.hi)?)\.srt$";

    protected override string DirectoryRegex => @"^.*\\_(?:Movies|Comedy|Concerts)(?:\s\(non-t[mv]db\))?\\(.*)\((\d{4})\)(-other)?.*$";

    public string Subtitles { get; private set; }

    private MovieBackupFile GetMovie()
    {
        if (FullDirectory.HasNoValue()) return null;

        var files = Utils.GetFiles(FullDirectory, new CancellationToken());
        var videoFiles = files.Where(static f => Utils.FileIsVideo(f) && !Utils.FileIsSpecialFeature(f)).ToArray();

        // if more than 1 movie file in this folder, we can't pick one
        return videoFiles.Length is 0 or > 1 ? null : new MovieBackupFile(videoFiles[0]);
    }

    private bool ParseInfoFromDirectory(string directoryPath)
    {
        FullDirectory = directoryPath;
        return Regex.Match(directoryPath, DirectoryRegex).Success;
    }

    public override string GetFileName()
    {
        return $"{Title}.{Subtitles}{Extension}";
    }

    public bool RefreshMediaInfo(MovieBackupFile movie)
    {
        if (movie == null) return false;

        Title = movie.GetFileNameWithoutExtension();
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
        var movie = GetMovie();
        _ = RefreshMediaInfo(movie);
        return Validate();
    }
}