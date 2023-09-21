// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="Rules.cs" company="Andy Reeves">
//
//  </copyright>
//  --------------------------------------------------------------------------------------------------------------------

namespace BackupManager.Entities
{
    using System;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Xml.Serialization;

    public class Rules
    {
        [XmlArrayItem("FileRule")]
        public Collection<FileRule> FileRules { get; set; }

        public Rules()
        {
        }

        public static Rules Load(string path)
        {
            try
            {
                Rules rules;
                XmlSerializer serializer = new(typeof(Rules));

                using (FileStream stream = new(path, FileMode.Open, FileAccess.Read))
                {
                    rules = serializer.Deserialize(stream) as Rules;
                }

                return rules;
            }

            catch (InvalidOperationException ex)
            {
                throw new ApplicationException($"Unable to load Rules.xml {ex}");
            }
        }
    }
}
