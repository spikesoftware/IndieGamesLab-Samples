using IGL;

using Microsoft.ServiceBus.Messaging;
using System.Diagnostics;
using System.Runtime.Serialization;

using System;
using System.Threading.Tasks;

public static void Run(BrokeredMessage message, TraceWriter log, out BrokeredMessage echoMessage)
{
    GamePacket packet = null;

    echoMessage = null;

    try
    {
        log.Info(string.Format("Processing message {0}", message.SequenceNumber));

        if (!message.Properties.ContainsKey(GamePacket.VERSION))
            throw new ApplicationException(string.Format("Message {0} does not have a valid {1} property.", message.SequenceNumber, GamePacket.VERSION));

        packet = message.GetBody<GamePacket>(new DataContractSerializer(typeof(GamePacket)));

        echoMessage = EchoTheMessage(packet);

        message.Complete();

        log.Info(string.Format("Processed message {0}", message.SequenceNumber));
    }
    catch (Exception ex)
    {
        message.DeadLetter(ex.Message, ex.GetFullMessage());

        log.Error(string.Format("Failed to process {1} with {0}", ex.GetFullMessage(), message.SequenceNumber));
    }
}

private static BrokeredMessage EchoTheMessage(GamePacket packet)
{
    var echoMessage = new BrokeredMessage(packet, new DataContractSerializer(typeof(GamePacket)));
    echoMessage.Properties["PlayerId"] = packet.PlayerId;

    return echoMessage;
}