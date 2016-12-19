using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace RapidNetwork
{
    /// <summary>
    /// Класс ассинхронного TCP-клиента.
    /// </summary>
    public class AsyncClient
    {
        /// <summary>
        /// IP сервера.
        /// </summary>
        public string ServerIP { get; private set; }

        /// <summary>
        /// Порт сервера.
        /// </summary>
        public int Port { get; private set; }

        /// <summary>
        /// ID клиента.
        /// </summary>
        public string ClientID { get; private set; }

        /// <summary>
        /// Функция обратного вызова получения данных.
        /// </summary>
        public AsyncCallback BeginReceiveCallBack;

        /// <summary>
        /// Сокет клиента.
        /// </summary>
        public Socket ClientSocket { get; set; }

        /// <summary>
        /// Подключен ли.
        /// </summary>
        public bool IsConnected
        {
            get
            {
                if (ClientSocket != null && ClientSocket.Connected)
                    return true;
                else
                    return false;
            }
        }

        /// <summary>
        /// Конструктор.
        /// </summary>
        public AsyncClient()
        {
        }

        /// <summary>
        /// Конструктор для инициализации клиента извне.
        /// </summary>
        public AsyncClient(Socket workSocket)
        {
            this.ClientSocket = workSocket;
        }

        public void Connect(string serverIP, int port, string clientID)
        {
            this.ServerIP = serverIP;
            this.Port = port;
            this.ClientID = clientID;

            Thread thread = new Thread(Run) { IsBackground = true };
            thread.Start();
        }

        public void Connect(string serverIP, int port)
        {
            Connect(serverIP, port, "");
        }

        /// <summary>
        /// Событие на отключение от сервера.
        /// </summary>
        public event SocketEventHandler<object, EventArgs> Disconnected = delegate { };
        public void Disconnect()
        {
            if (ClientSocket != null)
            {
                ClientSocket.Close();
                ClientSocket = null;
            }
            Disconnected(this, EventArgs.Empty);
        }

        /// <summary>
        /// Событие на появление исключений.
        /// </summary>
        public event SocketEventHandler<object, ExceptionEventArgs> ClientException = delegate { };

        private void Run()
        {
            try
            {
                ClientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                IPAddress ipAddress = IPAddress.Parse(ServerIP);

                ClientSocket.BeginConnect(new IPEndPoint(ipAddress, Port), ConnectCallBack, null);
            }
            catch (Exception ex)
            {
                ClientException(this, new ExceptionEventArgs(ex));
            }
        }

        /// <summary>
        /// Событие на подключение к серверу.
        /// </summary>
        public event SocketEventHandler<object, EventArgs> Connected = delegate { };

        private void ConnectCallBack(IAsyncResult ar)
        {
            try
            {
                ClientSocket.EndConnect(ar);

                if (ClientSocket.Connected)
                {
                    Connected(this, EventArgs.Empty);
                    //Wait for data asynchronously 
                    WaitForData();
                }
            }
            catch (Exception ex)
            {
                ClientException(this, new ExceptionEventArgs(ex));
            }
        }

        /// <summary>
        /// Ожидание получения данных.
        /// </summary>
        public void WaitForData()
        {
            try
            {
                if (BeginReceiveCallBack == null)
                {
                    BeginReceiveCallBack = new AsyncCallback(OnDataReceived);
                }
                SocketPacket theSocPkt = new SocketPacket();
                theSocPkt.currentSocket = ClientSocket;
                // Start listening to the data asynchronously
                ClientSocket.BeginReceive(theSocPkt.dataBuffer,
                                        0, theSocPkt.dataBuffer.Length,
                                        SocketFlags.None,
                                        BeginReceiveCallBack,
                                        theSocPkt);
            }
            catch (Exception ex)
            {
                ClientException(this, new ExceptionEventArgs(ex));
            }
        }

        /// <summary>
        /// Событие на поступление сообщения от клиента.
        /// </summary>
        public event SocketEventHandler<object, ReceivedEventArgs> MessageReceived = delegate { };

        public void OnDataReceived(IAsyncResult asyn)
        {
            SocketPacket theSockId = (SocketPacket)asyn.AsyncState;
            try
            {
                int byteReceived = theSockId.currentSocket.EndReceive(asyn);
                if (byteReceived > 0)
                {
                    string szData = Encoding.UTF8.GetString(theSockId.dataBuffer, 0, byteReceived);
                    MessageReceived(this, new ReceivedEventArgs(szData));
                }
                WaitForData();
            }
            catch (Exception ex)
            {
                ClientException(this, new ExceptionEventArgs(ex));
                if (!theSockId.currentSocket.Connected)
                    Disconnected(this, EventArgs.Empty);
            }
        }

        public void SendAsync(string message)
        {
            try
            {
                byte[] buffer = Encoding.UTF8.GetBytes(message);
                ClientSocket.BeginSend(buffer, 0, buffer.Length, 0, BeginSendCallBack, null);
            }
            catch (Exception ex)
            {
                ClientException(this, new ExceptionEventArgs(ex));
            }
        }

        private void BeginSendCallBack(IAsyncResult ar)
        {
            int byteSent = ClientSocket.EndSend(ar);
        }
    }
}
