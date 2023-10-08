// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="Rules.cs" company="Andy Reeves">
//
//  </copyright>
//  --------------------------------------------------------------------------------------------------------------------

using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

using BackupManager.Properties;

namespace BackupManager.Entities;

public class Rules
{
    [XmlArrayItem("FileRule")] public Collection<FileRule> FileRules { get; set; }

    public static Rules Load(string path)
    {
        try
        {
            XmlSerializer serializer = new(typeof(Rules));

            using FileStream stream = new(path, FileMode.Open, FileAccess.Read);
            if (serializer.Deserialize(stream) is not Rules rules) return null;

            var rule = rules.FileRules.SingleOrDefault(p => p.Number == "1");
            return rule == null ? throw new ArgumentException(Resources.Rules_Load_Missing_file_rules, nameof(path)) : rules;
        }

        catch (InvalidOperationException ex)
        {
            throw new ApplicationException($"Unable to load Rules.xml {ex}");
        }
    }
}