namespace BackupManager.TestProject
{
    using BackupManager.Entities;

    public class FileRulesUnitTest
    {
        static MediaBackup mediaBackup;

        static FileRulesUnitTest()
        {
            string localMediaXml = "..\\BackupManager\\MediaBackup.xml";
            mediaBackup = MediaBackup.Load(Path.Combine(Utils.GetProjectPath(typeof(FileRulesUnitTest)), localMediaXml));
        }

        [Fact]
        public void FileRuleTests()
        {
            string testsFromFile = File.ReadAllText(Path.Combine(Utils.GetProjectPath(typeof(FileRulesUnitTest)), "FileRuleTests.txt"));

            string[] lines = testsFromFile.Split('\n');

            foreach (string line in lines)
            {
                string[] cols = line.Split('|');

                for (int i = 0; i < cols.Length; i++)
                {
                    cols[i] = cols[i].TrimStart(new char[] { ' ', '"' }).TrimEnd(new char[] { ' ', '"', '\r', '\n' });
                }

                if (!cols[0].StartsWith("#") && cols.Length == 5)
                {
                    string testNumber = cols[0];
                    string ruleNumber = cols[1];
                    string testOrDiscovery = cols[2];
                    bool expectedResult = Convert.ToBoolean(cols[3]);
                    string testPath = cols[4];

                    FileRule? rule = mediaBackup.FileRules.SingleOrDefault(p => p.Number == ruleNumber);
                    Assert.NotNull(rule);

                    string regEx = testOrDiscovery.StartsWith("T") ? rule.FileRuleRegEx : rule.FileToMatchRegEx;

                    if (expectedResult)
                    {
                        Assert.True(testPath.IsMatch(regEx), $"Test {testNumber} of Rule {ruleNumber} {rule.Message} for {testPath}");
                    }
                    else
                    {
                        Assert.False(testPath.IsMatch(regEx), $"Test {testNumber} of Rule {ruleNumber} {rule.Message} for {testPath}");
                    }
                }
            }
        }
    }
}