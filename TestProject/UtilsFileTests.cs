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
    [InlineData(@"c:\bobby\subtitles.jp.srt", false)]
    [InlineData(@"c:\bobby\subtitles.jp.hi.srt", false)]
    [InlineData(@"c:\bobby\subtitles.es.srt", true)]
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

    [InlineData(@"File1CamelCase.mp4", @"file1camelcase.mp4")]
    [InlineData(@"Folder1\File1CamelCase.mp4", @"folder1\file1camelcase.mp4")]
    [InlineData(@"Folder1\folder2\File1CamelCase.mp4", @"folder1\Folder2\file1camelcase.mp4")]
    [InlineData(@"\\nas1\assets1\Folder1\folder2\File1CamelCase.mp4", @"\\nas1\assets1\folder1\Folder2\file1camelcase.mp4")]
    [Theory]
    public void FileMoveTests(string fileToCreate, string fileToRenameTo)
    {
        // Check File.Move works if filename only differs by case
        // or dest exists already
        // one of the directories in the path is different case
        var originPath = Path.Combine(Path.GetTempPath(), "sourceFileMoveTests");
        string sourceFile;
        string destinationFile;

        if (fileToCreate.StartsWithIgnoreCase(@"\\"))
        {
            sourceFile = fileToCreate;
            destinationFile = fileToRenameTo;
            originPath = Path.GetPathRoot(fileToCreate);
        }
        else
        {
            if (Utils.Directory.Exists(originPath)) _ = Utils.Directory.Delete(originPath, true);
            sourceFile = Path.Combine(originPath, fileToCreate);
            destinationFile = Path.Combine(originPath, fileToRenameTo);
        }
        Utils.Directory.EnsureForFilePath(sourceFile);
        Utils.Directory.EnsureForFilePath(destinationFile);
        Utils.File.Create(sourceFile);
        _ = Utils.File.Move(sourceFile, destinationFile);
        var destFileInfo = new FileInfo(destinationFile);

        if (destFileInfo.DirectoryName != null)
        {
            if (originPath != null)
            {
                var files = Directory.EnumerateFiles(originPath, "*.*", SearchOption.AllDirectories);
                var enumerable = files as string[] ?? files.ToArray();
                _ = Assert.Single(enumerable);
                var newFileInfo = new FileInfo(enumerable[0]);
                Assert.Equal(destinationFile, newFileInfo.FullName);
            }
        }

        if (fileToCreate.StartsWithIgnoreCase(@"\\"))
        {
            if (Utils.File.Exists(destinationFile)) _ = Utils.File.Delete(destinationFile);
        }
        else
        {
            if (Utils.Directory.Exists(originPath)) _ = Utils.Directory.Delete(originPath, true);
        }
    }
}