//using System;
//using System.Collections.Generic;
//using System.Configuration;
//using System.Linq;
//using System.Text;

//namespace IGL
//{
//    public class GamePacketConfigurationSection : ConfigurationSection
//    {
//        [ConfigurationProperty("serializationConfiguration")]
//        public SerializationConfigurationElement SerializationConfiguration
//        {
//            get
//            {
//                return (SerializationConfigurationElement)this["serializationConfiguration"];
//            }
//            set
//            { this["serializationConfiguration"] = value; }
//        }

//        [ConfigurationProperty("encryptionConfiguration")]
//        public EncryptionConfigurationElement EncryptionConfiguration
//        {
//            get
//            {
//                return (EncryptionConfigurationElement)this["encryptionConfiguration"];
//            }
//            set
//            { this["encryptionConfiguration"] = value; }
//        }

//        [ConfigurationProperty("backboneConfiguration")]
//        public BackboneConfigurationElement BackboneConfiguration
//        {
//            get
//            {
//                return (BackboneConfigurationElement)this["backboneConfiguration"];
//            }
//            set
//            { this["backboneConfiguration"] = value; }
//        }

//        [ConfigurationProperty("playerId", IsRequired = false)]
//        public string PlayerId
//        {
//            get
//            {
//                return (string)this["playerId"];
//            }
//            set
//            {
//                this["playerId"] = value;
//            }
//        }

//        [ConfigurationProperty("gameId")]
//        public int GameId
//        {
//            get
//            {
//                return (int)this["gameId"];
//            }
//            set
//            {
//                this["gameId"] = value;
//            }
//        }
//    }

//    public class SerializationConfigurationElement : ConfigurationElement
//    {
//        [ConfigurationProperty("isJsonEnabled", DefaultValue = "false", IsRequired = false)]
//        public Boolean IsJsonEnabled
//        {
//            get
//            {
//                return (Boolean)this["isJsonEnabled"];
//            }
//            set
//            {
//                this["isJsonEnabled"] = value;
//            }
//        }       
//    }

//    public class EncryptionConfigurationElement : ConfigurationElement
//    {
//        [ConfigurationProperty("enabled", DefaultValue = "false", IsRequired = false)]
//        public Boolean IsEncryptionEnabled
//        {
//            get
//            {
//                return (Boolean)this["enabled"];
//            }
//            set
//            {
//                this["enabled"] = value;
//            }
//        }

//        [ConfigurationProperty("salt")]
//        public string Salt
//        {
//            get
//            {
//                return this["salt"].ToString();
//            }
//            set
//            { this["salt"] = value; }
//        }
//    }

//    public class BackboneConfigurationElement : ConfigurationElement
//    {
//        [ConfigurationProperty("serviceNamespace")]
//        public string ServiceNamespace
//        {
//            get
//            {
//                return this["serviceNamespace"].ToString();
//            }
//            set
//            { this["serviceNamespace"] = value; }
//        }

//        [ConfigurationProperty("issuerName")]
//        public string IssuerName
//        {
//            get
//            {
//                return this["issuerName"].ToString();
//            }
//            set
//            { this["issuerName"] = value; }
//        }

//        [ConfigurationProperty("issuerSecret")]
//        public string IssuerSecret
//        {
//            get
//            {
//                return this["issuerSecret"].ToString();
//            }
//            set
//            { this["issuerSecret"] = value; }
//        }
//    }
//}
