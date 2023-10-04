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
        [XmlArrayItem("FileRule")] public Collection<FileRule> FileRules { get; set; }

        public static Rules Load(string path)
        {
            try
            {
                XmlSerializer serializer = new(typeof(Rules));

                using FileStream stream = new(path, FileMode.Open, FileAccess.Read);
                return serializer.Deserialize(stream) as Rules;
            }

            catch (InvalidOperationException ex)
            {
                throw new ApplicationException($"Unable to load Rules.xml {ex}");
            }
        }
    }
}