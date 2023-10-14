// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="SymbolicLink.cs" company="Andy Reeves">
//
//  </copyright>
//  --------------------------------------------------------------------------------------------------------------------

namespace BackupManager.Entities;

public class SymbolicLink
{
    public string RootFolder { get; set; }

    public string FileDiscoveryRegEx { get; set; }

    public string RelativePath { get; set; }

    public string PathToTarget { get; set; }
}