using IGL.Configuration;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Text;
using Microsoft.Azure.ServiceBus;
using Object = System.Object;

namespace IGL.Client
{
    public class ServiceBusWriter
    {
        internal static SharedAccessSignatureTokenProvider _sasProvider = null;
        internal static readonly string correlation = Guid.NewGuid().ToString().Replace("-", "");        
        static int _packet = 0;
        
        private static readonly object _syncRoot = new Object();

        public static event EventHandler<ErrorEventArgs> OnSubmitError;
        public static event EventHandler<MessageEventArgs> OnSubmitSuccess;

        public static Queue<GamePacket> _packets = new Queue<GamePacket>();

        public static bool _isHandlingRequest;        

        public static void ProcessQueue()
        {
            if(!_isHandlingRequest && _packets.Count > 0)
            {
                lock (_syncRoot)
                {
                    if (_sasProvider == null)
                    {
                        _sasProvider = new SharedAccessSignatureTokenProvider(CommonConfiguration.Instance.BackboneConfiguration.IssuerName,
                                                                              CommonConfiguration.Instance.BackboneConfiguration.IssuerSecret,
                                                                              new TimeSpan(1, 0, 0));
                    }

                    _isHandlingRequest = true;
                    var packet = _packets.Dequeue();

                    var content = Encoding.Default.GetBytes(DatacontractSerializerHelper.Serialize<GamePacket>(packet));

                    using (WebClient webClient = new WebClient())
                    {
                        var token = _sasProvider.GetToken(CommonConfiguration.Instance.BackboneConfiguration.GetRealm(), "POST", new TimeSpan(1, 0, 0));
                        webClient.Headers[HttpRequestHeader.Authorization] = token.TokenValue;

                        // add the properties
                        var collection = new NameValueCollection();
                        collection.Add(GamePacket.VERSION, GamePacket.Namespace);
                        webClient.Headers.Add(collection);

                        webClient.UploadDataCompleted += WebClient_UploadDataCompleted;
                        webClient.UploadDataAsync(new Uri(CommonConfiguration.Instance.BackboneConfiguration.GetServiceMessagesAddress(packet.Queue)), "POST", content);
                    }
                }
            }
        }

        public static bool SubmitGameEvent(string queueName, int eventId, GameEvent gameevent, KeyValuePair<string, string>[] properties = null, string sessionId = null)
        {
            try
            {
                GamePacket packet;

                lock (_syncRoot)
                {
                    // add the properties
                    var collection = new Dictionary<string, string>();

                    if (properties != null)
                    {
                        foreach (var property in properties)
                            collection.Add(property.Key, property.Value);
                    }

                    collection.Add(GamePacket.VERSION, GamePacket.Namespace);

                    packet = new GamePacket
                    {
                        Queue = queueName,
                        GameId = CommonConfiguration.Instance.GameId,
                        PlayerId = CommonConfiguration.Instance.PlayerId,
                        Correlation = correlation,
                        PacketNumber = _packet++,
                        PacketCreatedUTCDate = DateTime.UtcNow,
                        GameEvent = gameevent,
                        EventId = eventId,
                        Properties = collection
                    };

                    _packets.Enqueue(packet);
                }

                ProcessQueue();
            }
            catch(Exception ex)
            {
                if (OnSubmitError != null)
                    OnSubmitError.Invoke(null, new ErrorEventArgs(ex));

                return false;
            }
            return true;
        }

        private static void WebClient_UploadDataCompleted(object sender, UploadDataCompletedEventArgs e)
        {
            try
            {
                string responseString = Encoding.UTF8.GetString(e.Result);

                if(OnSubmitSuccess != null)
                    OnSubmitSuccess.Invoke(sender, new MessageEventArgs { message = responseString });
            }
            catch(Exception ex)
            {
                if (OnSubmitError != null)
                    OnSubmitError.Invoke(sender, new System.IO.ErrorEventArgs(ex));
            }

            _isHandlingRequest = false;

            ProcessQueue();
        }
    }
}
