using System;

namespace IGL
{

    public class GamePacketArgs : EventArgs
    {
        public GamePacket GamePacket { get; set; }
    }

    public delegate void GamePacketEventHandler(Object sender, GamePacketArgs e);


    public class MessageEventArgs : EventArgs
    {
        public string message { get; set; }
    }

    public delegate void MessageEventHandler(Object sender, MessageEventArgs e);
}
