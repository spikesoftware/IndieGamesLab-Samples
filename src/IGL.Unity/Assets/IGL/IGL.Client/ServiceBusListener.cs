using IGL.Configuration;
using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using Microsoft.Azure.ServiceBus;
using UnityEngine;

namespace IGL.Client
{

    public class ServiceBusListenerThread : IDisposable
    {
        static bool _shouldRun = false;
        static bool _isRunning = false;        

        Thread _thread = null;

        public void StartListening()
        {
            if (string.IsNullOrEmpty(CommonConfiguration.Instance.PlayerId))
                throw new ApplicationException("ServiceBuslistener.StartListening() PlayerId must be set before listening for events.");

            _shouldRun = true;

            _thread = new Thread(new ThreadStart(ListenForMessages));
            _thread.Start();
        }

        public void StopListening()
        {
            _shouldRun = false;
        }
        
        public static event EventHandler<ErrorEventArgs> OnListenError;

        /// <summary>
        /// receive an event targeted to this particular player
        /// </summary>
        /// <param name="post"></param>
        static void ListenForMessages()
        {
            _isRunning = true;
            var listener = new ServiceBusListener();
            
            while (_shouldRun)
            {
                try
                {
                    listener.ListenForMessages();                    
                }
                catch (WebException ex)
                {
                    // if the server has not created a topic yet for the client then a 404 error will be returned so do not report
                    if (!ex.Message.Contains("The remote server returned an error: (404) Not Found"))
                    {
                        if(OnListenError != null)
                            OnListenError.Invoke(null, new ErrorEventArgs(ex));
                    }
                }
                catch (Exception ex)
                {
                    if (OnListenError != null)
                        OnListenError.Invoke(null, new ErrorEventArgs(ex));
                }
            }

            _isRunning = false;
        }        

        public void Dispose()
        {
            // close down the listening
            _shouldRun = false;

            while (_isRunning)
            {
                Thread.Sleep(200);
            }
        }
    }

    /// <summary>
    /// ServiceBusListener will listen for events from the IGL services
    /// </summary>
    public class ServiceBusListener
    {
        IAsyncResult _isRunning;
        internal static SharedAccessSignatureTokenProvider _sasProvider = null;

        public static string Queue = "PlayerEvents";

        static ManualResetEvent allDone = new ManualResetEvent(false);
        const int BUFFER_SIZE = 1024;

        /// <summary>
        /// Event called when a GameEvent has been successfully received.
        /// </summary>
        public static event EventHandler<GamePacketArgs> OnGameEventReceived;
        public static event EventHandler<ErrorEventArgs> OnListenError;

        public void ListenForMessages()
        {
            if (_isRunning != null && !_isRunning.IsCompleted)
                return;

            var address = new Uri(CommonConfiguration.Instance.BackboneConfiguration.GetServiceSubscriptionsAddress(Queue, CommonConfiguration.Instance.PlayerId));
            try
            {
                WebRequest request = WebRequest.Create(address);

                if (_sasProvider == null)
                {
                    _sasProvider = new SharedAccessSignatureTokenProvider(CommonConfiguration.Instance.BackboneConfiguration.IssuerName,
                        CommonConfiguration.Instance.BackboneConfiguration.IssuerSecret,
                        new TimeSpan(1, 0, 0));
                }

                var token = _sasProvider.GetToken(CommonConfiguration.Instance.BackboneConfiguration.GetRealm(), "POST", new TimeSpan(1, 0, 0));
                request.Headers[HttpRequestHeader.Authorization] = token.TokenValue;
                request.Method = "DELETE";
                RequestState rs = new RequestState();
                rs.Request = request;                

                _isRunning = request.BeginGetResponse(new AsyncCallback(RespCallback), rs);
            }
            catch (WebException ex)
            {                
                // if the server has not created a topic yet for the client then a 404 error will be returned so do not report
                if (!ex.Message.Contains("The remote server returned an error: (404) Not Found"))
                    if (OnListenError != null)
                        OnListenError.Invoke(this, new System.IO.ErrorEventArgs(ex));
            }
            catch (Exception ex)
            {             
                if (OnListenError != null)
                    OnListenError.Invoke(this, new System.IO.ErrorEventArgs(ex));
            }
        }

        private static void RespCallback(IAsyncResult ar)
        {
            try
            {
                // Get the RequestState object from the async result.
                RequestState rs = (RequestState)ar.AsyncState;

                // Get the WebRequest from RequestState.
                WebRequest req = rs.Request;

                // Call EndGetResponse, which produces the WebResponse object
                //  that came from the request issued above.
                WebResponse resp = req.EndGetResponse(ar);

                //  Start reading data from the response stream.
                Stream ResponseStream = resp.GetResponseStream();

                // Store the response stream in RequestState to read 
                // the stream asynchronously.
                rs.ResponseStream = ResponseStream;

                //  Pass rs.BufferRead to BeginRead. Read data into rs.BufferRead
                ResponseStream.BeginRead(rs.BufferRead, 0, BUFFER_SIZE, new AsyncCallback(ReadCallBack), rs);                
            }
            catch(Exception ex)
            {
                if (OnListenError != null)
                    OnListenError.Invoke(null, new ErrorEventArgs(ex));
            }
        }

        static void ReadCallBack(IAsyncResult asyncResult)
        {
            try
            {
                // Get the RequestState object from AsyncResult.
                RequestState rs = (RequestState)asyncResult.AsyncState;

                // Retrieve the ResponseStream that was set in RespCallback. 
                Stream responseStream = rs.ResponseStream;

                // Read rs.BufferRead to verify that it contains data. 
                int read = responseStream.EndRead(asyncResult);
                if (read > 0)
                {
                    // Prepare a Char array buffer for converting to Unicode.
                    Char[] charBuffer = new Char[BUFFER_SIZE];

                    // Convert byte stream to Char array and then to String.
                    // len contains the number of characters converted to Unicode.
                    rs.StreamDecode.GetChars(rs.BufferRead, 0, read, charBuffer, 0);

                    // Append the recently read data to the RequestData stringbuilder
                    // object contained in RequestState.
                    rs.RequestData.Append(
                       Encoding.ASCII.GetString(rs.BufferRead, 0, read));

                    // Continue reading data until 
                    // responseStream.EndRead returns –1.
                    responseStream.BeginRead(rs.BufferRead, 0, BUFFER_SIZE, new AsyncCallback(ReadCallBack), rs);
                }
                else
                {
                    if (rs.RequestData.Length > 0)
                    {
                        HandlePacket(rs.RequestData.ToString());
                    }
                    // Close down the response stream.
                    responseStream.Close();
                    // Set the ManualResetEvent so the main thread can exit.
                    allDone.Set();
                }
            }
            catch (Exception ex)
            {
                if (OnListenError != null)
                    OnListenError.Invoke(null, new ErrorEventArgs(ex));
            }

            return;
        }

        static void HandlePacket(string message)
        {
            var packet = DatacontractSerializerHelper.Deserialize<GamePacket>(message);

            if (packet != null)
            {
                if (OnGameEventReceived != null)
                    OnGameEventReceived.Invoke(null, new GamePacketArgs { GamePacket = packet });
            }
        }
    }        
}
