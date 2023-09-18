// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="FoldersToScan.cs" company="Andy Reeves">
//
//  </copyright>
//  --------------------------------------------------------------------------------------------------------------------

namespace BackupManager.Entities
{
    using System;

    /// <summary>
    /// This class allows us to keep a Collection of FoldersToScan with the path and datetime it was last changed
    /// </summary>
    public class FoldersToScan : IEquatable<FoldersToScan>
    {
        /// <summary>
        /// The path to the folder that changed
        /// </summary>
        public string Path;

        /// <summary>
        /// The Timestamp the folder was last changed
        /// </summary>
        public DateTime ModifiedDateTime;

        public FoldersToScan()
        {
        }

        public FoldersToScan(string scanFolder)
        {
            Path = scanFolder;
            ModifiedDateTime = DateTime.Now;
        }

        public FoldersToScan(string scanFolder, DateTime dateTime)
        {
            Path = scanFolder;
            ModifiedDateTime = dateTime;
        }

        public bool Equals(FoldersToScan other)
        {
            return null != other && Path == other.Path;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as FoldersToScan);
        }
        public override int GetHashCode()
        {
            return Path.GetHashCode();
        }
    }
}
