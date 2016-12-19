using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;

namespace RapidNetwork
{
    public delegate void SocketEventHandler<T, U>(T sender, U eventArgs) where U : EventArgs;

    public class ServerReceivedEventArgs : EventArgs
    {
        public ServerReceivedEventArgs(AsyncClient client, string message)
        {
            Client = client;
            Message = message;
        }

        public AsyncClient Client { get; private set; }
        public string Message { get; private set; }
    }

    public class ServerClientEventArgs : EventArgs
    {
        public ServerClientEventArgs(AsyncClient client)
        {
            Client = client;
        }

        public AsyncClient Client { get; private set; }
    }

    public class ReceivedEventArgs : EventArgs
    {
        public ReceivedEventArgs(string message)
        {
            Message = message;
        }
        public string Message { get; private set; }
    }

    public class ExceptionEventArgs : EventArgs
    {
        public ExceptionEventArgs(Exception exception)
        {
            Exception = exception;
        }
        public Exception Exception { get; private set; }
    }
}
