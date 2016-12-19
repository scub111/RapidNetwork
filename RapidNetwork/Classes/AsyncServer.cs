using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace RapidNetwork
{
    public class AsyncServer
    {
        /// <summary>
        /// Порт сервера.
        /// </summary>
        public int Port { get; private set; }

        /// <summary>
        /// Функция обратного вызова получения данных.
        /// </summary>
        public AsyncCallback BeginReceiveCallBack;

        public Socket ServerSocket { get; private set; }

        /// <summary>
        /// Список клиентов.
        /// </summary>
        public Collection<AsyncClient> Clients { get; private set; }

        /// <summary>
        /// Словарь клинетов взависимости от сокета.
        /// </summary>
        public Dictionary<Socket, AsyncClient> AsyncClientsDict { get; private set; }

        public AsyncServer()
        {
            Clients = new Collection<AsyncClient>();
            AsyncClientsDict = new Dictionary<Socket, AsyncClient>();
        }

        public void StartListen(int port)
        {
            Port = port;
            Thread thread = new Thread(Run) { IsBackground = true };
            thread.Start();
            //Run();
        }

        /// <summary>
        /// Событие на успешный запуск.
        /// </summary>
        public event SocketEventHandler<object, EventArgs> Stopped = delegate { };

        public void StopListen()
        {
            if (ServerSocket != null)
            {
                ServerSocket.Close();
            }

            foreach (AsyncClient asyncClient in Clients)
            {
                if (asyncClient.ClientSocket != null)
                {
                    asyncClient.ClientSocket.Close();
                    asyncClient.ClientSocket = null;
                }
            }

            Stopped(this, EventArgs.Empty);
        }

        /// <summary>
        /// Событие на успешный запуск.
        /// </summary>
        public event SocketEventHandler<object, EventArgs> Started = delegate { };

        /// <summary>
        /// Событие на появление исключений.
        /// </summary>
        public event SocketEventHandler<object, ExceptionEventArgs> ServerException = delegate { };

        private void Run()
        {
            try
            {
                // Create the listening socket...
                ServerSocket = new Socket(AddressFamily.InterNetwork,
                              SocketType.Stream,
                              ProtocolType.Tcp);
                IPEndPoint ipLocal = new IPEndPoint(IPAddress.Any, Port);
                // Bind to local IP Address...
                ServerSocket.Bind(ipLocal);
                // Start listening...
                ServerSocket.Listen(100);
                // Create the call back for any client connections...
                ServerSocket.BeginAccept(new AsyncCallback(OnClientConnect), null);

                Started(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                ServerException(this, new ExceptionEventArgs(ex));
            }
        }

        /// <summary>
        /// Событие на поступление сообщения от клиента.
        /// </summary>
        public event SocketEventHandler<object, ServerClientEventArgs> ClientConnected = delegate { };

        // This is the call back function, which will be invoked when a client is connected
        public void OnClientConnect(IAsyncResult asyn)
        {
            try
            {
                // Here we complete/end the BeginAccept() asynchronous call
                // by calling EndAccept() - which returns the reference to
                // a new Socket object
                Socket clientSocket = ServerSocket.EndAccept(asyn);
                AsyncClient asyncClient = new AsyncClient(clientSocket);
                Clients.Add(asyncClient);
                AsyncClientsDict.Add(clientSocket, asyncClient);

                ClientConnected(this, new ServerClientEventArgs(asyncClient));

                // Let the worker Socket do the further processing for the 
                // just connected client
                WaitForData(clientSocket);
                // Now increment the client count

                // Since the main Socket is now free, it can go back and wait for
                // other clients who are attempting to connect
                ServerSocket.BeginAccept(new AsyncCallback(OnClientConnect), null);
            }
            catch (Exception ex)
            {
                ServerException(this, new ExceptionEventArgs(ex));
            }
        }

        // Start waiting for data from the client
        public void WaitForData(System.Net.Sockets.Socket soc)
        {
            try
            {
                if (BeginReceiveCallBack == null)
                {
                    // Specify the call back function which is to be 
                    // invoked when there is any write activity by the 
                    // connected client
                    BeginReceiveCallBack = new AsyncCallback(OnDataReceived);
                }
                SocketPacket theSocPkt = new SocketPacket();
                theSocPkt.currentSocket = soc;
                // Start receiving any data written by the connected client
                // asynchronously
                soc.BeginReceive(theSocPkt.dataBuffer, 0,
                                   theSocPkt.dataBuffer.Length,
                                   SocketFlags.None,
                                   BeginReceiveCallBack,
                                   theSocPkt);
            }
            catch (Exception ex)
            {
                ServerException(this, new ExceptionEventArgs(ex));
            }
        }

        /// <summary>
        /// Событие на поступление сообщения от клиента.
        /// </summary>
        public event SocketEventHandler<object, ServerReceivedEventArgs> MessageReceived = delegate { };

        // This the call back function which will be invoked when the socket
        // detects any client writing of data on the stream
        public void OnDataReceived(IAsyncResult asyn)
        {
            SocketPacket socketData = (SocketPacket)asyn.AsyncState;
            try
            {
                // Complete the BeginReceive() asynchronous call by EndReceive() method
                // which will return the number of characters written to the stream 
                // by the client
                int byteReceived = socketData.currentSocket.EndReceive(asyn);

                if (byteReceived > 0)
                {
                    string szData = Encoding.UTF8.GetString(socketData.dataBuffer, 0, byteReceived);

                    //Socket clientSocket = FindClientSocket(socketData.m_currentSocket);
                    Socket clientSocket = socketData.currentSocket;
                    if (clientSocket != null)
                    {
                        AsyncClient client = new AsyncClient(clientSocket);

                        MessageReceived(this, new ServerReceivedEventArgs(client, szData));
                    }
                }

                // Continue the waiting for data on the Socket
                WaitForData(socketData.currentSocket);
            }
            catch (Exception ex)
            {
                // Очистка списка и словаря клиентов.
                AsyncClient asyncClient = AsyncClientsDict[socketData.currentSocket];
                Clients.Remove(asyncClient);
                AsyncClientsDict.Remove(socketData.currentSocket);
                ServerException(this, new ExceptionEventArgs(ex));
            }
        }

        /// <summary>
        /// Синхронная передача сообщения всем клиентам.
        /// </summary>
        public void SendToAll(string message)
        {
            try
            {
                byte[] byData = System.Text.Encoding.ASCII.GetBytes(message);
                foreach (AsyncClient asyncClient in Clients)
                {
                    if (asyncClient.ClientSocket != null)
                        if (asyncClient.ClientSocket.Connected)
                            asyncClient.ClientSocket.Send(byData);
                }

            }
            catch (Exception ex)
            {
                ServerException(this, new ExceptionEventArgs(ex));
            }
        }

        /// <summary>
        /// Синхронная передача сообщения всем клиентам.
        /// </summary>
        public void SendToAllAsync(string message)
        {
            try
            {
                foreach (AsyncClient asyncClient in Clients)
                {
                    if (asyncClient.ClientSocket != null)
                        if (asyncClient.ClientSocket.Connected)
                            asyncClient.SendAsync(message);
                }

            }
            catch (Exception ex)
            {
                ServerException(this, new ExceptionEventArgs(ex));
            }
        }

        /// <summary>
        /// Очистка неипользуемых сокетов.
        /// </summary>
        public void ClearClients()
        {
            Collection<AsyncClient> AsyncClientsRemoving = new Collection<AsyncClient>();

            foreach (AsyncClient asyncClient in Clients)
                if (asyncClient.ClientSocket != null)
                    if (!asyncClient.ClientSocket.Connected)
                        AsyncClientsRemoving.Add(asyncClient);

            foreach (AsyncClient asyncClient in AsyncClientsRemoving)
                Clients.Remove(asyncClient);
        }
    }
}