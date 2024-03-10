// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="ExtendedBackupFileBase.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System.IO;
using System.Xml.Serialization;

namespace BackupManager.Entities;

internal abstract class ExtendedBackupFileBase
{
    public string GetFileNameWithoutExtension()
    {
        return Path.GetFileNameWithoutExtension(GetFileName());
    }

    public abstract string GetFileName();

    public abstract string GetFullName();

    [XmlIgnore] protected string Extension { get; set; }

    [XmlIgnore] public bool IsValid { get; protected set; }

    protected string OriginalPath { get; init; }

    protected string Title { get; set; }

    public string FullDirectory { get; protected set; }

    public abstract bool RefreshMediaInfo();

    protected abstract bool Validate();
}

