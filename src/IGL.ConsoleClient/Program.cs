using IGL;
using IGL.Client;
using IGL.Configuration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;

namespace IGL.ConsoleClient
{
    class Program
    {
        static Stopwatch _stopwath = new Stopwatch();
        static int _sent = 0;
        static int _received = 0;

        static void Main(string[] args)
        {
            // increase from the default 2 to 4 to handle both listener and writer (2 way communication)
            ServicePointManager.DefaultConnectionLimit = 400;

            Console.WriteLine("Starting the Echo test...");

            // set the following once - this is using ACS            
            CommonConfiguration.Instance.BackboneConfiguration.IssuerName = "IGLGuestClient";
            CommonConfiguration.Instance.BackboneConfiguration.IssuerSecret = "2PenhRgdmlf6F1oNglk9Wra1FRH31pcOwbB3q4X0vDs=";
            CommonConfiguration.Instance.BackboneConfiguration.ServiceNamespace = "indiegameslab";

            // optional       
            CommonConfiguration.Instance.GameId = 1;
            CommonConfiguration.Instance.PlayerId = "TestingTesting";

            ServiceBusListener.OnGameEventReceived += ServiceBusListener_OnGameEventReceived;
            ServiceBusListener.OnListenError += ServiceBusListener_OnListenError;

            Console.WriteLine("Retrieving Token....");
            while (ServiceBusWriter.Token == null)
            {
                Thread.Sleep(300);
            }

            Console.WriteLine("Token received.");
            using (var sbl = new Client.ServiceBusListenerThread())
            {
                sbl.StartListening();

                _stopwath.Start();
                while (_stopwath.ElapsedMilliseconds < 10000)
                {
                    // wait until the token has been received
                    if (ServiceBusWriter.Token != null)
                    {
                        try
                        {
                            var gameevent = new GameEvent
                            {
                                Properties = new Dictionary<string, string>()
                           {
                                { "PlayerName", "Joe Smith" },
                                { "StartMilliseconds", _stopwath.ElapsedMilliseconds.ToString() }
                            }
                            };

                            bool wasSuccessful = IGL.Client.ServiceBusWriter.SubmitGameEvent("Echo", 100, gameevent);

                            if (wasSuccessful)
                                _sent++;

                            Console.WriteLine(string.Format("Sent message without error: {0}", wasSuccessful));
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(string.Format("----------------------------------------"));
                            Console.WriteLine(string.Format("Echo test error {0}!!!!!!!", ex.GetFullMessage()));
                            Console.WriteLine(string.Format("----------------------------------------"));
                        }
                    }

                    Thread.Sleep(300);
                }
                Console.WriteLine(string.Format("{0} requests sent.", _sent));                
                Thread.Sleep(60000);
                Console.WriteLine("Listener stopping...");
                sbl.StopListening();
                Thread.Sleep(5000);
            }

            Console.WriteLine(string.Format("{0} messages received.", _received));
            Console.WriteLine("Hit return to end");
            Console.ReadLine();
        }

        static void ServiceBusListener_OnListenError(object sender, System.IO.ErrorEventArgs e)
        {
            Console.WriteLine(string.Format("----------------------------------------"));
            Console.WriteLine(string.Format("ServiceBusListener_OnListenError {0}!!!!!!!", e.GetException().GetFullMessage()));
            Console.WriteLine(string.Format("----------------------------------------"));
        }

        static void ServiceBusListener_OnGameEventReceived(object sender, GamePacketArgs e)
        {
            _received++;

            var elapsed = _stopwath.ElapsedMilliseconds - int.Parse(e.GamePacket.GameEvent.Properties["StartMilliseconds"]);
            Console.WriteLine(string.Format("Echo GameEvent received in \t{0} milliseconds.", elapsed)); 
        }       
    }
}
