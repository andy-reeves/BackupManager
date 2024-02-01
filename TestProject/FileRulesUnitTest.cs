// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="FileRulesUnitTest.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;
using System.Xml;

using BackupManager;
using BackupManager.Entities;
using BackupManager.Extensions;

namespace TestProject;

[SuppressMessage("ReSharper", "MemberCanBeFileLocal")]
[SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "<Pending>")]
public sealed class FileRulesUnitTest
{
    private static readonly MediaBackup _mediaBackup;

    static FileRulesUnitTest()
    {
        _mediaBackup = MediaBackup.Load(Path.Combine(Utils.GetProjectPath(typeof(FileRulesUnitTest)),
            "..\\BackupManager\\MediaBackup.xml"));
        Utils.Config = _mediaBackup.Config;
    }

    [Fact]
    public void FileRuleMainTests()
    {
        var testsFromFile = File.ReadAllText(Path.Combine(Utils.GetProjectPath(typeof(FileRulesUnitTest)), "FileRuleTests.txt"));
        var lines = testsFromFile.Split('\n');

        foreach (var cols in lines.Select(static line => line.Split('|')))
        {
            for (var i = 0; i < cols.Length; i++)
            {
                cols[i] = cols[i].TrimStart(' ', '"').TrimEnd(' ', '"', '\r', '\n');
            }
            if (cols[0].StartsWith("#", StringComparison.InvariantCultureIgnoreCase) || cols.Length != 4) continue;

            var ruleNumberTestNumber = cols[0];
            var a = ruleNumberTestNumber.Split(".");
            Assert.Equal(2, a.Length);
            var ruleNumber = Convert.ToInt32(a[0]);
            var testNumber = a[1];
            var testOrDiscovery = cols[1];
            var expectedResult = Convert.ToBoolean(cols[2]);
            var testPath = cols[3];
            var rule = _mediaBackup.Config.FileRules.SingleOrDefault(p => p.Number == ruleNumber);
            Assert.NotNull(rule);

            var regEx = testOrDiscovery.StartsWith("T", StringComparison.InvariantCulture)
                ? rule.FileTestRegEx
                : rule.FileDiscoveryRegEx;

            if (expectedResult)
                Assert.True(testPath.IsMatch(regEx), $"Test {testNumber} of Rule {ruleNumber} {rule.Message} for {testPath}");
            else
                Assert.False(testPath.IsMatch(regEx), $"Test {testNumber} of Rule {ruleNumber} {rule.Message} for {testPath}");
        }
    }

    [Fact]
    public void FileRuleTests3()
    {
        var rule1 = _mediaBackup.Config.FileRules.SingleOrDefault(static p => p.Number == 1);
        var rule2 = _mediaBackup.Config.FileRules.SingleOrDefault(static p => p.Number == 2);
        Assert.NotNull(rule1);
        Assert.NotNull(rule2);
        Assert.NotEqual(rule1, rule2);
        Assert.False(rule1.Equals(null));
        object obj = rule1;
        Assert.False(obj.Equals(rule2));
        Assert.StartsWith("Rule 1 TV files must contain {t", rule1.ToString());
        Assert.NotEqual(0, rule1.GetHashCode());
    }

    [Fact]
    public void FileRuleTests2()
    {
        var path1 = Path.Combine(Path.GetTempPath(), "FileMove");
        if (Directory.Exists(path1)) Directory.Delete(path1, true);
        var file1 = Path.Combine(path1, "test1.txt");
        Utils.EnsureDirectoriesForDirectoryPath(path1);
        Utils.CreateFile(file1);
        _ = Assert.Throws<ArgumentNullException>(static () => Rules.Load(null));
        _ = Assert.Throws<XmlException>(() => Rules.Load(file1));

        // Delete the folders we created
        if (Directory.Exists(path1)) Directory.Delete(path1, true);
    }
}