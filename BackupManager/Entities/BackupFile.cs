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
        public string Disk;
       
        [XmlElement("DiskChecked")]
        public string DiskChecked;

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
                    fullPath = Path.Combine(MasterFolder, IndexFolder, RelativePath);
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
                if (string.IsNullOrEmpty(Disk)) { return 0; }

                string diskNumberString = Disk.SubstringAfter(' ');
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
                    hash = GetFileHash(ContentsHash, RelativePath);
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
            SetFullPath(fullPath, masterFolder, indexFolder);
        }

        public void SetFullPath(string fullPath, string masterFolder, string indexFolder)
        {
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException();
            }

            this.fullPath = fullPath;

            RelativePath = GetRelativePath(fullPath, masterFolder, indexFolder);

            MasterFolder = masterFolder;
            IndexFolder = indexFolder;

            UpdateContentsHash();
            UpdateLastWriteTime();
            UpdateFileLength();
        }

        private static string GetRelativePath(string fullPath, string masterFolder, string indexFolder)
        {
            string combinedPath = Path.Combine(masterFolder, indexFolder);

            if (!fullPath.StartsWith(combinedPath))
            {
                throw new ArgumentException();
            }

            return fullPath.SubstringAfter(combinedPath, StringComparison.CurrentCultureIgnoreCase).TrimStart(new[] { '\\' });
        }

        public static string GetFileHash(string hash, string path)
        {
            return hash + "-" + Path.GetFileName(path);
        }

        public string GetFileName()
        {
            return Path.GetFileName(fullPath);
        }

        public void UpdateContentsHash()
        {
            ContentsHash = Utils.GetShortMd5HashFromFile(FullPath);

            if (ContentsHash == Utils.ZeroByteHash)
            {
                throw new ApplicationException($"File '{FullPath}' has zerobyte Hashcode");
            }

            // force the hash to be re-calculated 
            hash = null;
        }

        public void UpdateLastWriteTime()
        {
            LastWriteTime = Utils.GetFileLastWriteTime(FullPath);
        }

        public void UpdateFileLength()
        {
            Length = Utils.GetFileLength(FullPath);

            if (Length == 0)
            {
                throw new ApplicationException($"File '{FullPath}' has 0 length");
            }
        }
    }
}
