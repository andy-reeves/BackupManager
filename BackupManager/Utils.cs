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
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Security.Principal;
using System.ServiceProcess;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using BackupManager.Entities;
using BackupManager.Extensions;
using BackupManager.Properties;

namespace BackupManager;

/// <summary>
///     Common Utility functions in a static class
/// </summary>
[SuppressMessage("ReSharper", "UnusedMember.Global")]
internal static partial class Utils
{
    [LibraryImport("kernel32.dll", EntryPoint = "GetDiskFreeSpaceExW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetDiskFreeSpaceEx(string lpDirectoryName, out long lpFreeBytesAvailable, out long lpTotalNumberOfBytes,
        out long lpTotalNumberOfFreeBytes);

    #region Constants

#if DEBUG
    /// <summary>
    ///     The end block size.
    /// </summary>
    internal const int EndBlockSize = 16 * BytesInOneKilobyte; // 16K

    /// <summary>
    ///     The middle block size.
    /// </summary>
    internal const int MiddleBlockSize = 16 * BytesInOneKilobyte; // 16K

    /// <summary>
    ///     The start block size.
    /// </summary>
    internal const int StartBlockSize = 16 * BytesInOneKilobyte; // 16K
#else
    /// <summary>
    ///     The end block size.
    /// </summary>
    private const int EndBlockSize = 16 * BytesInOneKilobyte; // 16K

    /// <summary>
    ///     The middle block size.
    /// </summary>
    private const int MiddleBlockSize = 16 * BytesInOneKilobyte; // 16K

    /// <summary>
    ///     The start block size.
    /// </summary>
    private const int StartBlockSize = 16 * BytesInOneKilobyte; // 16K
#endif
    /// <summary>
    ///     The number of bytes in one Terabyte. 2^40 bytes.
    /// </summary>
    private const long BytesInOneTerabyte = 1_099_511_627_776;

    /// <summary>
    ///     The number of bytes in one Gigabyte. 2^30 bytes.
    /// </summary>
    private const int BytesInOneGigabyte = 1_073_741_824;

    /// <summary>
    ///     The number of bytes in one Megabyte. 2^20 bytes.
    /// </summary>
    private const int BytesInOneMegabyte = 1_048_576;

    /// <summary>
    ///     The number of bytes in one Kilobyte. 2^10 bytes.
    /// </summary>
    internal const int BytesInOneKilobyte = 1_024;

    /// <summary>
    ///     The URL of the Pushover messaging service.
    /// </summary>
    private const string PushoverAddress = "https://api.pushover.net/1/messages.json";

    /// <summary>
    ///     Delay between Pushover messages in milliseconds
    /// </summary>
    private const int TimeDelayOnPushoverMessages = 1000;

    #endregion

    #region Static Fields

    /// <summary>
    ///     We use this to pad our logging messages
    /// </summary>
    private static int _lengthOfLargestBackupActionEnumNames;

    /// <summary>
    ///     We start a new Process to Copy the files. Then we can safely Kill the process when cancelling is needed
    /// </summary>
    internal static Process CopyProcess;

    /// <summary>
    ///     The MD5 Crypto Provider
    /// </summary>
    private static readonly MD5 _md5 = MD5.Create();

#if DEBUG
    private static readonly string _logFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "BackupManager_Debug.log");
#else
    private static readonly string _logFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "BackupManager.log");
#endif

    /// <summary>
    ///     We use this to track when we sent the messages. This allows us to delay between messages
    /// </summary>
    private static DateTime _timeLastPushoverMessageSent = DateTime.UtcNow.AddSeconds(-60);

    /// <summary>
    ///     So we can get config values
    /// </summary>
    internal static Config Config;

    private static bool _alreadySendingPushoverMessage;

    #endregion

    #region internal Methods and Operators

    internal static void BackupLogFile()
    {
        var timeLog = DateTime.Now.ToString("yy-MM-dd-HH-mm-ss");
#if DEBUG
        const string suffix = "_Debug";
#else
        const string suffix = "";
#endif

        var destLogFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "BackupManager_Backups",
            $"BackupManager{suffix}_{timeLog}.log");
        if (File.Exists(_logFile)) FileMove(_logFile, destLogFile);

        var traceFiles = GetFiles(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "*BackupManager_Trace.log",
            SearchOption.TopDirectoryOnly);

        foreach (var file in traceFiles)
        {
            var destFileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "BackupManager_Backups",
                $"{new FileInfo(file).Name}_{timeLog}.log");

            try
            {
                FileMove(file, destFileName);
            }
            catch (IOException) { }
        }
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
    internal static void FileMove(string sourceFileName, string destFileName)
    {
        TraceIn(sourceFileName, destFileName);
        EnsureDirectoriesForFilePath(destFileName);
        File.Move(sourceFileName, destFileName);
        TraceOut();
    }

    /// <summary>
    ///     Copies an existing file to a new file asynchronously. Overwriting a file of the same name is not allowed. Ensures
    ///     the destination folder exists too.
    /// </summary>
    /// <param name="sourceFileName">The file to copy.</param>
    /// <param name="destFileName">The name of the destination file. This cannot be a directory or an existing file.</param>
    /// <returns>True if copy was successful or False</returns>
    internal static bool FileCopy(string sourceFileName, string destFileName)
    {
        TraceIn(sourceFileName, destFileName);
        if (destFileName == null || sourceFileName == null) return false;

        if (File.Exists(destFileName)) throw new NotSupportedException("Destination file already exists");

        EnsureDirectoriesForFilePath(destFileName);

        // ReSharper disable once CommentTypo
        // we create the destination file so xcopy knows its a file and can copy over it
        File.WriteAllText(destFileName, "Temp file");

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
        var returnValue = CopyProcess.Start();

        if (returnValue)
        {
            // We wait because otherwise lots of copy processes will start at once
            CopyProcess.WaitForExit();
        }
        else
            return TraceOut(false);

        return TraceOut(CopyProcess.ExitCode == 0);
    }

    /// <summary>
    ///     Converts a MB value to a size in bytes
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    internal static long ConvertMBtoBytes(long value)
    {
        return Convert.ToInt64(value * BytesInOneMegabyte);
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
#if DEBUG
    internal static string CreateHashForByteArray(byte[] firstByteArray, byte[] secondByteArray, byte[] thirdByteArray)
#else
    private static string CreateHashForByteArray(byte[] firstByteArray, byte[] secondByteArray, byte[] thirdByteArray)
#endif
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
        return ByteArrayToString(_md5.ComputeHash(byteArrayToHash));
    }

    /// <summary>
    ///     Clears any event handlers on the instance provided
    /// </summary>
    /// <param name="instance"></param>
    internal static void ClearEvents(object instance)
    {
        var eventsToClear = instance.GetType().GetEvents(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);

        foreach (var eventInfo in eventsToClear)
        {
            var fieldInfo = instance.GetType().GetField(eventInfo.Name, BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
            if (fieldInfo?.GetValue(instance) is not Delegate eventHandler) continue;

            foreach (var item in eventHandler.GetInvocationList())
            {
                eventInfo.GetRemoveMethod(fieldInfo.IsPrivate)?.Invoke(instance, new object[] { item });
            }
        }
    }

    /// <summary>
    ///     Ensures all the directories on the way to the file are created.
    /// </summary>
    /// <param name="filePath">
    /// </param>
#if DEBUG
    internal static void EnsureDirectoriesForFilePath(string filePath)
#else
    private static void EnsureDirectoriesForFilePath(string filePath)
#endif
    {
        var directoryName = new FileInfo(filePath).DirectoryName;
        if (directoryName != null) Directory.CreateDirectory(directoryName);
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
    internal static string[] GetFiles(string path)
    {
        return GetFiles(path, "*", SearchOption.AllDirectories, 0, 0);
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
    /// <returns>
    /// </returns>
    internal static string[] GetFiles(string path, string filters)
    {
        return GetFiles(path, filters, SearchOption.AllDirectories, 0, 0);
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
    /// <returns>
    /// </returns>
    internal static string[] GetFiles(string path, string filters, SearchOption searchOption, FileAttributes directoryAttributesToIgnore)
    {
        return GetFiles(path, filters, searchOption, directoryAttributesToIgnore, 0);
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
    /// <returns>
    /// </returns>
    internal static string[] GetFiles(string path, string filters, SearchOption searchOption)
    {
        return GetFiles(path, filters, searchOption, 0, 0);
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
    /// <returns>
    /// </returns>
    private static string[] GetFiles(string path, string filters, SearchOption searchOption, FileAttributes directoryAttributesToIgnore,
        FileAttributes fileAttributesToIgnore)
    {
        var sw = Stopwatch.StartNew();

        if (!Directory.Exists(path))
        {
            Trace("GetFiles exit with new string[]");
            return Array.Empty<string>();
        }
        DirectoryInfo directoryInfo = new(path);
        if (directoryInfo.Parent != null && AnyFlagSet(directoryInfo.Attributes, directoryAttributesToIgnore)) return TraceOut(Array.Empty<string>());

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
            var dir = pathsToSearch.Dequeue();

            if (searchOption == SearchOption.AllDirectories)
            {
                foreach (var subDir in Directory.GetDirectories(dir)
                             .Where(subDir => !AnyFlagSet(new DirectoryInfo(subDir).Attributes, directoryAttributesToIgnore)))
                {
                    pathsToSearch.Enqueue(subDir);
                }
            }

            foreach (var collection in includeAsArray2.Select(filter => Directory.GetFiles(dir, filter, SearchOption.TopDirectoryOnly))
                         .Select(allFiles => excludeAsArray.Any() ? allFiles.Where(p => !excludeRegex.Match(p).Success) : allFiles))
            {
                foundFiles.AddRange(collection.Where(p => !AnyFlagSet(new FileInfo(p).Attributes, fileAttributesToIgnore)));
            }
        }
        Trace($"Time taken = {sw.Elapsed.TotalSeconds} seconds");
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
        using BufferedStream stream = new(File.OpenRead(fileName), BytesInOneMegabyte);
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
    ///     The get short md 5 hash from file.
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

        if (size >= StartBlockSize + MiddleBlockSize + EndBlockSize)
        {
            var startDownloadPositionForEndBlock = size - EndBlockSize;
            var startDownloadPositionForMiddleBlock = size / 2;
            startBlock = GetLocalFileByteArray(stream, 0, StartBlockSize);
            middleBlock = GetLocalFileByteArray(stream, startDownloadPositionForMiddleBlock, MiddleBlockSize);
            endBlock = GetLocalFileByteArray(stream, startDownloadPositionForEndBlock, EndBlockSize);
        }
        else
            startBlock = GetLocalFileByteArray(stream, 0, size);
        return CreateHashForByteArray(startBlock, middleBlock, endBlock);
    }

    internal static long GetFileLength(string fileName)
    {
        return new FileInfo(fileName).Length;
    }

    internal static DateTime GetFileLastWriteTime(string fileName)
    {
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
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;

        var size = new FileInfo(path).Length;
        if (size == 0) return string.Empty;

        byte[] startBlock;
        byte[] middleBlock = null;
        byte[] endBlock = null;

        if (size >= StartBlockSize + MiddleBlockSize + EndBlockSize)
        {
            var startDownloadPositionForEndBlock = size - EndBlockSize;
            var startDownloadPositionForMiddleBlock = size / 2;
            startBlock = GetLocalFileByteArray(path, 0, StartBlockSize);
            middleBlock = GetLocalFileByteArray(path, startDownloadPositionForMiddleBlock, MiddleBlockSize);
            endBlock = GetLocalFileByteArray(path, startDownloadPositionForEndBlock, EndBlockSize);
        }
        else
            startBlock = GetLocalFileByteArray(path, 0, size);
        return TraceOut(CreateHashForByteArray(startBlock, middleBlock, endBlock));
    }

    private static void SendPushoverMessage(string title, PushoverPriority priority, string message)
    {
        SendPushoverMessage(title, priority, PushoverRetry.None, PushoverExpires.Immediately, message);
    }

    private static void SendPushoverMessage(string title, PushoverPriority priority, PushoverRetry retry, PushoverExpires expires, string message)
    {
        TraceIn();

        if (Config.PushoverOnOff && ((priority is PushoverPriority.Low or PushoverPriority.Lowest && Config.PushoverSendLowOnOff) ||
                                     (priority == PushoverPriority.Normal && Config.PushoverSendNormalOnOff) ||
                                     (priority == PushoverPriority.High && Config.PushoverSendHighOnOff) ||
                                     (priority == PushoverPriority.Emergency && Config.PushoverSendEmergencyOnOff)))
        {
            try
            {
                Dictionary<string, string> parameters = new()
                {
                    { "token", Config.PushoverAppToken },
                    { "user", Config.PushoverUserKey },
                    { "priority", Convert.ChangeType(priority, priority.GetTypeCode()).ToString() },
                    { "message", message },
                    { "title", title }
                };

                if (priority == PushoverPriority.Emergency)
                {
                    if (retry == PushoverRetry.None) retry = PushoverRetry.ThirtySeconds;
                    if (expires == PushoverExpires.Immediately) expires = PushoverExpires.FiveMinutes;
                }
                if (retry != PushoverRetry.None) parameters.Add("retry", Convert.ChangeType(retry, retry.GetTypeCode()).ToString());
                if (expires != PushoverExpires.Immediately) parameters.Add("expire", Convert.ChangeType(expires, expires.GetTypeCode()).ToString());

                // ensures there's a 1s gap between messages
                while (DateTime.UtcNow < _timeLastPushoverMessageSent.AddMilliseconds(TimeDelayOnPushoverMessages))
                {
                    Task.Delay(TimeDelayOnPushoverMessages / 10).Wait();
                }

                using (FormUrlEncodedContent postContent = new(parameters))
                {
                    HttpClient client = new();

                    // ReSharper disable once AccessToDisposedClosure
                    var task = Task.Run(() => client.PostAsync(PushoverAddress, postContent));
                    task.Wait();
                    var response = task.Result;
                    _ = response.EnsureSuccessStatusCode();
                    var applicationLimitRemaining = 0;

                    if (response.Headers.TryGetValues("X-Limit-App-Remaining", out var values))
                        applicationLimitRemaining = Convert.ToInt32(values.First());
                    Trace($"Pushover messages remaining: {applicationLimitRemaining}");

                    if (applicationLimitRemaining < Config.PushoverWarningMessagesRemaining)
                    {
                        if (!_alreadySendingPushoverMessage)
                        {
                            _alreadySendingPushoverMessage = true;

                            SendPushoverMessage("Message Limit Warning", PushoverPriority.High,
                                $"Application Limit Remaining is: {applicationLimitRemaining}");
                            _alreadySendingPushoverMessage = false;
                        }
                    }
                }
                _timeLastPushoverMessageSent = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                // we ignore any push problems
                Log($"Exception sending Pushover message {ex}");
            }
        }
        TraceOut();
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
        var processes = Process.GetProcesses().Where(p => p.ProcessName.StartsWith(processName, StringComparison.CurrentCultureIgnoreCase));

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
        bool returnValue;

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
            return false;
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
        try
        {
            using TcpClient tcpClient = new();
            tcpClient.Connect(host, port);
        }
        catch
        {
            return false;
        }
        return true;
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

    /// <summary>
    ///     Writes the text to the logfile
    /// </summary>
    /// <param name="text"></param>
    internal static void Log(string text)
    {
        var textArrayToWrite = text.Split('\n');

        foreach (var textToWrite in from line in textArrayToWrite where line.HasValue() select $"{DateTime.Now:dd-MM-yy HH:mm:ss} {line}")
        {
            Console.WriteLine(textToWrite);

            if (_logFile.HasValue())
            {
                EnsureDirectoriesForFilePath(_logFile);
                File.AppendAllLines(_logFile, new[] { textToWrite });
            }
            Trace(text);
        }
    }

    /// <summary>
    ///     Logs the text to the LogFile and sends a Pushover message
    /// </summary>
    /// <param name="backupAction"></param>
    /// <param name="text"></param>
    internal static void LogWithPushover(BackupAction backupAction, string text)
    {
        LogWithPushover(backupAction, PushoverPriority.Normal, text);
    }

    /// <summary>
    ///     Logs the text to the LogFile and sends a Pushover message
    /// </summary>
    /// <param name="backupAction"></param>
    /// <param name="priority"></param>
    /// <param name="text"></param>
    internal static void LogWithPushover(BackupAction backupAction, PushoverPriority priority, string text)
    {
        Log(backupAction, text);

        if (Config.PushoverAppToken.HasValue() && Config.PushoverAppToken != "InsertYourPushoverAppTokenHere")
            SendPushoverMessage(Enum.GetName(typeof(BackupAction), backupAction), priority, text);
    }

    /// <summary>
    ///     Logs the text to the LogFile and sends a Pushover message
    /// </summary>
    /// <param name="backupAction"></param>
    /// <param name="priority"></param>
    /// <param name="retry"></param>
    /// <param name="expires"></param>
    /// <param name="text"></param>
    internal static void LogWithPushover(BackupAction backupAction, PushoverPriority priority, PushoverRetry retry, PushoverExpires expires,
        string text)
    {
        Log(backupAction, text);
        if (Config.PushoverAppToken.HasValue()) SendPushoverMessage(Enum.GetName(typeof(BackupAction), backupAction), priority, retry, expires, text);
    }

    #endregion

    #region Methods

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
        return ByteArrayToString(_md5.ComputeHash(byteArrayToHash));
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
        return path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.InvariantCultureIgnoreCase)
            ? path
            : path + Path.DirectorySeparatorChar;
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
#if DEBUG
    private static byte[] GetLocalFileByteArray(Stream stream, long offset, long byteCountToReturn)
#else
    private static byte[] GetLocalFileByteArray(Stream stream, long offset, long byteCountToReturn)
#endif
    {
        stream.Seek(offset, SeekOrigin.Begin);
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
#if DEBUG
    internal static byte[] GetLocalFileByteArray(string fileName, long offset, long byteCountToReturn)
#else
    private static byte[] GetLocalFileByteArray(string fileName, long offset, long byteCountToReturn)
#endif
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

    #endregion

    /// <summary>
    /// </summary>
    /// <param name="folderName"></param>
    /// <param name="freeSpace">in bytes</param>
    /// <param name="totalBytes">in bytes</param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    internal static bool GetDiskInfo(string folderName, out long freeSpace, out long totalBytes)
    {
        if (string.IsNullOrEmpty(folderName)) throw new ArgumentNullException(nameof(folderName));

        if (!folderName.EndsWith("\\", StringComparison.InvariantCultureIgnoreCase)) folderName += '\\';
        return GetDiskFreeSpaceEx(folderName, out freeSpace, out totalBytes, out _);
    }

    /// <summary>
    ///     Returns the path to the folder containing the executing <see langword="type" />
    /// </summary>
    /// <param name="startupClass"></param>
    /// <returns></returns>
    public static string GetProjectPath(Type startupClass)
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
        return value > BytesInOneTerabyte ? $"{(decimal)value / BytesInOneTerabyte:0.#} TB" :
            value > 25 * (long)BytesInOneGigabyte ? $"{value / BytesInOneGigabyte:n0} GB" :
            value > BytesInOneGigabyte ? $"{(decimal)value / BytesInOneGigabyte:0.#} GB" :
            value > 25 * BytesInOneMegabyte ? $"{value / BytesInOneMegabyte:n0} MB" :
            value > BytesInOneMegabyte ? $"{(decimal)value / BytesInOneMegabyte:0.#} MB" :
            value > BytesInOneKilobyte ? $"{value / BytesInOneKilobyte:n0} KB" : $"{value:n0} bytes";
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
        return linkTarget != null && Directory.Exists(linkTarget);
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

        try
        {
            if (!Directory.Exists(path)) return false;

            var randomFileName = Path.GetRandomFileName();
            Trace(randomFileName);
            using var fs = File.Create(Path.Combine(path, randomFileName, ".tmp"), 1, FileOptions.DeleteOnClose);
            return TraceOut(true);
        }
        catch
        {
            return TraceOut(false);
        }
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
            < 120 => $"{seconds} seconds",
            < 3600 => $"{seconds / 60} minutes",
            < 4000 => "1 hour",
            < 86400 => $"{seconds / 3600} hours",
            _ => "a day or so"
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
    ///     Formats a string containing a speed with a suitable suffix
    /// </summary>
    /// <param name="value">in bytes per second</param>
    /// <returns>a string like x.yTB/s, xGB/s, xMB/s or xKB/s or bytes/s depending on speed</returns>
    internal static string FormatSpeed(long value)
    {
        // if disk speed greater than 1TB/s return x.yTB/s
        // if disk speed greater than 25GB/s return xGB/s
        // if disk speed greater than 1GB/s return x.yGB/s
        // if disk speed greater than 25MB/s return x.yMB/s
        // if disk speed greater than 1MB/s return x.yyMB/s
        // if disk speed greater than 1KB/s return xKB/s
        // else return bytes/s
        return value > BytesInOneTerabyte ? $"{(decimal)value / BytesInOneTerabyte:0.#} TB/s" :
            value > 25 * (long)BytesInOneGigabyte ? $"{value / BytesInOneGigabyte:n0} GB/s" :
            value > BytesInOneGigabyte ? $"{(decimal)value / BytesInOneGigabyte:0.#} GB/s" :
            value > 25 * BytesInOneMegabyte ? $"{value / BytesInOneMegabyte:n0} MB/s" :
            value > BytesInOneMegabyte ? $"{(decimal)value / BytesInOneMegabyte:0.#} MB/s" :
            value > BytesInOneKilobyte ? $"{value / BytesInOneKilobyte:n0} KB/s" : $"{value:n0} bytes/s";
    }

    /// <summary>
    ///     Runs a speed test on the disk provided.
    /// </summary>
    /// <param name="pathToDiskToTest">The path to test.</param>
    /// <param name="testIterations"></param>
    /// <param name="readSpeed">in bytes per second</param>
    /// <param name="writeSpeed">in bytes per second</param>
    /// <param name="testFileSize"></param>
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
    /// </summary>
    /// <param name="sourcePath"></param>
    /// <param name="destinationPath"></param>
    /// <param name="testFileSize"></param>
    /// <param name="testIterations"></param>
    /// <returns>The bytes read/written in 1s</returns>
    private static long DiskSpeedTest(string sourcePath, string destinationPath, long testFileSize, int testIterations, CancellationToken ct)
    {
        TraceIn();
        if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();
        const long randomStringSize = 500_000;
        const int streamWriteBufferSize = 20 * BytesInOneMegabyte;
        var randomText = RandomString(randomStringSize);
        var appendIterations = testFileSize / randomStringSize;
        double totalPerf = 0;

        for (var j = 1; j <= testIterations; j++)
        {
            if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();
            var firstPathFilename = sourcePath + "\\" + j + "test.tmp";
            var secondPathFilename = destinationPath + "\\" + j + "test.tmp";
            if (File.Exists(firstPathFilename)) File.Delete(firstPathFilename);
            if (File.Exists(secondPathFilename)) File.Delete(secondPathFilename);

            using (StreamWriter sWriter = new(firstPathFilename, true, Encoding.UTF8, streamWriteBufferSize))
            {
                for (long i = 1; i <= appendIterations; i++)
                {
                    sWriter.Write(randomText);
                }
            }
            Trace($"{firstPathFilename} created");
            testFileSize = GetFileLength(firstPathFilename);
            if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();
            var sw = Stopwatch.StartNew();
            File.Copy(firstPathFilename, secondPathFilename);
            Trace($"{firstPathFilename} copied as {secondPathFilename}");
            var interval = sw.Elapsed;
            File.Delete(firstPathFilename);
            File.Delete(secondPathFilename);
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
        System.Diagnostics.Trace.WriteLine($"{DateTime.Now:dd-MM-yy HH:mm:ss.ff} : {methodName} enter");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void TraceIn(params object[] parameters)
    {
        var methodName = GetFullyQualifiedCurrentMethodName();
        System.Diagnostics.Trace.WriteLine($"{DateTime.Now:dd-MM-yy HH:mm:ss.ff} : {methodName} enter");

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
            System.Diagnostics.Trace.WriteLine($"{DateTime.Now:dd-MM-yy HH:mm:ss.ff} : {methodName} exit {text}");

            foreach (var value in array)
            {
                System.Diagnostics.Trace.WriteLine($"{DateTime.Now:dd-MM-yy HH:mm:ss.ff} : {methodName} exit {value}");
            }
        }
        else
            System.Diagnostics.Trace.WriteLine($"{DateTime.Now:dd-MM-yy HH:mm:ss.ff} : {methodName} exit {t} {text}");
        return t;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static T TraceOut<T>(string text = "")
    {
        var methodName = GetFullyQualifiedCurrentMethodName();
        System.Diagnostics.Trace.WriteLine($"{DateTime.Now:dd-MM-yy HH:mm:ss.ff} : {methodName} exit {text}");
        return default;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static string TraceOut(string value)
    {
        var methodName = GetFullyQualifiedCurrentMethodName();
        System.Diagnostics.Trace.WriteLine($"{DateTime.Now:dd-MM-yy HH:mm:ss.ff} : {methodName} exit {value}");
        return value;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void TraceOut()
    {
        var methodName = GetFullyQualifiedCurrentMethodName();
        System.Diagnostics.Trace.WriteLine($"{DateTime.Now:dd-MM-yy HH:mm:ss.ff} : {methodName} exit");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void Trace(string value)
    {
        var textArrayToWrite = value.Split('\n');

        foreach (var line in textArrayToWrite.Where(static line => line.HasValue()))
        {
            System.Diagnostics.Trace.WriteLine($"{DateTime.Now:dd-MM-yy HH:mm:ss.ff} : {line}");
        }
    }

    /// <summary>
    ///     Deletes the file specified if it exists even if it was readonly.
    /// </summary>
    /// <param name="path"></param>
    internal static void FileDelete(string path)
    {
        if (!File.Exists(path)) return;

        ClearFileAttribute(path, FileAttributes.ReadOnly);
        File.Delete(path);
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
                        Trace($"Deleting empty folder {directory}");
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
    ///     Deletes any empty directories in the directory specified and checks recursively all its sub-directories.
    /// </summary>
    /// <param name="directory">The directory to check</param>
    /// <exception cref="ArgumentException"></exception>
    /// <returns>An array of the directory paths that were removed</returns>
    internal static IEnumerable<string> DeleteEmptyDirectories(string directory)
    {
        TraceIn();
        if (string.IsNullOrEmpty(directory)) throw new ArgumentException(Resources.DirectoryNameNullOrEmpty, nameof(directory));

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
                        Trace($"Deleting broken symbolic link folder {directory}");
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

    internal static string GetMasterFolder(string path)
    {
        var directoryInfo = new FileInfo(path).Directory;
        return directoryInfo?.Name;
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
        if (string.IsNullOrEmpty(directory)) throw new ArgumentException(Resources.DirectoryNameNullOrEmpty, nameof(directory));

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
        return Convert.ToInt64(value * BytesInOneGigabyte);
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
    internal static bool IsFileAccessible(string path)
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
}