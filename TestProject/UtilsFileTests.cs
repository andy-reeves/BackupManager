// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="UtilsFileTests.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;

using BackupManager;
using BackupManager.Extensions;

namespace TestProject;

[SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "<Pending>")]
[SuppressMessage("ReSharper", "MemberCanBeFileLocal")]
[SuppressMessage("ReSharper", "StringLiteralTypo")]
public sealed class UtilsFileTests
{
    [InlineData(@"c:\bobby\subtitles.srt", true)]
    [InlineData(@"c:\bobby\subtitles.en.hi.srt", true)]
    [InlineData(@"c:\bobby\subtitles.en.cc.srt", true)]
    [InlineData(@"c:\bobby\subtitles.en.sdh.srt", true)]
    [InlineData(@"c:\bobby\subtitles.jp.srt", true)]
    [InlineData(@"c:\bobby\subtitles.jp.hi.srt", true)]
    [InlineData(@"c:\bobby\subtitles.es.srt", true)]
    [InlineData(@"c:\bobby\subt.rgrg.rg.rg.rg.itles.es.srt", true)]
    [InlineData(@"c:\bobby\subt.rgrg.rg.rg.rg.itles.srt", true)]
    [InlineData(@"c:\bobby\subt.rgrg.rg.rg.rg.itles.es.sr", false)]
    [InlineData(@"c:\bobby\subt.rgrg.rg.rg.rg.itles.esp.srt", true)]
    [Theory]
    public void IsSubtitlesTests(string path, bool expectedResult)
    {
        Assert.Equal(expectedResult, Utils.File.IsSubtitles(path));
    }

    [InlineData(@"c:\bobby\file1-other.mkv", true)]
    [InlineData(@"c:\bobby\file2.mkv", false)]
    [InlineData(@"c:\bobby\file3-bobby", false)]
    [InlineData(@"c:\bobby\file4-behindthescenes.mkv", true)]
    [Theory]
    public void IsSpecialFeatureTests(string path, bool expectedResult)
    {
        Assert.Equal(expectedResult, Utils.File.IsSpecialFeature(path));
    }

    [InlineData(@"c:\bobby\file1-other.mkv", true)]
    [InlineData(@"c:\bobby\file2.mkv", true)]
    [InlineData(@"c:\bobby\file3-bobby", false)]
    [InlineData(@"c:\bobby\file4-behindthescenes.mkv", true)]
    [InlineData(@"c:\bobby\file4-behindthescenes.mp4", true)]
    [Theory]
    public void IsVideoTests(string path, bool expectedResult)
    {
        Assert.Equal(expectedResult, Utils.File.IsVideo(path));
    }

    [InlineData(@"File1CamelCase.mp4", @"file1camelcase.mp4", "", false)]
    [InlineData(@"Folder1\File1CamelCase.mp4", @"folder1\file1camelcase.mp4", "", false)]
    [InlineData(@"Folder1\folder2\File1CamelCase.mp4", @"folder1\Folder2\file1camelcase.mp4", "", false)]
    [InlineData(@"\\nas6\assets2\Folder1\folder2\File1CamelCase.mp4", @"\\nas6\assets2\folder1\Folder2\file1camelcase.mp4", @"\\nas6\assets2\folder1", false)]
    [InlineData(@"CaseSensitivePathRename\Roux Down The River {tvdb-445790}\TestFileToRename.txt",
        @"CaseSensitivePathRename\Roux Down the River {tvdb-445790}\TestFileToRename.txt", @"CaseSensitivePathRename", true)]
    [Theory]
    public void FileMoveTests(string fileToCreate, string fileToRenameTo, string originFolder, bool useTestDataDirectory)
    {
        // Check File.Move works if filename only differs by case
        // or dest exists already
        // one of the directories in the path is different case
        string sourceFile;
        string destinationFile;
        string originPath;

        if (useTestDataDirectory)
        {
            var testDataPath = Path.Combine(Utils.GetProjectPath(typeof(UtilsFileTests)), "TestData");
            fileToCreate = Path.Combine(testDataPath, fileToCreate);
            fileToRenameTo = Path.Combine(testDataPath, fileToRenameTo);
            originFolder = Path.Combine(testDataPath, originFolder);
        }

        if (fileToCreate.StartsWithIgnoreCase(@"\\") || Path.IsPathRooted(fileToCreate))
        {
            sourceFile = fileToCreate;
            destinationFile = fileToRenameTo;
            originPath = originFolder;
        }
        else
        {
            originPath = Path.Combine(Path.GetTempPath(), "sourceFileMoveTests");
            Utils.Directory.EnsurePath(originPath);
            originPath = Utils.File.GetWindowsPhysicalPath(originPath);
            if (Utils.Directory.Exists(originPath)) _ = Utils.Directory.Delete(originPath, true);
            sourceFile = Path.Combine(originPath, fileToCreate);
            destinationFile = Path.Combine(originPath, fileToRenameTo);
        }
        if (Utils.Directory.Exists(originPath)) _ = Utils.Directory.Delete(originPath, true);
        Utils.File.Create(sourceFile);
        sourceFile = Utils.File.GetWindowsPhysicalPath(sourceFile);
        Assert.True(Utils.File.Move(sourceFile, destinationFile));
        Assert.Equal(destinationFile, Utils.File.GetWindowsPhysicalPath(destinationFile));
        if (Utils.Directory.Exists(originPath)) _ = Utils.Directory.Delete(originPath, true);
    }
}