// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="FileRulesUnitTest.cs" company="Andy Reeves">
//
//  </copyright>
//  --------------------------------------------------------------------------------------------------------------------

#if DEBUG

using BackupManager;
using BackupManager.Entities;
using BackupManager.Extensions;

namespace TestProject;

public class FileRulesUnitTest
{
    private static readonly MediaBackup MediaBackup;

    static FileRulesUnitTest()
    {
        MediaBackup = MediaBackup.Load(Path.Combine(Utils.GetProjectPath(typeof(FileRulesUnitTest)), "..\\BackupManager\\MediaBackup.xml"));
    }

    [Fact]
    public void FileRuleTests()
    {
        var testsFromFile = File.ReadAllText(Path.Combine(Utils.GetProjectPath(typeof(FileRulesUnitTest)), "FileRuleTests.txt"));

        var lines = testsFromFile.Split('\n');

        foreach (var line in lines)
        {
            var cols = line.Split('|');

            for (var i = 0; i < cols.Length; i++)
            {
                cols[i] = cols[i].TrimStart(' ', '"').TrimEnd(' ', '"', '\r', '\n');
            }

            if (cols[0].StartsWith("#") || cols.Length != 4) continue;

            var ruleNumberTestNumber = cols[0];

            var a = ruleNumberTestNumber.Split(".");

            Assert.True(a.Length == 2);

            var ruleNumber = a[0];
            var testNumber = a[1];

            var testOrDiscovery = cols[1];
            var expectedResult = Convert.ToBoolean(cols[2]);
            var testPath = cols[3];

            var rule = MediaBackup.Config.FileRules.SingleOrDefault(p => p.Number == ruleNumber);
            Assert.NotNull(rule);

            var regEx = testOrDiscovery.StartsWith("T") ? rule.FileTestRegEx : rule.FileDiscoveryRegEx;

            if (expectedResult)
                Assert.True(testPath.IsMatch(regEx), $"Test {testNumber} of Rule {ruleNumber} {rule.Message} for {testPath}");
            else
                Assert.False(testPath.IsMatch(regEx), $"Test {testNumber} of Rule {ruleNumber} {rule.Message} for {testPath}");
        }
    }
}
#endif