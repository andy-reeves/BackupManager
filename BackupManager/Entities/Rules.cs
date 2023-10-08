// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="Rules.cs" company="Andy Reeves">
//
//  </copyright>
//  --------------------------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
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

            var rulesHashSet = new HashSet<int>();

            foreach (var rulesFileRule in rules.FileRules)
            {
                if (rulesHashSet.Contains(Convert.ToInt32(rulesFileRule.Number))) throw new ArgumentException(Resources.Rules_DuplicateRuleNumber, nameof(path));

                rulesHashSet.Add(Convert.ToInt32(rulesFileRule.Number));
            }
            return rules;
        }
        catch (InvalidOperationException ex)
        {
            throw new ApplicationException($"Unable to load Rules.xml {ex}");
        }
    }
}