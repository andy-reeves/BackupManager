// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="UtilsGetLatestApplicationVersion.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;

using BackupManager;
using BackupManager.Entities;
using BackupManager.Extensions;

namespace TestProject;

[SuppressMessage("ReSharper", "MemberCanBeFileLocal")]
[SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "<Pending>")]
public sealed class UtilsGetLatestApplicationVersion
{
    static UtilsGetLatestApplicationVersion()
    {
        var mediaBackup = MediaBackup.Load(Path.Combine(Utils.GetProjectPath(typeof(FileRulesUnitTest)), "..\\BackupManager\\MediaBackup.xml"));
        Utils.Config = mediaBackup.Config;
    }

    [Fact]
    [SuppressMessage("ReSharper", "IdentifierTypo")]
    public void GetVersionNumber()
    {
        Assert.Equal("1.4.1", Utils.GetApplicationVersionNumber(ApplicationType.Bazarr));
        Assert.Equal("1.40.0.7775", Utils.GetApplicationVersionNumber(ApplicationType.PlexPass));
        Assert.Equal("1.12.2.4211", Utils.GetApplicationVersionNumber(ApplicationType.Prowlarr));
        Assert.Equal("5.2.6.8376", Utils.GetApplicationVersionNumber(ApplicationType.Radarr));
        Assert.Equal("4.2.2", Utils.GetApplicationVersionNumber(ApplicationType.SABnzbd));
        Assert.Equal("4.0.1.929", Utils.GetApplicationVersionNumber(ApplicationType.Sonarr));
    }

    [Fact]
    [SuppressMessage("ReSharper", "CommentTypo")]
    public void GetLatestApplicationVersionNumber()
    {
        Assert.Equal("1.32.8.7639", Utils.GetLatestApplicationVersionNumber(ApplicationType.Plex));
        Assert.Equal("1.3.1", Utils.GetLatestApplicationVersionNumber(ApplicationType.Bazarr));
        Assert.Equal("1.12.2", Utils.GetLatestApplicationVersionNumber(ApplicationType.Prowlarr));
        Assert.Equal("5.2.6", Utils.GetLatestApplicationVersionNumber(ApplicationType.Radarr));
        Assert.Equal("4.2.2", Utils.GetLatestApplicationVersionNumber(ApplicationType.SABnzbd));
        Assert.Equal("4.0.1.929", Utils.GetLatestApplicationVersionNumber(ApplicationType.Sonarr));

        // These are the latest or develop branches

        Assert.Equal(Utils.Config.PlexToken.HasValue() ? "1.40.0.7775" : "1.32.8.7639",
            Utils.GetLatestApplicationVersionNumber(ApplicationType.PlexPass));
        Assert.Equal("1.13.3", Utils.GetLatestApplicationVersionNumber(ApplicationType.Prowlarr, "develop"));
        Assert.Equal("5.3.5", Utils.GetLatestApplicationVersionNumber(ApplicationType.Radarr, "develop"));
    }
}