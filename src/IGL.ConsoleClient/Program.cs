using IGL;
using IGL.Client;
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

        static void Main(string[] args)
        {
            // increase from the default 2 to 4 to handle both listener and writer (2 way communication)
            ServicePointManager.DefaultConnectionLimit = 400;

            Console.WriteLine("Starting the Echo test...");

            // set the following once - this is using ACS            
            IGL.Client.Configuration.IssuerName = "[IssuerName]";
            IGL.Client.Configuration.IssuerSecret = "[IssuerSecret]";
            IGL.Client.Configuration.ServiceNamespace = "[ServiceNamespace]";                              

            // optional       
            IGL.Client.Configuration.GameId = 1;
            IGL.Client.Configuration.PlayerId = "TestingTesting";

            ServiceBusListener.OnGameEventReceived += ServiceBusListener_OnGameEventReceived;
            ServiceBusListener.OnListenError += ServiceBusListener_OnListenError;            

            _stopwath.Start();

            using (var sbl = new Client.ServiceBusListener())
            {                
                sbl.StartListening();

                while (!Console.KeyAvailable && _stopwath.ElapsedMilliseconds < 10000)
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

                        IGL.Client.ServiceBusWriter.SubmitGameEvent("Echo", 100, gameevent);                        
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(string.Format("----------------------------------------"));
                        Console.WriteLine(string.Format("Echo test error {0}!!!!!!!", ex.GetFullMessage()));
                        Console.WriteLine(string.Format("----------------------------------------"));
                    }
                }
                
                Thread.Sleep(10000);

                Console.Write("Hit return to end client...");
                Console.ReadLine();

                sbl.StopListening();
            }


            
        }

        static void ServiceBusListener_OnListenError(object sender, System.IO.ErrorEventArgs e)
        {
            Console.WriteLine(string.Format("----------------------------------------"));
            Console.WriteLine(string.Format("ServiceBusListener_OnListenError {0}!!!!!!!", e.GetException().GetFullMessage()));
            Console.WriteLine(string.Format("----------------------------------------"));
        }

        static void ServiceBusListener_OnGameEventReceived(object sender, GamePacketArgs e)
        {
            var elapsed = _stopwath.ElapsedMilliseconds - int.Parse(e.GamePacket.GameEvent.Properties["StartMilliseconds"]);
            Console.WriteLine(string.Format("Echo GameEvent received in \t{0} milliseconds.", elapsed)); 
        }       
    }
}
