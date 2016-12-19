using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RapidNetwork
{
    public class SocketPacket
    {
        public System.Net.Sockets.Socket currentSocket;
        public byte[] dataBuffer = new byte[100];
    }
}
