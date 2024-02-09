// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="Utils.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Security.Principal;
using System.ServiceProcess;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Schema;

using BackupManager.Entities;
using BackupManager.Extensions;
using BackupManager.Properties;

using HtmlAgilityPack;

using Microsoft.Win32.SafeHandles;

using DirectoryNotFoundException = System.IO.DirectoryNotFoundException;

namespace BackupManager;

/// <summary>
///     Common Utility functions in a static class
/// </summary>
[SuppressMessage("ReSharper", "UnusedMember.Global")]
internal static partial class Utils
{
    private const uint FILE_ACCESS_GENERIC_READ = 0x80000000;

    private const uint FILE_ACCESS_GENERIC_WRITE = 0x40000000;

    private const int FILE_FLAG_BACKUP_SEMANTICS = 0x02000000;

    private const int OPEN_EXISTING = 3;

    /// <summary>
    ///     The end block size.
    /// </summary>
    internal const int END_BLOCK_SIZE = 16 * BYTES_IN_ONE_KILOBYTE; // 16K

    /// <summary>
    ///     The middle block size.
    /// </summary>
    internal const int MIDDLE_BLOCK_SIZE = 16 * BYTES_IN_ONE_KILOBYTE; // 16K

    /// <summary>
    ///     The start block size.
    /// </summary>
    internal const int START_BLOCK_SIZE = 16 * BYTES_IN_ONE_KILOBYTE; // 16K

    /// <summary>
    ///     The number of bytes in one Terabyte. 2^40 bytes.
    /// </summary>

    // ReSharper disable once MemberCanBePrivate.Global
    internal const long BYTES_IN_ONE_TERABYTE = 1_099_511_627_776;

    /// <summary>
    ///     The number of bytes in one Gigabyte. 2^30 bytes.
    /// </summary>
    internal const int BYTES_IN_ONE_GIGABYTE = 1_073_741_824;

    /// <summary>
    ///     The number of bytes in one Megabyte. 2^20 bytes.
    /// </summary>
    internal const int BYTES_IN_ONE_MEGABYTE = 1_048_576;

    /// <summary>
    ///     The number of bytes in one Kilobyte. 2^10 bytes.
    /// </summary>
    internal const int BYTES_IN_ONE_KILOBYTE = 1_024;

    /// <summary>
    ///     The URL of the Pushover messaging service.
    /// </summary>
    private const string PUSHOVER_MESSAGES_URL = "https://api.pushover.net/1/messages.json";

    /// <summary>
    ///     Windows MAX_PATH of 256 characters
    /// </summary>
    internal const int MAX_PATH = 256;

    internal const string IS_DIRECTORY_WRITABLE_GUID = "{A2E236CE-87F1-4942-93B0-31B463142B8D}";

    internal const string SPEED_TEST_GUID = "{AFEF4827-0AA2-4C0E-8D90-9BEFB5DBEA62}";

    /// <summary>
    ///     An array of the special feature prefixes for video files like -featurette, - other, etc.
    /// </summary>
    internal static readonly string[] SpecialFeatures =
    {
        // ReSharper disable once StringLiteralTypo
        "-featurette", "-other", "-interview", "-scene", "-short", "-deleted", "-behindthescenes", "-trailer"
    };

    /// <summary>
    ///     An array of file extensions for video file types like .mkv, .mp4, etc.
    ///     Regex would be (m(kv|p(4|e?g))|ts|avi|(e(n|s)(\.hi)?\.)srt)
    /// </summary>
    private static readonly string[] _videoExtensions = { ".mkv", ".mp4", ".mpeg", ".mpg", ".ts", ".avi" };

    /// <summary>
    ///     An array of allowed Subtitles extensions in video folders like .en.srt, .en.hi.srt, etc.
    /// </summary>

    // ReSharper disable once UnusedMember.Local
    private static readonly string[] _subtitlesExtensions = { ".en.srt", ".es.srt", "en.hi.srt", "es.hi.srt" };

    /// <summary>
    ///     True when we're in a DEBUG build otherwise False
    /// </summary>
    internal static readonly bool InDebugBuild;

    private static readonly object _lock = new();

    private static readonly HttpClient _client = new();

    /// <summary>
    ///     We use this to pad our logging messages
    /// </summary>
    private static int _lengthOfLargestBackupActionEnumNames;

    /// <summary>
    ///     We start a new Process to Copy the files. Then we can safely Kill the process when cancelling is needed
    /// </summary>
    internal static Process CopyProcess;

    /// <summary>
    ///     The path to the Log file
    /// </summary>
    private static readonly string _logFile;

    /// <summary>
    ///     So we can get config values
    /// </summary>
    internal static Config Config;

    private static bool _alreadySendingPushoverMessage;

    private static bool _sentAlertForLowPushoverMessages;

    static Utils()
    {
#if DEBUG
        InDebugBuild = true;
#else
        InDebugBuild = false;
#endif
        _logFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            InDebugBuild ? "BackupManager_Debug.log" : "BackupManager.log");
    }

    /// <summary>
    ///     The number of Pushover messages remaining this month
    /// </summary>
    internal static int PushoverMessagesRemaining { get; private set; }

    public static MediaBackup MediaBackup { get; internal set; }

    [LibraryImport("kernel32.dll", EntryPoint = "GetDiskFreeSpaceExW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetDiskFreeSpaceEx(string lpDirectoryName, out long lpFreeBytesAvailable, out long lpTotalNumberOfBytes,
        out long lpTotalNumberOfFreeBytes);

    [LibraryImport("kernel32.dll", EntryPoint = "CreateFileW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial SafeFileHandle CreateFile(string fileName, uint dwDesiredAccess, FileShare dwShareMode, IntPtr securityAttrsMustBeZero,
        FileMode dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFileMustBeZero);

    [LibraryImport("kernel32.dll", EntryPoint = "SetFileTime", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetFileTime(SafeFileHandle hFile, IntPtr lpCreationTimeUnused, IntPtr lpLastAccessTimeUnused,
        ref long lpLastWriteTime);

    /// <summary>
    ///     This file has a hash of 098f6bcd4621d373cade4e832627b4f6 and length of 4 containing the text 'test'
    /// </summary>
    /// <param name="filePath"></param>
    internal static void CreateFile(string filePath)
    {
        EnsureDirectoriesForFilePath(filePath);
        File.AppendAllText(filePath, "test");
    }

    internal static void OpenLogFile()
    {
        using var process = new Process();
        process.StartInfo.FileName = "cmd";
        var notePadPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "notepad.exe");
        process.StartInfo.Arguments = $"/c start /max \"{notePadPath}\" \"{_logFile}\"";
        _ = process.Start();
    }

    public static void CopyDirectory(string sourceDirectory, string targetDirectory)
    {
        ArgumentException.ThrowIfNullOrEmpty(sourceDirectory);
        ArgumentException.ThrowIfNullOrEmpty(targetDirectory);
        if (!Directory.Exists(sourceDirectory)) throw new DirectoryNotFoundException();
        if (Directory.Exists(targetDirectory)) throw new NotSupportedException("Target Directory exists");

        var diSource = new DirectoryInfo(sourceDirectory);
        var diTarget = new DirectoryInfo(targetDirectory);
        CopyAllFiles(diSource, diTarget);
    }

    private static void CopyAllFiles(DirectoryInfo source, DirectoryInfo target)
    {
        _ = Directory.CreateDirectory(target.FullName);

        foreach (var fi in source.GetFiles())
        {
            _ = fi.CopyTo(Path.Combine(target.FullName, fi.Name), true);
        }

        foreach (var diSourceSubDir in source.GetDirectories())
        {
            var nextTargetSubDir = target.CreateSubdirectory(diSourceSubDir.Name);
            CopyAllFiles(diSourceSubDir, nextTargetSubDir);
        }
    }

    internal static void BackupLogFile(CancellationToken ct)
    {
        var timeLog = DateTime.Now.ToString("yy-MM-dd-HH-mm-ss");

        // ReSharper disable once HeuristicUnreachableCode
#pragma warning disable CS0162 // Unreachable code detected
        var suffix = InDebugBuild ? "_Debug" : string.Empty;
#pragma warning restore CS0162 // Unreachable code detected

        var destLogFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "BackupManager_Backups",
            $"BackupManager{suffix}_{timeLog}.log");
        if (File.Exists(_logFile)) _ = FileMove(_logFile, destLogFile);

        var traceFiles = GetFiles(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "*BackupManager_Trace.log",
            SearchOption.TopDirectoryOnly, ct);
        if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();

        foreach (var file in traceFiles)
        {
            var destFileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "BackupManager_Backups",
                $"{new FileInfo(file).Name}_{timeLog}.log");

            try
            {
                _ = FileMove(file, destFileName);
            }
            catch (IOException) { }
        }
    }

    private static string GitHubVersionNumberParser(string versionUrl, string startsWith, string splitOn, int indexToReturn)
    {
        HttpClient client = new();
        var task = Task.Run(() => client.GetStringAsync(versionUrl));
        task.Wait();
        var response = task.Result;
        var lines = response.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

        return lines.Where(line => line.Trim().StartsWithIgnoreCase(startsWith))
            .Select(line => line.Split(splitOn)[indexToReturn].Replace("'", "").Replace("\"", "").Trim()).FirstOrDefault();
    }

    [SuppressMessage("ReSharper", "StringLiteralTypo")]
    internal static string GetLatestApplicationVersionNumber(ApplicationType applicationTypeName, string branchName = "master")
    {
        if (!Enum.IsDefined(applicationTypeName))
            throw new ArgumentOutOfRangeException(nameof(applicationTypeName), Resources.NotValidApplicationName);

        try
        {
            HttpClient client = new();
            var parameters = string.Empty;

            if (applicationTypeName == ApplicationType.PlexPass)
            {
                parameters = "?channel=plexpass";
                client.DefaultRequestHeaders.Add("x-plex-token", Config.PlexToken);
            }

            switch (applicationTypeName)
            {
                case ApplicationType.Plex:
                case ApplicationType.PlexPass:
                    var task = Task.Run(() => client.GetStringAsync("https://plex.tv/api/downloads/5.json" + parameters));
                    task.Wait();
                    var response = task.Result;
                    var node = JsonNode.Parse(response);
                    return node?["computer"]?["Windows"]?["version"]?.ToString().SubstringBefore('-');
                case ApplicationType.SABnzbd:
                    return GitHubVersionNumberParser($"https://raw.githubusercontent.com/sabnzbd/sabnzbd/{branchName}/sabnzbd/version.py",
                        "__version__", "=", 1);
                case ApplicationType.Sonarr:
                    // For up to v3 we did this
                    // GitHubVersionNumberParser($"https://raw.githubusercontent.com/Sonarr/Sonarr/{branchName}/version.sh", "packageVersion=", "=", 1);

                    // for v4 this
                    var doc = new HtmlWeb().Load("https://github.com/Sonarr/Sonarr/releases/latest");
                    return doc.DocumentNode.SelectNodes("//html/head/title")[0].InnerText.Split(" ")[1];
                case ApplicationType.Radarr:
                    return GitHubVersionNumberParser($"https://raw.githubusercontent.com/Radarr/Radarr/{branchName}/azure-pipelines.yml",
                        "majorVersion:", ":", 1);
                case ApplicationType.Prowlarr:
                    return GitHubVersionNumberParser($"https://raw.githubusercontent.com/Prowlarr/Prowlarr/{branchName}/azure-pipelines.yml",
                        "majorVersion:", ":", 1);
                case ApplicationType.Bazarr:
                    return GitHubVersionNumberParser(
                        $"https://raw.githubusercontent.com/morpheus65535/bazarr/{branchName}/libs/requests_oauthlib/__init__.py", "__version__",
                        "=", 1);

                // ReSharper disable once RedundantEnumCaseLabelForDefaultSection
                case ApplicationType.Unknown:
                default:
                    throw new ArgumentOutOfRangeException(nameof(applicationTypeName), applicationTypeName, null);
            }
        }
        catch
        {
            return null;
        }
    }

    internal static bool EventHandlerHasDelegate(object classInstance, string eventName)
    {
        var classType = classInstance.GetType();
        var eventField = classType.GetField(eventName, BindingFlags.GetField | BindingFlags.NonPublic | BindingFlags.Instance);
        if (eventField == null) return false;

        var eventDelegate = eventField.GetValue(classInstance);
        return eventDelegate != null;
    }

    [SuppressMessage("ReSharper", "StringLiteralTypo")]
    internal static string GetApplicationVersionNumber(ApplicationType applicationTypeName)
    {
        if (!Enum.IsDefined(applicationTypeName))
            throw new ArgumentOutOfRangeException(nameof(applicationTypeName), Resources.NotValidApplicationName);

        string applicationPath;
        /*
           ProgramData=C:\ProgramData
           ProgramFiles=C:\Program Files
           ProgramFiles(x86)=C:\Program Files (x86)
           ProgramW6432=C:\Program Files
        */

        switch (applicationTypeName)
        {
            case ApplicationType.Plex:
            case ApplicationType.PlexPass:
                applicationPath = @"%ProgramW6432%\Plex\Plex Media Server\Plex Media Server.exe";
                break;
            case ApplicationType.Prowlarr:
                applicationPath = @"%ProgramData%\Prowlarr\bin\Prowlarr.exe";
                break;
            case ApplicationType.Radarr:
                applicationPath = @"%ProgramData%\Radarr\bin\Radarr.exe";
                break;
            case ApplicationType.SABnzbd:
                applicationPath = @"%ProgramW6432%\SABnzbd\SABnzbd.exe";
                break;
            case ApplicationType.Sonarr:
                applicationPath = @"%ProgramData%\Sonarr\bin\Sonarr.exe";
                break;
            case ApplicationType.Bazarr:
            {
                applicationPath = @"%SystemDrive%\Bazarr\Version";
                applicationPath = Environment.ExpandEnvironmentVariables(applicationPath);
                return File.ReadAllText(applicationPath).Trim().TrimStart('v');
            }

            // ReSharper disable once RedundantEnumCaseLabelForDefaultSection
            case ApplicationType.Unknown:
            default:
                throw new ArgumentOutOfRangeException(nameof(applicationTypeName), applicationTypeName, null);
        }
        applicationPath = Environment.ExpandEnvironmentVariables(applicationPath);
        var versionInfo = FileVersionInfo.GetVersionInfo(applicationPath);
        var version = versionInfo.FileVersion;
        return version;
    }

    /// <summary>
    ///     Returns TRUE if we're running as Admin
    /// </summary>
    /// <returns></returns>
    internal static bool IsRunningAsAdmin()
    {
        return !RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ||
               new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
    }

    /// <summary>
    ///     Moves a specified file to a new location, providing the option to specify a new file name. Ensures the destination
    ///     folder exists too.
    /// </summary>
    /// <param name="sourceFileName">The name of the file to move. Can include a relative or absolute path.</param>
    /// <param name="destFileName">The new path and name for the file.</param>
    internal static bool FileMove(string sourceFileName, string destFileName)
    {
        TraceIn(sourceFileName, destFileName);
        EnsureDirectoriesForFilePath(destFileName);
        File.Move(sourceFileName, destFileName);
        return TraceOut(true);
    }

    /// <summary>
    ///     Copies an existing file to a new file asynchronously. Overwriting a file of the same name is not allowed. Ensures
    ///     the destination folder exists too.
    /// </summary>
    /// <param name="sourceFileName">The file to copy.</param>
    /// <param name="destFileName">The name of the destination file. This cannot be a directory or an existing file.</param>
    /// <param name="ct"></param>
    /// <returns>True if copy was successful or False</returns>
    internal static bool FileCopy(string sourceFileName, string destFileName, CancellationToken ct)
    {
        TraceIn(sourceFileName, destFileName);
        if (destFileName == null || sourceFileName == null) return false;

        if (sourceFileName.Length > 256) throw new NotSupportedException($"Source file name {sourceFileName} exceeds 256 characters");
        if (destFileName.Length > 256) throw new NotSupportedException($"Destination file name {destFileName} exceeds 256 characters");
        if (File.Exists(destFileName)) throw new NotSupportedException("Destination file already exists");

        EnsureDirectoriesForFilePath(destFileName);
        if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();

        // ReSharper disable once CommentTypo
        // we create the destination file so xcopy knows its a file and can copy over it
        File.WriteAllText(destFileName, "Temp file"); // hash of this is 88f85bbea58fbff062050bcb2d2aafcf

        CopyProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                FileName = "xcopy",
                Arguments = $"/H /Y \"{sourceFileName}\" \"{destFileName}\""
            }
        };
        if (!CopyProcess.Start()) return TraceOut(false);

        try
        {
            var processId = CopyProcess.Id;

            // CopyProcess may be null here already if the file was very small
            CopyProcess.WaitForExit();

            // We wait because otherwise lots of copy processes will start at once
            // WaitForExit sometimes returns too early (especially for small files)
            // So we then wait until the processID has gone completely
            // This will throw ArgumentException if the process has stopped already

            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            while (Process.GetProcessById(processId) != null)
            {
                Wait(1);
            }

            // We never exit here
            return TraceOut(false);
        }

        // ArgumentException is thrown when the process actually stops
        // Then we wait until the file is actually not locked anymore and the hash codes match
        // InvalidOperationException is thrown if we access the Process and its already completed
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            var hashSource = GetShortMd5HashFromFile(sourceFileName);

            while (!FileIsAccessible(destFileName) || GetShortMd5HashFromFile(destFileName) != hashSource)
            {
                Wait(1);
                if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();
            }
            return TraceOut(true);
        }
    }

    /// <summary>
    ///     Converts a MB value to a size in bytes
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    internal static long ConvertMBtoBytes(long value)
    {
        return Convert.ToInt64(value * BYTES_IN_ONE_MEGABYTE);
    }

    internal static bool FixDateTakenForPhotos(string path)
    {
        TraceIn(path);
        if (path == null) return false;

        if (!File.Exists(path)) throw new NotSupportedException("File doesn't exist");

        var filename = Path.GetFileNameWithoutExtension(path);
        var extension = Path.GetExtension(path);

        if (extension.ToLowerInvariant() != ".png" && extension.ToLowerInvariant() != ".jpg" && extension.ToLowerInvariant() != ".jpeg" &&
            extension.ToLowerInvariant() != ".mp4")
            return true;

        var creationTime = filename.StartsWithIgnoreCase("IMG_")

            // ReSharper disable once StringLiteralTypo
            ? DateTime.ParseExact(filename.SubstringAfter('.'), "yyyy'-'MM'-'dd'_'HHmmss", CultureInfo.InvariantCulture)
            : DateTime.ParseExact(filename[..10], "yyyy'-'MM'-'dd", CultureInfo.InvariantCulture);
        var creationTimeString = creationTime.ToString("yyyy:MM:dd hh:mm:ss");
        var arguments = string.Empty;

        switch (extension.ToLowerInvariant())
        {
            case ".mp4":
                // ReSharper disable once StringLiteralTypo
                arguments = $" -Quicktime:CreationDate=\"{creationTimeString}\" \"{path}\"";
                break;
            case ".png":
                arguments = $" -PNG:CreationTime=\"{creationTimeString}\" \"{path}\"";
                break;
            case ".jpg":
            case ".jpeg":
                arguments = $" -EXIF:DateTimeOriginal=\"{creationTimeString}\" \"{path}\"";
                break;
        }

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                UseShellExecute = true, WindowStyle = ProcessWindowStyle.Hidden, FileName = @"c:\tools\exiftool.exe", Arguments = arguments
            }
        };
        if (!process.Start()) return TraceOut(false);

        try
        {
            var processId = process.Id;

            // CopyProcess may be null here already if the file was very small
            process.WaitForExit();

            // We wait because otherwise lots of copy processes will start at once
            // WaitForExit sometimes returns too early (especially for small files)
            // So we then wait until the processID has gone completely
            // This will throw ArgumentException if the process has stopped already

            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            while (Process.GetProcessById(processId) != null)
            {
                Wait(1);
            }

            // We never exit here
            return TraceOut(false);
        }

        // ArgumentException is thrown when the process actually stops
        // Then we wait until the file is actually not locked anymore and the hash codes match
        // InvalidOperationException is thrown if we access the Process and its already completed
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            while (!FileIsAccessible(path))
            {
                Wait(1);
            }
            return TraceOut(true);
        }
    }

    /// <summary>
    ///     Returns True if any of the attributes to check for are set in the value.
    /// </summary>
    /// <param name="value">
    /// </param>
    /// <param name="flagsToCheckFor">
    /// </param>
    /// <returns>
    ///     The <see cref="bool" />.
    /// </returns>
    private static bool AnyFlagSet(FileAttributes value, FileAttributes flagsToCheckFor)
    {
        return flagsToCheckFor != 0 && Enum.GetValues(typeof(FileAttributes)).Cast<Enum>().Where(flagsToCheckFor.HasFlag).Any(value.HasFlag);
    }

    /// <summary>
    ///     Clears the attribute from the file if it were set.
    /// </summary>
    /// <param name="path"></param>
    /// <param name="attributeToRemove"></param>
    internal static void ClearFileAttribute(string path, FileAttributes attributeToRemove)
    {
        TraceIn(path, attributeToRemove);
        var attributes = File.GetAttributes(path);

        if ((attributes & attributeToRemove) == attributeToRemove)
        {
            attributes = RemoveAttribute(attributes, attributeToRemove);
            File.SetAttributes(path, attributes);
        }
        TraceOut();
    }

    private static FileAttributes RemoveAttribute(FileAttributes attributes, FileAttributes attributesToRemove)
    {
        return attributes & ~attributesToRemove;
    }

    /// <summary>
    ///     Creates a hash for the 3 byte arrays passed in.
    /// </summary>
    /// <param name="firstByteArray">
    ///     The start byte array.
    /// </param>
    /// <param name="secondByteArray">
    ///     The middle byte array.
    /// </param>
    /// <param name="thirdByteArray">
    ///     The end byte array.
    /// </param>
    /// <returns>
    ///     A String of the hash.
    /// </returns>
    internal static string CreateHashForByteArray(byte[] firstByteArray, byte[] secondByteArray, byte[] thirdByteArray)
    {
        var newSize = 0;
        newSize += firstByteArray.Length;
        int thirdByteArrayDestinationOffset;

        if (secondByteArray != null)
        {
            newSize += secondByteArray.Length;
            thirdByteArrayDestinationOffset = firstByteArray.Length + secondByteArray.Length;
        }
        else
            thirdByteArrayDestinationOffset = firstByteArray.Length;
        if (thirdByteArray != null) newSize += thirdByteArray.Length;
        var byteArrayToHash = new byte[newSize];
        Buffer.BlockCopy(firstByteArray, 0, byteArrayToHash, 0, firstByteArray.Length);
        if (secondByteArray != null) Buffer.BlockCopy(secondByteArray, 0, byteArrayToHash, firstByteArray.Length, secondByteArray.Length);
        if (thirdByteArray != null) Buffer.BlockCopy(thirdByteArray, 0, byteArrayToHash, thirdByteArrayDestinationOffset, thirdByteArray.Length);
        return ByteArrayToString(MD5.HashData(byteArrayToHash));
    }

    /// <summary>
    ///     Clears any event handlers on the instance provided
    /// </summary>
    /// <param name="instance"></param>
    internal static void ClearEvents(object instance)
    {
        var eventsToClear = instance.GetType()
            .GetEvents(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);

        foreach (var eventInfo in eventsToClear)
        {
            var fieldInfo = instance.GetType().GetField(eventInfo.Name, BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
            if (fieldInfo?.GetValue(instance) is not Delegate eventHandler) continue;

            foreach (var item in eventHandler.GetInvocationList())
            {
                _ = eventInfo.GetRemoveMethod(fieldInfo.IsPrivate)?.Invoke(instance, new object[] { item });
            }
        }
    }

    /// <summary>
    ///     Ensures all the directories on the way to the file are created.
    /// </summary>
    /// <param name="filePath">
    /// </param>
    private static void EnsureDirectoriesForFilePath(string filePath)
    {
        var directoryName = new FileInfo(filePath).DirectoryName;
        if (directoryName != null) _ = Directory.CreateDirectory(directoryName);
    }

    /// <summary>
    ///     Ensures all directories in the directoryPath are created.
    /// </summary>
    /// <param name="directoryPath"></param>
    internal static void EnsureDirectoriesForDirectoryPath(string directoryPath)
    {
        EnsureDirectoriesForFilePath(Path.Combine(directoryPath, "temp.txt"));
    }

    /// <summary>
    ///     Returns all the files in the path provided.
    /// </summary>
    /// <param name="path">
    ///     The path.
    /// </param>
    /// <param name="ct"></param>
    internal static string[] GetFiles(string path, CancellationToken ct)
    {
        return GetFiles(path, "*", SearchOption.AllDirectories, 0, 0, true, ct);
    }

    /// <summary>
    ///     The get files.
    /// </summary>
    /// <param name="path">
    ///     The path.
    /// </param>
    /// <param name="filters">
    ///     The filters.
    /// </param>
    /// <param name="ct"></param>
    /// <returns>
    /// </returns>
    internal static string[] GetFiles(string path, string filters, CancellationToken ct)
    {
        return GetFiles(path, filters, SearchOption.AllDirectories, 0, 0, true, ct);
    }

    /// <summary>
    ///     The get files.
    /// </summary>
    /// <param name="path">
    ///     The path.
    /// </param>
    /// <param name="filters">
    ///     The filters.
    /// </param>
    /// <param name="searchOption">
    ///     The search option.
    /// </param>
    /// <param name="directoryAttributesToIgnore">
    ///     The directory attributes to ignore.
    /// </param>
    /// <param name="ct"></param>
    /// <returns>
    /// </returns>
    internal static string[] GetFiles(string path, string filters, SearchOption searchOption, FileAttributes directoryAttributesToIgnore,
        CancellationToken ct)
    {
        return GetFiles(path, filters, searchOption, directoryAttributesToIgnore, 0, false, ct);
    }

    /// <summary>
    ///     Returns an array of full path names of files in the folder specified.
    /// </summary>
    /// <param name="path">
    ///     The path.
    /// </param>
    /// <param name="filters">
    ///     The filters.
    /// </param>
    /// <param name="searchOption">
    ///     The search option.
    /// </param>
    /// <param name="ct"></param>
    /// <returns>
    /// </returns>
    internal static string[] GetFiles(string path, string filters, SearchOption searchOption, CancellationToken ct)
    {
        return GetFiles(path, filters, searchOption, 0, 0, true, ct);
    }

    /// <summary>
    ///     Returns the disk names for the directories provided. If it's a UNC path its returns the path up to the first '\' so
    ///     for \\nas1\assets1 it will
    ///     return \\nas1\ and for c:\path1 it will return c:\ so they are always terminated
    /// </summary>
    /// <param name="directories"></param>
    /// <returns></returns>
    internal static string[] GetDiskNames(IEnumerable<string> directories)
    {
        var diskNames = new HashSet<string>();

        foreach (var diskName in directories.Select(static d => d.StartsWithIgnoreCase(@"\\")
                     ? @"\\" + d.SubstringAfterIgnoreCase(@"\\").SubstringBefore('\\') + @"\"
                     : d.SubstringBefore('\\') + @"\"))
        {
            _ = diskNames.Add(diskName);
        }
        return diskNames.ToArray();
    }

    internal static string[] GetDeviceAndFirstDirectoryNames(IEnumerable<string> directories)
    {
        var diskAndFirstDirectoryNames = new HashSet<string>();

        foreach (var d in directories)
        {
            var text = d.SubstringAfterIgnoreCase(@"\\");
            var machineName = text.SubstringBefore('\\');
            var firstDirectory = text.SubstringAfterIgnoreCase(@"\").SubstringBefore('\\');
            _ = diskAndFirstDirectoryNames.Add(Path.Combine(machineName, firstDirectory));
        }
        return diskAndFirstDirectoryNames.ToArray();
    }

    /// <summary>
    ///     Returns an array of paths to files that are on the disk provided.
    /// </summary>
    /// <param name="diskName"></param>
    /// <param name="backupFiles"></param>
    /// <returns></returns>
    internal static string[] GetFilesForDisk(string diskName, IEnumerable<string> backupFiles)
    {
        ArgumentException.ThrowIfNullOrEmpty(diskName);
        var terminatedDiskName = EnsurePathHasATerminatingSeparator(diskName);
        return backupFiles.Where(file => file.StartsWithIgnoreCase(terminatedDiskName)).ToArray();
    }

    /// <summary>
    ///     Returns an array of paths to directories that are on the disk provided.
    /// </summary>
    /// <param name="diskName"></param>
    /// <param name="directories"></param>
    /// <returns></returns>
    internal static string[] GetDirectoriesForDisk(string diskName, IEnumerable<string> directories)
    {
        ArgumentException.ThrowIfNullOrEmpty(diskName);
        var terminatedDiskName = EnsurePathHasATerminatingSeparator(diskName);
        return directories.Where(dir => dir.StartsWithIgnoreCase(terminatedDiskName)).ToArray();
    }

    /// <summary>
    ///     The get files.
    /// </summary>
    /// <param name="path">
    ///     The path.
    /// </param>
    /// <param name="filters">
    ///     The filters.
    /// </param>
    /// <param name="searchOption">
    ///     The search option.
    /// </param>
    /// <param name="directoryAttributesToIgnore">
    ///     The directory attributes to ignore.
    /// </param>
    /// <param name="fileAttributesToIgnore">
    ///     The file attributes to ignore.
    /// </param>
    /// <param name="deleteEmptyDirectories"></param>
    /// <param name="ct"></param>
    /// <returns>
    /// </returns>
    private static string[] GetFiles(string path, string filters, SearchOption searchOption, FileAttributes directoryAttributesToIgnore,
        FileAttributes fileAttributesToIgnore, bool deleteEmptyDirectories, CancellationToken ct)
    {
        if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();
        var sw = Stopwatch.StartNew();

        if (!Directory.Exists(path))
        {
            Trace("GetFiles exit with new string[]");
            return Array.Empty<string>();
        }
        DirectoryInfo directoryInfo = new(path);

        if (directoryInfo.Parent != null && AnyFlagSet(directoryInfo.Attributes, directoryAttributesToIgnore))
            return TraceOut(Array.Empty<string>());

        var include = from filter in filters.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
            where filter.Trim().HasValue()
            select filter.Trim();
        var includeAsArray = include as string[] ?? include.ToArray();
        var exclude = from filter in includeAsArray where filter.Contains('!') select filter;
        var excludeAsArray = exclude as string[] ?? exclude.ToArray();
        include = includeAsArray.Except(excludeAsArray);
        var includeAsArray2 = include as string[] ?? include.ToArray();
        if (!includeAsArray2.Any()) includeAsArray2 = new[] { "*" };

        var excludeFilters = from filter in excludeAsArray
            let replace = filter.Replace("!", string.Empty).Replace(".", @"\.").Replace("*", ".*").Replace("?", ".")
            select $"^{replace}$";
        Regex excludeRegex = new(string.Join("|", excludeFilters.ToArray()), RegexOptions.IgnoreCase);
        Queue<string> pathsToSearch = new();
        List<string> foundFiles = new();
        pathsToSearch.Enqueue(path);

        while (pathsToSearch.Count > 0)
        {
            if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();
            var dir = pathsToSearch.Dequeue();
            Trace($"Dequeued {dir}");

            if (deleteEmptyDirectories && IsDirectoryEmpty(dir))
            {
                Log($"Directory {dir} is empty so deleting.");
                Directory.Delete(dir);
            }
            else
            {
                if (searchOption == SearchOption.AllDirectories)
                {
                    foreach (var subDir in Directory.GetDirectories(dir).Where(subDir =>
                                 !AnyFlagSet(new DirectoryInfo(subDir).Attributes, directoryAttributesToIgnore)))
                    {
                        if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();
                        pathsToSearch.Enqueue(subDir);
                    }
                }

                foreach (var collection in includeAsArray2.Select(filter =>
                         {
                             if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();
                             return Directory.GetFiles(dir, filter, SearchOption.TopDirectoryOnly);
                         }).Select(allFiles => excludeAsArray.Any() ? allFiles.Where(p => !excludeRegex.Match(p).Success) : allFiles))
                {
                    foundFiles.AddRange(collection.Where(p => !AnyFlagSet(new FileInfo(p).Attributes, fileAttributesToIgnore)));
                }
            }
        }
        sw.Stop();
        Trace($"GetFiles for {path} = {(sw.Elapsed.TotalSeconds < 1 ? "<1 second" : $"{sw.Elapsed.TotalSeconds:#} seconds")}");
        return foundFiles.ToArray();
    }

    /// <summary>
    ///     The hash from file.
    /// </summary>
    /// <param name="fileName">
    ///     The file name.
    /// </param>
    /// <param name="algorithm">
    ///     The algorithm.
    /// </param>
    /// <returns>
    ///     The <see cref="string" />.
    /// </returns>
    internal static string GetHashFromFile(string fileName, HashAlgorithm algorithm)
    {
        using BufferedStream stream = new(File.OpenRead(fileName), BYTES_IN_ONE_MEGABYTE);
        return ByteArrayToString(algorithm.ComputeHash(stream));
    }

    /// <summary>
    ///     The get remote file byte array.
    /// </summary>
    /// <param name="fileStream">
    ///     The file stream.
    /// </param>
    /// <param name="byteCountToReturn">
    ///     The byte count to return.
    /// </param>
    /// <returns>
    /// </returns>
    internal static byte[] GetRemoteFileByteArray(Stream fileStream, long byteCountToReturn)
    {
        var buffer = new byte[byteCountToReturn];
        int count;
        var sum = 0;
        var length = Convert.ToInt32(byteCountToReturn);

        while ((count = fileStream.Read(buffer, sum, length - sum)) > 0)
        {
            sum += count; // sum is a buffer offset for next reading
        }
        if (sum >= byteCountToReturn) return buffer;

        var byteArray = new byte[sum];
        Buffer.BlockCopy(buffer, 0, byteArray, 0, sum);
        return byteArray;
    }

    /// <summary>
    ///     Calculates the short MD5 hash of the file.
    /// </summary>
    /// <param name="stream">
    ///     The stream.
    /// </param>
    /// <param name="size">
    ///     The size.
    /// </param>
    /// <returns>
    ///     The <see cref="string" />.
    /// </returns>
    internal static string GetShortMd5HashFromFile(FileStream stream, long size)
    {
        if (stream == null) return null;
        if (size <= 0) return string.Empty;

        byte[] startBlock;
        byte[] middleBlock = null;
        byte[] endBlock = null;

        if (size >= START_BLOCK_SIZE + MIDDLE_BLOCK_SIZE + END_BLOCK_SIZE)
        {
            var startDownloadPositionForEndBlock = size - END_BLOCK_SIZE;
            var startDownloadPositionForMiddleBlock = size / 2;
            startBlock = GetLocalFileByteArray(stream, 0, START_BLOCK_SIZE);
            middleBlock = GetLocalFileByteArray(stream, startDownloadPositionForMiddleBlock, MIDDLE_BLOCK_SIZE);
            endBlock = GetLocalFileByteArray(stream, startDownloadPositionForEndBlock, END_BLOCK_SIZE);
        }
        else
            startBlock = GetLocalFileByteArray(stream, 0, size);
        return CreateHashForByteArray(startBlock, middleBlock, endBlock);
    }

    internal static long GetFileLength(string fileName)
    {
        ArgumentException.ThrowIfNullOrEmpty(fileName, nameof(fileName));
        if (!File.Exists(fileName)) throw new FileNotFoundException(Resources.FileNotFound, fileName);

        return new FileInfo(fileName).Length;
    }

    internal static DateTime GetFileLastWriteTime(string fileName)
    {
        ArgumentException.ThrowIfNullOrEmpty(fileName, nameof(fileName));
        if (!File.Exists(fileName)) throw new FileNotFoundException(Resources.FileNotFound, fileName);

        FileInfo fileInfo = new(fileName);
        DateTime returnValue;

        try
        {
            returnValue = fileInfo.LastWriteTime;
        }
        catch (ArgumentOutOfRangeException)
        {
            // Sometimes the file doesn't have a valid LastWriteTime 
            //If we cant read the LastWriteTime then copy the LastAccessTime over it and use that instead
            fileInfo.LastWriteTime = fileInfo.LastAccessTime;
            returnValue = fileInfo.LastWriteTime;
        }
        return returnValue;
    }

    internal static bool SetFileLastWriteTime(string fileName, DateTime writeTimeToUse)
    {
        ArgumentException.ThrowIfNullOrEmpty(fileName, nameof(fileName));
        if (!File.Exists(fileName)) throw new FileNotFoundException(Resources.FileNotFound, fileName);

        FileInfo fileInfo = new(fileName);

        try
        {
            fileInfo.LastWriteTime = writeTimeToUse;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
        return true;
    }

    /// <summary>
    ///     Gets an MD5 hash of the first 16K, the 16K from the middle and last 16K of a file.
    /// </summary>
    /// <param name="path">
    ///     The local file name.
    /// </param>
    /// <returns>
    ///     An MD5 hash of the file or null if File doesn't exist or string.Empty if it has no size.
    /// </returns>
    internal static string GetShortMd5HashFromFile(string path)
    {
        TraceIn(path);
        ArgumentException.ThrowIfNullOrEmpty(path);
        if (!File.Exists(path)) throw new FileNotFoundException(Resources.FileNotFound, path);

        var size = new FileInfo(path).Length;
        if (size == 0) return string.Empty;

        byte[] startBlock;
        byte[] middleBlock = null;
        byte[] endBlock = null;

        if (size >= START_BLOCK_SIZE + MIDDLE_BLOCK_SIZE + END_BLOCK_SIZE)
        {
            var startDownloadPositionForEndBlock = size - END_BLOCK_SIZE;
            var startDownloadPositionForMiddleBlock = size / 2;
            startBlock = GetLocalFileByteArray(path, 0, START_BLOCK_SIZE);
            middleBlock = GetLocalFileByteArray(path, startDownloadPositionForMiddleBlock, MIDDLE_BLOCK_SIZE);
            endBlock = GetLocalFileByteArray(path, startDownloadPositionForEndBlock, END_BLOCK_SIZE);
        }
        else
            startBlock = GetLocalFileByteArray(path, 0, size);
        return TraceOut(CreateHashForByteArray(startBlock, middleBlock, endBlock));
    }

    internal static void Wait(int howManyMillisecondsToWait)
    {
        var howLongToWait = TimeSpan.FromMilliseconds(howManyMillisecondsToWait);
        var sw = Stopwatch.StartNew();

        while (sw.Elapsed < howLongToWait) { }
    }

    /// <summary>
    ///     Checks the path is a video file
    /// </summary>
    /// <param name="path"></param>
    /// <returns>False if its not a video file, True if its a video file. Exception if path is null or empty</returns>
    internal static bool FileIsVideo(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        return Path.GetExtension(path).ToLowerInvariant().ContainsAny(_videoExtensions);
    }

    /// <summary>
    ///     Returns True if the path contains [DV]
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    private static bool VideoFileIsDolbyVision(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        return path.HasValue() && path.ContainsIgnoreCase("[DV]");
    }

    /// <summary>
    ///     Returns True if a rename was required. newPath has the full path after the rename or the original path if not
    ///     renamed
    /// </summary>
    /// <param name="path"></param>
    /// <param name="newPath"></param>
    /// <exception cref="FileNotFoundException"></exception>
    /// <exception cref="IOException">If the file is locked and can't be read for info</exception>
    /// <returns>False if a rename was not required</returns>
    internal static bool RenameVideoCodec(string path, out string newPath)
    {
        TraceIn(path);
        newPath = path;
        ArgumentException.ThrowIfNullOrEmpty(path);
        if (!File.Exists(path)) throw new FileNotFoundException(Resources.FileNotFound, path);

        if (!FileIsVideo(path)) return TraceOut(false);

        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(path);
        var directoryName = Path.GetDirectoryName(path);
        if (directoryName == null) return TraceOut(false);
        if (!GetVideoCodecFromFileName(Path.GetFileName(path), out var currentVideoCodecInFileName)) return TraceOut(false);
        if (!GetVideoCodec(path, out var actualVideoCodec)) return TraceOut(false);
        if (actualVideoCodec == currentVideoCodecInFileName) return TraceOut(false);
        if (actualVideoCodec.HasNoValue()) return TraceOut(false);

        var newPathInternal = Path.Combine(directoryName,
                                  fileNameWithoutExtension.Replace(currentVideoCodecInFileName.WrapInSquareBrackets(),
                                      actualVideoCodec.WrapInSquareBrackets())) +
                              Path.GetExtension(path);
        Log($"Renaming {path} to {newPathInternal}");
        _ = FileMove(path, newPathInternal);
        Trace($"Renamed {path} to {newPathInternal}");
        newPath = newPathInternal;
        return TraceOut(true);
    }

    /// <summary>
    /// </summary>
    /// <param name="path">Must have a video extension and a valid video codec in square brackets in the path</param>
    /// <param name="videoCodec"></param>
    /// <returns>False if the codec was not determined</returns>
    internal static bool GetVideoCodecFromFileName(string path, out string videoCodec)
    {
        TraceIn(path);
        ArgumentException.ThrowIfNullOrEmpty(path);
        var fileName = Path.GetFileName(path);
        videoCodec = string.Empty;
        if (Config == null) return TraceOut(false);

        var videoCodecRegex = Config.DirectoriesRenameVideoFilesRegEx;
        if (videoCodecRegex.HasNoValue()) return TraceOut(false);

        var match = Regex.Match(fileName, videoCodecRegex);
        if (!match.Success) return TraceOut(false);

        videoCodec = match.Groups[1].Value;
        return TraceOut(true);
    }

    /// <summary>
    ///     Gets the actual video codec of the file
    /// </summary>
    /// <param name="path">Path to the video file</param>
    /// <param name="actualVideoCodec">The actual video codec of the file</param>
    /// <returns>True if the video codec was determined</returns>
    /// <exception cref="FileNotFoundException">If the file is not found</exception>
    /// <exception cref="IOException">If the file is locked</exception>
    /// <exception cref="NotSupportedException">If the file is not a video file</exception>
    internal static bool GetVideoCodec(string path, out string actualVideoCodec)
    {
        TraceIn(path);
        ArgumentException.ThrowIfNullOrEmpty(path);
        if (!File.Exists(path)) throw new FileNotFoundException(Resources.FileNotFound, path);
        if (!FileIsVideo(path)) throw new NotSupportedException("file is not video");

        actualVideoCodec = string.Empty;
        var info = new VideoFileInfoReader().GetMediaInfo(path) ?? throw new IOException(string.Format(Resources.UnableToLoadFFProbe, path));
        actualVideoCodec = FormatVideoCodec(info, Path.GetFileNameWithoutExtension(path));
        if (actualVideoCodec.HasNoValue()) LogWithPushover(BackupAction.Error, $"Actual video codec for {path} is string.Empty");
        return TraceOut(true);
    }

    /// <summary>
    /// </summary>
    /// <param name="mediaInfo"></param>
    /// <param name="fileName"></param>
    /// <returns>
    ///     The following values are possibly returned: null, string.Empty, h264, h265, XviD, DivX, MPEG4, VP6, MPEG2,
    ///     MPEG, VC1, AV1, VP7,VP8, VP9, WMV, RGB
    /// </returns>
    [SuppressMessage("ReSharper", "StringLiteralTypo")]
    private static string FormatVideoCodec(MediaInfoModel mediaInfo, string fileName)
    {
        if (mediaInfo.VideoFormat == null) return null;

        var videoFormat = mediaInfo.VideoFormat;
        var videoCodecId = mediaInfo.VideoCodecId ?? string.Empty;
        var result = videoFormat.Trim();
        if (videoFormat.Empty()) return result;
        if (videoCodecId == "x264" || videoFormat == "h264") return "h264";
        if (videoCodecId == "x265") return "h265";

        if (videoFormat == "mpeg4" || videoFormat.Contains("msmpeg4"))
        {
            return videoCodecId.ToLowerInvariant() switch
            {
                "xvid" => "XviD",
                "div3" or "dx50" or "divx" => "DivX",
                _ => "MPEG4"
            };
        }
        if (videoFormat.Contains("vp6")) return "VP6";

        switch (videoFormat)
        {
            case "hevc":
                return "h265";
            case "mpeg2video":
                return "MPEG2";
            case "mpeg1video":
                return "MPEG";
            case "vc1":
                return "VC1";
            case "av1":
                return "AV1";
            case "vp7":
            case "vp8":
            case "vp9":
                return videoFormat.ToUpperInvariant();
            case "wmv1":
            case "wmv2":
            case "wmv3":
                return "WMV";
            case "rawvideo":
                return "RGB";
            case "qtrle":
            case "rpza":
            case "rv10":
            case "rv20":
            case "rv30":
            case "rv40":
            case "cinepak":
            case "msvideo1":
                LogWithPushover(BackupAction.General, PushoverPriority.High, $"About to return string.Empty for {fileName}");
                return "";
        }
        return result;
    }

    internal static bool FileIsDolbyVisionProfile5(string path)
    {
        TraceIn(path);
        ArgumentException.ThrowIfNullOrEmpty(path);
        if (!FileIsVideo(path) || !VideoFileIsDolbyVision(path)) return TraceOut(false);

        if (!File.Exists(path)) throw new FileNotFoundException(Resources.FileNotFound, path);

        var info = new VideoFileInfoReader().GetMediaInfo(path);

        // ReSharper disable once StringLiteralTypo
        return info == null
            ? throw new ApplicationException("Unable to load ffprobe.exe")
            : TraceOut(info is { DoviConfigurationRecord.DvProfile: 5 });
    }

    private static bool PushoverServiceAvailable(string pushoverAppToken)
    {
        TraceIn();
        if (!pushoverAppToken.HasValue() || pushoverAppToken == "InsertYourPushoverAppTokenHere") return TraceOut(false);

        try
        {
            var pushoverLimitsAddress = $"https://api.pushover.net/1/apps/limits.json?token={pushoverAppToken}";
            HttpClient client = new();
            var task = Task.Run(() => client.GetAsync(pushoverLimitsAddress));
            task.Wait();
            var response = task.Result;
            _ = response.EnsureSuccessStatusCode();
            return TraceOut(GetHeaderValue(response.Headers, "X-Limit-App-Remaining") > 1);
        }
        catch (Exception ex)
        {
            Log($"Exception testing Pushover message {ex}");
            return TraceOut(false);
        }
    }

    private static void SetupPushoverAppToken()
    {
        TraceIn();
        Config.PushoverAppTokenToUse = Config.PushoverAppTokens.FirstOrDefault(static t => PushoverServiceAvailable(t));
        TraceOut();
    }

    private static void SendPushoverMessage(string title, PushoverPriority priority, PushoverRetry retry, PushoverExpires expires, string message)
    {
        TraceIn();

        try
        {
            if (!Config.PushoverAppTokenToUse.HasValue())
            {
                SetupPushoverAppToken();
                if (!Config.PushoverAppTokenToUse.HasValue()) return;
            }

            if (!Config.PushoverOnOff || ((priority is not (PushoverPriority.Low or PushoverPriority.Lowest) || !Config.PushoverSendLowOnOff) &&
                                          (priority != PushoverPriority.Normal || !Config.PushoverSendNormalOnOff) &&
                                          (priority != PushoverPriority.High || !Config.PushoverSendHighOnOff) &&
                                          (priority != PushoverPriority.Emergency || !Config.PushoverSendEmergencyOnOff)))
                return;

            var timestamp = DateTime.Now.ToUnixTimeMilliseconds();
            Trace($"timestamp is {timestamp} for {message}");

            Dictionary<string, string> parameters = new()
            {
                { "token", Config.PushoverAppTokenToUse },
                { "user", Config.PushoverUserKey },
                { "priority", Convert.ChangeType(priority, priority.GetTypeCode()).ToString() },
                { "message", message },
                { "title", title },
                { "timestamp", timestamp }
            };

            if (priority == PushoverPriority.Emergency)
            {
                if (retry == PushoverRetry.None) retry = PushoverRetry.ThirtySeconds;
                if (expires == PushoverExpires.Immediately) expires = PushoverExpires.FiveMinutes;
            }
            if (retry != PushoverRetry.None) parameters.Add("retry", Convert.ChangeType(retry, retry.GetTypeCode()).ToString());
            if (expires != PushoverExpires.Immediately) parameters.Add("expire", Convert.ChangeType(expires, expires.GetTypeCode()).ToString());
            using FormUrlEncodedContent postContent = new(parameters);

            // ReSharper disable once AccessToDisposedClosure
            var task = Task.Run(() => _client.PostAsync(PUSHOVER_MESSAGES_URL, postContent));
            task.Wait();
            var response = task.Result;
            _ = response.EnsureSuccessStatusCode();
            PushoverMessagesRemaining = GetHeaderValue(response.Headers, "X-Limit-App-Remaining");
            Trace($"Pushover messages remaining: {PushoverMessagesRemaining}");

            if (PushoverMessagesRemaining < Config.PushoverWarningMessagesRemaining)
            {
                if (!_sentAlertForLowPushoverMessages && !_alreadySendingPushoverMessage)
                {
                    _alreadySendingPushoverMessage = true;

                    SendPushoverMessage("Message Limit Warning", PushoverPriority.High, PushoverRetry.None, PushoverExpires.Immediately,
                        $"{PushoverMessagesRemaining} remaining");
                    _alreadySendingPushoverMessage = false;
                    _sentAlertForLowPushoverMessages = true;
                }
            }
            if (PushoverMessagesRemaining < 5) SetupPushoverAppToken();
        }
        catch (Exception ex)
        {
            // we ignore any pushover problems
            Log($"Exception sending Pushover message {ex}");
        }
        finally
        {
            TraceOut();
        }
    }

    private static int GetHeaderValue(HttpHeaders headers, string headerKey)
    {
        return headers.TryGetValues(headerKey, out var values) ? Convert.ToInt32(values.First()) : 0;
    }

    /// <summary>
    ///     KIlls matching processes. All the processes that start with ProcessName are stopped
    /// </summary>
    /// <param name="processName"></param>
    /// <returns>True if they were killed and False otherwise</returns>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    /// <exception cref="ArgumentException"></exception>
    internal static bool KillProcesses(string processName)
    {
        var processes = Process.GetProcesses().Where(p => p.ProcessName.StartsWithIgnoreCase(processName));

        try
        {
            foreach (var process in processes)
            {
                process.Kill();
            }
        }
        catch (Exception)
        {
            return false;
        }
        return true;
    }

    /// <summary>
    ///     Returns True if the Url returns a 200 response
    /// </summary>
    /// <param name="url">The url to check</param>
    /// <param name="timeout">Timeout in milliseconds</param>
    /// <returns>True if success code returned</returns>
    internal static bool UrlExists(string url, int timeout = 30 * 1000)
    {
        var returnValue = false;

        try
        {
            HttpClient client = new() { Timeout = TimeSpan.FromMilliseconds(timeout) };
            var task = Task.Run(() => client.GetAsync(url));
            task.Wait();
            var response = task.Result;
            returnValue = response.StatusCode == HttpStatusCode.OK;
        }
        catch
        {
            // ignored
        }
        return returnValue;
    }

    /// <summary>
    ///     Returns True if the host can be connected to on that port
    /// </summary>
    /// <param name="host">The host to check</param>
    /// <param name="port">The port to connect on</param>
    /// <returns>True if the connection is made</returns>
    internal static bool ConnectionExists(string host, int port)
    {
        var returnValue = false;

        try
        {
            using TcpClient tcpClient = new();
            tcpClient.Connect(host, port);
            returnValue = true;
        }
        catch
        {
            // ignored
        }
        return returnValue;
    }

    internal static void Log(BackupAction action, string message)
    {
        if (_lengthOfLargestBackupActionEnumNames == 0)
        {
            foreach (var enumName in Enum.GetNames(typeof(BackupAction))
                         .Where(static enumName => enumName.Length > _lengthOfLargestBackupActionEnumNames))
            {
                _lengthOfLargestBackupActionEnumNames = enumName.Length;
            }
        }
        var actionText = Enum.GetName(typeof(BackupAction), action) + " ";
        var textArrayToWrite = message.Split('\n');

        foreach (var line in textArrayToWrite.Where(static line => line.HasValue()))
        {
            Log(actionText.PadRight(_lengthOfLargestBackupActionEnumNames + 1) + line);
        }
    }

    internal static void LogHeader()
    {
        const string headerText = @" ____             _                  __  __" + "\n" +
                                  @"| __ )  __ _  ___| | ___   _ _ __   |  \/  | __ _ _ __   __ _  __ _  ___ _ __" + "\n" +
                                  @"|  _ \ / _` |/ __| |/ / | | | '_ \  | |\/| |/ _` | '_ \ / _` |/ _` |/ _ \ '__|" + "\n" +
                                  @"| |_) | (_| | (__|   <| |_| | |_) | | |  | | (_| | | | | (_| | (_| |  __/ |" + "\n" +
                                  @"|____/ \__,_|\___|_|\_\\__,_| .__/  |_|  |_|\__,_|_| |_|\__,_|\__, |\___|_|" + "\n" +
                                  @"                            |_|                               |___/";
        Log(headerText);
    }

    /// <summary>
    ///     Writes the text to the logfile
    /// </summary>
    /// <param name="text"></param>
    internal static void Log(string text)
    {
        lock (_lock)
        {
            EnsureDirectoriesForFilePath(_logFile);
            Trace(text);
            var textArrayToWrite = text.Split('\n');

            foreach (var textToWrite in from line in textArrayToWrite where line.HasValue() select $"{DateTime.Now:dd-MM-yy HH:mm:ss} {line}")
            {
                Console.WriteLine(textToWrite);
                if (_logFile.HasValue()) File.AppendAllLines(_logFile, new[] { textToWrite });
            }
        }
    }

    /// <summary>
    ///     Logs the text to the LogFile and sends a Pushover message
    /// </summary>
    /// <param name="backupAction"></param>
    /// <param name="text"></param>
    /// <param name="delayBeforeSending"></param>
    /// <param name="delayAfterSending"></param>
    internal static void LogWithPushover(BackupAction backupAction, string text, bool delayBeforeSending = false, bool delayAfterSending = false)
    {
        LogWithPushover(backupAction, PushoverPriority.Normal, text, delayBeforeSending, delayAfterSending);
    }

    /// <summary>
    ///     Logs the text to the LogFile and sends a Pushover message
    /// </summary>
    /// <param name="backupAction"></param>
    /// <param name="priority"></param>
    /// <param name="text"></param>
    /// <param name="delayBeforeSending"></param>
    /// <param name="delayAfterSending"></param>
    internal static void LogWithPushover(BackupAction backupAction, PushoverPriority priority, string text, bool delayBeforeSending = false,
        bool delayAfterSending = false)
    {
        LogWithPushover(backupAction, priority, PushoverRetry.None, PushoverExpires.Immediately, text, delayBeforeSending, delayAfterSending);
    }

    private static async Task TaskWrapper(Task task, CancellationToken ct)
    {
        try
        {
            TraceIn();
            await task;
            Trace("After await");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            Trace(Resources.Cancelling);
        }
        catch (Exception u)
        {
            LogWithPushover(BackupAction.Error, PushoverPriority.High, string.Format(Resources.TaskWrapperException, u));
        }
        finally
        {
            TraceOut();
        }
    }

    /// <summary>
    ///     Logs the text to the LogFile and sends a Pushover message
    ///     Messages can be delayed before sending for 1s.
    ///     Errors are always sent immediately
    /// </summary>
    /// <param name="backupAction"></param>
    /// <param name="priority"></param>
    /// <param name="retry"></param>
    /// <param name="expires"></param>
    /// <param name="text"></param>
    /// <param name="delayBeforeSending"></param>
    /// <param name="delayAfterSending"></param>
    internal static void LogWithPushover(BackupAction backupAction, PushoverPriority priority, PushoverRetry retry, PushoverExpires expires,
        string text, bool delayBeforeSending = false, bool delayAfterSending = false)
    {
        Log(backupAction, text);

        if (!Config.PushoverOnOff || ((priority is not (PushoverPriority.Low or PushoverPriority.Lowest) || !Config.PushoverSendLowOnOff) &&
                                      (priority != PushoverPriority.Normal || !Config.PushoverSendNormalOnOff) &&
                                      (priority != PushoverPriority.High || !Config.PushoverSendHighOnOff) &&
                                      (priority != PushoverPriority.Emergency || !Config.PushoverSendEmergencyOnOff)))
            return;

        if (backupAction == BackupAction.Error || (!delayBeforeSending && !delayAfterSending))
        {
            _ = TaskWrapper(Task.Run(() => SendPushoverMessage(Enum.GetName(typeof(BackupAction), backupAction), priority, retry, expires, text)),
                new CancellationTokenSource().Token);
        }
        else
        {
            if (delayBeforeSending) Task.Delay(1000).Wait();
            SendPushoverMessage(Enum.GetName(typeof(BackupAction), backupAction), priority, retry, expires, text);
            if (delayAfterSending) Task.Delay(1000).Wait();
        }
    }

    /// <summary>
    ///     The byte array to string.
    /// </summary>
    /// <param name="value">
    ///     The value.
    /// </param>
    /// <returns>
    ///     The <see cref="string" />.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// </exception>
    private static string ByteArrayToString(byte[] value)
    {
        return value == null ? throw new ArgumentNullException(nameof(value)) : ByteArrayToString(value, 0, value.Length);
    }

    /// <summary>
    ///     The byte array to string.
    /// </summary>
    /// <param name="value">
    ///     The value.
    /// </param>
    /// <param name="startIndex">
    ///     The start index.
    /// </param>
    /// <param name="length">
    ///     The length.
    /// </param>
    /// <returns>
    ///     The <see cref="string" />.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// </exception>
    /// <exception cref="ArgumentException">
    /// </exception>
    private static string ByteArrayToString(IReadOnlyList<byte> value, int startIndex, int length)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (startIndex < 0 || (startIndex >= value.Count && startIndex > 0)) throw new ArgumentOutOfRangeException(nameof(startIndex));
        if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));
        if (startIndex > value.Count - length) throw new ArgumentException(null, nameof(length));

        switch (length)
        {
            case 0:
                return string.Empty;
            case > 715827882:
                throw new ArgumentOutOfRangeException(nameof(length));
        }
        var length1 = length * 2;
        var chArray = new char[length1];
        var num1 = startIndex;
        var index = 0;

        while (index < length1)
        {
            var num2 = value[num1++];
            chArray[index] = GetLowercaseHexValue(num2 / 16);
            chArray[index + 1] = GetLowercaseHexValue(num2 % 16);
            index += 2;
        }
        return new string(chArray, 0, chArray.Length);
    }

    /// <summary>
    ///     Creates a hash for the two byte arrays passed in.
    /// </summary>
    /// <param name="firstByteArray">
    ///     The start byte array.
    /// </param>
    /// <param name="endByteArray">
    ///     The end byte array.
    /// </param>
    /// <returns>
    ///     A String of the hash.
    /// </returns>
    internal static string CreateHashForByteArray(byte[] firstByteArray, byte[] endByteArray)
    {
        var byteArrayToHash = endByteArray == null ? new byte[firstByteArray.Length] : new byte[firstByteArray.Length + endByteArray.Length];
        Buffer.BlockCopy(firstByteArray, 0, byteArrayToHash, 0, firstByteArray.Length);
        if (endByteArray != null) Buffer.BlockCopy(endByteArray, 0, byteArrayToHash, firstByteArray.Length, endByteArray.Length);
        return ByteArrayToString(MD5.HashData(byteArrayToHash));
    }

    /// <summary>
    ///     The ensure path has a terminating separator.
    /// </summary>
    /// <param name="path">
    ///     The path.
    /// </param>
    /// <returns>
    ///     The <see cref="string" />.
    /// </returns>
    internal static string EnsurePathHasATerminatingSeparator(string path)
    {
        return path.EndsWithIgnoreCase(Path.DirectorySeparatorChar.ToString()) ? path : path + Path.DirectorySeparatorChar;
    }

    /// <summary>
    ///     The get local file byte array.
    /// </summary>
    /// <param name="stream">
    ///     The file stream.
    /// </param>
    /// <param name="offset">
    ///     The offset.
    /// </param>
    /// <param name="byteCountToReturn">
    ///     The byte count to return.
    /// </param>
    /// <returns>
    ///     The byte[]
    /// </returns>
    private static byte[] GetLocalFileByteArray(Stream stream, long offset, long byteCountToReturn)
    {
        _ = stream.Seek(offset, SeekOrigin.Begin);
        var buffer = new byte[byteCountToReturn];
        int count;
        var sum = 0;
        var length = Convert.ToInt32(byteCountToReturn);

        while ((count = stream.Read(buffer, sum, length - sum)) > 0)
        {
            sum += count; // sum is a buffer offset for next reading
        }
        if (sum >= byteCountToReturn) return buffer;

        var byteArray = new byte[sum];
        Buffer.BlockCopy(buffer, 0, byteArray, 0, sum);
        return byteArray;
    }

    /// <summary>
    ///     The get file byte array.
    /// </summary>
    /// <param name="fileName">
    ///     The file name.
    /// </param>
    /// <param name="offset">
    ///     The offset.
    /// </param>
    /// <param name="byteCountToReturn">
    ///     The byte count to return.
    /// </param>
    /// <returns>
    ///     The byte[]
    /// </returns>
    internal static byte[] GetLocalFileByteArray(string fileName, long offset, long byteCountToReturn)
    {
        byte[] buffer;
        FileStream fileStream = new(fileName, FileMode.Open, FileAccess.Read);
        _ = fileStream.Seek(offset, SeekOrigin.Begin);
        var sum = 0;

        try
        {
            buffer = new byte[byteCountToReturn];
            var length = Convert.ToInt32(byteCountToReturn);
            int count;

            while ((count = fileStream.Read(buffer, sum, length - sum)) > 0)
            {
                sum += count; // sum is a buffer offset for next reading
            }
        }
        finally
        {
            fileStream.Close();
        }
        if (sum >= byteCountToReturn) return buffer;

        var byteArray = new byte[sum];
        Buffer.BlockCopy(buffer, 0, byteArray, 0, sum);
        return byteArray;
    }

    /// <summary>
    ///     The get lowercase hex value.
    /// </summary>
    /// <param name="i">
    ///     The i.
    /// </param>
    /// <returns>
    ///     The <see cref="char" />.
    /// </returns>
    private static char GetLowercaseHexValue(int i)
    {
        return i < 10 ? (char)(i + 48) : (char)(i - 10 + 65 + 32);
    }

    /// <summary>
    /// </summary>
    /// <param name="path"></param>
    /// <param name="freeSpace">in bytes</param>
    /// <param name="totalBytes">in bytes</param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    internal static bool GetDiskInfo(string path, out long freeSpace, out long totalBytes)
    {
        ArgumentException.ThrowIfNullOrEmpty(path, nameof(path));
        if (!path.EndsWithIgnoreCase("\\")) path += '\\';
        return GetDiskFreeSpaceEx(path, out freeSpace, out totalBytes, out _);
    }

    /// <summary>
    ///     Returns the path to the folder containing the executing <see langword="type" />
    /// </summary>
    /// <param name="startupClass"></param>
    /// <returns></returns>
    internal static string GetProjectPath(Type startupClass)
    {
        var assembly = startupClass.GetTypeInfo().Assembly;
        var projectName = assembly.GetName().Name;
        if (projectName == null) return null;

        var applicationBasePath = AppContext.BaseDirectory;
        DirectoryInfo directoryInfo = new(applicationBasePath);

        do
        {
            directoryInfo = directoryInfo.Parent;
            if (directoryInfo == null) continue;

            DirectoryInfo projectDirectoryInfo = new(directoryInfo.FullName);
            if (!projectDirectoryInfo.Exists) continue;

            {
                FileInfo projectFileInfo = new(Path.Combine(projectDirectoryInfo.FullName, projectName, $"{projectName}.csproj"));
                if (projectFileInfo.Exists) return Path.Combine(projectDirectoryInfo.FullName, projectName);
            }
        } while (directoryInfo is { Parent: not null });
        return null;
    }

    /// <summary>
    ///     Formats a string containing a size in bytes with a suitable suffix
    /// </summary>
    /// <param name="value">Size in bytes</param>
    /// <returns>a string like x.yTB, xGB, xMB or xKB depending on the size</returns>
    internal static string FormatSize(long value)
    {
        return value > BYTES_IN_ONE_TERABYTE ? $"{(decimal)value / BYTES_IN_ONE_TERABYTE:0.#} TB" :
            value > 25 * (long)BYTES_IN_ONE_GIGABYTE ? $"{value / BYTES_IN_ONE_GIGABYTE:n0} GB" :
            value > BYTES_IN_ONE_GIGABYTE ? $"{(decimal)value / BYTES_IN_ONE_GIGABYTE:0.#} GB" :
            value > 25 * BYTES_IN_ONE_MEGABYTE ? $"{value / BYTES_IN_ONE_MEGABYTE:n0} MB" :
            value > BYTES_IN_ONE_MEGABYTE ? $"{(decimal)value / BYTES_IN_ONE_MEGABYTE:0.#} MB" :
            value > BYTES_IN_ONE_KILOBYTE ? $"{value / BYTES_IN_ONE_KILOBYTE:n0} KB" : $"{value:n0} bytes";
    }

    /// <summary>
    ///     Generates a random character string for the size provided.
    /// </summary>
    /// <param name="size"></param>
    /// <returns></returns>
    private static string RandomString(long size)
    {
        StringBuilder builder = new();
        Random random = new();

        for (long i = 0; i < size; i++)
        {
            var ch = Convert.ToChar(Convert.ToInt32(Math.Floor(26 * random.NextDouble() + 65)));
            _ = builder.Append(ch);
        }
        return builder.ToString();
    }

    /// <summary>
    ///     Checks to see if the directory specified is empty
    /// </summary>
    /// <param name="path">The directory to check</param>
    /// <returns>
    ///     True if the directory exists as a normal directory and it's empty. If its a Symbolic link and the target is missing
    ///     or the target is empty it returns True. Otherwise False.
    /// </returns>
    internal static bool IsDirectoryEmpty(string path)
    {
        if (!Directory.Exists(path)) return false;
        if (!IsSymbolicLink(path)) return !Directory.EnumerateFileSystemEntries(path).Any();

        var linkTarget = new FileInfo(path).LinkTarget;
        return linkTarget != null && (!SymbolicLinkTargetExists(path) || !Directory.GetFileSystemEntries(linkTarget).Any());
    }

    /// <summary>
    ///     Checks to see if the directory is a Symbolic link and its LinkTarget exists
    /// </summary>
    /// <param name="path"></param>
    /// <returns>True if its a symbolic Link with a target that exists otherwise False</returns>
    private static bool SymbolicLinkTargetExists(string path)
    {
        var linkTarget = new FileInfo(path).LinkTarget;
        return Directory.Exists(linkTarget);
    }

    /// <summary>
    ///     Checks to see if path is a Symbolic link
    /// </summary>
    /// <param name="path"></param>
    /// <returns>True if path is a symbolic link otherwise False</returns>
    private static bool IsSymbolicLink(string path)
    {
        FileInfo file = new(path);
        return file.LinkTarget != null;
    }

    /// <summary>
    ///     Checks the directory is writable
    /// </summary>
    /// <param name="path"></param>
    /// <returns>True if writable else False</returns>
    internal static bool IsDirectoryWritable(string path)
    {
        TraceIn(path);
        if (!Directory.Exists(path)) return TraceOut(false);

        try
        {
            var lastWriteDate = Directory.GetLastWriteTimeUtc(path);
            var tempFile = Path.GetFileNameWithoutExtension(Path.GetRandomFileName()) + IS_DIRECTORY_WRITABLE_GUID + ".tmp";
            using var fs = File.Create(Path.Combine(path, tempFile), 1, FileOptions.DeleteOnClose);
            return TraceOut(SetDirectoryLastWriteUtc(path, lastWriteDate));
        }
        catch
        {
            return TraceOut(false);
        }
    }

    private static bool SetDirectoryLastWriteUtc(string dirPath, DateTime lastWriteDate)
    {
        using var hDir = CreateFile(dirPath, FILE_ACCESS_GENERIC_READ | FILE_ACCESS_GENERIC_WRITE, FileShare.ReadWrite, IntPtr.Zero,
            (FileMode)OPEN_EXISTING, FILE_FLAG_BACKUP_SEMANTICS, IntPtr.Zero);
        var lastWriteTime = lastWriteDate.ToFileTime();
        return SetFileTime(hDir, IntPtr.Zero, IntPtr.Zero, ref lastWriteTime);
    }

    /// <summary>
    ///     Formats a time in seconds ready for display with a suitable suffix like '2 minutes'
    /// </summary>
    /// <param name="seconds">The time in seconds to format</param>
    /// <returns>A formatted time ready for display with a suffix</returns>
    internal static string FormatTimeFromSeconds(int seconds)
    {
        return seconds switch
        {
            60 => "1 minute",
            < 60 => "less than 1 minute",
            3600 => "1 hour",
            < 5400 and > 60 when seconds % 60 == 0 => $"{seconds / 60} minutes",
            < 5400 => $"{seconds / 60}-{seconds / 60 + 1} minutes",
            < 86400 => $"{seconds / 3600}-{seconds / 3600 + 1} hours",
            _ => "a day or more"
        };
    }

    /// <summary>
    ///     Formats a TimeSpan for display
    /// </summary>
    /// <param name="timeSpan"></param>
    /// <returns></returns>
    internal static string FormatTimeSpan(TimeSpan timeSpan)
    {
        return FormatTimeFromSeconds(Convert.ToInt32(timeSpan.TotalSeconds));
    }

    /// <summary>
    ///     Formats a TimeSpan for display in minutes only
    /// </summary>
    /// <param name="timeSpan"></param>
    /// <returns></returns>
    internal static string FormatTimeSpanMinutesOnly(TimeSpan timeSpan)
    {
        var totalMinutes = Convert.ToInt32(timeSpan.TotalMinutes);
        if (totalMinutes < 1) return timeSpan.TotalSeconds == 0 ? "0s" : timeSpan.TotalSeconds < 1 ? "1s" : $"{timeSpan.TotalSeconds:n0}s";

        return $"{totalMinutes}m";
    }

    /// <summary>
    ///     Formats a string containing a speed with a suitable suffix
    /// </summary>
    /// <param name="value">in bytes per second</param>
    /// <returns>a string like x.yTB/s, xGB/s, xMB/s or xKB/s or bytes/s depending on speed</returns>
    internal static string FormatSpeed(long value)
    {
        return value switch
        {
            // if disk speed greater than 1TB/s return x.yTB/s
            // if disk speed greater than 25GB/s return xGB/s
            // if disk speed greater than 1GB/s return x.yGB/s
            // if disk speed greater than 25MB/s return x.yMB/s
            // if disk speed greater than 1MB/s return x.yyMB/s
            // if disk speed greater than 1KB/s return xKB/s
            // else return bytes/s
            > BYTES_IN_ONE_TERABYTE => $"{(decimal)value / BYTES_IN_ONE_TERABYTE:0.#} TB/s",
            > 25 * (long)BYTES_IN_ONE_GIGABYTE => $"{value / BYTES_IN_ONE_GIGABYTE:n0} GB/s",
            > BYTES_IN_ONE_GIGABYTE => $"{(decimal)value / BYTES_IN_ONE_GIGABYTE:0.#} GB/s",
            > 25 * BYTES_IN_ONE_MEGABYTE => $"{value / BYTES_IN_ONE_MEGABYTE:n0} MB/s",
            > BYTES_IN_ONE_MEGABYTE => $"{(decimal)value / BYTES_IN_ONE_MEGABYTE:0.#} MB/s",
            _ => value > BYTES_IN_ONE_KILOBYTE ? $"{value / BYTES_IN_ONE_KILOBYTE:n0} KB/s" : $"{value:n0} bytes/s"
        };
    }

    /// <summary>
    ///     Runs a speed test on the disk provided.
    /// </summary>
    /// <param name="pathToDiskToTest">The path to test.</param>
    /// <param name="testIterations"></param>
    /// <param name="readSpeed">in bytes per second</param>
    /// <param name="writeSpeed">in bytes per second</param>
    /// <param name="testFileSize"></param>
    /// <param name="ct">A cancellation token so we can act on cancelling</param>
    internal static void DiskSpeedTest(string pathToDiskToTest, long testFileSize, int testIterations, out long readSpeed, out long writeSpeed,
        CancellationToken ct)
    {
        TraceIn(pathToDiskToTest, testFileSize, testIterations);
        var tempPath = Path.GetTempPath();
        readSpeed = DiskSpeedTest(pathToDiskToTest, tempPath, testFileSize, testIterations, ct);
        writeSpeed = DiskSpeedTest(tempPath, pathToDiskToTest, testFileSize, testIterations, ct);
        TraceOut();
    }

    /// <summary>
    ///     Stops the Windows Service specified if its running
    /// </summary>
    /// <param name="serviceName"></param>
    /// <param name="timeoutMilliseconds"></param>
    /// <returns>True if the service stopped successfully or it was stopped already</returns>
    [SupportedOSPlatform("windows")]
    internal static bool StopService(string serviceName, int timeoutMilliseconds)
    {
        TraceIn();
        ServiceController service = new(serviceName);

        try
        {
            var timeout = TimeSpan.FromMilliseconds(timeoutMilliseconds);
            if (service.Status == ServiceControllerStatus.Running) service.Stop();
            service.WaitForStatus(ServiceControllerStatus.Stopped, timeout);
        }
        catch
        {
            return TraceOut(false);
        }
        return TraceOut(true);
    }

    /// <summary>
    ///     Restarts the Windows Service specified
    /// </summary>
    /// <param name="serviceName"></param>
    /// <param name="timeoutMilliseconds"></param>
    /// <returns>True if the service restarted successfully</returns>
    [SupportedOSPlatform("windows")]
    internal static bool RestartService(string serviceName, int timeoutMilliseconds)
    {
        TraceIn();
        ServiceController service = new(serviceName);

        try
        {
            var tickCount = Environment.TickCount;
            var timeout = TimeSpan.FromMilliseconds(timeoutMilliseconds);
            if (service.Status == ServiceControllerStatus.Running) service.Stop();
            service.WaitForStatus(ServiceControllerStatus.Stopped, timeout);

            // count the rest of the timeout
            var tickCount2 = Environment.TickCount;
            timeout = TimeSpan.FromMilliseconds(timeoutMilliseconds - (tickCount2 - tickCount));

            switch (service.Status)
            {
                case ServiceControllerStatus.Stopped:
                    service.Start();
                    break;
                case ServiceControllerStatus.Paused:
                    service.Continue();
                    break;
                case ServiceControllerStatus.StartPending:
                    break;
                case ServiceControllerStatus.StopPending:
                    break;
                case ServiceControllerStatus.Running:
                    break;
                case ServiceControllerStatus.ContinuePending:
                    break;
                case ServiceControllerStatus.PausePending:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(serviceName));
            }
            service.WaitForStatus(ServiceControllerStatus.Running, timeout);
        }
        catch
        {
            return TraceOut(false);
        }
        return TraceOut(true);
    }

    /// <summary>
    ///     Validates the xml provided against the xsd provided.
    /// </summary>
    /// <param name="xmlPath"></param>
    /// <param name="xsdPath"></param>
    /// <returns>True is the xml is valid</returns>
    internal static bool ValidateXml(string xmlPath, string xsdPath)
    {
        var xml = new XmlDocument();
        xml.Load(xmlPath);
        _ = xml.Schemas.Add(null, xsdPath);

        try
        {
            xml.Validate(null);
        }
        catch (XmlSchemaValidationException)
        {
            return false;
        }
        return true;
    }

    /// <summary>
    ///     Validates the xml provided against the xsd provided.
    /// </summary>
    /// <param name="xmlPath"></param>
    /// <param name="resourceName"></param>
    /// <returns>True is the xml is valid</returns>
    internal static bool ValidateXmlFromResources(string xmlPath, string resourceName)
    {
        var xml = new XmlDocument();
        xml.Load(xmlPath);
        var myAssembly = Assembly.GetExecutingAssembly();

        using (var schemaStream = myAssembly.GetManifestResourceStream(resourceName))
        {
            if (schemaStream != null)
            {
                using var schemaReader = XmlReader.Create(schemaStream);
                _ = xml.Schemas.Add(null, schemaReader);
            }
        }

        try
        {
            xml.Validate(null);
        }
        catch (XmlSchemaValidationException)
        {
            return false;
        }
        return true;
    }

    /// <summary>
    /// </summary>
    /// <param name="sourcePath"></param>
    /// <param name="destinationPath"></param>
    /// <param name="testFileSize"></param>
    /// <param name="testIterations"></param>
    /// <param name="ct">A cancellation token so we can act on cancelling</param>
    /// <returns>The bytes read/written in 1s</returns>
    private static long DiskSpeedTest(string sourcePath, string destinationPath, long testFileSize, int testIterations, CancellationToken ct)
    {
        TraceIn();
        if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();
        const long randomStringSize = 500_000;
        const int streamWriteBufferSize = 20 * BYTES_IN_ONE_MEGABYTE;
        var randomText = RandomString(randomStringSize);
        var appendIterations = testFileSize / randomStringSize;
        double totalPerf = 0;

        for (var j = 1; j <= testIterations; j++)
        {
            var firstPathFilename = sourcePath + "\\" + j + SPEED_TEST_GUID + "test.tmp";
            var secondPathFilename = destinationPath + "\\" + j + SPEED_TEST_GUID + "test.tmp";
            if (File.Exists(firstPathFilename)) _ = FileDelete(firstPathFilename);
            if (File.Exists(secondPathFilename)) _ = FileDelete(secondPathFilename);

            using (StreamWriter sWriter = new(firstPathFilename, true, Encoding.UTF8, streamWriteBufferSize))
            {
                for (long i = 1; i <= appendIterations; i++)
                {
                    if (!ct.IsCancellationRequested) sWriter.Write(randomText);
                }
            }
            Trace($"{firstPathFilename} created");
            testFileSize = GetFileLength(firstPathFilename);

            if (ct.IsCancellationRequested)
            {
                if (File.Exists(firstPathFilename)) _ = FileDelete(firstPathFilename);
                if (File.Exists(secondPathFilename)) _ = FileDelete(secondPathFilename);
                ct.ThrowIfCancellationRequested();
            }
            var sw = Stopwatch.StartNew();
            File.Copy(firstPathFilename, secondPathFilename);

            if (ct.IsCancellationRequested)
            {
                if (File.Exists(firstPathFilename)) _ = FileDelete(firstPathFilename);
                if (File.Exists(secondPathFilename)) _ = FileDelete(secondPathFilename);
                ct.ThrowIfCancellationRequested();
            }
            Trace($"{firstPathFilename} copied as {secondPathFilename}");
            var interval = sw.Elapsed;
            if (File.Exists(firstPathFilename)) _ = FileDelete(firstPathFilename);
            if (File.Exists(secondPathFilename)) _ = FileDelete(secondPathFilename);
            Trace($"testFileSize: {testFileSize}, interval.TotalSeconds: {interval.TotalSeconds}");
            totalPerf += testFileSize / interval.TotalSeconds;
        }

        // maybe interval.TotalSeconds is so small sometimes that we get an error
        // may need to check for TotalSeconds <0.0.17 and exit accordingly without the division attempt
        Trace($"totalPerf: {totalPerf}, testIterations: {testIterations}");
        var returnValue = Convert.ToInt64(totalPerf / testIterations);
        return TraceOut(returnValue);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string GetFullyQualifiedCurrentMethodName()
    {
        var sf = new StackTrace().GetFrame(2);
        if (sf == null) return string.Empty;

        var name = sf.GetMethod()?.Name;

        if (name is "MoveNext")

            // We're inside an async method
            name = sf.GetMethod()?.ReflectedType?.Name.Split(new[] { '<', '>' }, StringSplitOptions.RemoveEmptyEntries)[0];
        return $"{sf.GetMethod()?.DeclaringType?.FullName}.{name}";
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void TraceIn()
    {
        var methodName = GetFullyQualifiedCurrentMethodName();
        Trace($"{methodName} enter");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void TraceIn(params object[] parameters)
    {
        var methodName = GetFullyQualifiedCurrentMethodName();
        Trace($"{methodName} enter");

        for (var index = 0; index < parameters.Length; index++)
        {
            var parameter = parameters[index];
            Trace($"param{index} = {parameter}");
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static T TraceOut<T>(T t, string text = "")
    {
        var methodName = GetFullyQualifiedCurrentMethodName();

        if (t is IEnumerable array)
        {
            Trace($"{methodName} exit {text}");

            foreach (var value in array)
            {
                Trace($"{methodName} exit {value}");
            }
        }
        else
            Trace($"{methodName} exit {t} {text}");
        return t;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static T TraceOut<T>(string text = "")
    {
        var methodName = GetFullyQualifiedCurrentMethodName();
        Trace($"{methodName} exit {text}");
        return default;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static string TraceOut(string value)
    {
        var methodName = GetFullyQualifiedCurrentMethodName();
        Trace($"{methodName} exit {value}");
        return value;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void TraceOut()
    {
        var methodName = GetFullyQualifiedCurrentMethodName();
        Trace($"{methodName} exit");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void Trace(string value)
    {
        var textArrayToWrite = value.Split('\n');
        var threadId = Environment.CurrentManagedThreadId;

        foreach (var line in textArrayToWrite.Where(static line => line.HasValue()))
        {
            System.Diagnostics.Trace.WriteLine($"{DateTime.Now:dd-MM-yy HH:mm:ss.ff} : {threadId,-2} : {line}");
        }
    }

    /// <summary>
    ///     Deletes the file specified if it exists even if it was readonly. Returns true if the file doesn't exist or if it
    ///     was deleted succesfully
    /// </summary>
    /// <param name="path"></param>
    internal static bool FileDelete(string path)
    {
        if (!File.Exists(path)) return true;

        ClearFileAttribute(path, FileAttributes.ReadOnly);
        File.Delete(path);
        return true;
    }

    private static void DeleteEmptyDirectories(string directory, ICollection<string> list, string rootDirectory)
    {
        TraceIn(directory);

        try
        {
            foreach (var subDirectory in Directory.EnumerateDirectories(directory))
            {
                DeleteEmptyDirectories(subDirectory, list, rootDirectory);
            }

            if (IsDirectoryEmpty(directory))
            {
                try
                {
                    if (directory != rootDirectory)
                    {
                        Trace($"Deleting empty directory {directory}");
                        list.Add(directory);
                        Directory.Delete(directory);
                    }
                }
                catch (UnauthorizedAccessException) { }
                catch (DirectoryNotFoundException) { }
            }
        }
        catch (UnauthorizedAccessException) { }
        TraceOut();
    }

    /// <summary>
    ///     Deletes any empty directories in the directory specified and checks recursively all its subdirectories.
    /// </summary>
    /// <param name="directory">The directory to check</param>
    /// <exception cref="ArgumentException"></exception>
    /// <returns>An array of the directory paths that were removed</returns>
    internal static IEnumerable<string> DeleteEmptyDirectories(string directory)
    {
        TraceIn();
        ArgumentException.ThrowIfNullOrEmpty(directory, nameof(directory));
        List<string> listOfDirectoriesDeleted = new();
        DeleteEmptyDirectories(directory, listOfDirectoriesDeleted, directory);
        return TraceOut(listOfDirectoriesDeleted.ToArray());
    }

    private static void DeleteBrokenSymbolicLinks(string directory, bool includeRoot, ICollection<string> list, string rootDirectory)
    {
        TraceIn(directory);

        try
        {
            if (!SymbolicLinkTargetExists(directory))
            {
                try
                {
                    if (includeRoot || directory != rootDirectory)
                    {
                        Trace($"Deleting broken symbolic link directory {directory}");
                        list.Add(directory);
                        Directory.Delete(directory);
                    }
                }
                catch (UnauthorizedAccessException) { }
                catch (DirectoryNotFoundException) { }
            }

            if (Directory.Exists(directory) && !IsSymbolicLink(directory))
            {
                foreach (var subDirectory in Directory.EnumerateDirectories(directory))
                {
                    DeleteBrokenSymbolicLinks(subDirectory, includeRoot, list, rootDirectory);
                }
            }
        }
        catch (UnauthorizedAccessException) { }
        TraceOut();
    }

    internal static void RegexPathFixer(Match m, ref string path, ref string pathToTarget)
    {
        for (var i = 0; i < m.Groups.Count; i++)
        {
            if (m.Groups[i].GetType() != typeof(Group)) continue;

            var g = m.Groups[i];
            path = path.Replace($"${i}", g.Value);
            pathToTarget = pathToTarget.Replace($"${i}", g.Value);
        }
    }

    /// <summary>
    ///     Removes any '$0', '$1', '$2' etc from the input string
    /// </summary>
    /// <param name="input">The string to remove the $0 from</param>
    /// <returns>The string without the $ values</returns>
    internal static string RemoveRegexGroupsFromString(string input)
    {
        const int maxValueToCheck = 20;
        var newString = input;

        for (var i = 0; i <= maxValueToCheck; i++)
        {
            newString = newString.Replace($"${i}", "");
        }
        return newString;
    }

    internal static string GetIndexFolder(string path)
    {
        var a = new DirectoryInfo(path);
        return a.Name;
    }

    /// <summary>
    ///     Returns the path to the topmost directory in the path provided
    /// </summary>
    /// <param name="path">The full path to a file or directory</param>
    /// <returns>The path to the topmost writable folder or null if no folders are writable</returns>
    internal static string GetRootPath(string path)
    {
        // consider \\nas1.local/assets1/_TV or \\nas1/assets1/_TV/Show1/Season 1/Episode1.mkv or c:\folder1\folder2
        // first we need to process UNC paths differently to local paths
        var directoryInfo = new FileInfo(path).Directory;
        var fullName = directoryInfo?.Root.FullName;
        return IsDirectoryWritable(fullName) ? fullName : null;
    }

    /// <summary>
    ///     Deletes any empty directories in the directory specified and checks recursively all its sub-directories.
    /// </summary>
    /// <param name="directory">The directory to check</param>
    /// <param name="includeRoot">True to include the root folder for deletion</param>
    /// <exception cref="ArgumentException"></exception>
    /// <returns>An array of the directory paths that were removed</returns>
    internal static IEnumerable<string> DeleteBrokenSymbolicLinks(string directory, bool includeRoot)
    {
        TraceIn();
        ArgumentException.ThrowIfNullOrEmpty(directory, nameof(directory));
        List<string> listOfDirectoriesDeleted = new();
        DeleteBrokenSymbolicLinks(directory, includeRoot, listOfDirectoriesDeleted, directory);
        return TraceOut(listOfDirectoriesDeleted.ToArray());
    }

    /// <summary>
    ///     Converts a GB value to a size in bytes
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    internal static long ConvertGBtoBytes(long value)
    {
        return Convert.ToInt64(value * BYTES_IN_ONE_GIGABYTE);
    }

    /// <summary>
    ///     Returns the TimeSpan between the DateTime provided and the target TimeSpan
    /// </summary>
    /// <param name="startTime"></param>
    /// <param name="targetTime"></param>
    /// <returns></returns>
    internal static TimeSpan TimeLeft(DateTime startTime, TimeSpan targetTime)
    {
        TimeSpan oneDay = new(24, 0, 0);
        var timeLeft = oneDay - new TimeSpan(startTime.Hour, startTime.Minute, startTime.Second) + targetTime;
        if (timeLeft.TotalHours > 24) timeLeft -= oneDay;
        return timeLeft;
    }

    /// <summary>
    ///     Returns True if FileName is accessible (not locked) by another process
    /// </summary>
    /// <param name="path"></param>
    /// <returns>True if not locked or False if locked</returns>
    private static bool FileIsAccessible(string path)
    {
        try
        {
            FileStream fileStream = new(path, FileMode.Open, FileAccess.Read);
            fileStream.Close();
        }
        catch (IOException)
        {
            return false;
        }
        return true;
    }

    /// <summary>
    ///     Compares version numbers and returns True if the available version is newer than installed
    /// </summary>
    /// <param name="installedVersion"></param>
    /// <param name="availableVersion"></param>
    /// <returns>False if either are Null</returns>
    internal static bool VersionIsNewer(string installedVersion, string availableVersion)
    {
        if (installedVersion.HasNoValue() || availableVersion.HasNoValue()) return false;

        var installed = new Version(installedVersion);
        var available = new Version(availableVersion);
        return installed.CompareTo(available) < 0;
    }

    internal static bool StringContainsFixedSpace(string stringToTest)
    {
        var fixedSpace = Convert.ToChar(160);

        for (var i = 0; i < stringToTest.Length; i++)
        {
            var chr = stringToTest[i];
            if (chr != fixedSpace) continue;

            Trace($"{stringToTest} contains a Fixed Space at {i}");
            return true;
        }
        return false;
    }

    internal static string ReplaceFixedSpace(string stringToReplace)
    {
        var fixedSpace = Convert.ToChar(160);
        var regularSpace = Convert.ToChar(32);
        return stringToReplace.Replace(fixedSpace, regularSpace);
    }

    /// <summary>
    /// </summary>
    /// <param name="path"></param>
    /// <returns>The new path without fixed spaces</returns>
    /// <exception cref="FileNotFoundException"></exception>
    internal static string RenameFileToRemoveFixedSpaces(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path, nameof(path));
        if (!File.Exists(path)) throw new FileNotFoundException(Resources.FileNotFound, path);

        if (!StringContainsFixedSpace(path)) return path;

        var newPath = ReplaceFixedSpace(path);
        _ = FileMove(path, newPath);
        if (!File.Exists(newPath)) throw new ApplicationException("Unable to rename file to remove fixed spaces");

        return newPath;
    }

    [SupportedOSPlatform("windows")]
    internal static bool ShareFolder(string folderPath, string shareName, string description)
    {
        try
        {
            _ = Directory.CreateDirectory(folderPath);
            var oManagementClass = new ManagementClass("Win32_Share");
            var inputParameters = oManagementClass.GetMethodParameters("Create");
            inputParameters["Description"] = description;
            inputParameters["Name"] = shareName;
            inputParameters["Path"] = folderPath;
            inputParameters["Type"] = 0x0; //disk drive 
            inputParameters["MaximumAllowed"] = null;
            inputParameters["Access"] = null;
            inputParameters["Password"] = null;
            var outputParameters = oManagementClass.InvokeMethod("Create", inputParameters, null);

            if ((uint)outputParameters.Properties["ReturnValue"].Value != 0)
                throw new Exception("There is a problem while sharing the directory.");
        }
        catch (Exception ex)
        {
            Trace(ex.ToString());
            return false;
        }
        return true;
    }

    [SupportedOSPlatform("windows")]
    internal static void AddPermissions(string sharedFolderName, string domain, string userName)
    {
        // Step 1 - Getting the user Account Object
        var sharedFolder = GetSharedFolderObject(sharedFolderName);

        if (sharedFolder == null)
        {
            Trace("The shared folder with given name does not exist");
            return;
        }
        var securityDescriptorObject = sharedFolder.InvokeMethod("GetSecurityDescriptor", null, null);

        if (securityDescriptorObject == null)
        {
            Trace(string.Format(CultureInfo.InvariantCulture, "Error extracting security descriptor of the shared path {0}.", sharedFolderName));
            return;
        }
        var returnCode = Convert.ToInt32(securityDescriptorObject.Properties["ReturnValue"].Value);

        if (returnCode != 0)
        {
            Trace(string.Format(CultureInfo.InvariantCulture, "Error extracting security descriptor of the shared path {0}. Error Code{1}.",
                sharedFolderName, returnCode.ToString()));
            return;
        }
        var securityDescriptor = securityDescriptorObject.Properties["Descriptor"].Value as ManagementBaseObject;

        // Step 2 -- Extract Access Control List from the security descriptor
        var existingAccessControlEntriesCount = 0;

        if (securityDescriptor != null)
        {
            // ReSharper disable once StringLiteralTypo
            if (securityDescriptor.Properties["DACL"].Value is not ManagementBaseObject[] accessControlList)
            {
                // If there aren't any entries in access control list or the list is empty - create one
                accessControlList = new ManagementBaseObject[1];
            }
            else
            {
                // Otherwise, resize the list to allow for all new users.
                existingAccessControlEntriesCount = accessControlList.Length;
                Array.Resize(ref accessControlList, accessControlList.Length + 1);
            }

            // Step 3 - Getting the user Account Object
            var userAccountObject = GetUserAccountObject(domain, userName);
            var securityIdentifierObject = new ManagementObject($"Win32_SID.SID='{(string)userAccountObject.Properties["SID"].Value}'");
            securityIdentifierObject.Get();

            // Step 4 - Create Trustee Object
            var trusteeObject = CreateTrustee(domain, userName, securityIdentifierObject);

            // Step 5 - Create Access Control Entry
            var accessControlEntry = CreateAccessControlEntry(trusteeObject, false);

            // Step 6 - Add Access Control Entry to the Access Control List
            accessControlList[existingAccessControlEntriesCount] = accessControlEntry;

            // Step 7 - Assign access Control list to security descriptor 
            // ReSharper disable once StringLiteralTypo
            securityDescriptor.Properties["DACL"].Value = accessControlList;
        }

        // Step 8 - Assign access Control list to security descriptor 
        var parameterForSetSecurityDescriptor = sharedFolder.GetMethodParameters("SetSecurityDescriptor");
        parameterForSetSecurityDescriptor["Descriptor"] = securityDescriptor;
        _ = sharedFolder.InvokeMethod("SetSecurityDescriptor", parameterForSetSecurityDescriptor, null);
    }

    /// <summary>
    ///     The method returns ManagementObject object for the shared folder with given name
    /// </summary>
    /// <param name="sharedFolderName">string containing name of shared folder</param>
    /// <returns>Object of type ManagementObject for the shared folder.</returns>
    [SupportedOSPlatform("windows")]
    private static ManagementObject GetSharedFolderObject(string sharedFolderName)
    {
        ManagementObject sharedFolderObject = null;

        //Creating a searcher object to search 
        var searcher = new ManagementObjectSearcher("Select * from Win32_LogicalShareSecuritySetting where Name = '" + sharedFolderName + "'");
        var resultOfSearch = searcher.Get();
        if (resultOfSearch.Count <= 0) return null;

        //The search might return a number of objects with same shared name. I assume there is just going to be one
        foreach (var o in resultOfSearch)
        {
            sharedFolderObject = (ManagementObject)o;
            break;
        }
        return sharedFolderObject;
    }

    /// <summary>
    ///     The method returns ManagementObject object for the user folder with given name
    /// </summary>
    /// <param name="domain">string containing domain name of user </param>
    /// <param name="alias">string containing the user's network name </param>
    /// <returns>Object of type ManagementObject for the user folder.</returns>
    [SupportedOSPlatform("windows")]
    private static ManagementObject GetUserAccountObject(string domain, string alias)
    {
        ManagementObject userAccountObject = null;
        var searcher = new ManagementObjectSearcher($"select * from Win32_Account where Name = '{alias}' and Domain='{domain}'");
        var resultOfSearch = searcher.Get();
        if (resultOfSearch.Count <= 0) return null;

        foreach (var o in resultOfSearch)
        {
            userAccountObject = (ManagementObject)o;
            break;
        }
        return userAccountObject;
    }

    /// <summary>
    ///     Returns the Security Identifier Sid of the given user
    /// </summary>
    /// <param name="userAccountObject">The user object who's Sid needs to be returned</param>
    /// <returns></returns>
    [SupportedOSPlatform("windows")]
    internal static ManagementObject GetAccountSecurityIdentifier(ManagementBaseObject userAccountObject)
    {
        var securityIdentifierObject = new ManagementObject($"Win32_SID.SID='{(string)userAccountObject.Properties["SID"].Value}'");
        securityIdentifierObject.Get();
        return securityIdentifierObject;
    }

    /// <summary>
    ///     Create a trustee object for the given user
    /// </summary>
    /// <param name="domain">name of domain</param>
    /// <param name="userName">the network name of the user</param>
    /// <param name="securityIdentifierOfUser">Object containing User's sid</param>
    /// <returns></returns>
    [SupportedOSPlatform("windows")]
    private static ManagementObject CreateTrustee(string domain, string userName, ManagementBaseObject securityIdentifierOfUser)
    {
        var trusteeObject = new ManagementClass("Win32_Trustee").CreateInstance();
        trusteeObject.Properties["Domain"].Value = domain;
        trusteeObject.Properties["Name"].Value = userName;
        trusteeObject.Properties["SID"].Value = securityIdentifierOfUser.Properties["BinaryRepresentation"].Value;
        trusteeObject.Properties["SidLength"].Value = securityIdentifierOfUser.Properties["SidLength"].Value;
        trusteeObject.Properties["SIDString"].Value = securityIdentifierOfUser.Properties["SID"].Value;
        return trusteeObject;
    }

    /// <summary>
    ///     Create an Access Control Entry object for the given user
    /// </summary>
    /// <param name="trustee">The user's trustee object</param>
    /// <param name="deny">boolean to say if user permissions should be assigned or denied</param>
    /// <returns></returns>
    [SupportedOSPlatform("windows")]
    private static ManagementObject CreateAccessControlEntry(ICloneable trustee, bool deny)
    {
        var aceObject = new ManagementClass("Win32_ACE").CreateInstance();

        aceObject.Properties["AccessMask"].Value = 0x1U | 0x2U | 0x4U | 0x8U | 0x10U | 0x20U | 0x40U | 0x80U | 0x100U | 0x10000U | 0x20000U |
                                                   0x40000U | 0x80000U | 0x100000U; // all permissions
        aceObject.Properties["AceFlags"].Value = 0x0U; // no flags
        aceObject.Properties["AceType"].Value = deny ? 1U : 0U; // 0 = allow, 1 = deny
        aceObject.Properties["Trustee"].Value = trustee;
        return aceObject;
    }
}

[SupportedOSPlatform("windows")]
[SuppressMessage("ReSharper", "UnusedMember.Global")]
internal sealed class Win32Share
{
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    public enum ShareType : uint
    {
        DiskDrive = 0x0,

        PrintQueue = 0x1,

        Device = 0x2,

        Ipc = 0x3,

        DiskDriveAdmin = 0x80000000,

        PrintQueueAdmin = 0x80000001,

        DeviceAdmin = 0x80000002,

        IpcAdmin = 0x80000003
    }

    private readonly ManagementObject mWinShareObject;

    private Win32Share(ManagementObject obj)
    {
        mWinShareObject = obj;
    }

    public uint AccessMask => Convert.ToUInt32(mWinShareObject[nameof(AccessMask)]);

    public bool AllowMaximum => Convert.ToBoolean(mWinShareObject[nameof(AllowMaximum)]);

    public string Caption => Convert.ToString(mWinShareObject[nameof(Caption)]);

    public string Description => Convert.ToString(mWinShareObject[nameof(Description)]);

    public DateTime InstallDate => Convert.ToDateTime(mWinShareObject[nameof(InstallDate)]);

    public uint MaximumAllowed => Convert.ToUInt32(mWinShareObject[nameof(MaximumAllowed)]);

    public string Name => Convert.ToString(mWinShareObject[nameof(Name)]);

    public string Path => Convert.ToString(mWinShareObject[nameof(Path)]);

    [SupportedOSPlatform("windows")] public string Status => Convert.ToString(mWinShareObject[nameof(Status)]);

    [SupportedOSPlatform("windows")] public ShareType Type => (ShareType)Convert.ToUInt32(mWinShareObject[nameof(Type)]);

    [SupportedOSPlatform("windows")]
    public MethodStatus Delete()
    {
        var result = mWinShareObject.InvokeMethod("Delete", Array.Empty<object>());
        var r = Convert.ToUInt32(result);
        return (MethodStatus)r;
    }

    [SupportedOSPlatform("windows")]
    internal static MethodStatus Create(string path, string name, ShareType type, uint maximumAllowed, string description, string password)
    {
        var mc = new ManagementClass("Win32_Share");
        object[] parameters = { path, name, (uint)type, maximumAllowed, description, password, null };
        var result = mc.InvokeMethod("Create", parameters);
        var r = Convert.ToUInt32(result);
        return (MethodStatus)r;
    }

    [SupportedOSPlatform("windows")]
    private static IEnumerable<Win32Share> GetAllShares()
    {
        IList<Win32Share> result = new List<Win32Share>();
        var moc = new ManagementClass("Win32_Share").GetInstances();

        foreach (var o in moc)
        {
            result.Add(new Win32Share((ManagementObject)o));
        }
        return result;
    }

    [SupportedOSPlatform("windows")]
    internal static Win32Share GetNamedShare(string name)
    {
        return GetAllShares().FirstOrDefault(s => s.Name == name);
    }

    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    internal enum MethodStatus : uint
    {
        Success = 0,

        AccessDenied = 2,

        UnknownFailure = 8,

        InvalidName = 9,

        InvalidLevel = 10,

        InvalidParameter = 21,

        DuplicateShare = 22,

        RedirectedPath = 23,

        UnknownDevice = 24,

        NetNameNotFound = 25
    }
}