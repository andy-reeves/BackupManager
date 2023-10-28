// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="FileRulesUnitTest.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

#if DEBUG
using System.Diagnostics.CodeAnalysis;

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
        _mediaBackup = MediaBackup.Load(Path.Combine(Utils.GetProjectPath(typeof(FileRulesUnitTest)), "..\\BackupManager\\MediaBackup.xml"));
    }

    [Fact]
    public void FileRuleTests()
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
            Assert.True(a.Length == 2);
            var ruleNumber = Convert.ToInt32(a[0]);
            var testNumber = a[1];
            var testOrDiscovery = cols[1];
            var expectedResult = Convert.ToBoolean(cols[2]);
            var testPath = cols[3];
            var rule = _mediaBackup.Config.FileRules.SingleOrDefault(p => p.Number == ruleNumber);
            Assert.NotNull(rule);
            var regEx = testOrDiscovery.StartsWith("T", StringComparison.InvariantCulture) ? rule.FileTestRegEx : rule.FileDiscoveryRegEx;

            if (expectedResult)
                Assert.True(testPath.IsMatch(regEx), $"Test {testNumber} of Rule {ruleNumber} {rule.Message} for {testPath}");
            else
                Assert.False(testPath.IsMatch(regEx), $"Test {testNumber} of Rule {ruleNumber} {rule.Message} for {testPath}");
        }
    }
}
#endif