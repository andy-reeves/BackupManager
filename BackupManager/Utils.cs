// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="Utils.cs" company="Andy Reeves">
//
//  </copyright>
//  --------------------------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Security.Principal;
using System.ServiceProcess;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using BackupManager.Entities;
using BackupManager.Extensions;

namespace BackupManager;

/// <summary>
///     Common Utilty functions in a static class
/// </summary>
public static class Utils
{
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetDiskFreeSpaceEx(string lpDirectoryName, out long lpFreeBytesAvailable, out long lpTotalNumberOfBytes,
        out long lpTotalNumberOfFreeBytes);

    #region Constants

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
    ///     This is the Hash for a file containing 48K of only zero bytes.
    /// </summary>
    internal static readonly string ZeroByteHash = "f4f35d60b3cc18aaa6d8d92f0cd3708a";

    /// <summary>
    ///     We use this to pad our logging messages
    /// </summary>
    private static int lengthOfLargestBackupActionEnumNames;

    /// <summary>
    ///     We start a new Process to Copy the files. Then we can safely Kill the process when cancelling is needed
    /// </summary>
    internal static Process CopyProcess;

    /// <summary>
    ///     The MD5 Crypto Provider
    /// </summary>
    private static readonly MD5 Md5 = MD5.Create();

#if DEBUG
    private static readonly string LogFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "BackupManager_Debug.log");
#else
        private static readonly string LogFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "BackupManager.log");
#endif
    /// <summary>
    ///     We use this to track when we sent the messages. This allows us to delay between messages
    /// </summary>
    private static DateTime timeLastPushoverMessageSent = DateTime.UtcNow.AddSeconds(-60);

    /// <summary>
    ///     So we can get config values
    /// </summary>
    internal static Config Config;

    private static bool alreadySendingPushoverMessage;

    #endregion

    #region internal Methods and Operators

    internal static void BackupLogFile()
    {
        var timeLog = DateTime.Now.ToString("yy-MM-dd-HH-mm-ss");
        var suffix = string.Empty;

#if DEBUG
        suffix = "_Debug";
#endif
        var destLogFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "BackupManager_Backups",
            $"BackupManager{suffix}_{timeLog}.log");

        if (File.Exists(LogFile)) FileMove(LogFile, destLogFile);

        var traceFiles = GetFiles(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "*BackupManager_Trace.log", SearchOption.TopDirectoryOnly);

        foreach (var file in traceFiles)
        {
            var destfile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "BackupManager_Backups",
                $"{new FileInfo(file).Name}_{timeLog}.log");

            try
            {
                FileMove(file, destfile);
            }
            catch (IOException)
            {
            }
        }
    }

    internal static bool CreateSymbolicLink()
    {
        // mklink /d "j:\_TV\%%~nxi" "%%i"
        //System.IO.Directory.CreateSymbolicLink()

        return true;
    }

    /// <summary>
    ///     Returns TRUE if we're running as Admin
    /// </summary>
    /// <returns></returns>
    internal static bool IsRunningAsAdmin()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);

        //for mac and linux
        return true;
    }

    /// <summary>
    ///     Moves a specified file to a new location, providing the option to specify a new file name. Ensures the destination
    ///     folder exists too.
    /// </summary>
    /// <param name="sourceFileName">The name of the file to move. Can include a relative or absolute path.</param>
    /// <param name="destFileName">The new path and name for the file.</param>
    internal static void FileMove(string sourceFileName, string destFileName)
    {
        Trace("FileMove enter");
        Trace($"Params: sourceFileName={sourceFileName}, destFileName={destFileName}");

        EnsureDirectories(destFileName);
        File.Move(sourceFileName, destFileName);
        Trace("FileMove exit");
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
        Trace("FileCopyNewProcess enter");
        Trace($"Params: sourceFileName={sourceFileName}, destFileName={destFileName}");

        if (File.Exists(destFileName)) throw new NotSupportedException("Destination file already exists");

        EnsureDirectories(destFileName);

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
        {
            Trace("FileCopyNewProcess exit with FALSE");
            return false;
        }

        Trace($"FileCopyNewProcess exit with {CopyProcess.ExitCode}");
        return CopyProcess.ExitCode == 0;
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
    internal static bool AnyFlagSet(FileAttributes value, FileAttributes flagsToCheckFor)
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
        Trace("ClearFileAttribute enter");
        Trace($"Params: path={path}, attributeToRemove={attributeToRemove}");

        var attributes = File.GetAttributes(path);

        if ((attributes & attributeToRemove) == attributeToRemove)
        {
            attributes = RemoveAttribute(attributes, attributeToRemove);
            File.SetAttributes(path, attributes);
        }

        Trace("ClearFileAttribute exit");
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
        var byteArrayToHash = secondByteArray == null && thirdByteArray == null ? new byte[firstByteArray.Length] :
            thirdByteArray == null ? new byte[firstByteArray.Length + secondByteArray.Length] :
            new byte[firstByteArray.Length + secondByteArray.Length + thirdByteArray.Length];
        Buffer.BlockCopy(firstByteArray, 0, byteArrayToHash, 0, firstByteArray.Length);

        if (secondByteArray != null) Buffer.BlockCopy(secondByteArray, 0, byteArrayToHash, secondByteArray.Length, secondByteArray.Length);

        if (thirdByteArray != null) Buffer.BlockCopy(thirdByteArray, 0, byteArrayToHash, firstByteArray.Length + secondByteArray.Length, thirdByteArray.Length);

        return ByteArrayToString(Md5.ComputeHash(byteArrayToHash));
    }

    /// <summary>
    ///     Ensures all the folders on the way to the file are created.
    /// </summary>
    /// <param name="filePath">
    /// </param>
    internal static void EnsureDirectories(string filePath)
    {
        _ = Directory.CreateDirectory(new FileInfo(filePath).DirectoryName);
    }

    /// <summary>
    ///     Returns all the files in the path provided.
    /// </summary>
    /// <param name="path">
    ///     The path.
    /// </param>
    /// <returns>
    ///     The <see cref="string[]" />.
    /// </returns>
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
    ///     The <see cref="string[]" />.
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
    ///     The <see cref="string[]" />.
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
    ///     The <see cref="string[]" />.
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
    ///     The <see cref="string[]" />.
    /// </returns>
    internal static string[] GetFiles(string path, string filters, SearchOption searchOption, FileAttributes directoryAttributesToIgnore,
        FileAttributes fileAttributesToIgnore)
    {
        Trace("GetFiles enter");

        var sw = Stopwatch.StartNew();

        if (!Directory.Exists(path))
        {
            Trace("GetFiles exit with new string[]");
            return Array.Empty<string>();
        }

        DirectoryInfo directoryInfo = new(path);

        if (directoryInfo.Parent != null && AnyFlagSet(directoryInfo.Attributes, directoryAttributesToIgnore))
        {
            Trace("GetFiles exit with new string[]");
            return Array.Empty<string>();
        }

        var include = from filter in filters.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries) where filter.Trim().HasValue() select filter.Trim();

        var exclude = from filter in include where filter.Contains('!') select filter;

        include = include.Except(exclude);

        if (!include.Any()) include = new[] { "*" };

        var excludeFilters = from filter in exclude
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
                foreach (var subDir in Directory.GetDirectories(dir)
                             .Where(subDir => !AnyFlagSet(new DirectoryInfo(subDir).Attributes, directoryAttributesToIgnore)))
                {
                    pathsToSearch.Enqueue(subDir);
                }

            foreach (var filter in include)
            {
                var allfiles = Directory.GetFiles(dir, filter, SearchOption.TopDirectoryOnly);

                var collection = exclude.Any() ? allfiles.Where(p => !excludeRegex.Match(p).Success) : allfiles;

                foundFiles.AddRange(collection.Where(p => !AnyFlagSet(new FileInfo(p).Attributes, fileAttributesToIgnore)));
            }
        }

        Trace($"Time taken = {sw.Elapsed.TotalSeconds} seconds");
        Trace("GetFiles exit");
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
    ///     The <see cref="byte[]" />.
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

        if (sum < byteCountToReturn)
        {
            var byteArray = new byte[sum];
            Buffer.BlockCopy(buffer, 0, byteArray, 0, sum);

            return byteArray;
        }

        return buffer;
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
        {
            startBlock = GetLocalFileByteArray(stream, 0, size);
        }

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
        Trace("GetShortMd5HashFromFile enter");
        Trace($"Params: path={path}");

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
        {
            startBlock = GetLocalFileByteArray(path, 0, size);
        }

        return CreateHashForByteArray(startBlock, middleBlock, endBlock);
    }

    internal static void SendPushoverMessage(string title, PushoverPriority priority, string message)
    {
        SendPushoverMessage(title, priority, PushoverRetry.None, PushoverExpires.Immediately, message);
    }

    internal static void SendPushoverMessage(string title, PushoverPriority priority, PushoverRetry retry, PushoverExpires expires, string message)
    {
        Trace("SendPushoverMessage enter");

        if (Config.PushoverONOFF && ((priority is PushoverPriority.Low or PushoverPriority.Lowest && Config.PushoverSendLowONOFF) ||
                                     (priority == PushoverPriority.Normal && Config.PushoverSendNormalONOFF) ||
                                     (priority == PushoverPriority.High && Config.PushoverSendHighONOFF) ||
                                     (priority == PushoverPriority.Emergency && Config.PushoverSendEmergencyONOFF)))
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
                while (DateTime.UtcNow < timeLastPushoverMessageSent.AddMilliseconds(TimeDelayOnPushoverMessages))
                {
                    Task.Delay(TimeDelayOnPushoverMessages / 10).Wait();
                }

                using (FormUrlEncodedContent postContent = new(parameters))
                {
                    HttpClient client = new();
                    var task = Task.Run(() => client.PostAsync(PushoverAddress, postContent));
                    task.Wait();
                    var response = task.Result;

                    _ = response.EnsureSuccessStatusCode(); // Throw if httpcode is an error
                    var applicationLimitRemaining = 0;

                    if (response.Headers.TryGetValues("X-Limit-App-Remaining", out var values)) applicationLimitRemaining = Convert.ToInt32(values.First());

                    Trace($"Pushover messages remaining: {applicationLimitRemaining}");

                    if (applicationLimitRemaining < Config.PushoverWarningMessagesRemaining)
                        if (!alreadySendingPushoverMessage)
                        {
                            alreadySendingPushoverMessage = true;
                            SendPushoverMessage("Message Limit Warning", PushoverPriority.High, $"Application Limit Remaining is: {applicationLimitRemaining}");
                            alreadySendingPushoverMessage = false;
                        }
                }

                timeLastPushoverMessageSent = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                // we ignore any push problems
                Log($"Exception sending Pushover message {ex}");
            }

        Trace("SendPushoverMessage exit");
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
    ///     Returns True if the Url returns a 200 response with a timeout of 30 seconds
    /// </summary>
    /// <param name="url">The url to check</param>
    /// <returns>True if success code returned</returns>
    internal static bool UrlExists(string url)
    {
        return UrlExists(url, 30 * 1000);
    }

    /// <summary>
    ///     Returns True if the Url returns a 200 response
    /// </summary>
    /// <param name="url">The url to check</param>
    /// <param name="timeout">Timeout in milliseconds</param>
    /// <returns>True if success code returned</returns>
    internal static bool UrlExists(string url, int timeout)
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
        if (lengthOfLargestBackupActionEnumNames == 0)
            foreach (var enumName in Enum.GetNames(typeof(BackupAction)))
            {
                if (enumName.Length > lengthOfLargestBackupActionEnumNames) lengthOfLargestBackupActionEnumNames = enumName.Length;
            }

        var actionText = Enum.GetName(typeof(BackupAction), action) + " ";
        var textArrayToWrite = message.Split('\n');

        foreach (var line in textArrayToWrite)
        {
            if (line.HasValue()) Log(actionText.PadRight(lengthOfLargestBackupActionEnumNames + 1) + line);
        }
    }

    /// <summary>
    ///     Writes the text to the logfile
    /// </summary>
    /// <param name="text"></param>
    internal static void Log(string text)
    {
        var textArrayToWrite = text.Split('\n');

        foreach (var line in textArrayToWrite)
        {
            if (!line.HasValue()) continue;

            var textToWrite = $"{DateTime.Now:dd-MM-yy HH:mm:ss} {line}";

            Console.WriteLine(textToWrite);

            if (LogFile.HasValue())
            {
                EnsureDirectories(LogFile);
                File.AppendAllLines(LogFile, new[] { textToWrite });
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
    internal static void LogWithPushover(BackupAction backupAction, PushoverPriority priority, PushoverRetry retry, PushoverExpires expires, string text)
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
    private static string ByteArrayToString(byte[] value, int startIndex, int length)
    {
        if (value == null) throw new ArgumentNullException(nameof(value));

        if (startIndex < 0 || (startIndex >= value.Length && startIndex > 0)) throw new ArgumentOutOfRangeException(nameof(startIndex));

        if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));

        if (startIndex > value.Length - length) throw new ArgumentException(null, nameof(length));

        if (length == 0) return string.Empty;

        if (length > 715827882) throw new ArgumentOutOfRangeException(nameof(length));

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

        return ByteArrayToString(Md5.ComputeHash(byteArrayToHash));
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
        return path.EndsWith(Path.DirectorySeparatorChar.ToString()) ? path : path + Path.DirectorySeparatorChar;
    }

    /// <summary>
    ///     The get local file byte array.
    /// </summary>
    /// <param name="fileStream">
    ///     The file stream.
    /// </param>
    /// <param name="offset">
    ///     The offset.
    /// </param>
    /// <param name="byteCountToReturn">
    ///     The byte count to return.
    /// </param>
    /// <returns>
    ///     The <see cref="byte[]" />.
    /// </returns>
    private static byte[] GetLocalFileByteArray(FileStream fileStream, long offset, long byteCountToReturn)
    {
        _ = fileStream.Seek(offset, SeekOrigin.Begin);

        var buffer = new byte[byteCountToReturn];

        int count;
        var sum = 0;
        var length = Convert.ToInt32(byteCountToReturn);

        while ((count = fileStream.Read(buffer, sum, length - sum)) > 0)
        {
            sum += count; // sum is a buffer offset for next reading
        }

        if (sum < byteCountToReturn)
        {
            var byteArray = new byte[sum];
            Buffer.BlockCopy(buffer, 0, byteArray, 0, sum);
            return byteArray;
        }

        return buffer;
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
    ///     The <see cref="byte[]" />.
    /// </returns>
    private static byte[] GetLocalFileByteArray(string fileName, long offset, long byteCountToReturn)
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

        if (sum < byteCountToReturn)
        {
            var byteArray = new byte[sum];
            Buffer.BlockCopy(buffer, 0, byteArray, 0, sum);
            return byteArray;
        }

        return buffer;
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
    /// <param name="freespace">in bytes</param>
    /// <param name="totalBytes">in bytes</param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    internal static bool GetDiskInfo(string folderName, out long freespace, out long totalBytes)
    {
        if (string.IsNullOrEmpty(folderName)) throw new ArgumentNullException(nameof(folderName));

        if (!folderName.EndsWith("\\")) folderName += '\\';

        return GetDiskFreeSpaceEx(folderName, out freespace, out totalBytes, out _);
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
        var applicationBasePath = AppContext.BaseDirectory;
        DirectoryInfo directoryInfo = new(applicationBasePath);

        do
        {
            directoryInfo = directoryInfo.Parent;

            DirectoryInfo projectDirectoryInfo = new(directoryInfo.FullName);

            if (projectDirectoryInfo.Exists)
            {
                FileInfo projectFileInfo = new(Path.Combine(projectDirectoryInfo.FullName, projectName, $"{projectName}.csproj"));

                if (projectFileInfo.Exists) return Path.Combine(projectDirectoryInfo.FullName, projectName);
            }
        } while (directoryInfo.Parent != null);

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
    internal static string RandomString(long size)
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
    ///     Checks to see if the folder specified is empty
    /// </summary>
    /// <param name="folderPath">The folder to check</param>
    /// <returns>
    ///     True if the folder exists as a normal folder and its empty. If its a Symbolic link and the target is missing
    ///     or the target is empty it returns True. Otherwise False
    /// </returns>
    internal static bool IsDirectoryEmpty(string folderPath)
    {
        if (!Directory.Exists(folderPath)) return false;

        if (!IsSymbolicLink(folderPath)) return !Directory.EnumerateFileSystemEntries(folderPath).Any();

        var linkTarget = new FileInfo(folderPath).LinkTarget;
        return linkTarget != null && (!SymbolicLinkTargetExists(folderPath) || !Directory.GetFileSystemEntries(linkTarget).Any());
    }

    /// <summary>
    ///     Checks to see if the folder is a Symbolic link and its LinkTarget exists
    /// </summary>
    /// <param name="folderPath"></param>
    /// <returns>True if its a symbolic Link with a target that exists otherwise False</returns>
    internal static bool SymbolicLinkTargetExists(string folderPath)
    {
        var linkTarget = new FileInfo(folderPath).LinkTarget;
        return linkTarget != null && Directory.Exists(linkTarget);
    }

    /// <summary>
    ///     Checks to see if folderPath is a Symbolic link
    /// </summary>
    /// <param name="folderPath"></param>
    /// <returns>True if folderPath is a symbolic link otherwise False</returns>
    internal static bool IsSymbolicLink(string folderPath)
    {
        FileInfo file = new(folderPath);
        return file.LinkTarget != null;
    }

    /// <summary>
    ///     Checks the folder is writable
    /// </summary>
    /// <param name="folderPath"></param>
    /// <returns>True if writable else False</returns>
    internal static bool IsFolderWritable(string folderPath)
    {
        try
        {
            using var fs = File.Create(Path.Combine(folderPath, Path.GetRandomFileName()), 1, FileOptions.DeleteOnClose);
            return true;
        }
        catch
        {
            return false;
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
    ///     Runs a speedtest on the disk provided.
    /// </summary>
    /// <param name="pathToDiskToTest">The path to test.</param>
    /// <param name="readSpeed">in bytes per second</param>
    /// <param name="writeSpeed">in bytes per second</param>
    internal static void DiskSpeedTest(string pathToDiskToTest, long testFileSize, int testIterations, out long readSpeed, out long writeSpeed)
    {
        Trace("DiskSpeedTest enter");
        Trace($"Params: pathToDiskToTest={pathToDiskToTest}, testFileSize={testFileSize}, testIterations={testIterations}");

        var tempPath = Path.GetTempPath();

        Trace("Starting read test");
        readSpeed = DiskSpeedTest(pathToDiskToTest, tempPath, testFileSize, testIterations);
        Trace("Starting write test");
        writeSpeed = DiskSpeedTest(tempPath, pathToDiskToTest, testFileSize, testIterations);

        Trace("DiskSpeedTest exit");
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
        Trace("StopService enter");

        ServiceController service = new(serviceName);

        try
        {
            var timeout = TimeSpan.FromMilliseconds(timeoutMilliseconds);

            if (service.Status == ServiceControllerStatus.Running) service.Stop();

            service.WaitForStatus(ServiceControllerStatus.Stopped, timeout);
        }
        catch
        {
            return false;
        }

        Trace("StopService exit");
        return true;
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
        Trace("RestartService enter");

        ServiceController service = new(serviceName);

        try
        {
            var millisec1 = Environment.TickCount;
            var timeout = TimeSpan.FromMilliseconds(timeoutMilliseconds);

            if (service.Status == ServiceControllerStatus.Running) service.Stop();

            service.WaitForStatus(ServiceControllerStatus.Stopped, timeout);

            // count the rest of the timeout
            var millisec2 = Environment.TickCount;
            timeout = TimeSpan.FromMilliseconds(timeoutMilliseconds - (millisec2 - millisec1));

            if (service.Status.Equals(ServiceControllerStatus.Stopped))
                service.Start();
            else if (service.Status.Equals(ServiceControllerStatus.Paused)) service.Continue();

            service.WaitForStatus(ServiceControllerStatus.Running, timeout);
        }
        catch
        {
            return false;
        }

        Trace("RestartService exit");
        return true;
    }

    /// <summary>
    /// </summary>
    /// <param name="sourcePath"></param>
    /// <param name="destinationPath"></param>
    /// <param name="testFileSize"></param>
    /// <param name="testIterations"></param>
    /// <returns>The bytes read/written in 1s</returns>
    internal static long DiskSpeedTest(string sourcePath, string destinationPath, long testFileSize, int testIterations)
    {
        Trace("DiskSpeedTest enter");

        long randomStringSize = 500_000;
        var streamWriteBufferSize = 20 * BytesInOneMegabyte;

        var randomText = RandomString(randomStringSize);

        var appendIterations = testFileSize / randomStringSize;

        double totalPerf = 0;

        for (var j = 1; j <= testIterations; j++)
        {
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
        Trace("Iterations complete");
        Trace($"totalPerf: {totalPerf}, testIterations: {testIterations}");
        var returnValue = Convert.ToInt64(totalPerf / testIterations);
        Trace("Speed Calculation complete");
        Trace($"DiskSpeedTest exit with {returnValue}");
        return returnValue;
    }

    internal static void Trace(string value)
    {
        var textArrayToWrite = value.Split('\n');

        foreach (var line in textArrayToWrite)
        {
            if (line.HasValue()) System.Diagnostics.Trace.WriteLine($"{DateTime.Now:dd-MM-yy HH:mm:ss.ff} : {line}");
        }
    }

    /// <summary>
    ///     Deletes the file specified if it exists even if it was readonly.
    /// </summary>
    /// <param name="path"></param>
    internal static void FileDelete(string path)
    {
        if (File.Exists(path))
        {
            ClearFileAttribute(path, FileAttributes.ReadOnly);
            File.Delete(path);
        }
    }

    private static void DeleteEmptyDirectories(string directory, List<string> list, string rootDirectory)
    {
        Trace("DeleteEmptyDirectories enter");

        try
        {
            foreach (var subDirectory in Directory.EnumerateDirectories(directory))
            {
                DeleteEmptyDirectories(subDirectory, list, rootDirectory);
            }

            if (IsDirectoryEmpty(directory))
                try
                {
                    if (directory != rootDirectory)
                    {
                        Trace($"Deleting empty folder {directory}");
                        list.Add(directory);
                        Directory.Delete(directory);
                    }
                }
                catch (UnauthorizedAccessException)
                {
                }
                catch (DirectoryNotFoundException)
                {
                }
        }
        catch (UnauthorizedAccessException)
        {
        }

        Trace("DeleteEmptyDirectories exit");
    }

    /// <summary>
    ///     Deletes any empty directories in the directory specified and checks recursively all its sub-directories.
    /// </summary>
    /// <param name="directory">The directory to check</param>
    /// <exception cref="ArgumentException"></exception>
    /// <returns>An array of the directory paths that were removed</returns>
    internal static string[] DeleteEmptyDirectories(string directory)
    {
        Trace("DeleteEmptyDirectories enter");

        if (string.IsNullOrEmpty(directory)) throw new ArgumentException("Directory is a null reference or an empty string", nameof(directory));

        List<string> listOfDirectoriesDeleted = new();

        DeleteEmptyDirectories(directory, listOfDirectoriesDeleted, directory);

        Trace("DeleteEmptyDirectories exit");
        return listOfDirectoriesDeleted.ToArray();
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
    /// <param name="fileName"></param>
    /// <returns>True if not locked or False if locked</returns>
    internal static bool IsFileAccessible(string fileName)
    {
        try
        {
            FileStream fileStream = new(fileName, FileMode.Open, FileAccess.Read);

            fileStream.Close();
        }
        catch (IOException)
        {
            return false;
        }

        return true;
    }
}