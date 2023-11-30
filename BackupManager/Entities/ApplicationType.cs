// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="ApplicationType.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;

namespace BackupManager.Entities;

[SuppressMessage("ReSharper", "IdentifierTypo")]
[SuppressMessage("ReSharper", "InconsistentNaming")]
public enum ApplicationType
{
    Unknown = 0,

    Plex = 1,

    PlexPass = 2,

    SABnzbd = 3,

    Sonarr = 4,

    Radarr = 5,

    Bazarr = 6,

    Prowlarr = 7
}