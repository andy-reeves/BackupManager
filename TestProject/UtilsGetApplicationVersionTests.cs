// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="UtilsGetApplicationVersionTests.cs" company="Andy Reeves">
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
public sealed class UtilsGetApplicationVersionTests
{
    [Theory]
    [InlineData("1.6.2", ApplicationType.Bazarr)]
    [InlineData("1.43.5.11079", ApplicationType.PlexPass)]
    [InlineData("2.7.0.5723", ApplicationType.Prowlarr)]
    [InlineData("7.0.3.10918", ApplicationType.Radarr)]
    [InlineData("5.1.3", ApplicationType.SABnzbd)]
    [InlineData("4.0.20.3014", ApplicationType.Sonarr)]
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
    [InlineData("5.1.3", ApplicationType.SABnzbd)]
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
    [InlineData("3.1.0", ApplicationType.Prowlarr)]
    [InlineData("3.1.0", ApplicationType.Prowlarr, "develop")]
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
    [InlineData("1.43.5.11079", ApplicationType.Plex)]
    [InlineData("1.43.0.10467", ApplicationType.PlexPass)]
    public void GetLatestVersionNumber(string expectedVersionNumber, ApplicationType applicationType, string branchName = "master")
    {
        if (applicationType != ApplicationType.PlexPass || Utils.Config.PlexToken.HasValue())
            Assert.Equal(expectedVersionNumber, Utils.GetLatestApplicationVersionNumber(applicationType, branchName));
    }
}

[SuppressMessage("ReSharper", "IdentifierTypo")]
[SuppressMessage("ReSharper", "MemberCanBeFileLocal")]
public sealed class UtilsGetVersionBazarr
{
    [Theory]
    [InlineData("1.6.2", ApplicationType.Bazarr)]
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
    [InlineData("4.0.20.3014", ApplicationType.Sonarr)]
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
    [InlineData("5.28.0.10274", ApplicationType.Radarr)]
    public void GetLatestVersionNumber(string expectedVersionNumber, ApplicationType applicationType, string branchName = "master")
    {
        Assert.Equal(expectedVersionNumber, Utils.GetLatestApplicationVersionNumber(applicationType, branchName));
    }
}