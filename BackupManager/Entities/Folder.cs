// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="Folder.cs" company="Andy Reeves">
//
//  </copyright>
//  --------------------------------------------------------------------------------------------------------------------

using System;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace BackupManager.Entities;

/// <summary>
///     This class allows us to keep a Collection of FoldersToScan with the path and datetime it was last changed
/// </summary>
public class Folder : IEquatable<Folder>, IXmlSerializable
{
    private string path;

    public Folder() { }

    public Folder(string path) : this(path, DateTime.Now) { }

    public Folder(string path, DateTime dateTime)
    {
        Path = path;
        ModifiedDateTime = dateTime;
    }

    /// <summary>
    ///     The path to the folder that changed
    /// </summary>

    // ReSharper disable once ConvertToAutoProperty
    public string Path
    {
        get => path;

        private set => path = value;
    }

    /// <summary>
    ///     The Timestamp the folder was last changed
    /// </summary>
    public DateTime ModifiedDateTime { get; set; }

    public bool Equals(Folder other)
    {
        return null != other && Path == other.Path;
    }

    public XmlSchema GetSchema()
    {
        return null;
    }

    public void ReadXml(XmlReader reader)
    {
        var isEmptyElement = reader.IsEmptyElement;
        Path = reader.GetAttribute("Path");
        ModifiedDateTime = XmlConvert.ToDateTime(reader.GetAttribute("ModifiedDateTime") ?? string.Empty, XmlDateTimeSerializationMode.Local);
        reader.ReadStartElement();
        if (!isEmptyElement) reader.ReadEndElement();
    }

    public void WriteXml(XmlWriter writer)
    {
        writer.WriteAttributeString("Path", Path);
        writer.WriteAttributeString("ModifiedDateTime", XmlConvert.ToString(ModifiedDateTime, XmlDateTimeSerializationMode.Local));
    }

    public override bool Equals(object obj)
    {
        return Equals(obj as Folder);
    }

    public override int GetHashCode()
    {
        return Path.GetHashCode();
    }
}