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

    [Theory]
    [InlineData("1.4.2", ApplicationType.Bazarr)]
    [InlineData("1.40.2.8312", ApplicationType.PlexPass)]
    [InlineData("1.15.0.4361", ApplicationType.Prowlarr)]
    [InlineData("5.3.6.8612", ApplicationType.Radarr)]
    [InlineData("4.2.3", ApplicationType.SABnzbd)]
    [InlineData("4.0.3.1413", ApplicationType.Sonarr)]
    public void GetVersionNumber(string expectedInstalledVersionNumber, ApplicationType applicationType)
    {
        Assert.Equal(expectedInstalledVersionNumber, Utils.GetApplicationVersionNumber(applicationType));
    }

    [Theory]
    [InlineData("1.4.2", ApplicationType.Bazarr)]
    [InlineData("1.40.1.8227", ApplicationType.Plex)]
    [InlineData("1.40.2.8312", ApplicationType.PlexPass)]
    [InlineData("1.15.0", ApplicationType.Prowlarr)]
    [InlineData("1.16.0", ApplicationType.Prowlarr, "develop")]
    [InlineData("5.3.6", ApplicationType.Radarr)]
    [InlineData("5.4.5", ApplicationType.Radarr, "develop")]
    [InlineData("4.2.3", ApplicationType.SABnzbd)]
    [InlineData("4.0.3.1413", ApplicationType.Sonarr)]
    public void GetLatestApplicationVersionNumber(string expectedVersionNumber, ApplicationType applicationType, string branchName = "master")
    {
        if (applicationType != ApplicationType.PlexPass || Utils.Config.PlexToken.HasValue())
            Assert.Equal(expectedVersionNumber, Utils.GetLatestApplicationVersionNumber(applicationType, branchName));
    }
}