namespace BackupManager.Entities
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Xml.Serialization;

    [DebuggerDisplay("RelativePath = {RelativePath}")]
    public class BackupFile
    {
        private string fullPath;

        private string hash;

        private string contentsHash;

        /// <summary>
        /// The relative path of the file.
        /// </summary>
        [XmlElement("Path")]
        public string RelativePath;

        public string MasterFolder;

        public string IndexFolder;

        /// <summary>
        /// The MD5 hash of the file contents.
        /// </summary>
        [XmlElement("Hash")]
        public string ContentsHash
        {
            get { return contentsHash; }
            set { 
                contentsHash = value;

                // force the combined hash to be re-calculated
                this.hash = null;
            }
        }

        [XmlElement("Disk")]
        public string BackupDisk;
       
        [XmlElement("DiskChecked")]
        public string BackupDiskChecked;

        /// <summary>
        /// The last modified date/time of the file.
        /// </summary>
        public DateTime LastWriteTime;

        /// <summary>
        /// The size in bytes of the file.
        /// </summary>
        public long Length;

        [XmlIgnore]
        public bool Flag;

        [XmlIgnore()]
        public string FullPath
        {
            get
            {
                if (fullPath == null)
                {
                    fullPath = Path.Combine(this.MasterFolder, this.IndexFolder, this.RelativePath);
                }

                return fullPath;
            }
        }

        /// <summary>
        /// Gets the number only of this disk this file is on. 0 if not backed up
        /// </summary>
        [XmlIgnore()]
        public int BackupDiskNumber
        {
            get
            {
                if (string.IsNullOrEmpty(BackupDisk)) {  return 0; }

                string diskNumberString = BackupDisk.SubstringAfter(' ');
                if (string.IsNullOrEmpty(diskNumberString)) { return 0; }

                return int.Parse(diskNumberString);
            }
        }

        /// <summary>
        /// This is a combination key of the hash of the file contents and the file name.
        /// </summary>
        [XmlIgnore()]
        public string Hash
        {
            get
            {
                if (hash == null)
                {
                    hash = GetFileHash(this.ContentsHash, this.RelativePath);
                }

                return hash;
            }
        }

        public BackupFile()
        {
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="fullPath"></param>
        /// <param name="masterFolder"></param>
        /// <param name="indexFolder"></param>
        public BackupFile(string fullPath, string masterFolder, string indexFolder)
        {
            this.SetFullPath(fullPath, masterFolder, indexFolder);
        }

        public void SetFullPath(string fullPath, string masterFolder, string indexFolder)
        {
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException();
            }

            this.fullPath = fullPath;

            this.RelativePath = GetRelativePath(fullPath, masterFolder, indexFolder);

            this.MasterFolder = masterFolder;
            this.IndexFolder = indexFolder;

            this.UpdateContentsHash();
            this.UpdateLastWriteTime();
            this.UpdateFileLength();
        }

        private static string GetRelativePath(string fullPath, string masterFolder, string indexFolder)
        {
            var p = System.IO.Path.Combine(masterFolder, indexFolder);

            if (!fullPath.StartsWith(p))
            {
                throw new ArgumentException();
            }

            var g = fullPath.SubstringAfter(p, StringComparison.CurrentCultureIgnoreCase);

            return g.TrimStart(new[] { '\\' });
        }

        public static string GetFileHash(string hash, string path)
        {
            return hash + "-" + System.IO.Path.GetFileName(path);
        }

        public string GetFileName()
        {
            return System.IO.Path.GetFileName(this.fullPath);
        }

        public void UpdateContentsHash()
        {
            this.ContentsHash = Utils.GetShortMd5HashFromFile(this.FullPath);

            if (this.ContentsHash == Utils.ZeroByteHash)
            {
                throw new ApplicationException(string.Format("File '{0}' has zerobyte Hashcode", this.FullPath));
            }

            // force the hash to be re-calculated 
            this.hash = null;
        }

        public void UpdateLastWriteTime()
        {
            this.LastWriteTime = Utils.GetFileLastWriteTime(this.FullPath);
        }

        public void UpdateFileLength()
        {
            this.Length = Utils.GetFileLength(this.FullPath);

            if (this.Length == 0)
            {
                throw new ApplicationException(string.Format("File '{0}' has 0 length", this.FullPath));
            }
        }
    }
}
