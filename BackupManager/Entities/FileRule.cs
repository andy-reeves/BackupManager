// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="FileRule.cs" company="Andy Reeves">
//
//  </copyright>
//  --------------------------------------------------------------------------------------------------------------------

namespace BackupManager.Entities
{
    using System.Xml.Serialization;

    public class FileRule
    {
        /// <summary>
        ///     We use this to track if the rule has been used for any files at all in a full scan
        /// </summary>
        [XmlIgnore] internal bool Matched;

        /// <summary>
        ///     If the file path matches this then it must match FileTestRegEx
        /// </summary>
        public string FileDiscoveryRegEx { get; set; }

        /// <summary>
        ///     If the file path matches FileDiscoveryRegEx then it must match this
        /// </summary>
        public string FileTestRegEx { get; set; }

        /// <summary>
        ///     The message to display if the rule is not matched
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        ///     The name of the rule
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        ///     The number of the rule. Must be unique
        /// </summary>
        public string Number { get; set; }
    }
}