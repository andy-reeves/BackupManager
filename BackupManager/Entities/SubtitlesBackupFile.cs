// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="SubtitlesBackupFile.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.RegularExpressions;

using BackupManager.Extensions;

namespace BackupManager.Entities;

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
internal sealed class SubtitlesBackupFile : ExtendedBackupFileBase
{
    private const string FILE_NAME_PATTERN = @"^(.*)\.(e[ns](?:\.hi)?)\.srt$";

    private const string DIRECTORY_ONLY_PATTERN = @"^.*\\_(?:Movies|Comedy|Concerts)(?:\s\(non-t[mv]db\))?\\(.*)\((\d{4})\)(-other)?.*$";

    public string Subtitles { get; private set; }

    public SubtitlesBackupFile(string path)
    {
        string fileName;
        string directoryPath;

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

        Valid = ParseInfoFromFileName(fileName);
        if (!Valid || !directoryPath.HasValue()) return;

        Valid = ParseDirectory(directoryPath);
        if (Valid) Extension = Path.GetExtension(path);
    }

    private bool ParseDirectory(string directoryPath)
    {
        var match = Regex.Match(directoryPath, DIRECTORY_ONLY_PATTERN);
        if (match.Success) FullDirectory = directoryPath;
        return match.Success;
    }

    public override string GetFileName()
    {
        return $"{Title}.{Subtitles}{Extension}";
    }

    public bool RefreshInfo(MovieBackupFile movie)
    {
        Title = movie.GetFileNameWithoutExtension();
        return true;
    }

    private bool ParseInfoFromFileName(string filename)
    {
        const int subtitlesGroup = 2;
        const int title = 1;
        var match = Regex.Match(filename, FILE_NAME_PATTERN);
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
}
