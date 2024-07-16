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
    [InlineData("1.4.3", ApplicationType.Bazarr)]
    [InlineData("1.40.4.8679", ApplicationType.PlexPass)]
    [InlineData("1.20.1.4603", ApplicationType.Prowlarr)]
    [InlineData("5.7.0.8882", ApplicationType.Radarr)]
    [InlineData("4.3.2", ApplicationType.SABnzbd)]
    [InlineData("4.0.8.1874", ApplicationType.Sonarr)]
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
    [InlineData("4.3.2", ApplicationType.SABnzbd)]
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
    [InlineData("1.20.1", ApplicationType.Prowlarr)]
    [InlineData("1.21.1", ApplicationType.Prowlarr, "develop")]
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
    [InlineData("1.40.4.8679", ApplicationType.Plex)]
    [InlineData("1.40.3.8502", ApplicationType.PlexPass)]
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
    [InlineData("1.4.3", ApplicationType.Bazarr)]
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
    [InlineData("4.0.8.1874", ApplicationType.Sonarr)]
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
    [InlineData("5.7.0.8882", ApplicationType.Radarr)]
    public void GetLatestVersionNumber(string expectedVersionNumber, ApplicationType applicationType, string branchName = "master")
    {
        Assert.Equal(expectedVersionNumber, Utils.GetLatestApplicationVersionNumber(applicationType, branchName));
    }
}