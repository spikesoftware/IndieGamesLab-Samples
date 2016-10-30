using IGL.Service;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.ServiceBus.Messaging;
using System.Diagnostics;
using System.Runtime.Serialization;

namespace IGL.ServiceRole
{
    internal class EchoTask : IRoleTask
    {
        public event EventHandler<GamePacketArgs> OnGamePacketCompleted;
        public event EventHandler<GamePacketErrorArgs> OnGamePacketError;

        public async Task ProcessReceivedMessage(Task<IEnumerable<BrokeredMessage>> task)
        {
            foreach (var message in task.Result)
            {
                GamePacket packet = null;

                try
                {
                    Trace.TraceInformation("IGL.ServiceRole.EchoTask.ProcessReceivedMessage() processing message {0}", message.SequenceNumber);

                    if (!message.Properties.ContainsKey(GamePacket.VERSION))
                        throw new ApplicationException(string.Format("IGL.Service.GameEventsListenerTask.RunAsync() message {0} does not have a valid {1} property.", message.SequenceNumber, GamePacket.VERSION));

                    packet = message.GetBody<GamePacket>(new DataContractSerializer(typeof(GamePacket)));

                    EchoTheMessage(packet);

                    await message.CompleteAsync();

                    // alert any listeners
                    OnGamePacketCompleted?.Invoke(null, new GamePacketArgs { GamePacket = packet });                    

                    Trace.TraceInformation("IGL.ServiceRole.EchoTask.ProcessReceivedMessage() processed message {0}", message.SequenceNumber);
                }
                catch (Exception ex)
                {
                    message.DeadLetter(ex.Message, ex.GetFullMessage());

                    OnGamePacketError?.Invoke(null, new GamePacketErrorArgs
                    {
                        Message = string.Format("Message {0} added to deadletter queue at UTC {1}.", message.SequenceNumber, DateTime.UtcNow.ToString()),
                        GameEvent = packet,
                        Exception = ex
                    });

                    Trace.TraceError(string.Format("IGL.ServiceRole.EchoTask.ProcessReceivedMessage() failed with {0}", ex.GetFullMessage()));
                }
            }

            Trace.TraceInformation("IGL.ServiceRole.EchoTask.ProcessReceivedMessage() completed");
        }

        private void EchoTheMessage(GamePacket packet)
        {
            var correlation = packet.Correlation;

            var queue1 = ServiceBusMessagingFactory.GetTopicClientByName("playerevents");
              
            var msg1 = new BrokeredMessage(packet, new DataContractSerializer(typeof(GamePacket)));
            msg1.Properties["PlayerId"] = packet.PlayerId;

            queue1.Send(msg1);
        }
        
    }
}
