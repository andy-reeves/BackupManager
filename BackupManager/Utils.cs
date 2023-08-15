// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Utils.cs" company="">
//   
// </copyright>
// <summary>
//   The utils.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace BackupManager
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text.RegularExpressions;
    using System.Runtime.InteropServices;
    using System.Collections.Specialized;
    using System.Net;
    using System.Text;
    using System.Diagnostics;
    using System.Net.Sockets;
    using System.ServiceProcess;
    using System.Reflection;

    /// <summary>
    /// Common Utilty fuctions in a static class
    /// </summary>
    public class Utils
    {
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetDiskFreeSpaceEx(string lpDirectoryName,
        out long lpFreeBytesAvailable,
        out long lpTotalNumberOfBytes,
        out long lpTotalNumberOfFreeBytes);

        #region Constants

        /// <summary>
        /// The end block size.
        /// </summary>
        private const int EndBlockSize = 16 * BytesInOneKilobyte; // 16K

        /// <summary>
        /// The middle block size.
        /// </summary>
        private const int MiddleBlockSize = 16 * BytesInOneKilobyte; // 16K

        /// <summary>
        /// The start block size.
        /// </summary>
        private const int StartBlockSize = 16 * BytesInOneKilobyte; // 16K

        /// <summary>
        /// The number of bytes in one Terabyte. 2^40 bytes.
        /// </summary>
        internal const long BytesInOneTerabyte = 1_099_511_627_776;

        /// <summary>
        /// The number of bytes in one Gigabyte. 2^30 bytes.
        /// </summary>
        internal const int BytesInOneGigabyte = 1_073_741_824;

        /// <summary>
        /// The number of bytes in one Megabyte. 2^20 bytes.
        /// </summary>
        internal const int BytesInOneMegabyte = 1_048_576;

        /// <summary>
        /// The number of bytes in one Kilobyte. 2^10 bytes.
        /// </summary>
        internal const int BytesInOneKilobyte = 1_024;

        /// <summary>
        /// The URL of the Pushover messaging service.
        /// </summary>
        private const string PushoverAddress = "https://api.pushover.net/1/messages.json";

        #endregion

        #region Static Fields

        /// <summary>
        /// This is the Hash for a file containing 48K of only zero bytes.
        /// </summary>
        internal static readonly string ZeroByteHash = "f4f35d60b3cc18aaa6d8d92f0cd3708a";

        /// <summary>
        /// The Pushover UserKey used for all logging.
        /// </summary>
        internal static string PushoverUserKey;

        /// <summary>
        /// The Pushover AppToken used for all logging.
        /// </summary>
        internal static string PushoverAppToken;

        /// <summary>
        /// We use this to pad our logging messages
        /// </summary>
        private static int LengthOfLargestBackupActionEnumNames;

        /// <summary>
        /// The MD5 Crypto Provider
        /// </summary>
        private static readonly MD5CryptoServiceProvider Md5 = new MD5CryptoServiceProvider();

#if DEBUG
        private static readonly string LogFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "BackupManagerDebug.log");
#else
        private static readonly string LogFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "BackupManager.log");
#endif
        /// <summary>
        /// We use this to track when we sent the messages. This allows us to delay for at least 1000ms between messages
        /// </summary>
        private static DateTime timeLastPushoverMessageSent = DateTime.UtcNow;
        #endregion

        #region Public Methods and Operators

        /// <summary>
        /// Moves a specified file to a new location, providing the option to specify a new file name. Ensures the destination folder exists too.
        /// </summary>
        /// <param name="sourceFileName">The name of the file to move. Can include a relative or absolute path.</param>
        /// <param name="destFileName">The new path and name for the file.</param>
        public static void FileMove(string sourceFileName, string destFileName)
        {
            Trace("FileMove enter");
            Trace($"Params: sourceFileName={sourceFileName}, destFileName={destFileName}");

            EnsureDirectories(destFileName);
            File.Move(sourceFileName, destFileName);
            Trace("FileMove exit");
        }

        /// <summary>
        /// Copies an existing file to a new file. Overwriting a file of the same name is not allowed. Ensures the destination folder exists too.
        /// </summary>
        /// <param name="sourceFileName">The file to copy.</param>
        /// <param name="destFileName">The name of the destination file. This cannot be a directory or an existing file.</param>
        public static void FileCopy(string sourceFileName, string destFileName)
        {
            Trace("FileCopy enter");
            Trace($"Params: sourceFileName={sourceFileName}, destFileName={destFileName}");

            EnsureDirectories(destFileName);
            File.Copy(sourceFileName, destFileName);
            Trace("FileCopy exit");
        }

        /// <summary>
        /// Converts a MB value to a size in bytes
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static long ConvertMBtoBytes(long value)
        {
            return Convert.ToInt64(value * BytesInOneMegabyte);
        }

        /// <summary>
        /// Returns True if any of the attributes to check for are set in the value.
        /// </summary>
        /// <param name="value">
        /// </param>
        /// <param name="flagsToCheckFor">
        /// </param>
        /// <returns>
        /// The <see cref="bool"/>.
        /// </returns>
        public static bool AnyFlagSet(FileAttributes value, FileAttributes flagsToCheckFor)
        {
            if (flagsToCheckFor == 0)
            {
                return false;
            }

            return Enum.GetValues(typeof(FileAttributes)).Cast<Enum>().Where(flagsToCheckFor.HasFlag).Any(value.HasFlag);
        }

        /// <summary>
        /// Clears the attribute from the file if it were set.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="attributeToRemove"></param>
        public static void ClearFileAttribute(string path, FileAttributes attributeToRemove)
        {
            FileAttributes attributes = File.GetAttributes(path);

            if ((attributes & attributeToRemove) == attributeToRemove)
            {
                attributes = RemoveAttribute(attributes, attributeToRemove);
                File.SetAttributes(path, attributes);
            }
        }

        private static FileAttributes RemoveAttribute(FileAttributes attributes, FileAttributes attributesToRemove)
        {
            return attributes & ~attributesToRemove;
        }

        /// <summary>
        /// Creates a hash for the 3 byte arrays passed in.
        /// </summary>
        /// <param name="firstByteArray">
        /// The start byte array.
        /// </param>
        /// <param name="secondByteArray">
        /// The middle byte array.
        /// </param>
        /// <param name="thirdByteArray">
        /// The end byte array.
        /// </param>
        /// <returns>
        /// A String of the hash.
        /// </returns>
        public static string CreateHashForByteArray(
            byte[] firstByteArray,
            byte[] secondByteArray,
            byte[] thirdByteArray)
        {
            byte[] byteArrayToHash;

            if (secondByteArray == null && thirdByteArray == null)
            {
                byteArrayToHash = new byte[firstByteArray.Length];
            }
            else if (thirdByteArray == null)
            {
                byteArrayToHash = new byte[firstByteArray.Length + secondByteArray.Length];
            }
            else
            {
                byteArrayToHash = new byte[firstByteArray.Length + secondByteArray.Length + thirdByteArray.Length];
            }

            Buffer.BlockCopy(firstByteArray, 0, byteArrayToHash, 0, firstByteArray.Length);

            if (secondByteArray != null)
            {
                Buffer.BlockCopy(secondByteArray, 0, byteArrayToHash, secondByteArray.Length, secondByteArray.Length);
            }

            if (thirdByteArray != null)
            {
                Buffer.BlockCopy(
                    thirdByteArray,
                    0,
                    byteArrayToHash,
                    firstByteArray.Length + secondByteArray.Length,
                    thirdByteArray.Length);
            }

            return ByteArrayToString(Md5.ComputeHash(byteArrayToHash));
        }

        /// <summary>
        /// Ensures all the folders on the way to the file are created.
        /// </summary>
        /// <param name="filePath">
        /// </param>
        public static void EnsureDirectories(string filePath)
        {
            Directory.CreateDirectory(new FileInfo(filePath).DirectoryName);
        }

        /// <summary>
        /// Returns all the files in the path provided.
        /// </summary>
        /// <param name="path">
        /// The path.
        /// </param>
        /// <returns>
        /// The <see cref="string[]"/>.
        /// </returns>
        public static string[] GetFiles(string path)
        {
            return GetFiles(path, "*", SearchOption.AllDirectories, 0, 0);
        }

        /// <summary>
        /// The get files.
        /// </summary>
        /// <param name="path">
        /// The path.
        /// </param>
        /// <param name="filters">
        /// The filters.
        /// </param>
        /// <returns>
        /// The <see cref="string[]"/>.
        /// </returns>
        public static string[] GetFiles(string path, string filters)
        {
            return GetFiles(path, filters, SearchOption.AllDirectories, 0, 0);
        }

        /// <summary>
        /// The get files.
        /// </summary>
        /// <param name="path">
        /// The path.
        /// </param>
        /// <param name="filters">
        /// The filters.
        /// </param>
        /// <param name="searchOption">
        /// The search option.
        /// </param>
        /// <param name="directoryAttributesToIgnore">
        /// The directory attributes to ignore.
        /// </param>
        /// <returns>
        /// The <see cref="string[]"/>.
        /// </returns>
        public static string[] GetFiles(
            string path,
            string filters,
            SearchOption searchOption,
            FileAttributes directoryAttributesToIgnore)
        {
            return GetFiles(path, filters, searchOption, directoryAttributesToIgnore, 0);
        }

        /// <summary>
        /// Returns an array of full path names of files in the folder specified.
        /// </summary>
        /// <param name="path">
        /// The path.
        /// </param>
        /// <param name="filters">
        /// The filters.
        /// </param>
        /// <param name="searchOption">
        /// The search option.
        /// </param>
        /// <returns>
        /// The <see cref="string[]"/>.
        /// </returns>
        public static string[] GetFiles(string path, string filters, SearchOption searchOption)
        {
            return GetFiles(path, filters, searchOption, 0, 0);
        }

        /// <summary>
        /// The get files.
        /// </summary>
        /// <param name="path">
        /// The path.
        /// </param>
        /// <param name="filters">
        /// The filters.
        /// </param>
        /// <param name="searchOption">
        /// The search option.
        /// </param>
        /// <param name="directoryAttributesToIgnore">
        /// The directory attributes to ignore.
        /// </param>
        /// <param name="fileAttributesToIgnore">
        /// The file attributes to ignore.
        /// </param>
        /// <returns>
        /// The <see cref="string[]"/>.
        /// </returns>
        public static string[] GetFiles(
            string path,
            string filters,
            SearchOption searchOption,
            FileAttributes directoryAttributesToIgnore,
            FileAttributes fileAttributesToIgnore)
        {
            Trace("GetFiles enter");

            if (!Directory.Exists(path))
            {
                return new string[] { };
            }

            var directoryInfo = new DirectoryInfo(path);

            if (directoryInfo.Parent != null && AnyFlagSet(directoryInfo.Attributes, directoryAttributesToIgnore))
            {
                return new string[] { };
            }

            IEnumerable<string> include =
                from filter in filters.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                where filter.Trim().HasValue()
                select filter.Trim();

            IEnumerable<string> exclude = from filter in include where filter.Contains(@"!") select filter;

            include = include.Except(exclude);

            if (!include.Any())
            {
                include = new[] { "*" };
            }

            IEnumerable<string> excludeFilters = from filter in exclude
                                                 let replace =
                                                     filter.Replace("!", string.Empty)
                                                     .Replace(".", @"\.")
                                                     .Replace("*", ".*")
                                                     .Replace("?", ".")
                                                 select $"^{replace}$";

            var excludeRegex = new Regex(string.Join("|", excludeFilters.ToArray()), RegexOptions.IgnoreCase);

            var pathsToSearch = new Queue<string>();
            var foundFiles = new List<string>();

            pathsToSearch.Enqueue(path);

            while (pathsToSearch.Count > 0)
            {
                string dir = pathsToSearch.Dequeue();

                if (searchOption == SearchOption.AllDirectories)
                {
                    foreach (string subDir in
                        Directory.GetDirectories(dir)
                            .Where(
                                subDir =>
                                (!AnyFlagSet(new DirectoryInfo(subDir).Attributes, directoryAttributesToIgnore))))
                    {
                        pathsToSearch.Enqueue(subDir);
                    }
                }

                foreach (string filter in include)
                {
                    string[] allfiles = Directory.GetFiles(dir, filter, SearchOption.TopDirectoryOnly);

                    IEnumerable<string> collection = exclude.Any()
                                                         ? allfiles.Where(p => !excludeRegex.Match(p).Success)
                                                         : allfiles;

                    foundFiles.AddRange(
                        collection.Where(p => (!AnyFlagSet(new FileInfo(p).Attributes, fileAttributesToIgnore))));
                }
            }

            Trace("GetFiles exit");
            return foundFiles.ToArray();
        }

        /// <summary>
        /// The hash from file.
        /// </summary>
        /// <param name="fileName">
        /// The file name.
        /// </param>
        /// <param name="algorithm">
        /// The algorithm.
        /// </param>
        /// <returns>
        /// The <see cref="string"/>.
        /// </returns>
        public static string GetHashFromFile(string fileName, HashAlgorithm algorithm)
        {
            Trace("GetHashFromFile enter");

            using (BufferedStream stream = new BufferedStream(File.OpenRead(fileName), BytesInOneMegabyte))
            {
                string value = ByteArrayToString(algorithm.ComputeHash(stream));

                Trace("GetHashFromFile exit");
                return value;
            }
        }

        /// <summary>
        /// The get remote file byte array.
        /// </summary>
        /// <param name="fileStream">
        /// The file stream.
        /// </param>
        /// <param name="offset">
        /// The offset.
        /// </param>
        /// <param name="byteCountToReturn">
        /// The byte count to return.
        /// </param>
        /// <returns>
        /// The <see cref="byte[]"/>.
        /// </returns>
        public static byte[] GetRemoteFileByteArray(Stream fileStream, long offset, long byteCountToReturn)
        {
            Trace("GetRemoteFileByteArray enter");

            byte[] buffer = new byte[byteCountToReturn];

            int count;
            int sum = 0;
            int length = Convert.ToInt32(byteCountToReturn);
            while ((count = fileStream.Read(buffer, sum, length - sum)) > 0)
            {
                sum += count; // sum is a buffer offset for next reading
            }

            if (sum < byteCountToReturn)
            {
                var byteArray = new byte[sum];
                Buffer.BlockCopy(buffer, 0, byteArray, 0, sum);

                Trace("GetRemoteFileByteArray exit");
                return byteArray;
            }

            Trace("GetRemoteFileByteArray exit");
            return buffer;
        }

        /// <summary>
        /// The get short md 5 hash from file.
        /// </summary>
        /// <param name="stream">
        /// The stream.
        /// </param>
        /// <param name="size">
        /// The size.
        /// </param>
        /// <returns>
        /// The <see cref="string"/>.
        /// </returns>
        public static string GetShortMd5HashFromFile(FileStream stream, long size)
        {
            Trace("GetShortMd5HashFromFile enter");

            if (stream == null)
            {
                return null;
            }

            if (size <= 0)
            {
                return string.Empty;
            }

            byte[] startBlock;
            byte[] middleBlock = null;
            byte[] endBlock = null;

            if (size >= StartBlockSize + MiddleBlockSize + EndBlockSize)
            {
                long startDownloadPositionForEndBlock = size - EndBlockSize;

                long startDownloadPositionForMiddleBlock = size / 2;

                startBlock = GetLocalFileByteArray(stream, 0, StartBlockSize);

                middleBlock = GetLocalFileByteArray(stream, startDownloadPositionForMiddleBlock, MiddleBlockSize);

                endBlock = GetLocalFileByteArray(stream, startDownloadPositionForEndBlock, EndBlockSize);
            }
            else
            {
                startBlock = GetLocalFileByteArray(stream, 0, size);
            }

            string value = CreateHashForByteArray(startBlock, middleBlock, endBlock);

            Trace("GetShortMd5HashFromFile exit");
            return value;
        }

        public static long GetFileLength(string fileName)
        {
            return new FileInfo(fileName).Length;
        }

        public static DateTime GetFileLastWriteTime(string fileName)
        {
            FileInfo fileInfo = new FileInfo(fileName);

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
        /// Gets an MD5 hash of the first 16K, the 16K from the middle and last 16K of a file.
        /// </summary>
        /// <param name="path">
        /// The local file name.
        /// </param>
        /// <returns>
        /// An MD5 hash of the file or null if File doesn't exist or string.Empty if it has no size.
        /// </returns>
        public static string GetShortMd5HashFromFile(string path)
        {
            Trace("GetShortMd5HashFromFile enter");
            Trace($"Params: path={path}");

            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            long size = new FileInfo(path).Length;

            if (size == 0)
            {
                Trace("GetShortMd5HashFromFile exit1");
                return string.Empty;
            }

            byte[] startBlock;
            byte[] middleBlock = null;
            byte[] endBlock = null;

            if (size >= StartBlockSize + MiddleBlockSize + EndBlockSize)
            {
                long startDownloadPositionForEndBlock = size - EndBlockSize;

                long startDownloadPositionForMiddleBlock = size / 2;

                startBlock = GetLocalFileByteArray(path, 0, StartBlockSize);

                middleBlock = GetLocalFileByteArray(path, startDownloadPositionForMiddleBlock, MiddleBlockSize);

                endBlock = GetLocalFileByteArray(path, startDownloadPositionForEndBlock, EndBlockSize);
            }
            else
            {
                startBlock = GetLocalFileByteArray(path, 0, size);
            }

            string value = CreateHashForByteArray(startBlock, middleBlock, endBlock);

            Trace("GetShortMd5HashFromFile exit2");
            return value;
        }

        public static void SendPushoverMessage(string title, PushoverPriority priority, string message)
        {
            SendPushoverMessage(title, priority, PushoverRetry.None, PushoverExpires.Immediately, message);
        }

        public static void SendPushoverMessage(string title, PushoverPriority priority, PushoverRetry retry, PushoverExpires expires, string message)
        {
            Trace("SendPushoverMessage enter");

            string timeStamp = new DateTimeOffset(DateTime.Now).ToUnixTimeSeconds().ToString();

            try
            {
                NameValueCollection parameters = new NameValueCollection {
                { "token", PushoverAppToken},
                { "user", PushoverUserKey },
                { "priority", Convert.ChangeType(priority, priority.GetTypeCode()).ToString() },
                { "message", message },
                { "title", title } ,
                { "timestamp", timeStamp }
                };

                if (priority == PushoverPriority.Emergency)
                {
                    if (retry == PushoverRetry.None)
                    {
                        retry = PushoverRetry.ThirtySeconds;
                    }

                    if (expires == PushoverExpires.Immediately)
                    {
                        expires = PushoverExpires.FiveMinutes;
                    }
                }

                if (retry != PushoverRetry.None)
                {
                    parameters.Add("retry", Convert.ChangeType(retry, retry.GetTypeCode()).ToString());
                }

                if (expires != PushoverExpires.Immediately)
                {
                    parameters.Add("expire", Convert.ChangeType(expires, expires.GetTypeCode()).ToString());
                }

                using (var client = new WebClient())
                {
                    // ensures there's a 1s gap between messages
                    while (DateTime.UtcNow < timeLastPushoverMessageSent.AddSeconds(1))
                    {
                        System.Threading.Thread.Sleep(100);
                    }
                    client.UploadValues(PushoverAddress, parameters);
                    timeLastPushoverMessageSent = DateTime.UtcNow;
                }
            }
            catch (Exception ex)
            {
                // we ignore any push problems
                Log($"Exception sending Pushover message {ex}");
            }

            Trace("SendPushoverMessage exit");
        }

        /// <summary>
        /// KIlls matching processes. All the processes that start with ProcessName are stopped
        /// </summary>
        /// <param name="processName"></param>
        /// <returns>True if they were killed and False otherwise</returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        /// <exception cref="ArgumentException"></exception>
        public static bool KillProcesses(string processName)
        {
            Trace("KillProcesses enter");

            // without '.exe' and check for all that match 
            IEnumerable<Process> processes = Process.GetProcesses().Where(p =>
                    p.ProcessName.StartsWith(processName, StringComparison.CurrentCultureIgnoreCase));

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

            Trace("KillProcesses exit");
            return true;
        }

        /// <summary>
        /// Returns True if the Url returns a 200 response with a timeout of 30 seconds
        /// </summary>
        /// <param name="url">The url to check</param>
        /// <param name="timeout">Timeout in seconds</param>
        /// <returns>True if success code returned</returns>
        public static bool UrlExists(string url)
        {
            return UrlExists(url, 30 * 1000);
        }

        /// <summary>
        /// Returns True if the Url returns a 200 response
        /// </summary>
        /// <param name="url">The url to check</param>
        /// <param name="timeout">Timeout in milliseconds</param>
        /// <returns>True if success code returned</returns>
        public static bool UrlExists(string url, int timeout)
        {
            Trace("UrlExists enter");
            bool returnValue;
            try
            {
                HttpWebRequest request = WebRequest.Create(url) as HttpWebRequest;
                request.Method = "GET";
                request.Timeout = timeout;

                using (HttpWebResponse response = request.GetResponse() as HttpWebResponse)
                {
                    returnValue = response.StatusCode == HttpStatusCode.OK;
                }
            }
            catch
            {
                return false;
            }

            Trace("UrlExists exit");
            return returnValue;
        }

        /// <summary>
        /// Returns True if the host can be connected to on that port
        /// <param name="host">The host to check</param>
        /// <param name="port">The port to connect on</param>
        /// <returns>True if the connection is made</returns>
        public static bool ConnectionExists(string host, int port)
        {
            Trace("ConnectionExists enter");

            try
            {
                using (TcpClient tcpClient = new TcpClient())
                {
                    tcpClient.Connect(host, port);
                }
            }

            catch
            {
                return false;
            }

            Trace("ConnectionExists exit");
            return true;
        }

        internal static void Log(BackupAction action, string message)
        {
            if (LengthOfLargestBackupActionEnumNames == 0)
            {
                foreach (string enumName in Enum.GetNames(typeof(BackupAction)))
                {
                    if (enumName.Length > LengthOfLargestBackupActionEnumNames)
                    {
                        LengthOfLargestBackupActionEnumNames = enumName.Length;
                    }
                }
            }

            string actionText = Enum.GetName(typeof(BackupAction), action) + " ";
            string[] textArrayToWrite = message.Split('\n');

            foreach (var line in textArrayToWrite)
            {
                if (line.HasValue())
                {
                    Log(actionText.PadRight(LengthOfLargestBackupActionEnumNames + 1) + line);
                }
            }
        }

        /// <summary>
        /// Writes the text to the logfile
        /// </summary>
        /// <param name="text"></param>
        public static void Log(string text)
        {
            string[] textArrayToWrite = text.Split('\n');

            foreach (string line in textArrayToWrite)
            {
                if (line.HasValue())
                {
                    string textToWrite = $"{DateTime.Now:dd-MM-yy HH:mm:ss} {line}";

                    Console.WriteLine(textToWrite);
                    if (LogFile.HasValue())
                    {
                        EnsureDirectories(LogFile);
                        File.AppendAllLines(LogFile, new[] { textToWrite });
                    }

                    Trace(text);
                }
            }
        }
        /// <summary>
        /// Logs the text to the LogFile and sends a Pushover message
        /// </summary>
        /// <param name="backupAction"></param>
        /// <param name="text"></param>
        public static void LogWithPushover(BackupAction backupAction, string text)
        {
            LogWithPushover(backupAction, PushoverPriority.Normal, text);
        }

        /// <summary>
        /// Logs the text to the LogFile and sends a Pushover message
        /// </summary>
        /// <param name="backupAction"></param>
        /// <param name="priority"></param>
        /// <param name="text"></param>
        public static void LogWithPushover(BackupAction backupAction, PushoverPriority priority, string text)
        {
            Log(backupAction, text);

            if (PushoverAppToken.HasValue() && PushoverAppToken != "InsertYourPushoverAppTokenHere")
            {
                SendPushoverMessage(Enum.GetName(typeof(BackupAction), backupAction),
                                    priority,
                                    text);
            }
        }

        /// <summary>
        /// Logs the text to the LogFile and sends a Pushover message
        /// </summary>
        /// <param name="backupAction"></param>
        /// <param name="priority"></param>
        /// <param name="retry"></param>
        /// <param name="expires"></param>
        /// <param name="text"></param>
        public static void LogWithPushover(BackupAction backupAction, PushoverPriority priority, PushoverRetry retry, PushoverExpires expires, string text)
        {
            Log(backupAction, text);

            if (PushoverAppToken.HasValue())
            {
                SendPushoverMessage(Enum.GetName(typeof(BackupAction), backupAction),
                                    priority,
                                    retry,
                                    expires,
                                    text);
            }
        }
        #endregion

        #region Methods

        /// <summary>
        /// The byte array to string.
        /// </summary>
        /// <param name="value">
        /// The value.
        /// </param>
        /// <returns>
        /// The <see cref="string"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// </exception>
        private static string ByteArrayToString(byte[] value)
        {
            if (value == null)
            {
                throw new ArgumentNullException("value");
            }

            return ByteArrayToString(value, 0, value.Length);
        }

        /// <summary>
        /// The byte array to string.
        /// </summary>
        /// <param name="value">
        /// The value.
        /// </param>
        /// <param name="startIndex">
        /// The start index.
        /// </param>
        /// <param name="length">
        /// The length.
        /// </param>
        /// <returns>
        /// The <see cref="string"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// </exception>
        /// <exception cref="ArgumentException">
        /// </exception>
        private static string ByteArrayToString(byte[] value, int startIndex, int length)
        {
            if (value == null)
            {
                throw new ArgumentNullException("value");
            }

            if (startIndex < 0 || startIndex >= value.Length && startIndex > 0)
            {
                throw new ArgumentOutOfRangeException("startIndex");
            }

            if (length < 0)
            {
                throw new ArgumentOutOfRangeException("length");
            }

            if (startIndex > value.Length - length)
            {
                throw new ArgumentException("length");
            }

            if (length == 0)
            {
                return string.Empty;
            }

            if (length > 715827882)
            {
                throw new ArgumentOutOfRangeException("length");
            }

            int length1 = length * 2;
            var chArray = new char[length1];
            int num1 = startIndex;
            int index = 0;
            while (index < length1)
            {
                byte num2 = value[num1++];
                chArray[index] = GetLowercaseHexValue(num2 / 16);
                chArray[index + 1] = GetLowercaseHexValue(num2 % 16);
                index += 2;
            }

            return new string(chArray, 0, chArray.Length);
        }

        /// <summary>
        /// Creates a hash for the two byte arrays passed in.
        /// </summary>
        /// <param name="firstByteArray">
        /// The start byte array.
        /// </param>
        /// <param name="endByteArray">
        /// The end byte array.
        /// </param>
        /// <returns>
        /// A String of the hash.
        /// </returns>
        private static string CreateHashForByteArray(byte[] firstByteArray, byte[] endByteArray)
        {
            byte[] byteArrayToHash = endByteArray == null
                                         ? new byte[firstByteArray.Length]
                                         : new byte[firstByteArray.Length + endByteArray.Length];

            Buffer.BlockCopy(firstByteArray, 0, byteArrayToHash, 0, firstByteArray.Length);
            if (endByteArray != null)
            {
                Buffer.BlockCopy(endByteArray, 0, byteArrayToHash, firstByteArray.Length, endByteArray.Length);
            }

            return ByteArrayToString(Md5.ComputeHash(byteArrayToHash));
        }

        /// <summary>
        /// The ensure path has a terminating separator.
        /// </summary>
        /// <param name="path">
        /// The path.
        /// </param>
        /// <returns>
        /// The <see cref="string"/>.
        /// </returns>
        private static string EnsurePathHasATerminatingSeparator(string path)
        {
            if (path.EndsWith(Path.DirectorySeparatorChar.ToString()))
            {
                return path;
            }

            return path + Path.DirectorySeparatorChar;
        }

        /// <summary>
        /// The get local file byte array.
        /// </summary>
        /// <param name="fileStream">
        /// The file stream.
        /// </param>
        /// <param name="offset">
        /// The offset.
        /// </param>
        /// <param name="byteCountToReturn">
        /// The byte count to return.
        /// </param>
        /// <returns>
        /// The <see cref="byte[]"/>.
        /// </returns>
        private static byte[] GetLocalFileByteArray(FileStream fileStream, long offset, long byteCountToReturn)
        {
            fileStream.Seek(offset, SeekOrigin.Begin);

            var buffer = new byte[byteCountToReturn];

            int count;
            int sum = 0;
            int length = Convert.ToInt32(byteCountToReturn);
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
        /// The get file byte array.
        /// </summary>
        /// <param name="fileName">
        /// The file name.
        /// </param>
        /// <param name="offset">
        /// The offset.
        /// </param>
        /// <param name="byteCountToReturn">
        /// The byte count to return.
        /// </param>
        /// <returns>
        /// The <see cref="byte[]"/>.
        /// </returns>
        private static byte[] GetLocalFileByteArray(string fileName, long offset, long byteCountToReturn)
        {
            byte[] buffer;
            var fileStream = new FileStream(fileName, FileMode.Open, FileAccess.Read);
            fileStream.Seek(offset, SeekOrigin.Begin);
            int sum = 0;
            try
            {
                buffer = new byte[byteCountToReturn];

                int length = Convert.ToInt32(byteCountToReturn);
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
        /// The get lowercase hex value.
        /// </summary>
        /// <param name="i">
        /// The i.
        /// </param>
        /// <returns>
        /// The <see cref="char"/>.
        /// </returns>
        private static char GetLowercaseHexValue(int i)
        {
            if (i < 10)
            {
                return (char)(i + 48);
            }

            return (char)(i - 10 + 65 + 32);
        }

        #endregion
        /// <summary>
        /// 
        /// </summary>
        /// <param name="folderName"></param>
        /// <param name="freespace">in bytes</param>
        /// <param name="totalBytes">in bytes</param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static bool GetDiskInfo(string folderName, out long freespace, out long totalBytes)
        {
            Trace("GetDiskInfo enter");

            if (string.IsNullOrEmpty(folderName))
            {
                throw new ArgumentNullException("folderName");
            }

            if (!folderName.EndsWith("\\"))
            {
                folderName += '\\';
            }

            bool returnValue = GetDiskFreeSpaceEx(folderName, out freespace, out totalBytes, out _);

            Trace("GetDiskInfo exit");
            return returnValue;
        }

        /// <summary>
        /// Returns the path to the folder containing the executing type
        /// </summary>
        /// <param name="startupClass"></param>
        /// <returns></returns>
        public static string GetProjectPath(Type startupClass)
        {
            var assembly = startupClass.GetTypeInfo().Assembly;
            var projectName = assembly.GetName().Name;
            var applicationBasePath = AppContext.BaseDirectory;
            var directoryInfo = new DirectoryInfo(applicationBasePath);
            do
            {
                directoryInfo = directoryInfo.Parent;

                var projectDirectoryInfo = new DirectoryInfo(directoryInfo.FullName);
                if (projectDirectoryInfo.Exists)
                {
                    var projectFileInfo = new FileInfo(Path.Combine(projectDirectoryInfo.FullName, projectName, $"{projectName}.csproj"));
                    if (projectFileInfo.Exists)
                    {
                        return Path.Combine(projectDirectoryInfo.FullName, projectName);
                    }
                }
            } while (directoryInfo.Parent != null);

            return null;
        }

        /// <summary>
        /// Formats a string containing a size in bytes with a suitable suffix
        /// </summary>
        /// <param name="value">Size in bytes</param>
        /// <returns>a string like x.yTB, xGB, xMB or xKB depending on the size</returns>
        public static string FormatSize(long value)
        {
            if (value > BytesInOneTerabyte)
            {
                return $"{(decimal)value / BytesInOneTerabyte:0.#}TB";
            }

            if (value > 25 * (long)BytesInOneGigabyte)
            {
                return $"{value / BytesInOneGigabyte:n0}GB";
            }

            if (value > BytesInOneGigabyte)
            {
                return $"{(decimal)value / BytesInOneGigabyte:0.#}GB";
            }

            if (value > (25 * BytesInOneMegabyte))
            {
                return $"{value / BytesInOneMegabyte:n0}MB";
            }

            if (value > BytesInOneMegabyte)
            {
                return $"{(decimal)value / BytesInOneMegabyte:0.#}MB";
            }

            if (value > BytesInOneKilobyte)
            {
                return $"{value / BytesInOneKilobyte:n0}KB";
            }

            return $"{value:n0}bytes";
        }

        /// <summary>
        /// Generates a random character string for the size provided.
        /// </summary>
        /// <param name="size"></param>
        /// <returns></returns>
        public static string RandomString(long size)
        {
            Trace("RandomString enter");

            StringBuilder builder = new StringBuilder();
            Random random = new Random();
            char ch;
            for (long i = 0; i < size; i++)
            {
                ch = Convert.ToChar(Convert.ToInt32(Math.Floor(26 * random.NextDouble() + 65)));
                builder.Append(ch);
            }

            string returnValue = builder.ToString();

            Trace("RandomString exit");
            return returnValue;
        }

        /// <summary>
        /// Checks the folder is writeable
        /// </summary>
        /// <param name="folderPath"></param>
        /// <returns>True if writeable else False</returns>
        public static bool IsFolderWritable(string folderPath)
        {
            try
            {
                using (FileStream fs = File.Create(
                  Path.Combine(folderPath, Path.GetRandomFileName()),
                  1, FileOptions.DeleteOnClose)
                  )
                { }
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Formats a string containing a speed with a suitable suffix
        /// </summary>
        /// <param name="value">in bytes per second</param>
        /// <returns>a string like x.yTB/s, xGB/s, xMB/s or xKB/s or bytes/s depending on speed</returns>
        public static string FormatSpeed(long value)
        {
            // if disk speed greater than 1TB/s return x.yTB/s
            // if disk speed greater than 25GB/s return xGB/s
            // if disk speed greater than 1GB/s return x.yGB/s
            // if disk speed greater than 25MB/s return x.yMB/s
            // if disk speed greater than 1MB/s return x.yyMB/s
            // if disk speed greater than 1KB/s return xKB/s
            // else return bytes/s

            if (value > BytesInOneTerabyte)
            {
                return $"{(decimal)value / BytesInOneTerabyte:0.#}TB/s";
            }

            if (value > (25 * (long)BytesInOneGigabyte))
            {
                return $"{value / BytesInOneGigabyte:n0}GB/s";
            }

            if (value > BytesInOneGigabyte)
            {
                return $"{(decimal)value / BytesInOneGigabyte:0.#}GB/s";
            }

            if (value > (25 * BytesInOneMegabyte))
            {
                return $"{value / BytesInOneMegabyte:n0}MB/s";
            }

            if (value > BytesInOneMegabyte)
            {
                return $"{(decimal)value / BytesInOneMegabyte:0.#}MB/s";
            }

            if (value > BytesInOneKilobyte)
            {
                return $"{value / BytesInOneKilobyte:n0}KB/s";
            }

            return $"{value:n0}bytes/s";
        }

        /// <summary>
        /// Runs a speedtest on the disk provided.
        /// </summary>
        /// <param name="pathToDiskToTest">The path to test.</param>
        /// <param name="readSpeed">in bytes per second</param>
        /// <param name="writeSpeed">in bytes per second</param>
        /// <returns></returns>
        public static bool DiskSpeedTest(string pathToDiskToTest, long testFileSize, int testIterations, out long readSpeed, out long writeSpeed)
        {
            Trace("DiskSpeedTest enter");

            string tempPath = Path.GetTempPath();

            readSpeed = DiskSpeedTest(pathToDiskToTest, tempPath, testFileSize, testIterations);
            writeSpeed = DiskSpeedTest(tempPath, pathToDiskToTest, testFileSize, testIterations);

            Trace("DiskSpeedTest exit");
            return true;
        }

        /// <summary>
        /// Stops the Windows Service specified if its running
        /// </summary>
        /// <param name="serviceName"></param>
        /// <param name="timeoutMilliseconds"></param>
        /// <returns>True if the service stopped successfully or it was stopped already</returns>
        public static bool StopService(string serviceName, int timeoutMilliseconds)
        {
            Trace("StopService enter");

            ServiceController service = new ServiceController(serviceName);

            try
            {
                TimeSpan timeout = TimeSpan.FromMilliseconds(timeoutMilliseconds);

                if (service.Status == ServiceControllerStatus.Running)
                {
                    service.Stop();
                }

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
        /// Restarts the Windows Service specified
        /// </summary>
        /// <param name="serviceName"></param>
        /// <param name="timeoutMilliseconds"></param>
        /// <returns>True if the service restarted successfully</returns>
        public static bool RestartService(string serviceName, int timeoutMilliseconds)
        {
            Trace("RestartService enter");

            ServiceController service = new ServiceController(serviceName);

            try
            {
                int millisec1 = Environment.TickCount;
                TimeSpan timeout = TimeSpan.FromMilliseconds(timeoutMilliseconds);

                if (service.Status == ServiceControllerStatus.Running)
                {
                    service.Stop();
                }

                service.WaitForStatus(ServiceControllerStatus.Stopped, timeout);

                // count the rest of the timeout
                int millisec2 = Environment.TickCount;
                timeout = TimeSpan.FromMilliseconds(timeoutMilliseconds - (millisec2 - millisec1));

                if (service.Status.Equals(ServiceControllerStatus.Stopped))
                {
                    service.Start();
                }
                else if (service.Status.Equals(ServiceControllerStatus.Paused))
                {
                    service.Continue();
                }

                service.WaitForStatus(ServiceControllerStatus.Running, timeout);
            }
            catch
            {
                return false;
            }

            Trace("RestartService exit");
            return true;
        }
        public static long DiskSpeedTest(string sourcePath, string destinationPath, long testFileSize, int testIterations)
        {
            Trace("DiskSpeedTest enter");

            long randomStringSize = 500_000;
            int streamWriteBufferSize = 20 * BytesInOneMegabyte;

            string randomText = RandomString(randomStringSize);
            double totalPerf;
            DateTime startTime;
            DateTime stopTime;
            long appendIterations = testFileSize / randomStringSize;

            totalPerf = 0;

            for (int j = 1; j <= testIterations; j++)
            {
                string firstPathFilename = sourcePath + "\\" + j + "test.tmp";
                string secondPathFilename = destinationPath + "\\" + j + "test.tmp";

                if (File.Exists(firstPathFilename))
                {
                    File.Delete(firstPathFilename);
                }

                if (File.Exists(secondPathFilename))
                {
                    File.Delete(secondPathFilename);
                }

                using (StreamWriter sWriter = new StreamWriter(firstPathFilename, true, Encoding.UTF8, streamWriteBufferSize))
                {
                    for (long i = 1; i <= appendIterations; i++)
                    {
                        sWriter.Write(randomText);
                    }
                }

                testFileSize = GetFileLength(firstPathFilename);

                startTime = DateTime.Now;
                File.Copy(firstPathFilename, secondPathFilename);
                stopTime = DateTime.Now;

                File.Delete(firstPathFilename);
                File.Delete(secondPathFilename);

                TimeSpan interval = stopTime - startTime;
                totalPerf += testFileSize / interval.TotalSeconds;
            }

            long returnValue = Convert.ToInt64(totalPerf / testIterations);

            Trace("DiskSpeedTest exit");
            return returnValue;
        }

        internal static void Trace(string value)
        {
            string[] textArrayToWrite = value.Split('\n');

            foreach (string line in textArrayToWrite)
            {
                if (line.HasValue())
                {
                    System.Diagnostics.Trace.WriteLine($"{DateTime.Now:dd-MM-yy HH:mm:ss.ff} : {line}");
                }
            }
        }

        /// <summary>
        /// Deletes the file specified if it exists even if it was readonly.
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

                var entries = Directory.EnumerateFileSystemEntries(directory);

                if (!entries.Any())
                {
                    try
                    { if (directory != rootDirectory)
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

            Trace("DeleteEmptyDirectories exit");
        }

        /// <summary>
        /// Deletes any empty directories in the directory specified and checks recursively all its sub-directories.
        /// </summary>
        /// <param name="directory">The directory to check</param>
        /// <exception cref="ArgumentException"></exception>
        /// <returns>An array of the directory paths that were removed</returns>
        internal static string[] DeleteEmptyDirectories(string directory)
        {
            Trace("DeleteEmptyDirectories enter");

            if (string.IsNullOrEmpty(directory))
            {
                throw new ArgumentException("Directory is a null reference or an empty string", "directory");
            }

            List<string> listOfDirectoriesDeleted = new List<string>();

            DeleteEmptyDirectories(directory, listOfDirectoriesDeleted, directory);

            Trace("DeleteEmptyDirectories exit");
            return listOfDirectoriesDeleted.ToArray();
        }

        /// <summary>
        /// Converts a GB value to a size in bytes
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        internal static long ConvertGBtoBytes(long value)
        {
            return Convert.ToInt64(value * BytesInOneGigabyte);
        }
    }
}