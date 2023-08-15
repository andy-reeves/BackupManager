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
            mediaBackup = MediaBackup.Load(Path.Combine(Utils.GetProjectPath(typeof(FileRulesUnitTest)), localMediaXml));
        }

        /// <summary>
        /// Path must contain tvdb or tmdb
        /// </summary>
        [Fact]
        public void Rule01Tests()
        {
            var rule = mediaBackup.FileRules.Where(p => p.Name == "Rule 1").SingleOrDefault();
            Assert.True(rule != null, "Rule is missing");

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
            var rule = mediaBackup.FileRules.Where(p => p.Name == "Rule 2").SingleOrDefault();
            Assert.True(rule != null, "Rule is missing");

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
            var rule = mediaBackup.FileRules.Where(p => p.Name == "Rule 3").SingleOrDefault();
            Assert.True(rule != null, "Rule is missing");

            string filePath = "Z:\\_TV\\Tom and Jerry {tvdb-72860}\\Season 1940\\Tom and Jerry s1940e01 Puss Gets The Boot [SDTV].mkv";
            Assert.True(filePath.IsMatch(rule.FileToMatchRegEx), rule.Message);

            filePath = "X:\\_TV\\Cheers {tvdb-77623}\\Season 11\\Cheers s11e26-e28 One for the Road [SDTV].avi";
            Assert.True(filePath.IsMatch(rule.FileRuleRegEx), rule.Message);

            filePath = "X:\\_TV\\Cheers {tvdb-77623}\\Season 11\\Cheers s11e26e27 One for the Road [SDTV].avi";
            Assert.True(filePath.IsMatch(rule.FileRuleRegEx), rule.Message);

            filePath = "X:\\_TV\\Cheers {tvdb-77623}\\Season 11\\Cheers s11.e26-e28 One for the Road [SDTV].avi";
            Assert.False(filePath.IsMatch(rule.FileRuleRegEx), rule.Message);

            filePath = "X:\\_TV\\Cheers {tvdb-77623}\\Season 11\\Cheers s11e26f27 One for the Road [SDTV].avi";
            Assert.False(filePath.IsMatch(rule.FileRuleRegEx), rule.Message);

            filePath = "X:\\_TV\\The Late Late Show with James Corden {tvdb-292421}\\Season 1\\The Late Late Show with James Corden 2015-03-23 [HDTV-720p].mkv";
            Assert.True(filePath.IsMatch(rule.FileRuleRegEx), rule.Message);

            filePath = "X:\\_Movies (non-tmdb)\\Blood Brothers (1989)\\Example 1-behindthescenes.mkv";
            Assert.True(filePath.IsMatch(rule.FileRuleRegEx), rule.Message);

            filePath = "X:\\_TV\\The Late Late Show with James Corden {tvdb-292421}\\Season 1\\The Late Late Show with James Corden 20150323 [HDTV-720p].mkv";
            Assert.False(filePath.IsMatch(rule.FileRuleRegEx), rule.Message);

            filePath = "Z:\\_TV\\Tom and Jerry {tvdb-72860}\\Season 1940\\Tom and Jerry s1940e01 Puss Gets The Boot [SDTV].mkv";
            Assert.True(filePath.IsMatch(rule.FileRuleRegEx), rule.Message);

            filePath = "X:\\_TV\\Cheers {tvdb-77623}\\Season 11\\Long Way Round s01e08.mkv";
            Assert.True(filePath.IsMatch(rule.FileRuleRegEx), "Test 10 - " + rule.Message);

            filePath = "X:\\_TV\\Cheers {tvdb-77623}\\Season 11\\Long Way Round s01e108e109.mkv";
            Assert.True(filePath.IsMatch(rule.FileRuleRegEx), "Test 11 - " + rule.Message);

            filePath = "X:\\_TV\\Cheers {tvdb-77623}\\Season 11\\Long Way Round s01e08-e12.mkv";
            Assert.True(filePath.IsMatch(rule.FileRuleRegEx), "Test 12 - " + rule.Message);

            filePath = "\\Z:\\_TV\\The Queen's Christmas Broadcast {tvdb-359422}\\Season 1\\The Queen's Christmas Broadcast s01e2015 2015 [HDTV-720p].mp4";
            Assert.True(filePath.IsMatch(rule.FileRuleRegEx), "Test 13 - " + rule.Message);
        }

        /// <summary>
        /// TV file name matches the folder name
        /// </summary>
        [Fact]
        public void Rule04Tests()
        {
            var rule = mediaBackup.FileRules.Where(p => p.Name == "Rule 4").SingleOrDefault();
            Assert.True(rule != null, "Rule is missing");

            string filePath = "Z:\\_TV\\Tom and Jerry {tvdb-72860}\\Season 1940\\Tom and Jerry s1940e01 Puss Gets The Boot [SDTV].mkv";
            Assert.True(filePath.IsMatch(rule.FileToMatchRegEx), rule.Message);

            filePath = "X:\\_TV\\Chernobyl {tvdb-360893}\\Season 1\\Chernobyl s01e01 12345 [Bluray-2160p Remux].mkv";
            Assert.True(filePath.IsMatch(rule.FileRuleRegEx), rule.Message);

            filePath = "X:\\_Movies (non-tmdb)\\Blood Brothers (1989)\\Example 1-featurette.mkv";
            Assert.True(filePath.IsMatch(rule.FileRuleRegEx), rule.Message);

            filePath = "X\\_TV (non-tvdb)\\Das Boot (1985) {tmdb-156249}\\Season 1\\Das Boot (1985) s01e01 [Bluray-1080p Remux].mkv";
            Assert.True(filePath.IsMatch(rule.FileRuleRegEx), rule.Message);

            filePath = "X:\\_TV\\Chernobyl {tvdb-360893}\\Chernobyl s01e01 12345 [Bluray-2160p Remux].mkv";
            Assert.False(filePath.IsMatch(rule.FileRuleRegEx), rule.Message);

            filePath = "Z:\\_TV\\Tom and Jerry {tvdb-72860}\\Season 1940\\Tom and Jerry s1940e01 Puss Gets The Boot [SDTV].mkv";
            Assert.True(filePath.IsMatch(rule.FileRuleRegEx), rule.Message);
        }

        /// <summary>
        /// Movies, Comedy and Concerts files must be a special feature or have {tmdb- in the file name
        /// </summary>
        [Fact]
        public void Rule05Tests()
        {
            var rule = mediaBackup.FileRules.Where(p => p.Name == "Rule 5").SingleOrDefault();
            Assert.True(rule != null, "Rule is missing");

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
            var rule = mediaBackup.FileRules.Where(p => p.Name == "Rule 6").SingleOrDefault();
            Assert.True(rule != null, "Rule is missing");

            string filePath = "X:\\_Movies (non-tmdb)\\Blood Brothers (1989)\\Blood Brothers (1989).mkv";
            Assert.True(filePath.IsMatch(rule.FileRuleRegEx), rule.Message);

            filePath = "X:\\_Movies (non-tmdb)\\Blood Brothers (1989)\\Example 1-featurette.mkv";
            Assert.True(filePath.IsMatch(rule.FileRuleRegEx), rule.Message);
            filePath = "X:\\_Movies (non-tmdb)\\Blood Brothers (1989)\\Example 1-other.mkv";
            Assert.True(filePath.IsMatch(rule.FileRuleRegEx), rule.Message);
            filePath = "X:\\_Movies (non-tmdb)\\Blood Brothers (1989)\\Example 1-interview.mkv";
            Assert.True(filePath.IsMatch(rule.FileRuleRegEx), rule.Message);
            filePath = "X:\\_Movies (non-tmdb)\\Blood Brothers (1989)\\Example 1-scene.mkv";
            Assert.True(filePath.IsMatch(rule.FileRuleRegEx), rule.Message);
            filePath = "X:\\_Movies (non-tmdb)\\Blood Brothers (1989)\\Example 1-short.mkv";
            Assert.True(filePath.IsMatch(rule.FileRuleRegEx), rule.Message);
            filePath = "X:\\_Movies (non-tmdb)\\Blood Brothers (1989)\\Example 1-deleted.mkv";
            Assert.True(filePath.IsMatch(rule.FileRuleRegEx), rule.Message);
            filePath = "X:\\_Movies (non-tmdb)\\Blood Brothers (1989)\\Example 1-behindthescenes.mkv";
            Assert.True(filePath.IsMatch(rule.FileRuleRegEx), rule.Message);
            filePath = "X:\\_Movies (non-tmdb)\\Blood Brothers (1989)\\Example 1-trailer.mkv";
            Assert.True(filePath.IsMatch(rule.FileRuleRegEx), rule.Message);

            filePath = "X:\\_Movies (non-tmdb)\\The Lord of the Rings (2003)\\The Lord of the Rings (2003) {tmdb-120} {edition-THE COMPLETE EXTENDED} [Remux-2160p][DV HDR10][TrueHD Atmos 7.1][h265].mkv";
            Assert.True(filePath.IsMatch(rule.FileRuleRegEx), rule.Message);

            filePath = "X:\\_Movies\\12 Angry Men (1957)\\12 Angry Men {edition-BLURAY} [Remux-1080p][DTS-HD MA 1.0][h264].mkv";
            Assert.False(filePath.IsMatch(rule.FileRuleRegEx), rule.Message);

            filePath = "X:\\_Movies (non-tmdb)\\Blood Brothers (1989)\\Example 1-behindscenes.mkv";
            Assert.False(filePath.IsMatch(rule.FileRuleRegEx), rule.Message);
            filePath = "X:\\_Movies (non-tmdb)\\Blood Brothers (1989)\\Example 1-trailertest.mkv";
            Assert.False(filePath.IsMatch(rule.FileRuleRegEx), rule.Message);

        }

        /// <summary>
        /// Edition rule
        /// </summary>
        [Fact]
        public void Rule07Tests()
        {
            var rule = mediaBackup.FileRules.Where(p => p.Name == "Rule 7").SingleOrDefault();
            Assert.True(rule != null, "Rule is missing");

            string filePath = "X:\\_Movies\\12 Angry Men (1957)\\12 Angry Men {edition-BLURAY} [Remux-1080p][DTS-HD MA 1.0][h264].mkv";
            Assert.True(filePath.IsMatch(rule.FileToMatchRegEx), rule.Message);

            filePath = "X:\\_Movies\\12 Angry Men (1957)\\12 Angry Men [Remux-1080p][DTS-HD MA 1.0][h264].mkv";
            Assert.False(filePath.IsMatch(rule.FileToMatchRegEx), rule.Message);

            filePath = "X:\\_Movies\\12 Angry Men (1957)\\12 Angry Men {edition-BLURAY} [Remux-1080p][DTS-HD MA 1.0][h264].mkv";
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
            var rule = mediaBackup.FileRules.Where(p => p.Name == "Rule 8").SingleOrDefault();
            Assert.True(rule != null, "Rule is missing");

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
            var rule = mediaBackup.FileRules.Where(p => p.Name == "Rule 9").SingleOrDefault();
            Assert.True(rule != null, "Rule is missing");

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
            var rule = mediaBackup.FileRules.Where(p => p.Name == "Rule 10").SingleOrDefault();
            Assert.True(rule != null, "Rule is missing");

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
            var rule = mediaBackup.FileRules.Where(p => p.Name == "Rule 11").SingleOrDefault();
            Assert.True(rule != null, "Rule is missing");

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
            var rule = mediaBackup.FileRules.Where(p => p.Name == "Rule 12").SingleOrDefault();
            Assert.True(rule != null, "Rule is missing");

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
            var rule = mediaBackup.FileRules.Where(p => p.Name == "Rule 13").SingleOrDefault();
            Assert.True(rule != null, "Rule is missing");

            string filePath = "X:\\_Concerts\\12 Angry Men (1957)\\12 Angry Men {edition-BLURAY} [Remux-1080p][DTS-HD MA 1.0][h264].mkv";
            Assert.True(filePath.IsMatch(rule.FileRuleRegEx), rule.Message);

            filePath = "X:\\_Movies\\12 Angry Men (0)\\12 Angry Men [Remux-1080p].bob";
            Assert.False(filePath.IsMatch(rule.FileRuleRegEx), rule.Message);
        }

        /// <summary>
        /// TV extra features must be in the root of the show folder
        /// </summary>
        [Fact]
        public void Rule14Tests()
        {
            var rule = mediaBackup.FileRules.Where(p => p.Name == "Rule 14").SingleOrDefault();
            Assert.True(rule != null, "Rule is missing");

            string filePath = "X:\\_TV\\Chernobyl {tdb-360893}\\Season 1\\Chernobyl s01e01 12345 [Bluray-2160p Remux]-other.mkv";
            Assert.True(filePath.IsMatch(rule.FileToMatchRegEx), rule.Message);

            filePath = "X:\\_TV\\Chernobyl {tdb-360893}\\Chernobyl s01e01 12345 [Bluray-2160p Remux]-other.mkv";
            Assert.True(filePath.IsMatch(rule.FileToMatchRegEx), rule.Message);

            filePath = "X:\\_TV\\Chernobyl {tdb-360893}\\Season 1\\Chernobyl s01e01 12345 [Bluray-2160p Remux]-bobby.mkv";
            Assert.True(filePath.IsMatch(rule.FileToMatchRegEx), rule.Message);

            filePath = "X:\\_TV\\Chernobyl {tdb-360893}\\Chernobyl s01e01 12345 [Bluray-2160p Remux]-bobby.mkv";
            Assert.True(filePath.IsMatch(rule.FileToMatchRegEx), rule.Message);

            filePath = "K:\\_TV\\American Horror Story {tvdb-250487}\\Season 1\\American Horror Story s01e01 Pilot [HDTV-720p].mkv";
            Assert.False(filePath.IsMatch(rule.FileToMatchRegEx), rule.Message);

            filePath = "Z:\\_TV\\MasterChef Australia {tvdb-92091}\\Season 7\\MasterChef Australia s07e34 Off-site Challenge Tokyo Tina vs. Saigon Sally [SDTV].mp4";
            Assert.False(filePath.IsMatch(rule.FileToMatchRegEx), rule.Message);

            filePath = "Z:\\_TV\\MasterChef Australia {tvdb-92091}\\Season 7\\MasterChef Australia s07e34 Off-site Challenge Tokyo Tina vs. Saigon Sally [SDTV]-other.mp4";
            Assert.True(filePath.IsMatch(rule.FileToMatchRegEx), rule.Message);

            filePath = "K:\\_TV\\Westworld {tvdb-296762}\\Season 2\\Westworld s02e50 - An Evocative Location.mkv";
            Assert.False(filePath.IsMatch(rule.FileToMatchRegEx), rule.Message);


            filePath = "X:\\_TV\\Game of Thrones {tvdb-121361}\\Reunion Special-featurette.mkv";
            Assert.True(filePath.IsMatch(rule.FileRuleRegEx), rule.Message);

            filePath = "X:\\_TV\\Game of Thrones {tvdb-121361}\\Season 1\\Reunion Special-featurette.mkv";
            Assert.False(filePath.IsMatch(rule.FileRuleRegEx), rule.Message);

            filePath = "Z:\\_TV\\MasterChef Australia {tvdb-92091}\\Season 7\\MasterChef Australia s07e34 Off-site Challenge Tokyo Tina vs. Saigon Sally [SDTV].mp4";
            Assert.False(filePath.IsMatch(rule.FileRuleRegEx), rule.Message);

            filePath = "Z:\\_TV\\MasterChef Australia {tvdb-92091}\\Season 7\\MasterChef Australia s07e34 Off-site Challenge Tokyo Tina vs. Saigon Sally [SDTV]-other.mp4";
            Assert.False(filePath.IsMatch(rule.FileRuleRegEx), rule.Message);
        }

        /// <summary>
        /// Movies, Comedy, and Concerts files special features must be in the root folder of the movie with a correct suffix
        /// </summary>
        [Fact]
        public void Rule15Tests()
        {
            var rule = mediaBackup.FileRules.Where(p => p.Name == "Rule 15").SingleOrDefault();
            Assert.True(rule != null, "Rule is missing");

            string filePath = "X:\\_Movies\\12 Angry Men (1957)\\12 Angry Men {edition-BLURAY} [Remux-1080p][DTS-HD MA 1.0][h264]-other.mkv";
            Assert.True(filePath.IsMatch(rule.FileToMatchRegEx), rule.Message);

            filePath = "X:\\_Concerts\\12 Angry Men (1957)\\12 Angry Men {edition-BLURAY} [Remux-1080p][DTS-HD MA 1.0][h264]-bobby.mkv";
            Assert.True(filePath.IsMatch(rule.FileToMatchRegEx), rule.Message);

            filePath = "X:\\_Movies\\12 Angry Men (1957)\\Other\\12 Angry Men {edition-BLURAY} [Remux-1080p][DTS-HD MA 1.0][h264]-other.mkv";
            Assert.True(filePath.IsMatch(rule.FileToMatchRegEx), rule.Message);

            filePath = "X:\\_Movies\\12 Angry Men (1957)\\Other\\12 Angry Men {edition-BLURAY} [Remux-1080p][DTS-HD MA 1.0][h264]-bobby.mkv";
            Assert.True(filePath.IsMatch(rule.FileToMatchRegEx), rule.Message);

            filePath = "X:\\_Movies\\12 Angry Men (1957)\\12 Angry Men {edition-BLURAY} [Remux-1080p][DTS-HD MA 1.0][h264]-featurette.mkv";
            Assert.True(filePath.IsMatch(rule.FileRuleRegEx), rule.Message);

            filePath = "X:\\_Movies (non-tmdb)\\The Lord of the Rings (2003)\\The Lord of the Rings (2003) {tmdb-120} {edition-THE COMPLETE EXTENDED} [Remux-2160p][DV HDR10][TrueHD Atmos 7.1][h265]-other.mkv";
            Assert.True(filePath.IsMatch(rule.FileRuleRegEx), rule.Message);

            filePath = "X:\\_Movies\\12 Angry Men (1957)\\12 Angry Men {edition-BLURAY} [Remux-1080p][DTS-HD MA 1.0][h264]-feature.mkv";
            Assert.False(filePath.IsMatch(rule.FileRuleRegEx), rule.Message);

            filePath = "X:\\_Movies\\12 Angry Men (1957)\\Special Features\\12 Angry Men Making Of-featurette.mkv";
            Assert.False(filePath.IsMatch(rule.FileRuleRegEx), rule.Message);

            filePath = "X:\\_Movies (non-tmdb)\\The Lord of the Rings (2003)\\Other\\The Lord of the Rings (2003) {tmdb-120} {edition-THE COMPLETE EXTENDED} [Remux-2160p][DV HDR10][TrueHD Atmos 7.1][h265]-featurette.mkv";
            Assert.False(filePath.IsMatch(rule.FileRuleRegEx), rule.Message);

            filePath = "X:\\_Movies\\12 Angry Men (1957)\\12 Angry Men {edition-BLURAY} [Remux-1080p][DTS-HD MA 1.0][h264]-other.mkv";
            Assert.True(filePath.IsMatch(rule.FileRuleRegEx), rule.Message);

            filePath = "Q:\\_Comedy\\Rhod Gilbert and the Award-Winning Mince Pie (2009)\\The Audience-short.mkv";
            Assert.True(filePath.IsMatch(rule.FileRuleRegEx), rule.Message);
        }
    }
}