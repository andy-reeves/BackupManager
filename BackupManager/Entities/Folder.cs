// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="Folder.cs" company="Andy Reeves">
//
//  </copyright>
//  --------------------------------------------------------------------------------------------------------------------

namespace BackupManager.Entities
{
    using System;

    public class Folder
    {
        /// <summary>
        /// The path of the folder.
        /// </summary>
        public string Path;

        /// <summary>
        /// The DateTime this folder last changed.
        /// </summary>
        public DateTime ModifiedDateTime;
    }
}
