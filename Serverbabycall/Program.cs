using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Data.SqlClient;
using System.Data;

namespace Serverbabycall
{

    // State object for reading client data asynchronously
    public class StateObject
    {
        // Client  socket.
        public Socket workSocket = null;
        // Size of receive buffer.
        public const int BufferSize = 4;
        // Receive buffer.
        public byte[] buffer = new byte[BufferSize];

        public int bytesRead = 0;

        // Received data string.

        public byte[] name;

        public Socket listener = null;
        public StateObject listeningTo;

        public int endIdex = 0;
    }

    public class AsynchronousSocketListener
    {
        static LinkedList<StateObject> socketlist = new LinkedList<StateObject>();
        // Thread signal.
        public static ManualResetEvent allDone = new ManualResetEvent(false);
        public static int audio_count = 0;

        public const byte USE_VIDEO = 5;
        public const byte USE_HD_VIDEO = 6;
        public const byte PASSIVE_STATUS = 7;
        public const byte AUDIO_DATA = 8;
        public const byte REGISTER = 9;
        public const byte VIDEO_DATA = 10;
        public const byte VIDEO_HEIGHT = 11;
        public const byte VIDEO_WIDTH = 12;

        public const byte GET = 101;
        public const byte CLIENT_LIST = 102;
        public const byte LISTEN = 103;


        public static void StartListening()
        {
            // Data buffer for incoming data.
            byte[] bytes = new Byte[1024];

            // Establish the local endpoint for the socket.
            // The DNS name of the computer
            // running the listener is "host.contoso.com".
            IPHostEntry ipHostInfo = Dns.Resolve(Dns.GetHostName());
            IPAddress ipAddress = ipHostInfo.AddressList[0];
            IPEndPoint localEndPoint = new IPEndPoint(ipAddress, 13270);

            // Create a TCP/IP socket.
            Socket listener = new Socket(AddressFamily.InterNetwork,
                SocketType.Stream, ProtocolType.Tcp);

            // Bind the socket to the local endpoint and listen for incoming connections.
            try
            {
                listener.Bind(localEndPoint);
                listener.Listen(100);

                while (true)
                {
                    // Set the event to nonsignaled state.
                    allDone.Reset();

                    // Start an asynchronous socket to listen for connections.
                    Console.WriteLine("Waiting for a connection...");
                    listener.BeginAccept(
                        new AsyncCallback(AcceptCallback),
                        listener);

                    // Wait until a connection is made before continuing.
                    allDone.WaitOne();
                }

            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }

            Console.WriteLine("\nPress ENTER to continue...");
            Console.Read();

        }

        public static void AcceptCallback(IAsyncResult ar)
        {
            // Signal the main thread to continue.
            allDone.Set();

            // Get the socket that handles the client request.
            Socket listener = (Socket)ar.AsyncState;
            Socket handler = listener.EndAccept(ar);

            IPEndPoint remoteIpEndPoint = handler.RemoteEndPoint as IPEndPoint;
            IPEndPoint localIpEndPoint = handler.LocalEndPoint as IPEndPoint;
            if (remoteIpEndPoint != null)
            {
                // Using the RemoteEndPoint property.
                Console.WriteLine("I am connected to " + remoteIpEndPoint.Address + "on port number " + remoteIpEndPoint.Port);
            }

            if (localIpEndPoint != null)
            {
                // Using the LocalEndPoint property.
                Console.WriteLine("My local IpAddress is :" + localIpEndPoint.Address + "I am connected on port number " + localIpEndPoint.Port);
            }
            using (SqlConnection connection = new SqlConnection("Persist Security Info=False;Integrated Security=true;server=(local)"))
            {
                SqlCommand cmd = new SqlCommand("INSERT INTO [babycallUserdatabase].[dbo].[connectionTable] (IPaddress, Time) VALUES (@IPaddress, @Time)");
                cmd.CommandType = CommandType.Text;
                cmd.Connection = connection;
                cmd.Parameters.AddWithValue("@IPaddress", "" + remoteIpEndPoint.Address);
                cmd.Parameters.AddWithValue("@Time", DateTime.Now.ToString("h:mm:ss tt"));
                connection.Open();
                cmd.ExecuteNonQuery();
            }
            // Create the state object.
            StateObject state = new StateObject();
            state.workSocket = handler;
            handler.BeginReceive(state.buffer, 0, state.buffer.Length, 0,
                new AsyncCallback(ReadCallback), state);

        }
        /*
         SqlConnection conn = new SqlConnection("Persist Security Info=False;Integrated Security=true;server=(local)");
            conn.Open();
            SqlCommand cmd = new SqlCommand("SELECT Name, Password from [babycallUserdatabase].[dbo].[User_table]", conn);
            SqlDataReader reader= cmd.ExecuteReader();
        */
        static System.Object lockThis = new System.Object();

        public static void ReadCallback(IAsyncResult ar)
        {
            lock (lockThis)
            {


                // Retrieve the state object and the handler socket
                // from the asynchronous state object.
                StateObject state = (StateObject)ar.AsyncState;
                Socket handler = state.workSocket;


                // Read data from the client socket. 

                try
                {
                    state.bytesRead += handler.EndReceive(ar);
                }
                catch (Exception e)
                {
                    if (state.listeningTo != null)

                        state.listeningTo.listener = null;
                    Console.WriteLine(e.ToString());
                }

                if (state.bytesRead > 4 || state.bytesRead < 0)
                {
                    Console.WriteLine("ERROR: byres read is greater than 4 : {0}", state.bytesRead);
                }
                if (state.bytesRead == 4)
                {

                    state.bytesRead = 0;

                    int packageSize = byteArrayToInt(state.buffer);
                    Console.WriteLine("package size: {0}\n", packageSize);

                    byte[] buffer = new byte[packageSize];
                    int bytesRead = 0;
                    while (bytesRead < packageSize)
                    {
                        int recv = state.workSocket.Receive(buffer, bytesRead, packageSize - bytesRead, 0);
                        if (recv > 0)
                            bytesRead += recv;
                    }
                    // Console.WriteLine("Read {0} bytes from socket. \n Data : {1}",
                    //     content.Length, content);
                    //Console.WriteLine("Content {0}\n",content);
                    //Console.WriteLine("state.sb {0}\n", state.sb.ToString());


                    //  Console.WriteLine("Content {0}\n", content);
                    //  Console.WriteLine("state.sb {0}\n", state.sb.ToString());
                    if (buffer[0] == REGISTER)
                    {
                        Console.WriteLine("REGISTER {0}\n", buffer.ToString());
                        state.name = buffer.Skip(1).Take(buffer.Length - 1).ToArray();
                        socketlist.AddLast(state);

                    }
                    else if (buffer[0] == GET)
                    {
                        for (int i = 0; i < socketlist.Count; i++)
                        {
                            if (!socketlist.ElementAt(i).workSocket.Connected)
                                socketlist.Remove(socketlist.ElementAt(i));
                        }

                        foreach (StateObject so in socketlist)
                        {
                            if (so.workSocket.Connected)
                            {
                                Send(handler, CLIENT_LIST, so.name);
                            }
                        }

                    }
                    else if (buffer[0] == LISTEN)
                    {


                        byte[] data = buffer.Skip(1).Take(buffer.Length - 1).ToArray();
                        foreach (StateObject so in socketlist)
                        {
                            if (so.name.SequenceEqual(data))
                            {
                                Console.WriteLine("LISTENING TO {0}\n", so.listener);
                                if (state.listeningTo != null)
                                    state.listeningTo.listener = null;
                                so.listener = handler;
                                state.listeningTo = so;
                                byte[] byteData = { PASSIVE_STATUS, 0, 0, 0 };
                                try
                                {
                                    so.workSocket.BeginSend(byteData, 0, byteData.Length, 0,
                                                                                   new AsyncCallback(SendCallback), so.workSocket);
                                }
                                catch (Exception e)
                                {
                                    state.listeningTo = null;
                                    state.listener = null;
                                    socketlist.Remove(so);
                                    break;
                                }

                            }
                        }
                    }
                    /* Console.WriteLine("SENDING TO LISTENER {0} from {1}  {2} {3}\n",
                          ((IPEndPoint)state.listener.RemoteEndPoint).Address,
                          ((IPEndPoint)state.workSocket.RemoteEndPoint).Address,
                          DateTime.Now.ToString("h:mm:ss tt"), audio_count++);
                     */
                    else if (buffer[0] == AUDIO_DATA || buffer[0] == VIDEO_DATA || buffer[0] == VIDEO_HEIGHT || buffer[0] == VIDEO_WIDTH)
                    {
                        if (state.listener != null)
                        {
                            Console.WriteLine("writing to listener {0}\n", buffer[0]);

                            byte[] data = buffer.Skip(1).Take(buffer.Length - 1).ToArray();

                            if (Send(state.listener, buffer[0], data) == false)
                            {
                                state.listener = null;
                            }

                        }
                        else
                        {
                            byte[] byteData = { PASSIVE_STATUS, 1, 0, 0 };
                            state.workSocket.BeginSend(byteData, 0, byteData.Length, 0,
                                            new AsyncCallback(SendCallback), state.workSocket);
                        }
                    }

                    else
                    {
                        Console.WriteLine("got something I dont understand {0}\n", buffer[0]);

                    }

                    handler.BeginReceive(state.buffer, 0, state.buffer.Length, 0,
                     new AsyncCallback(ReadCallback), state);
                }
                else
                {                // Not all data received. Get more.
                    try
                    {
                        handler.BeginReceive(state.buffer, state.bytesRead, state.buffer.Length - state.bytesRead, 0,
                                       new AsyncCallback(ReadCallback), state);
                    }
                    catch (Exception e)
                    {
                        if (state.listeningTo != null)
                            state.listeningTo.listener = null;
                    }


                }
            }
        }
        public static int byteArrayToInt(byte[] array)
        {
            int B0 = unchecked((int)0xff000000);
            int B1 = 0x00ff0000;
            int B2 = 0x0000ff00;
            int B3 = 0x000000ff;

            int a0 = (array[0] << 24);
            int a1 = (array[1] << 16);
            int a2 = (array[2] << 8);
            int a3 = array[3];

            return B0 & a0 | B1 & a1 | B2 & a2 | B3 & a3;
        }
        public static byte[] intToByteArray(uint value)
        {
            return new byte[] {
				(byte)(value >> 24),
				(byte)(value >> 16),
				(byte)(value >> 8),
				(byte)value};
        }


        private static Boolean Send(Socket handler, byte tag, byte[] data)
        {
            // Convert the string data to byte data using ASCII encoding.

            int data_length = (data == null ? 0 : data.Length);
            int package_length = data_length + 1;

            // Begin sending the data to the remote device.
            byte[] tagA = { tag };
            try
            {

                handler.BeginSend(intToByteArray((uint)package_length), 0, 4, 0,
                  new AsyncCallback(SendCallback), handler);

                handler.BeginSend(tagA, 0, tagA.Length, 0,
                  new AsyncCallback(SendCallback), handler);

                handler.BeginSend(data, 0, data_length, 0,
                    new AsyncCallback(SendCallback), handler);
            }
            catch (Exception e)
            {
                return false;

            }
            return true;
        }

        private static void SendCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the socket from the state object.
                //Socket handler = (Socket)ar.AsyncState;

                // Complete sending the data to the remote device.
                //int bytesSent = handler.EndSend(ar);
                // Console.WriteLine("Sent {0} bytes to client.", bytesSent);

                // handler.Shutdown(SocketShutdown.Both);
                // handler.Close();

            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }


        public static int Main(String[] args)
        {
            StartListening();
            return 0;
        }
    }
}

