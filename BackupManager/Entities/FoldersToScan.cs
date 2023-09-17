// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="FoldersToScan.cs" company="Andy Reeves">
//
//  </copyright>
//  --------------------------------------------------------------------------------------------------------------------

namespace BackupManager.Entities
{
    using System;

    public class FoldersToScan
    {
        public string Path;

        public DateTime Timestamp;

        public FoldersToScan()
        {
        }

        public FoldersToScan(string scanFolder, DateTime dateTime)
        {
            Path = scanFolder;
            Timestamp = dateTime;
        }
    }
}
