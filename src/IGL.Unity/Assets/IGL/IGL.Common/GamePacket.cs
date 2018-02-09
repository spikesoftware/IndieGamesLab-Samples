using IGL.Configuration;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;

namespace IGL
{
    [DataContract(Namespace = "uri:igl:v1")]
    public class GamePacket
    {
        public string Queue { get; set; }
        /// <summary>
        /// The IndieGamesLab Game ID
        /// </summary>
        [DataMember]
        public int GameId { get; set; }

        [DataMember]
        public string Correlation { get; set; }

        [DataMember]
        public string PlayerId { get; set; }

        [DataMember]
        public long PacketNumber { get; set; }

        [DataMember]
        public int EventId { get; set; }

        [DataMember]
        public DateTime PacketCreatedUTCDate { get; set; }        

        [DataMember]
        public string Content { get; set; }

        public const string VERSION = "IGL.V1.Version";

        private static string _namespace;
        public static string Namespace
        {
            get
            {
                if (_namespace == null)
                {
                    try
                    {
                        // determine the namespace from the datacontract
                        var attribute = typeof(GamePacket).GetCustomAttributes(typeof(DataContractAttribute), false).FirstOrDefault();

                        if (attribute != null)
                        {
                            var contract = attribute as DataContractAttribute;
                            _namespace = contract.Namespace;
                        }
                    }
                    catch (Exception ex)
                    {
                        Trace.TraceError("IGL.GamePacket Failed to determine the namespace.  Setting the namespace to v0 so this can be investigated server side. Note:" + ex.GetFullMessage());
                        _namespace = "uri:igl:v0";
                    }
                }
                return _namespace;
            }
        }

        [DataMember]
        public Dictionary<string, string> Properties { get; set; }

        public GameEvent GameEvent
        {
            get
            {
                if (string.IsNullOrEmpty(Content))
                    return null;
                else
                {
                    if (CommonConfiguration.Instance.SerializationConfiguration.IsJsonEnabled)
                    {
                        if (CommonConfiguration.Instance.EncryptionConfiguration.IsEncryptionEnabled)
                        {
                            return JsonSerializerHelper.Deserialize<GameEvent>(DecryptStringAES(Content));
                        }
                        else
                        {
                            return JsonSerializerHelper.Deserialize<GameEvent>(Content);
                        }
                    }
                    else
                    {
                        if (CommonConfiguration.Instance.EncryptionConfiguration.IsEncryptionEnabled)
                        {
                            return GameEventSerializer.Deserialize(DecryptStringAES(Content));
                        }
                        else
                        {
                            return GameEventSerializer.Deserialize(Content);
                        }
                    }
                    
                }

            }
            set
            {
                if (CommonConfiguration.Instance.SerializationConfiguration.IsJsonEnabled)
                {
                    if (CommonConfiguration.Instance.EncryptionConfiguration.IsEncryptionEnabled)
                    {
                        Content = EncryptStringAES(JsonSerializerHelper.Serialize(value));
                    }
                    else
                    {
                        Content = JsonSerializerHelper.Serialize(value);
                    }
                }
                else
                {
                    if (CommonConfiguration.Instance.EncryptionConfiguration.IsEncryptionEnabled)
                    {
                        Content = EncryptStringAES(GameEventSerializer.Serialize(value));
                    }
                    else
                    {
                        Content = GameEventSerializer.Serialize(value);
                    }
                }
            }
        }


        #region Encryption Related
        /************************************
         * http://stackoverflow.com/questions/202011/encrypt-and-decrypt-a-string
         ************************************/

        public static byte[] SALT = Encoding.ASCII.GetBytes(CommonConfiguration.Instance.EncryptionConfiguration.Salt);

        /// <summary>
        /// Encrypt the given string using AES.  The string can be decrypted using 
        /// DecryptStringAES().  The sharedSecret parameters must match.
        /// </summary>
        /// <param name="plainText">The text to encrypt.</param>
        /// <param name="sharedSecret">A password used to generate a key for encryption.</param>
        public static string EncryptStringAES(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
                throw new ArgumentNullException("plainText");

            string outStr = null;                       // Encrypted string to return
            RijndaelManaged aesAlg = null;              // RijndaelManaged object used to encrypt the data.

            try
            {
                // generate the key from the shared secret and the salt
                Rfc2898DeriveBytes key = new Rfc2898DeriveBytes(Namespace, SALT);

                // Create a RijndaelManaged object
                aesAlg = new RijndaelManaged();
                aesAlg.Key = key.GetBytes(aesAlg.KeySize / 8);

                // Create a decryptor to perform the stream transform.
                ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);

                // Create the streams used for encryption.
                using (MemoryStream msEncrypt = new MemoryStream())
                {
                    // prepend the IV
                    msEncrypt.Write(BitConverter.GetBytes(aesAlg.IV.Length), 0, sizeof(int));
                    msEncrypt.Write(aesAlg.IV, 0, aesAlg.IV.Length);
                    using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                    {
                        using (StreamWriter swEncrypt = new StreamWriter(csEncrypt))
                        {
                            //Write all data to the stream.
                            swEncrypt.Write(plainText);
                        }
                    }
                    outStr = Convert.ToBase64String(msEncrypt.ToArray());
                }
            }
            finally
            {
                // Clear the RijndaelManaged object.
                if (aesAlg != null)
                    aesAlg.Clear();
            }

            // Return the encrypted bytes from the memory stream.
            return outStr;
        }

        /// <summary>
        /// Decrypt the given string.  Assumes the string was encrypted using 
        /// EncryptStringAES(), using an identical sharedSecret.
        /// </summary>
        /// <param name="cipherText">The text to decrypt.</param>
        /// <param name="sharedSecret">A password used to generate a key for decryption.</param>
        public static string DecryptStringAES(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText))
                throw new ArgumentNullException("cipherText");
            
            // Declare the RijndaelManaged object
            // used to decrypt the data.
            RijndaelManaged aesAlg = null;

            // Declare the string used to hold
            // the decrypted text.
            string plaintext = null;

            try
            {
                // generate the key from the shared secret and the salt
                Rfc2898DeriveBytes key = new Rfc2898DeriveBytes(Namespace, SALT);

                // Create the streams used for decryption.                
                byte[] bytes = Convert.FromBase64String(cipherText);
                using (MemoryStream msDecrypt = new MemoryStream(bytes))
                {
                    // Create a RijndaelManaged object
                    // with the specified key and IV.
                    aesAlg = new RijndaelManaged();
                    aesAlg.Key = key.GetBytes(aesAlg.KeySize / 8);
                    // Get the initialization vector from the encrypted stream
                    aesAlg.IV = ReadByteArray(msDecrypt);
                    // Create a decrytor to perform the stream transform.
                    ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);
                    using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                    {
                        using (StreamReader srDecrypt = new StreamReader(csDecrypt))

                            // Read the decrypted bytes from the decrypting stream
                            // and place them in a string.
                            plaintext = srDecrypt.ReadToEnd();
                    }
                }
            }
            finally
            {
                // Clear the RijndaelManaged object.
                if (aesAlg != null)
                    aesAlg.Clear();
            }

            return plaintext;
        }

        private static byte[] ReadByteArray(Stream s)
        {
            byte[] rawLength = new byte[sizeof(int)];
            if (s.Read(rawLength, 0, rawLength.Length) != rawLength.Length)
            {
                throw new SystemException("Stream did not contain properly formatted byte array");
            }

            byte[] buffer = new byte[BitConverter.ToInt32(rawLength, 0)];
            if (s.Read(buffer, 0, buffer.Length) != buffer.Length)
            {
                throw new SystemException("Did not read byte array properly");
            }

            return buffer;
        }
        #endregion
    }
}
