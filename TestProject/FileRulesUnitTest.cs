namespace BackupManager.TestProject
{
    using BackupManager.Entities;
    using System.Reflection;

    public class FileRulesUnitTest
    {
        static MediaBackup mediaBackup;

        static FileRulesUnitTest()
        {
            string localMediaXml = "..\\BackupManager\\MediaBackup.xml";
            mediaBackup = MediaBackup.Load(Path.Combine(Utils.GetProjectPath(typeof(FileRulesUnitTest)),localMediaXml));
        }

        /// <summary>
        /// Path must contain tvdb or tmdb
        /// </summary>
        [Fact]
        public void Rule01Tests()
        {
            var rule = mediaBackup.FileRules.Where(p => p.Name == "Rule 1").Single();
            string filePath = "X:\\_TV\\Chernobyl {tvdb-360893}\\Season 1\\Chernobyl s01e01 12345 [Bluray-2160p Remux].mkv";
            Assert.True(filePath.IsMatch(rule.FileRuleRegEx), rule.Message);

            filePath = "X:\\_TV\\Chernobyl {tdb-360893}\\Season 1\\Chernobyl s01e01 12345 [Bluray-2160p Remux].mkv";
            Assert.False(filePath.IsMatch(rule.FileRuleRegEx), rule.Message);
        }

        /// <summary>
        /// Path cannot contain TBA
        /// </summary>
        [Fact]
        public void Rule02Tests()
        {
            var rule = mediaBackup.FileRules.Where(p => p.Name == "Rule 2").Single();
            string filePath = "X:\\_TV\\Chernobyl {tvdb-360893}\\Season 1\\Chernobyl s01e01 12345 [Bluray-2160p Remux].mkv";
            Assert.True(filePath.IsMatch(rule.FileRuleRegEx), rule.Message);

            filePath = "X:\\_TV\\Chernobyl {tvdb-360893}\\Season 1\\Chernobyl s01e01 TBA [Bluray-2160p Remux].mkv";
            Assert.False(filePath.IsMatch(rule.FileRuleRegEx), rule.Message);
        }

        /// <summary>
        /// TV files must either be a special feature or have s00e00 or yyyy-mm-dd in the file name
        /// </summary>
        [Fact]
        public void Rule03Tests()
        {
            var rule = mediaBackup.FileRules.Where(p => p.Name == "Rule 3").Single();
            string filePath = "X:\\_TV\\Cheers {tvdb-77623}\\Season 11\\Cheers s11e26-e28 One for the Road [SDTV].avi";
            Assert.True(filePath.IsMatch(rule.FileRuleRegEx), rule.Message);

            filePath = "X:\\_TV\\Cheers {tvdb-77623}\\Season 11\\Cheers s11.e26-e28 One for the Road [SDTV].avi";
            Assert.False(filePath.IsMatch(rule.FileRuleRegEx), rule.Message);

            filePath = "X:\\_TV\\The Late Late Show with James Corden {tvdb-292421}\\Season 1.The Late Late Show with James Corden 2015-03-23 [HDTV-720p].mkv";
            Assert.True(filePath.IsMatch(rule.FileRuleRegEx), rule.Message);

            filePath = "X:\\_Movies (non-tmdb)\\Blood Brothers (1989)\\Example 1-behindthescenes.mkv";
            Assert.True(filePath.IsMatch(rule.FileRuleRegEx), rule.Message);

            filePath = "X:\\_TV\\The Late Late Show with James Corden {tvdb-292421}\\Season 1.The Late Late Show with James Corden 20150323 [HDTV-720p].mkv";
            Assert.False(filePath.IsMatch(rule.FileRuleRegEx), rule.Message);
        }

        /// <summary>
        /// TV file name matches the folder name
        /// </summary>
        [Fact]
        public void Rule04Tests()
        {
            var rule = mediaBackup.FileRules.Where(p => p.Name == "Rule 4").Single();
            string filePath = "X:\\_TV\\Chernobyl {tvdb-360893}\\Season 1\\Chernobyl s01e01 12345 [Bluray-2160p Remux].mkv";
            Assert.True(filePath.IsMatch(rule.FileRuleRegEx), rule.Message);

            filePath = "X:\\_Movies (non-tmdb)\\Blood Brothers (1989)\\Example 1-featurette.mkv";
            Assert.True(filePath.IsMatch(rule.FileRuleRegEx), rule.Message);

            filePath = "X\\_TV (non-tvdb)\\Das Boot (1985) {tmdb-156249}\\Season 1\\Das Boot (1985) s01e01 [Bluray-1080p Remux].mkv";
            Assert.True(filePath.IsMatch(rule.FileRuleRegEx), rule.Message);

            filePath = "X:\\_TV\\Chernobyl {tvdb-360893}\\Chernobyl s01e01 12345 [Bluray-2160p Remux].mkv";
            Assert.False(filePath.IsMatch(rule.FileRuleRegEx), rule.Message);
        }

        /// <summary>
        /// Movies, Comedy and Concerts files must be a special feature or have {tmdb- in the file name
        /// </summary>
        [Fact]
        public void Rule05Tests()
        {
            var rule = mediaBackup.FileRules.Where(p => p.Name == "Rule 5").Single();
            string filePath = "X:\\_Movies\\12 Angry Men (1957)\\12 Angry Men (1957) {tmdb-389} {edition-BLURAY} [Remux-1080p][DTS-HD MA 1.0][h264].mkv";
            Assert.True(filePath.IsMatch(rule.FileRuleRegEx), rule.Message);
            
            filePath = "X:\\_Movies (non-tmdb)\\Blood Brothers (1989)\\Example 1-scene.mkv";
            Assert.True(filePath.IsMatch(rule.FileRuleRegEx), rule.Message);

            filePath = "X:\\_Movies\\12 Angry Men (1957)\\12 Angry Men (1957) {edition-BLURAY} [Remux-1080p][DTS-HD MA 1.0][h264].mkv";
            Assert.False(filePath.IsMatch(rule.FileRuleRegEx), rule.Message);
        }

        /// <summary>
        /// Movie name matches the folder name
        /// </summary>
        [Fact]
        public void Rule06Tests()
        {
            var rule = mediaBackup.FileRules.Where(p => p.Name == "Rule 6").Single();
            string filePath = "X:\\_Movies (non-tmdb)\\Blood Brothers (1989)\\Blood Brothers (1989).mkv";
            Assert.True(filePath.IsMatch(rule.FileRuleRegEx), rule.Message);

            filePath = "X:\\_Movies (non-tmdb)\\Blood Brothers (1989)\\Example 1-other.mkv";
            Assert.True(filePath.IsMatch(rule.FileRuleRegEx), rule.Message);

            filePath = "X:\\_Movies (non-tmdb)\\The Lord of the Rings (2003)\\The Lord of the Rings (2003) {tmdb-120} {edition-THE COMPLETE EXTENDED} [Remux-2160p][DV HDR10][TrueHD Atmos 7.1][h265].mkv";
            Assert.True(filePath.IsMatch(rule.FileRuleRegEx), rule.Message);

            filePath = "X:\\_Movies\\12 Angry Men (1957)\\12 Angry Men {edition-BLURAY} [Remux-1080p][DTS-HD MA 1.0][h264].mkv";
            Assert.False(filePath.IsMatch(rule.FileRuleRegEx), rule.Message);
        }

        /// <summary>
        /// Edition rule
        /// </summary>
        [Fact]
        public void Rule07Tests()
        {
            var rule = mediaBackup.FileRules.Where(p => p.Name == "Rule 7").Single();

            string filePath = "X:\\_Movies\\12 Angry Men (1957)\\12 Angry Men {edition-BLURAY} [Remux-1080p][DTS-HD MA 1.0][h264].mkv";
            Assert.True(filePath.IsMatch(rule.FileRuleRegEx), rule.Message);

            filePath = "X:\\_Movies\\12 Angry Men (1957)\\12 Angry Men {edition-MYFAVOURITE} [Remux-1080p][DTS-HD MA 1.0][h264].mkv";
            Assert.False(filePath.IsMatch(rule.FileRuleRegEx), rule.Message);
        }

        /// <summary>
        /// No subtitles
        /// </summary>
        [Fact]
        public void Rule08Tests()
        {
            var rule = mediaBackup.FileRules.Where(p => p.Name == "Rule 8").Single();

            string filePath = "X:\\_Concerts\\12 Angry Men (1957)\\12 Angry Men {edition-BLURAY} [Remux-1080p][DTS-HD MA 1.0][h264].mkv";
            Assert.True(filePath.IsMatch(rule.FileRuleRegEx), rule.Message);

            filePath = "X:\\_Movies\\12 Angry Men (1957)\\12 Angry Men.subtitles.srt";
            Assert.False(filePath.IsMatch(rule.FileRuleRegEx), rule.Message);
        }

        /// <summary>
        /// No parenthesis
        /// </summary>
        [Fact]
        public void Rule09Tests()
        {
            var rule = mediaBackup.FileRules.Where(p => p.Name == "Rule 9").Single();

            string filePath = "X:\\_Concerts\\12 Angry Men (1957)\\12 Angry Men {edition-BLURAY} [Remux-1080p][DTS-HD MA 1.0][h264].mkv";
            Assert.True(filePath.IsMatch(rule.FileRuleRegEx), rule.Message);

            filePath = "X:\\_Movies\\12 Angry Men ()\\12 Angry Men.mkv";
            Assert.False(filePath.IsMatch(rule.FileRuleRegEx), rule.Message);
        }

        /// <summary>
        /// No (0)
        /// </summary>
        [Fact]
        public void Rule10Tests()
        {
            var rule = mediaBackup.FileRules.Where(p => p.Name == "Rule 10").Single();

            string filePath = "X:\\_Concerts\\12 Angry Men (1957)\\12 Angry Men {edition-BLURAY} [Remux-1080p][DTS-HD MA 1.0][h264].mkv";
            Assert.True(filePath.IsMatch(rule.FileRuleRegEx), rule.Message);

            filePath = "X:\\_Movies\\12 Angry Men (0)\\12 Angry Men.mkv";
            Assert.False(filePath.IsMatch(rule.FileRuleRegEx), rule.Message);
        }

        /// <summary>
        /// No Proper]
        /// </summary>
        [Fact]
        public void Rule11Tests()
        {
            var rule = mediaBackup.FileRules.Where(p => p.Name == "Rule 11").Single();

            string filePath = "X:\\_Concerts\\12 Angry Men (1957)\\12 Angry Men {edition-BLURAY} [Remux-1080p][DTS-HD MA 1.0][h264].mkv";
            Assert.True(filePath.IsMatch(rule.FileRuleRegEx), rule.Message);

            filePath = "X:\\_Movies\\12 Angry Men (0)\\12 Angry Men [Remux-1080p Proper].mkv";
            Assert.False(filePath.IsMatch(rule.FileRuleRegEx), rule.Message);
        }

        /// <summary>
        /// No REAL]
        /// </summary>
        [Fact]
        public void Rule12Tests()
        {
            var rule = mediaBackup.FileRules.Where(p => p.Name == "Rule 12").Single();

            string filePath = "X:\\_Concerts\\12 Angry Men (1957)\\12 Angry Men {edition-BLURAY} [Remux-1080p][DTS-HD MA 1.0][h264].mkv";
            Assert.True(filePath.IsMatch(rule.FileRuleRegEx), rule.Message);

            filePath = "X:\\_Movies\\12 Angry Men (0)\\12 Angry Men [Remux-1080p Proper REAL].mkv";
            Assert.False(filePath.IsMatch(rule.FileRuleRegEx), rule.Message);
        }

        /// <summary>
        /// Valid file extension
        /// </summary>
        [Fact]
        public void Rule13Tests()
        {
            var rule = mediaBackup.FileRules.Where(p => p.Name == "Rule 13").Single();

            string filePath = "X:\\_Concerts\\12 Angry Men (1957)\\12 Angry Men {edition-BLURAY} [Remux-1080p][DTS-HD MA 1.0][h264].mkv";
            Assert.True(filePath.IsMatch(rule.FileRuleRegEx), rule.Message);

            filePath = "X:\\_Movies\\12 Angry Men (0)\\12 Angry Men [Remux-1080p].bob";
            Assert.False(filePath.IsMatch(rule.FileRuleRegEx), rule.Message);
        }
    }
}