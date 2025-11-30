// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="Directory.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using BackupManager.Extensions;

// ReSharper disable once CheckNamespace
namespace BackupManager;

internal static partial class Utils
{
    internal static class Directory
    {
        /// <summary>
        ///     Renames the directory at the end of the path ensures it's the correct case
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        /// <exception cref="DirectoryNotFoundException"></exception>
        internal static bool Rename(string path)
        {
            if (!System.IO.Directory.Exists(path)) throw new DirectoryNotFoundException($"{path} not found");

            if (!DirectoryExistsWithDifferentCase(path)) return true;
            if (!path.StartsWithIgnoreCase(@"\\")) return File.MoveFile(path, path);

            var dir = new DirectoryInfo(path);
            Trace($"Renaming ${dir.FullName} to {path + "tmp"}");
            dir.MoveTo(path + "tmp");
            Trace($"Renaming ${dir.FullName} to {path}");
            dir.MoveTo(path);
            return true;
        }

        private static bool DirectoryExistsWithDifferentCase(string directoryName)
        {
            if (!Exists(directoryName)) throw new DirectoryNotFoundException($"{directoryName} not found");

            var result = false;
            directoryName = directoryName.TrimEnd(Path.DirectorySeparatorChar);
            var lastPathSeparatorIndex = directoryName.LastIndexOf(Path.DirectorySeparatorChar);

            if (lastPathSeparatorIndex >= 0)
            {
                var baseDirectory = directoryName[(lastPathSeparatorIndex + 1)..];
                var parentDirectory = directoryName[..lastPathSeparatorIndex];
                var directories = System.IO.Directory.GetDirectories(parentDirectory, baseDirectory);
                if (directories.Length <= 0) return false;

                if (string.CompareOrdinal(directories[0], directoryName) != 0) result = true;
                return result;
            }

            //if directory is a drive
            directoryName += Path.DirectorySeparatorChar.ToString();
            var drives = DriveInfo.GetDrives();

            foreach (var driveInfo in drives.Where(driveInfo => string.Compare(driveInfo.Name, directoryName, StringComparison.OrdinalIgnoreCase) == 0))
            {
                if (string.CompareOrdinal(driveInfo.Name, directoryName) != 0) result = true;
                break;
            }
            return result;
        }

        internal static void EnsurePath(string directoryPath)
        {
            EnsureForFilePath(Path.Combine(directoryPath, "temp.txt"));
        }

        internal static void EnsureForFilePath(string filePath)
        {
            var directoryName = new FileInfo(filePath).DirectoryName;
            if (directoryName != null) _ = System.IO.Directory.CreateDirectory(directoryName);
        }

        /// <summary>
        ///     Deletes any empty directories in the directory specified and checks recursively all its subdirectories.
        /// </summary>
        /// <param name="directory">The directory to check</param>
        /// <exception cref="ArgumentException"></exception>
        /// <returns>An array of the directory paths that were removed</returns>
        internal static IEnumerable<string> DeleteEmpty(string directory)
        {
            TraceIn();
            ArgumentException.ThrowIfNullOrEmpty(directory);
            List<string> listOfDirectoriesDeleted = [];
            DeleteEmpty(directory, listOfDirectoriesDeleted, directory);
            return TraceOut(listOfDirectoriesDeleted.ToArray());
        }

        private static void DeleteEmpty(string directory, ICollection<string> list, string rootDirectory)
        {
            TraceIn(directory);

            try
            {
                foreach (var subDirectory in System.IO.Directory.EnumerateDirectories(directory))
                {
                    DeleteEmpty(subDirectory, list, rootDirectory);
                }

                if (IsEmpty(directory))
                {
                    try
                    {
                        if (directory != rootDirectory)
                        {
                            Trace($"Deleting empty directory {directory}");
                            list.Add(directory);
                            _ = Delete(directory);
                        }
                    }
                    catch (UnauthorizedAccessException) { }
                    catch (DirectoryNotFoundException) { }
                }
            }
            catch (UnauthorizedAccessException) { }
            TraceOut();
        }

        private static bool SetLastWriteUtc(string dirPath, DateTime lastWriteDate)
        {
            using var hDir = File.CreateFile(dirPath, FILE_ACCESS_GENERIC_READ | FILE_ACCESS_GENERIC_WRITE, FileShare.ReadWrite, IntPtr.Zero, (FileMode)OPEN_EXISTING,
                FILE_FLAG_BACKUP_SEMANTICS, IntPtr.Zero);
            var lastWriteTime = lastWriteDate.ToFileTime();
            return File.SetFileTime(hDir, IntPtr.Zero, IntPtr.Zero, ref lastWriteTime);
        }

        /// <summary>
        ///     Checks the directory is writable
        /// </summary>
        /// <param name="path"></param>
        /// <returns>True if writable else False</returns>
        internal static bool IsWritable(string path)
        {
            TraceIn(path);
            if (!System.IO.Directory.Exists(path)) return TraceOut(false);

            try
            {
                var lastWriteDate = System.IO.Directory.GetLastWriteTimeUtc(path);
                var tempFile = Path.GetFileNameWithoutExtension(Path.GetRandomFileName()) + IS_DIRECTORY_WRITABLE_GUID + ".tmp";
                using var fs = File.Create(Path.Combine(path, tempFile), 1, FileOptions.DeleteOnClose);
                return TraceOut(SetLastWriteUtc(path, lastWriteDate));
            }
            catch
            {
                return TraceOut(false);
            }
        }

        /// <summary>
        ///     Checks to see if the directory specified is empty
        /// </summary>
        /// <param name="path">The directory to check</param>
        /// <returns>
        ///     True if the directory exists as a normal directory and it's empty. If it's a Symbolic link and the target is
        ///     missing
        ///     or the target is empty it returns True, otherwise False.
        /// </returns>
        internal static bool IsEmpty(string path)
        {
            if (!System.IO.Directory.Exists(path)) return false;
            if (!IsSymbolicLink(path)) return !System.IO.Directory.EnumerateFileSystemEntries(path).Any();

            var linkTarget = new FileInfo(path).LinkTarget;
            return linkTarget != null && (!SymbolicLinkTargetExists(path) || !(System.IO.Directory.GetFileSystemEntries(linkTarget).Length > 0));
        }

        internal static bool Delete(string path, bool recursive = false)
        {
            TraceIn(path);
            if (!System.IO.Directory.Exists(path)) return TraceOut(true);

#if DIRECTORYDELETE
            File.ClearFileAttribute(path, FileAttributes.ReadOnly);
            System.IO.Directory.Delete(path, recursive);
#else
            LogWithPushover(BackupAction.General, PushoverPriority.High, $"DirectoryDelete with {path} - NOT DELETING", true, true);
#endif
            return TraceOut(true);
        }

        internal static void Copy(string sourceDirectory, string targetDirectory)
        {
            ArgumentException.ThrowIfNullOrEmpty(sourceDirectory);
            ArgumentException.ThrowIfNullOrEmpty(targetDirectory);
            if (!System.IO.Directory.Exists(sourceDirectory)) throw new DirectoryNotFoundException();
            if (System.IO.Directory.Exists(targetDirectory)) throw new NotSupportedException("Target Directory exists");

            var diSource = new DirectoryInfo(sourceDirectory);
            var diTarget = new DirectoryInfo(targetDirectory);
            CopyAllFiles(diSource, diTarget);
        }

        private static void CopyAllFiles(DirectoryInfo source, DirectoryInfo target)
        {
            _ = System.IO.Directory.CreateDirectory(target.FullName);

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

        internal static bool Exists(string directory)
        {
            return System.IO.Directory.Exists(directory);
        }

        internal static DirectoryInfo CreateDirectory(string path)
        {
            return System.IO.Directory.CreateDirectory(path);
        }
    }
}