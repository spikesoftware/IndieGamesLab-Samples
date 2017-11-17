using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Remoting.Metadata.W3cXsd2001;
using System.Runtime.Serialization;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using UnityEngine;

namespace IGL
{
    public static class XmlSerializerHelper
    {
        public static string Serialize<T>(T obj)
        {
            var outStream = new StringWriter();
            var ser = new XmlSerializer(typeof(T));
            ser.Serialize(outStream, obj);
            return outStream.ToString();
        }

        public static T Deserialize<T>(string serialized)
        {
            var inStream = new StringReader(serialized);
            var ser = new XmlSerializer(typeof(T));
            return (T)ser.Deserialize(inStream);
        }

    }

    public static class DatacontractSerializerHelper
    {
        public static string Serialize<T>(T item, List<Type> knownTypes = null)
        {
            if (item != null)
            {
                using (MemoryStream memoryStream = new MemoryStream())
                {
                    // List<Type> knownTypes = new List<Type> { typeof(List<string>), typeof(NameValueCollection) };
                    DataContractSerializer serializer = new DataContractSerializer(typeof(T), knownTypes);
                    serializer.WriteObject(memoryStream, item);
                    return Encoding.UTF8.GetString(memoryStream.ToArray());
                }
            }
            return null;
        }

        public static T Deserialize<T>(string item)
        {
            if (!string.IsNullOrEmpty(item))
            {
                XmlDictionaryReader xmlDictionaryReader = null;
                try
                {
                    xmlDictionaryReader = XmlDictionaryReader.CreateTextReader(Encoding.UTF8.GetBytes(item), XmlDictionaryReaderQuotas.Max);
                    DataContractSerializer serializer = new DataContractSerializer(typeof(T));
                    return (T)serializer.ReadObject(xmlDictionaryReader, false);
                }
                finally
                {
                    if (xmlDictionaryReader != null)
                    {
                        xmlDictionaryReader.Close();
                    }
                }
            }
            return default(T);
        }

        public static T Deserialize<T>(byte[] bytes)
        {
            if (bytes.Length > 0)
            {
                using (MemoryStream stream = new MemoryStream(bytes))
                {
                    DataContractSerializer serializer = new DataContractSerializer(typeof(T));

                    return (T)serializer.ReadObject(stream);
                }
            }
            return default(T);
        }

    }

    public static class JsonSerializerHelper
    {
        public static string Serialize(object item)
        {
            if (item != null)
            {                
                return JsonUtility.ToJson(item);                
            }
            return null;
        }

        public static T Deserialize<T>(string item)
        {
            if (!string.IsNullOrEmpty(item))
            {
                JsonUtility.FromJson<T>(item);
            }
            return default(T);
        }

    }

    public static class GameEventSerializer
    {
        public static string Serialize(GameEvent item)
        {
            byte[] bytes;

            using (MemoryStream stream = new MemoryStream())
            {
                BinaryWriter writer = new BinaryWriter(stream);

                writer.Write(item.Properties.Count);

                foreach(var property in item.Properties)
                {
                    writer.Write(property.Key);
                    writer.Write(property.Value);
                }

                bytes = stream.ToArray();
            }

            // convert to string
            return new SoapHexBinary(bytes).ToString();
        }
        public static GameEvent Deserialize(string content)
        {
            var bytes = SoapHexBinary.Parse(content).Value;

            var gameEvent = new GameEvent { Properties = new Dictionary<string, string>() };

            using (BinaryReader reader = new BinaryReader(new MemoryStream(bytes)))
            {
                var numProperties = reader.ReadInt32();

                for(int index=0; index < numProperties; index++)
                {
                    gameEvent.Properties.Add(reader.ReadString(), reader.ReadString());
                }                   
            }

            return gameEvent;
        }
    }
}
