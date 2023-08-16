using System.Xml.Serialization;

namespace BackupManager.Entities
{
    public class FileRule
    {
        /// <summary>
        /// If the file path matches this then it must match FileRuleRegEx
        /// </summary>
        public string FileToMatchRegEx;

        /// <summary>
        /// If the file path matches FileToMatchRegEx then it must match this
        /// </summary>
        public string FileRuleRegEx;

        /// <summary>
        /// The message to display if the rule is not matched
        /// </summary>
        public string Message;

        /// <summary>
        /// The name of the rule
        /// </summary>
        public string Name;

        /// <summary>
        /// The number of the rule. Must be unique
        /// </summary>
        public string Number;

        [XmlIgnore]
        public bool Matched;
    }
}
