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

        //var pathString = reader.GetAttribute("Path");
        // if (!string.IsNullOrEmpty(pathString)) Path = pathString;
        // var modifiedDateTimeString = reader.GetAttribute("ModifiedDateTime");
        // if (!string.IsNullOrEmpty(modifiedDateTimeString)) ModifiedDateTime = DateTime.Parse(modifiedDateTimeString);
        /*reader.MoveToContent();

        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.Element && reader.Name == "Path")
            {
                reader.Read();
                if (reader.NodeType == XmlNodeType.Text) path = reader.Value;
            }

            if (reader.NodeType == XmlNodeType.Element && reader.Name == "ModifiedDateTime")
            {
                reader.Read();
                if (reader.NodeType == XmlNodeType.Text) ModifiedDateTime = DateTime.Parse(reader.Value);
            }
        }*/
    }

    public void WriteXml(XmlWriter writer)
    {
        writer.WriteAttributeString("Path", Path);
        writer.WriteAttributeString("ModifiedDateTime", XmlConvert.ToString(ModifiedDateTime, XmlDateTimeSerializationMode.Local));

        //writer.WriteElementString("Path", Path);
        //writer.WriteElementString("ModifiedDateTime", ModifiedDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ"));
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