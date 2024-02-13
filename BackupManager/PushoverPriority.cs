// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="PushoverPriority.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System.Xml.Serialization;

namespace BackupManager;

public enum PushoverPriority
{
    [XmlEnum(Name = "Lowest")] Lowest = -2,

    [XmlEnum(Name = "Low")] Low = -1,

    [XmlEnum(Name = "Normal")] Normal = 0,

    [XmlEnum(Name = "High")] High = 1,

    [XmlEnum(Name = "Emergency")] Emergency = 2
}