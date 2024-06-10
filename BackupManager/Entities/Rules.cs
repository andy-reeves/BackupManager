// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="Rules.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Xml.Schema;
using System.Xml.Serialization;

using BackupManager.Properties;

namespace BackupManager.Entities;

[SuppressMessage("ReSharper", "MemberCanBeInternal")]
[SuppressMessage("ReSharper", "AutoPropertyCanBeMadeGetOnly.Global")]
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
[SuppressMessage("ReSharper", "UnusedMember.Global")]
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
[SuppressMessage("ReSharper", "CollectionNeverUpdated.Global")]
[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
public sealed class Rules
{
    [XmlArrayItem("FileRule")] public Collection<FileRule> FileRules { get; set; }

    public static Rules Load(string path)
    {
        {
            try
            {
                Utils.ValidateXmlFromResources(path, "BackupManager.RulesSchema.xsd");
                var xRoot = new XmlRootAttribute { ElementName = "Rules", Namespace = "RulesSchema.xsd", IsNullable = true };
                XmlSerializer serializer = new(typeof(Rules), xRoot);
                using FileStream stream = new(path, FileMode.Open, FileAccess.Read);
                if (serializer.Deserialize(stream) is not Rules rules) return null;

                if (rules.FileRules.Select(static x => x.Number).Distinct().Count() != rules.FileRules.Count) throw new ArgumentException(Resources.DuplicateRuleNumber, nameof(path));

                return rules;
            }
            catch (InvalidOperationException ex)
            {
                throw new ApplicationException(string.Format(Resources.UnableToLoadXml, $"{path}", ex));
            }
            catch (XmlSchemaValidationException ex)
            {
                throw new ApplicationException(string.Format(Resources.UnableToLoadXml, $"{path} failed validation", ex));
            }
        }
    }
}