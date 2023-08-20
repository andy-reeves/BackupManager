// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="Rules.cs" company="Andy Reeves">
//
//  </copyright>
//  --------------------------------------------------------------------------------------------------------------------

namespace BackupManager.Entities
{
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Xml.Serialization;

    public class Rules
    {
        [XmlArrayItem("FileRule")]
        public Collection<FileRule> FileRules;

        public Rules()
        {
        }

        public static Rules Load(string path)
        {
            Rules rules;
            XmlSerializer serializer = new XmlSerializer(typeof(Rules));

            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                rules = serializer.Deserialize(stream) as Rules;
            }

            return rules ?? null;
        }
    }
}
