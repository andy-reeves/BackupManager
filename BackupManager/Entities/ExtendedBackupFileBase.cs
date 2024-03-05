// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="ExtendedBackupFileBase.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System.IO;
using System.Xml.Serialization;

namespace BackupManager.Entities;

internal abstract class ExtendedBackupFileBase : BackupFile
{
    public string GetFileNameWithoutExtension()
    {
        return Path.GetFileNameWithoutExtension(GetFileName());
    }

    public abstract string GetFileName();

    public abstract string GetFullName();

    [XmlIgnore] public string Extension { get; set; }

    [XmlIgnore] public bool Valid { get; set; }

    public string OriginalPath { get; set; }

    public string Title { get; set; }

    public string FullDirectory { get; set; }
}

