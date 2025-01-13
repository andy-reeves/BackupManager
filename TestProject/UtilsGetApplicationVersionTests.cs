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
    [InlineData("1.5.1", ApplicationType.Bazarr)]
    [InlineData("1.41.3.9314", ApplicationType.PlexPass)]
    [InlineData("1.29.2.4915", ApplicationType.Prowlarr)]
    [InlineData("5.17.2.9580", ApplicationType.Radarr)]
    [InlineData("4.4.1", ApplicationType.SABnzbd)]
    [InlineData("4.0.12.2823", ApplicationType.Sonarr)]
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
    [InlineData("4.4.1", ApplicationType.SABnzbd)]
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
    [InlineData("1.29.2", ApplicationType.Prowlarr)]
    [InlineData("1.30.1", ApplicationType.Prowlarr, "develop")]
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
    [InlineData("1.41.3.9314", ApplicationType.Plex)]
    [InlineData("1.41.2.9134", ApplicationType.PlexPass)]
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
    [InlineData("1.5.1", ApplicationType.Bazarr)]
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
    [InlineData("4.0.12.2823", ApplicationType.Sonarr)]
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
    [InlineData("5.17.2.9580", ApplicationType.Radarr)]
    public void GetLatestVersionNumber(string expectedVersionNumber, ApplicationType applicationType, string branchName = "master")
    {
        Assert.Equal(expectedVersionNumber, Utils.GetLatestApplicationVersionNumber(applicationType, branchName));
    }
}