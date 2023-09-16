// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="SerializableDictionary.cs" company="Andy Reeves">
//
//  </copyright>
//  --------------------------------------------------------------------------------------------------------------------

namespace BackupManager.Entities
{
    using System.Collections.Generic;
    using System.Xml;
    using System.Xml.Schema;
    using System.Xml.Serialization;

    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    [XmlRoot("dictionary")]
    public class SerializableDictionary<TKey, TValue> : Dictionary<TKey, TValue>, IXmlSerializable
    {
        // XmlSerializer.Deserialize() will create a new Object, and then call ReadXml()
        // So cannot use instance field, use class field.

        public static string itemTag = "item";
        public static string keyTag = "key";
        public static string valueTag = "value";

        public XmlSchema GetSchema()
        {
            return null;
        }

        public void ReadXml(XmlReader reader)
        {
            if (reader.IsEmptyElement)
            {
                return;
            }

            XmlSerializer keySerializer = new XmlSerializer(typeof(TKey));
            XmlSerializer valueSerializer = new XmlSerializer(typeof(TValue));

            reader.ReadStartElement();

            // IsStartElement() will call MoveToContent()
            // reader.MoveToContent();
            while (reader.IsStartElement(itemTag))
            {
                reader.ReadStartElement(itemTag);

                reader.ReadStartElement(keyTag);
                TKey key = (TKey)keySerializer.Deserialize(reader);
                reader.ReadEndElement();

                reader.ReadStartElement(valueTag);
                TValue value = (TValue)valueSerializer.Deserialize(reader);
                reader.ReadEndElement();

                reader.ReadEndElement();
                Add(key, value);

                // IsStartElement() will call MoveToContent()
                // reader.MoveToContent();
            }
            reader.ReadEndElement();
        }

        public void WriteXml(XmlWriter writer)
        {
            XmlSerializer keySerializer = new XmlSerializer(typeof(TKey));
            XmlSerializer valueSerializer = new XmlSerializer(typeof(TValue));

            foreach (KeyValuePair<TKey, TValue> kvp in this)
            {
                writer.WriteStartElement(itemTag);

                writer.WriteStartElement(keyTag);
                keySerializer.Serialize(writer, kvp.Key);
                writer.WriteEndElement();

                writer.WriteStartElement(valueTag);
                valueSerializer.Serialize(writer, kvp.Value);
                writer.WriteEndElement();

                writer.WriteEndElement();
            }
        }
    }
}