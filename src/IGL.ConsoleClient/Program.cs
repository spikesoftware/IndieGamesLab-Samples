using IGL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IGL.ConsoleClient
{
    class Program
    {
        static void Main(string[] args)
        {
            // set the following once - this is using ACS            
            IGL.Client.Configuration.IssuerName = "[IssuerName]";
            IGL.Client.Configuration.IssuerSecret = "[IssuerSecret]";
            IGL.Client.Configuration.ServiceNamespace = "[ServiceNamespace]";     
            
            // optional       
            IGL.Client.ServiceBusWriter.GameId = 1;

            // create a i have started event
            var gameevent = new GameEvent
            {
                Properties = new Dictionary<string, string>()
                {
                    { "PlayerId", "somePlayerId" },
                    { "PlayerName", "Joe Smith" }                    
                }
            };

            Console.WriteLine("Sent Event 100 to IGL Service with a response '{0}'.", IGL.Client.ServiceBusWriter.SubmitGameEvent("gameevents", 100, gameevent));

            // create a i have ended event 
            gameevent = new GameEvent
            {
                Properties = new Dictionary<string, string>()
                {
                    { "PlayerId", "somePlayerId" },
                    { "PlayerName", "Joe Smith" }
                }
            };

            Console.WriteLine("Sent Event 101 to IGL Service with a response '{0}'.", IGL.Client.ServiceBusWriter.SubmitGameEvent("gameevents", 101, gameevent));

            Console.Write("Hit return to end...");
            Console.ReadLine();
        }
    }
}
