// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="Folder.cs" company="Andy Reeves">
//
//  </copyright>
//  --------------------------------------------------------------------------------------------------------------------

namespace BackupManager.Entities
{
    using System;

    /// <summary>
    /// This class allows us to keep a Collection of FoldersToScan with the path and datetime it was last changed
    /// </summary>
    public class Folder : IEquatable<Folder>
    {
        /// <summary>
        /// The path to the folder that changed
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// The Timestamp the folder was last changed
        /// </summary>
        public DateTime ModifiedDateTime { get; set; }

        public Folder()
        {
        }

        public Folder(string path) : this(path, DateTime.Now) { }

        public Folder(string path, DateTime dateTime)
        {
            Path = path;
            ModifiedDateTime = dateTime;
        }

        public bool Equals(Folder other)
        {
            return null != other && Path == other.Path;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as Folder);
        }
        public override int GetHashCode()
        {
            return Path.GetHashCode();
        }
    }
}
