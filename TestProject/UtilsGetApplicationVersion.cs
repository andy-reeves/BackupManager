// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="UtilsGetApplicationVersion.cs" company="Andy Reeves">
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
public sealed class UtilsGetApplicationVersion
{
    [Theory]
    [InlineData("1.4.2", ApplicationType.Bazarr)]
    [InlineData("1.40.2.8395", ApplicationType.PlexPass)]
    [InlineData("1.16.2.4435", ApplicationType.Prowlarr)]
    [InlineData("5.4.6.8723", ApplicationType.Radarr)]
    [InlineData("4.3.0", ApplicationType.SABnzbd)]
    [InlineData("4.0.4.1491", ApplicationType.Sonarr)]
    public void GetVersionNumber(string expectedInstalledVersionNumber, ApplicationType applicationType)
    {
        Assert.Equal(expectedInstalledVersionNumber, Utils.GetApplicationVersionNumber(applicationType));
    }
}

[SuppressMessage("ReSharper", "IdentifierTypo")]
[SuppressMessage("ReSharper", "MemberCanBeFileLocal")]
[SuppressMessage("ReSharper", "InconsistentNaming")]
public sealed class UtilsGetVersionSABnzbd
{
    [Theory]
    [InlineData("4.3.1", ApplicationType.SABnzbd)]
    public void GetLatestVersionNumber(string expectedVersionNumber, ApplicationType applicationType, string branchName = "master")
    {
        Assert.Equal(expectedVersionNumber, Utils.GetLatestApplicationVersionNumber(applicationType, branchName));
    }
}

[SuppressMessage("ReSharper", "IdentifierTypo")]
[SuppressMessage("ReSharper", "MemberCanBeFileLocal")]
public sealed class UtilsGetVersionProwlarr
{
    [Theory]
    [InlineData("1.16.2", ApplicationType.Prowlarr)]
    [InlineData("1.17.1", ApplicationType.Prowlarr, "develop")]
    public void GetLatestVersionNumber(string expectedVersionNumber, ApplicationType applicationType, string branchName = "master")
    {
        Assert.Equal(expectedVersionNumber, Utils.GetLatestApplicationVersionNumber(applicationType, branchName));
    }
}

[SuppressMessage("ReSharper", "IdentifierTypo")]
[SuppressMessage("ReSharper", "MemberCanBeFileLocal")]
public sealed class UtilsGetVersionPlex
{
    static UtilsGetVersionPlex()
    {
        var mediaBackup = MediaBackup.Load(Path.Combine(Utils.GetProjectPath(typeof(FileRulesUnitTest)), "..\\BackupManager\\MediaBackup.xml"));
        Utils.Config = mediaBackup.Config;
    }

    [Theory]
    [InlineData("1.40.2.8395", ApplicationType.Plex)]
    [InlineData("1.40.2.8395", ApplicationType.PlexPass)]
    public void GetLatestVersionNumber(string expectedVersionNumber, ApplicationType applicationType, string branchName = "master")
    {
        if (applicationType != ApplicationType.PlexPass || Utils.Config.PlexToken.HasValue()) Assert.Equal(expectedVersionNumber, Utils.GetLatestApplicationVersionNumber(applicationType, branchName));
    }
}

[SuppressMessage("ReSharper", "IdentifierTypo")]
[SuppressMessage("ReSharper", "MemberCanBeFileLocal")]
public sealed class UtilsGetVersionBazarr
{
    [Theory]
    [InlineData("1.4.2", ApplicationType.Bazarr)]
    public void GetLatestVersionNumber(string expectedVersionNumber, ApplicationType applicationType, string branchName = "master")
    {
        Assert.Equal(expectedVersionNumber, Utils.GetLatestApplicationVersionNumber(applicationType, branchName));
    }
}

[SuppressMessage("ReSharper", "IdentifierTypo")]
[SuppressMessage("ReSharper", "MemberCanBeFileLocal")]
public sealed class UtilsGetVersionSonarr
{
    [Theory]
    [InlineData("4.0.4.1491", ApplicationType.Sonarr)]
    [InlineData("4.0.4.1572", ApplicationType.Sonarr, "develop")]
    [InlineData("4.0.4.1491", ApplicationType.Sonarr, "nightly")]
    public void GetLatestVersionNumber(string expectedVersionNumber, ApplicationType applicationType, string branchName = "master")
    {
        Assert.Equal(expectedVersionNumber, Utils.GetLatestApplicationVersionNumber(applicationType, branchName));
    }
}

[SuppressMessage("ReSharper", "IdentifierTypo")]
[SuppressMessage("ReSharper", "MemberCanBeFileLocal")]
public sealed class UtilsGetVersionRadarr
{
    [Theory]
    [InlineData("5.4.6.8723", ApplicationType.Radarr)]
    [InlineData("5.5.1.8747", ApplicationType.Radarr, "develop")]
    [InlineData("5.5.2.8766", ApplicationType.Radarr, "nightly")]
    public void GetLatestVersionNumber(string expectedVersionNumber, ApplicationType applicationType, string branchName = "master")
    {
        Assert.Equal(expectedVersionNumber, Utils.GetLatestApplicationVersionNumber(applicationType, branchName));
    }
}