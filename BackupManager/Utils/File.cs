﻿// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="File.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;

using BackupManager.Extensions;
using BackupManager.Properties;
using BackupManager.Radarr;

using Microsoft.Win32.SafeHandles;

// ReSharper disable once CheckNamespace
namespace BackupManager;

internal static partial class Utils
{
    [SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
    internal static partial class File
    {
        [LibraryImport("kernel32.dll", EntryPoint = "CreateFileW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        internal static partial SafeFileHandle CreateFile(string fileName, uint dwDesiredAccess, FileShare dwShareMode,
            IntPtr securityAttrsMustBeZero, FileMode dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFileMustBeZero);

        [LibraryImport("kernel32.dll", EntryPoint = "SetFileTime", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool SetFileTime(SafeFileHandle hFile, IntPtr lpCreationTimeUnused, IntPtr lpLastAccessTimeUnused,
            ref long lpLastWriteTime);

        /// <summary>
        ///     Checks the path is a video file
        /// </summary>
        /// <param name="path"></param>
        /// <returns>False if it's not a video file, True if it's a video file. Exception if path is null or empty</returns>
        internal static bool IsVideo(string path)
        {
            ArgumentException.ThrowIfNullOrEmpty(path);
            return Path.GetExtension(path).ToLowerInvariant().ContainsAny(_videoExtensions);
        }

        /// <summary>
        ///     Hash is now different depending on the filename as we write that into the file
        ///     for test1.txt the hash is b3d5cf638ed2f6a94d6b3c628f946196
        /// </summary>
        /// <param name="filePath"></param>
        internal static void Create(string filePath)
        {
            Directory.EnsureForFilePath(filePath);
            AppendAllText(filePath, Path.GetFileName(filePath));
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
        internal static string GetHash(string fileName, HashAlgorithm algorithm)
        {
            using BufferedStream stream = new(OpenRead(fileName), BYTES_IN_ONE_MEGABYTE);
            return ByteArrayToString(algorithm.ComputeHash(stream));
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
                startBlock = GetByteArray(stream, 0, START_BLOCK_SIZE);
                middleBlock = GetByteArray(stream, startDownloadPositionForMiddleBlock, MIDDLE_BLOCK_SIZE);
                endBlock = GetByteArray(stream, startDownloadPositionForEndBlock, END_BLOCK_SIZE);
            }
            else
                startBlock = GetByteArray(stream, 0, size);
            return CreateHashForByteArray(startBlock, middleBlock, endBlock);
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
        internal static string GetShortMd5Hash(string path)
        {
            TraceIn(path);
            ArgumentException.ThrowIfNullOrEmpty(path);
            if (!Exists(path)) throw new FileNotFoundException(Resources.FileNotFound, path);

            var size = new FileInfo(path).Length;
            if (size == 0) return string.Empty;

            byte[] startBlock;
            byte[] middleBlock = null;
            byte[] endBlock = null;

            if (size >= START_BLOCK_SIZE + MIDDLE_BLOCK_SIZE + END_BLOCK_SIZE)
            {
                var startDownloadPositionForEndBlock = size - END_BLOCK_SIZE;
                var startDownloadPositionForMiddleBlock = size / 2;
                startBlock = GetByteArray(path, 0, START_BLOCK_SIZE);
                middleBlock = GetByteArray(path, startDownloadPositionForMiddleBlock, MIDDLE_BLOCK_SIZE);
                endBlock = GetByteArray(path, startDownloadPositionForEndBlock, END_BLOCK_SIZE);
            }
            else
                startBlock = GetByteArray(path, 0, size);
            return TraceOut(CreateHashForByteArray(startBlock, middleBlock, endBlock));
        }

        internal static bool SetLastWriteTime(string fileName, DateTime writeTimeToUse)
        {
            ArgumentException.ThrowIfNullOrEmpty(fileName, nameof(fileName));
            if (!Exists(fileName)) throw new FileNotFoundException(Resources.FileNotFound, fileName);

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

        internal static DateTime GetLastWriteTime(string fileName)
        {
            ArgumentException.ThrowIfNullOrEmpty(fileName, nameof(fileName));
            if (!Exists(fileName)) throw new FileNotFoundException(Resources.FileNotFound, fileName);

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

        internal static long GetLength(string fileName)
        {
            ArgumentException.ThrowIfNullOrEmpty(fileName, nameof(fileName));
            if (!Exists(fileName)) throw new FileNotFoundException(Resources.FileNotFound, fileName);

            return new FileInfo(fileName).Length;
        }

        /// <summary>
        ///     Checks the path is a subtitles file
        /// </summary>
        /// <param name="path"></param>
        /// <returns>False if it's not a subtitles file, True if it's a subtitles file. Exception if path is null or empty</returns>
        internal static bool IsSubtitles(string path)
        {
            ArgumentException.ThrowIfNullOrEmpty(path);
            return Path.GetExtension(path).ToLowerInvariant().ContainsAny(_subtitlesExtensions);
        }

        internal static bool IsSpecialFeature(string path)
        {
            ArgumentException.ThrowIfNullOrEmpty(path);
            return path.ContainsAny(_specialFeatures);
        }

        private static FileAttributes RemoveAttribute(FileAttributes attributes, FileAttributes attributesToRemove)
        {
            return attributes & ~attributesToRemove;
        }

        /// <summary>
        ///     Returns True if FileName is accessible (not locked) by another process
        /// </summary>
        /// <param name="path"></param>
        /// <returns>True if not locked or False if locked</returns>
        internal static bool IsAccessible(string path)
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
        ///     Clears the attribute from the file if it were set.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="attributeToRemove"></param>
        internal static void ClearFileAttribute(string path, FileAttributes attributeToRemove)
        {
            TraceIn(path, attributeToRemove);
            var attributes = GetAttributes(path);

            if ((attributes & attributeToRemove) == attributeToRemove)
            {
                attributes = RemoveAttribute(attributes, attributeToRemove);
                SetAttributes(path, attributes);
            }
            TraceOut();
        }

        internal static bool IsDolbyVisionProfile5(string path)
        {
            TraceIn(path);
            ArgumentException.ThrowIfNullOrEmpty(path);
            ArgumentException.ThrowIfNullOrEmpty(path);
            if (!IsVideo(path) || !MediaHelper.VideoFileIsDolbyVision(path)) return TraceOut(false);

            if (!Exists(path)) throw new FileNotFoundException(Resources.FileNotFound, path);

            var info = new VideoFileInfoReader().GetMediaInfo(path);

            // ReSharper disable once StringLiteralTypo
            return info == null
                ? throw new ApplicationException("Unable to load ffprobe.exe")
                : TraceOut(info is { DoviConfigurationRecord.DvProfile: 5 });
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
        internal static byte[] GetByteArray(string fileName, long offset, long byteCountToReturn)
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
        private static byte[] GetByteArray(Stream stream, long offset, long byteCountToReturn)
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
        ///     Copies an existing file to a new file asynchronously. Overwriting a file of the same name is not allowed. Ensures
        ///     the destination folder exists too.
        /// </summary>
        /// <param name="sourceFileName">The file to copy.</param>
        /// <param name="destFileName">The name of the destination file. This cannot be a directory or an existing file.</param>
        /// <param name="ct"></param>
        /// <returns>True if copy was successful or False</returns>
        internal static bool Copy(string sourceFileName, string destFileName, CancellationToken ct)
        {
            TraceIn(sourceFileName, destFileName);
            if (destFileName == null || sourceFileName == null) return false;

            if (sourceFileName.Length > 256) throw new NotSupportedException($"Source file name {sourceFileName} exceeds 256 characters");
            if (destFileName.Length > 256) throw new NotSupportedException($"Destination file name {destFileName} exceeds 256 characters");
            if (Exists(destFileName)) throw new NotSupportedException("Destination file already exists");

            Directory.EnsureForFilePath(destFileName);
            if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();

            // ReSharper disable once CommentTypo
            // we create the destination file so xcopy knows it's a file and can copy over it
            WriteAllText(destFileName, "Temp file"); // hash of this is 88f85bbea58fbff062050bcb2d2aafcf

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
                var hashSource = GetShortMd5Hash(sourceFileName);

                while (!IsAccessible(destFileName) || GetShortMd5Hash(destFileName) != hashSource)
                {
                    Wait(1);
                    if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();
                }
                return TraceOut(true);
            }
        }

        /// <summary>
        ///     Deletes the file specified if it exists even if it was readonly. Returns true if the file doesn't exist or if it
        ///     was deleted successfully
        /// </summary>
        /// <param name="path"></param>
        internal static bool Delete(string path)
        {
            TraceIn(path);
            if (!Exists(path)) return TraceOut(true);

#if FILEDELETE
            ClearFileAttribute(path, FileAttributes.ReadOnly);
            System.IO.File.Delete(path);
#else
            LogWithPushover(BackupAction.General, PushoverPriority.High, $"FileDelete with {path} - NOT DELETING", true, true);
#endif
            return TraceOut(true);
        }

        /// <summary>
        ///     Moves a specified file to a new location, providing the option to specify a new file name. Ensures the destination
        ///     folder exists too.
        /// </summary>
        /// <param name="sourceFileName">The name of the file to move. Can include a relative or absolute path.</param>
        /// <param name="destFileName">The new path and name for the file.</param>
        /// <returns>True if successfully renamed otherwise False</returns>
        internal static bool Move(string sourceFileName, string destFileName)
        {
            TraceIn(sourceFileName, destFileName);
#if FILEMOVE
            Directory.EnsureForFilePath(destFileName);
            System.IO.File.Move(sourceFileName, destFileName);
#else
            LogWithPushover(BackupAction.General, PushoverPriority.High, $"FileMove with {sourceFileName} to {destFileName} - NOT MOVING", true, true);
#endif
            return TraceOut(true);
        }

        internal static void AppendAllText(string filePath, string contents)
        {
            System.IO.File.AppendAllText(filePath, contents);
        }

        internal static bool Exists(string path)
        {
            return System.IO.File.Exists(path);
        }

        internal static string ReadAllText(string path)
        {
            return System.IO.File.ReadAllText(path);
        }

        internal static void WriteAllText(string path, string contents)
        {
            System.IO.File.WriteAllText(path, contents);
        }

        internal static FileAttributes GetAttributes(string path)
        {
            return System.IO.File.GetAttributes(path);
        }

        internal static void SetAttributes(string path, FileAttributes fileAttributes)
        {
            System.IO.File.SetAttributes(path, fileAttributes);
        }

        internal static Stream OpenRead(string path)
        {
            return System.IO.File.OpenRead(path);
        }

        internal static void AppendAllLines(string path, IEnumerable<string> contents)
        {
            System.IO.File.AppendAllLines(path, contents);
        }

        internal static FileStream Create(string path, int bufferSize, FileOptions options)
        {
            return System.IO.File.Create(path, bufferSize, options);
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
            return GetFiles(path, "*", SearchOption.AllDirectories, 0, 0, ct);
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
            return GetFiles(path, filters, SearchOption.AllDirectories, 0, 0, ct);
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
            return GetFiles(path, filters, searchOption, directoryAttributesToIgnore, 0, ct);
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
            return GetFiles(path, filters, searchOption, 0, 0, ct);
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
        internal static byte[] GetByteArray(Stream fileStream, long byteCountToReturn)
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
        /// <param name="ct"></param>
        /// <returns>
        /// </returns>
        internal static string[] GetFiles(string path, string filters, SearchOption searchOption, FileAttributes directoryAttributesToIgnore,
            FileAttributes fileAttributesToIgnore, CancellationToken ct)
        {
            if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();
            var sw = Stopwatch.StartNew();

            if (!System.IO.Directory.Exists(path))
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

                if (searchOption == SearchOption.AllDirectories)
                {
                    foreach (var subDir in System.IO.Directory.GetDirectories(dir).Where(subDir =>
                                 !AnyFlagSet(new DirectoryInfo(subDir).Attributes, directoryAttributesToIgnore)))
                    {
                        if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();
                        pathsToSearch.Enqueue(subDir);
                    }
                }

                foreach (var collection in includeAsArray2.Select(filter =>
                         {
                             if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();
                             return System.IO.Directory.GetFiles(dir, filter, SearchOption.TopDirectoryOnly);
                         }).Select(allFiles => excludeAsArray.Any() ? allFiles.Where(p => !excludeRegex.Match(p).Success) : allFiles))
                {
                    foundFiles.AddRange(collection.Where(p => !AnyFlagSet(new FileInfo(p).Attributes, fileAttributesToIgnore)));
                }
            }
            sw.Stop();
            Trace($"GetFiles for {path} = {(sw.Elapsed.TotalSeconds < 1 ? "<1 second" : $"{sw.Elapsed.TotalSeconds:#} seconds")}");
            return foundFiles.ToArray();
        }
    }
}