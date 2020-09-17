
//STD Includes
using System;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using Stopwatch = System.Diagnostics.Stopwatch;

//Unity Includes
using UnityEngine;

//NUNet Includes
using NUNet.Internal;

namespace NUNet
{
	public static class NUServer
	{
		//TCP Socket Listener
		private static TcpListener listener;

		//UDP Broadcast Client
		private static Socket broadcaster;

		//List of Clients
		public static readonly object clientsLock;
		public static Dictionary<Guid, NUClientInfo> clients;

		//TODO: Remove this or change to NUClientInfo
		private static List<Guid> freeSlots;

		//Network callbacks
		private static Action<Guid> m_onClientConnected;
		public static Action<Guid> onClientConnected
		{
			get { return m_onClientConnected; }
			set { m_onClientConnected = NUUtilities.SanitizeAction(value); }
		}

		private static Action<Guid> m_onClientReconnected;
		public static Action<Guid> onClientReconnected
		{
			get { return m_onClientReconnected; }
			set { m_onClientReconnected = NUUtilities.SanitizeAction(value); }
		}

		private static Action<Guid> m_onClientDisconnected;
		public static Action<Guid> onClientDisconnected
		{
			get { return m_onClientDisconnected; }
			set { m_onClientDisconnected = NUUtilities.SanitizeAction(value); }
		}

		private static Action<Guid> m_onClientTimedOut;
		public static Action<Guid> onClientTimedOut
		{
			get { return m_onClientTimedOut; }
			set { m_onClientTimedOut = NUUtilities.SanitizeAction(value); }
		}

		private static Action<Guid, Packet> m_onClientPacketReceived;
		public static Action<Guid, Packet> onClientPacketReceived
		{
			get { return m_onClientPacketReceived; }
			set { m_onClientPacketReceived = NUUtilities.SanitizeAction(value); }
		}

		public delegate Packet BroadcastPacketCallback(Packet packet);
		private static BroadcastPacketCallback m_onBroadcastPacketReceived;
		public static BroadcastPacketCallback onBroadcastPacketReceived
		{
			get { return m_onBroadcastPacketReceived; }
			set { m_onBroadcastPacketReceived = (BroadcastPacketCallback)NUUtilities.SanitizeDelegate(value); }
		}

		private static Action<bool, string> m_onServerStarted;
		public static Action<bool, string> onServerStarted
		{
			get { return m_onServerStarted; }
			set { m_onServerStarted = NUUtilities.SanitizeAction(value); }
		}

		//Pool of received Packets
		private static readonly object connQueueLock;
		private static Queue<Guid> connectionQueue;
		private static readonly object reconnQueueLock;
		private static Queue<Guid> reconnectionQueue;
		private static readonly object timeOutQueueLock;
		private static Queue<Guid> timeOutQueue;
		private static readonly object discQueueLock;
		private static Queue<Guid> disconnectionQueue;
		private static readonly object dataQueueLock;
		private static Queue<PacketClientID> dataQueue;

		//Structure to control packet sequence
		private static readonly Dictionary<Guid, object> seqDataQueueLock;
		private static Dictionary<Guid, List<PacketClientID>> seqDataQueue;
		private static Dictionary<Guid, LastPacketID> lastPacketIds;

		//Multipart Control Structure
		private static readonly object multiPartLock;
		private static Dictionary<Hash, MultiPartBuffer> multiPartBuffers;

		//Server Stopwatch
		private static Stopwatch stopwatch;

		//MonoBehaviour instance of server component
		private static NUServerComponent serverComponent;

		//Server Started flag
		public static bool started { private set; get; }

		//Server properties
		public static IPAddress ipAddress { get; private set; }
		public static ushort port { get; private set; }

		//Default constructor
		static NUServer()
		{
			listener = null;
			broadcaster = null;
			clientsLock = new object();
			clients = null;
			freeSlots = null;
			connQueueLock = new object();
			connectionQueue = null;
			reconnQueueLock = new object();
			reconnectionQueue = null;
			timeOutQueueLock = new object();
			timeOutQueue = null;
			discQueueLock = new object();
			disconnectionQueue = null;
			dataQueueLock = new object();
			dataQueue = null;
			seqDataQueueLock = new Dictionary<Guid, object>();
			seqDataQueue = null;
			lastPacketIds = null;
			multiPartLock = new object();
			multiPartBuffers = null;
			onClientConnected = null;
			m_onClientDisconnected = null;
			m_onClientPacketReceived = null;
			stopwatch = null;
			serverComponent = null;
			started = false;
		}

		/// <summary>
		/// Start server on given IP string and port (default 25565), optional
		/// update hook to queue all Packets treating to a given thread and loop.
		/// </summary>
		/// <param name="ipAddress">IPAddress of the network interface to be used</param>
		/// <param name="port">Port of the network interface to be used</param>
		/// <param name="updateHook">Action to hook on for a timed update</param>
		/// <param name="reservedBufferedPackets">Max number of packets in queued for update hook</param>
		public static void Start(string ipAddress, ushort port = 25565, Action updateHook = null,
			int reservedBufferedPackets = NUUtilities.MaxBufferedPackets)
		{
			IPAddress address;
			if (IPAddress.TryParse(ipAddress, out address))
			{
				Start(address, port, updateHook);
			}
			else
			{
				Debug.LogError("Could not parse IP " + ipAddress + "!");
			}
		}

		/// <summary>
		/// Start server on given IP string and port (default 25565), optional
		/// update hook to queue all Packets treating to a given thread and loop.
		/// </summary>
		/// <param name="ipAddress">IPAddress of the network interface to be used</param>
		/// <param name="port">Port of the network interface to be used</param>
		/// <param name="updateHook">Action to hook on for a timed update</param>
		/// <param name="reservedBufferedPackets">Max number of packets in queued for update hook</param>
		public static void Start(IPAddress ipAddress = null, ushort port = 25565, Action updateHook = null,
			ushort broadcastPort = 56552, int reservedBufferedPackets = NUUtilities.MaxBufferedPackets)
		{
			if (!started)
			{
				//Loopback address if none are given
				if (ipAddress == null)
					ipAddress = IPAddress.Loopback;

				try
				{
					//Setup TCP Listener
					listener = new TcpListener(ipAddress, port);

					//Setup broadcaster
					IPEndPoint broadcastEp = new IPEndPoint(ipAddress, broadcastPort);
					broadcaster = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp)
					{
						ExclusiveAddressUse = true
					};
					broadcaster.Bind(broadcastEp); //Bind it locally to the Broadcast Address

					//Create Slots and Clients list
					if (freeSlots != null)
						freeSlots.Clear();
					else
						freeSlots = new List<Guid>();

					lock (clientsLock)
					{
						if (clients != null)
						{
							clients.Clear();
						}
						else
						{

							clients = new Dictionary<Guid, NUClientInfo>();
						}
					}

					//Create packets queue
					connectionQueue = new Queue<Guid>(reservedBufferedPackets);
					reconnectionQueue = new Queue<Guid>(reservedBufferedPackets);
					timeOutQueue = new Queue<Guid>(reservedBufferedPackets);
					disconnectionQueue = new Queue<Guid>(reservedBufferedPackets);
					dataQueue = new Queue<PacketClientID>(reservedBufferedPackets);
					seqDataQueue = new Dictionary<Guid, List<PacketClientID>>();
					lastPacketIds = new Dictionary<Guid, LastPacketID>();

					//Create multipart control data with custom Hash comparator
					multiPartBuffers = new Dictionary<Hash, MultiPartBuffer>(reservedBufferedPackets,
						Hash.comparer);

					//Start stopwatch (for ping measurement)
					stopwatch = new Stopwatch();
					stopwatch.Start();

					//Start Listener with backlog of 100 queued connections
					listener.Start(100);

					//Set server started flag
					started = true;

					//Hook on updateHook otherwise create NUServerComponent
					if (updateHook != null)
					{
						updateHook += PingAllClients;
						updateHook += ProcessQueues;
					}
					else
					{
						//Create MonoBehaviour instance if it doesn't exists
						if (serverComponent != null)
						{
							GameObject.DestroyImmediate(serverComponent.gameObject);
							serverComponent = null;
						}
						GameObject serverObject = new GameObject("NUServerObject");
						//serverObject.hideFlags = HideFlags.HideAndDontSave;
						GameObject.DontDestroyOnLoad(serverObject);
						serverComponent = serverObject.AddComponent<NUServerComponent>();
						//serverComponent.hideFlags = HideFlags.HideInInspector;
					}

					//Begin receiving Broadcast messages
					BroadcastTransmissionState broadcastState = new BroadcastTransmissionState(
						NUUtilities.MTU, ref broadcaster);
					EndPoint broadcastSenderEp = broadcastState.senderEp;
					broadcaster.BeginReceiveFrom(broadcastState.data, 0, broadcastState.data.Length, SocketFlags.None,
						ref broadcastSenderEp, new AsyncCallback(EndBroadcastReceive), broadcastState);

					//Begin accepting the TCP client
					listener.BeginAcceptTcpClient(OnIncomingConnection, listener);
					Debug.Log("Server Started on IP " + ipAddress + " and port " + port + ".\n" +
						"Waiting for incoming connection...");

					//Hold variables
					NUServer.ipAddress = ipAddress;
					NUServer.port = port;

					//Invoke Callbacks
					if (m_onServerStarted != null)
					{
						m_onServerStarted(true, "Server Started Successfully!");
					}
				}
				catch (Exception ex)
				{
					// Cleanup
					broadcaster?.Close();
					listener?.Stop();

					Debug.LogError("Could not initialize server, error: " + ex.ToString());
					if (m_onServerStarted != null)
					{
						m_onServerStarted(false, "Server couldn't initialize, error:" + ex.ToString());
					}
				}
			}
			else
			{
				Debug.LogError("Server already running!");
				if (m_onServerStarted != null)
				{
					m_onServerStarted(false, "Server already running!");
				}
			}
		}

		/// <summary>
		/// On listened for an incoming connection
		/// </summary>
		/// <param name="asyncResult"></param>
		private static void OnIncomingConnection(IAsyncResult asyncResult)
		{
			//Extract listener from AsyncState
			TcpListener listener = (TcpListener)asyncResult.AsyncState;

			//Try to get accepted client and extract information
			try
			{
				//Server stopped
				if (listener == null)
					return;

				//Accept TCP Client
				TcpClient tcpClient = listener.EndAcceptTcpClient(asyncResult);
				tcpClient.ReceiveBufferSize = 1048576;
				tcpClient.SendBufferSize = 1048576;

				//Get Client EndPoint
				IPEndPoint clientEP = (IPEndPoint)tcpClient.Client.RemoteEndPoint;
				IPEndPoint localClientEP = (IPEndPoint)tcpClient.Client.LocalEndPoint;
				//Debug.Log(localClientEP);

				//Create UDP socket
				Socket udpClient = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp)
				{
					ExclusiveAddressUse = true
				};

				//Create NUClient and add it to the clients list
				Guid clientGuid = Guid.NewGuid();
				int portOffset = clients.Count;
				NUClientInfo clientInfo = new NUClientInfo(clientGuid, clientEP.Address, ref tcpClient, ref udpClient, portOffset: portOffset);

				//Setup Sequential Packet Structures
				if (!seqDataQueueLock.ContainsKey(clientGuid))
				{
					seqDataQueueLock.Add(clientGuid, new object());
				}
				List<PacketClientID> seqPacketList;
				if (!seqDataQueue.TryGetValue(clientGuid, out seqPacketList))
				{
					seqDataQueue.Add(clientGuid, new List<PacketClientID>());
				}
				else
				{
					seqPacketList.Clear();
				}
				LastPacketID lastPacketId;
				if (!lastPacketIds.TryGetValue(clientGuid, out lastPacketId))
				{
					lastPacketIds.Add(clientGuid, new LastPacketID(-1));
				}
				else
				{
					lastPacketId.id = -1;
				}

				//Start receiving data from the client...
				//On the reliable connection
				TcpTransmissionState tcpReceivingState = new TcpTransmissionState(NUUtilities.MTU, ref tcpClient, clientInfo);
				tcpClient.GetStream().BeginRead(tcpReceivingState.data, 0, NUUtilities.MTU,
					EndReliableReceive, tcpReceivingState);

			}
			catch (SocketException ex)
			{
				Debug.LogError(ex.ToString());
			}

			//Server stopped
			if (listener == null)
				return;

			//Keep accepting more clients
			listener.BeginAcceptTcpClient(OnIncomingConnection, listener);
		}

		/// <summary>
		/// Send a Packet through a reliable but latency prone way
		/// </summary>
		/// <param name="packet"></param>
		public static void SendReliable(Packet packet, Action callback = null)
		{
			if (packet.data == null)
			{
				Debug.LogError("Data to be sent is null!");
				return;
			}

			if (packet.data.Length == 0)
			{
				Debug.LogError("Data to be sent is empty!");
				return;
			}

			if (packet.destinationIDs == null)
			{
				Debug.LogError("Destination IDs are null!");
				return;
			}

			//Must split packets
			if (packet.data.Length > NUUtilities.MTU)
			{
				Packet[] partPackets = NUUtilities.SplitPacket(packet);
				for (int i = 0; i < partPackets.Length - 1; i++)
				{
					SendReliable(partPackets[i]);
				}

				//Set callback on last packet send
				SendReliable(partPackets[partPackets.Length - 1], callback);
				return;
			}

			foreach (Guid id in packet.destinationIDs)
			{
				NUClientInfo client;
				lock (clientsLock)
				{
					if (!clients.ContainsKey(id))
					{
						Debug.LogError("One of the packet destination IDs is out of bounds!\nID: " + id);
						return;
					}

					client = clients[id];
				}

				if (!client.Connected())
				{
					Debug.LogWarning("One of the packet destination IDs is disconnected!\nID: " + id);
					return;
				}

				if (!client.tcpClient.Connected)
				{
					Debug.LogWarning("Client lost connection!");
					lock (timeOutQueueLock)
					{
						//Disconnect client on main thread
						client.Disconnect();

						//Release slot
						freeSlots.Add(id);

						timeOutQueue.Enqueue(id);
					}
					return;
				}

				try
				{
					TcpTransmissionState transmissionState = new TcpTransmissionState(0, ref client.tcpClient,
						client, callback);
					client.tcpClient.GetStream().BeginWrite(packet.data, 0, packet.data.Length,
						EndReliableSend, transmissionState);
				}
				catch (Exception ex)
				{
					Debug.LogError("Error while sending data to client: " + ex.ToString());

					lock (timeOutQueueLock)
					{
						//Disconnect client on main thread
						client.Disconnect();

						//Release slot
						freeSlots.Add(id);

						timeOutQueue.Enqueue(id);
					}
				}
			}
		}

		/// <summary>
		/// Send a Packet through a reliable but latency prone way with sequence guarantee.
		/// <para>This call might have more latency then <see cref="SendReliable(Packet, Action)"/>.</para>
		/// </summary>
		/// <param name="packet"></param>
		public static void SendReliableSequenced(Packet packet, Action callback = null)
		{
			if (packet.data == null)
			{
				Debug.LogError("Data to be sent is null!");
				return;
			}

			if (packet.data.Length == 0)
			{
				Debug.LogError("Data to be sent is empty!");
				return;
			}

			if (packet.destinationIDs == null)
			{
				Debug.LogError("Destination IDs are null!");
				return;
			}

			//Must split packets
			if (packet.data.Length > NUUtilities.MTU)
			{
				Packet[] partPackets = NUUtilities.SplitPacket(packet);
				for (int i = 0; i < partPackets.Length - 1; i++)
				{
					SendReliable(partPackets[i]);
				}

				//Set callback on last packet send
				SendReliable(partPackets[partPackets.Length - 1], callback);
				return;
			}

			foreach (Guid id in packet.destinationIDs)
			{
				NUClientInfo client;
				lock (clientsLock)
				{
					if (!clients.ContainsKey(id))
					{
						Debug.LogError("One of the packet destination IDs is out of bounds!\nID: " + id);
						return;
					}

					client = clients[id];
				}

				if (!client.Connected())
				{
					Debug.LogWarning("One of the packet destination IDs is disconnected!\nID: " + id);
					return;
				}

				if (!client.tcpClient.Connected)
				{
					Debug.LogWarning("Client lost connection!");
					lock (timeOutQueueLock)
					{
						//Disconnect client on main thread
						client.Disconnect();

						//Release slot
						freeSlots.Add(id);

						timeOutQueue.Enqueue(id);
					}
					return;
				}

				if (packet.id == -1)
				{
					packet.OverrideID(NUUtilities.GeneratePacketId(client.guid));
				}

				try
				{
					TcpTransmissionState transmissionState = new TcpTransmissionState(0, ref client.tcpClient,
						client, callback);
					client.tcpClient.GetStream().BeginWrite(packet.data, 0, packet.data.Length,
						EndReliableSend, transmissionState);
				}
				catch (Exception ex)
				{
					Debug.LogError("Error while sending data to client: " + ex.ToString());

					lock (timeOutQueueLock)
					{
						//Disconnect client on main thread
						client.Disconnect();

						//Release slot
						freeSlots.Add(id);

						timeOutQueue.Enqueue(id);
					}
				}
			}
		}

		/// <summary>
		/// Send a Packet through a unreliable but low latency way
		/// </summary>
		/// <param name="packet"></param>
		public static void SendUnreliable(Packet packet, Action callback = null)
		{
			if (packet.data == null)
			{
				Debug.LogError("Data to be sent is null!");
				return;
			}

			if (packet.data.Length == 0)
			{
				Debug.LogError("Data to be sent is empty!");
				return;
			}

			if (packet.destinationIDs == null)
			{
				Debug.LogError("Destination IDs are null!");
				return;
			}

			if (packet.data.Length > NUUtilities.MTU)
			{
				Debug.LogErrorFormat("Cannot send Packet with size ({0}) bigger then {1} bytes" +
					" through unreliable channel! Use SendReliable instead.",
					packet.data.Length, (NUUtilities.MTU - 1));
				return;
			}

			foreach (Guid id in packet.destinationIDs)
			{
				NUClientInfo client;
				lock (clientsLock)
				{
					if (!clients.ContainsKey(id))
					{
						Debug.LogError("One of the packet destination IDs is out of bounds!\nID: " + id);
						return;
					}

					client = clients[id];
				}

				if (!client.Connected())
				{
					Debug.LogWarning("One of the packet destination IDs is disconnected!\nID: " + id);
					return;
				}

				try
				{
					if (!client.udpClient.Connected) continue; //Avoid errors here
					UdpTransmissionState transmissionState = new UdpTransmissionState(0, ref client.udpClient, client, callback);
					client.udpClient.BeginSend(packet.data, 0, packet.data.Length, SocketFlags.None,
						new AsyncCallback(EndUnreliableSend), transmissionState);
				}
				catch (Exception ex)
				{
					Debug.LogError(ex.ToString());
				}
			}
		}

		//When finished reliably sending data to the Client
		private static void EndReliableSend(IAsyncResult asyncResult)
		{
			//Extract ReliableTransmissionState from Async State
			TcpTransmissionState transmissionState = (TcpTransmissionState)asyncResult.AsyncState;

			//Get tcpClient stream
			NetworkStream stream = transmissionState.tcpClient.GetStream();

			try
			{
				//Finish sending data
				stream.EndWrite(asyncResult);

				//Callback of transmission state
				if (transmissionState.callback != null)
					transmissionState.callback();
			}
			catch (Exception ex)
			{
				Debug.LogError("Error while sending data to " + transmissionState.client.guid +
					" on IP " + transmissionState.client.address.ToString() + ": " + ex.ToString());

				//Hold Guid because it's lost on Disconnect()
				Guid guid = transmissionState.client.guid;

				//Disconnect client
				transmissionState.client.Disconnect();

				//Release slot
				freeSlots.Add(transmissionState.client.guid);

				lock (timeOutQueueLock)
				{
					timeOutQueue.Enqueue(guid);
				}
			}
		}

		//When finished unreliably sending data to the Client
		private static void EndUnreliableSend(IAsyncResult asyncResult)
		{
			//Extract UnreliableTransmissionState from Async State
			UdpTransmissionState transmissionState = (UdpTransmissionState)asyncResult.AsyncState;

			//Get UdpClient
			Socket udpClient = transmissionState.socket;

			try
			{
				//Finish sending data
				udpClient.EndSend(asyncResult);

				//Callback of transmission state
				if (transmissionState.callback != null)
					transmissionState.callback();
			}
			catch (Exception ex)
			{
				Debug.LogError("Error while sending data to " + transmissionState.client.guid +
					" on IP " + transmissionState.client.address.ToString() + ": " + ex.ToString());

				//Hold Guid because it's lost on Disconnect()
				Guid guid = transmissionState.client.guid;

				//Disconnect client
				transmissionState.client.Disconnect();

				//Release slot
				freeSlots.Add(guid);

				lock (timeOutQueueLock)
				{
					timeOutQueue.Enqueue(guid);
				}
			}
		}

		//When finished reliably receiving data from the client
		private static void EndReliableReceive(IAsyncResult asyncResult)
		{
			//Extract TransmissionState from AsyncState
			TcpTransmissionState transmissionState = (TcpTransmissionState)asyncResult.AsyncState;
			TcpClient tcpClient = transmissionState.tcpClient;

			try
			{
				//End receiving data
				NetworkStream stream = tcpClient.GetStream();
				int receivedSize = stream.EndRead(asyncResult);
				int packetItt = 0; //Iterator for packets bytes

				//Create all packets within this buffer
				while (receivedSize > 0)
				{
					//Read packet size from the beginning of the packet buffer
					int packetSize;

					//Check if the end of the buffer match partial packet size data
					if (packetItt > transmissionState.data.Length - 4)
					{
						packetSize = -1;
					}
					else
					{
						packetSize = BitConverter.ToInt32(transmissionState.data, packetItt);
						if (packetSize < 0)
						{
							Debug.LogError("Error while receiving reliable packet! Size is negative: " + packetSize);
							return;
						}
					}

					//Check if there is missing packet data
					if (receivedSize + transmissionState.offset < packetSize || packetSize == -1)
					{
						//Append received data to buffer (next EndRead will append remaining data on it)
						Array.Copy(transmissionState.data, packetItt + transmissionState.offset, transmissionState.data,
							transmissionState.offset, receivedSize);

						//Hold received size so far to check for packet completition later
						transmissionState.offset += receivedSize;

						//Keep receiving this packet with offset
						stream.BeginRead(transmissionState.data, transmissionState.offset, NUUtilities.MTU - transmissionState.offset,
							new AsyncCallback(EndReliableReceive), transmissionState);

						//This receive operation stops here
						return;
					}

					//Update current size (is the same as the data ID for this packet!)
					receivedSize -= packetSize - transmissionState.offset;

					//Create packet from raw data
					Packet packet = new Packet(transmissionState.data, packetItt, packetSize);

					//Treat packet accordingly
					TreatPacket(ref packet, transmissionState.client);

					//Reset transmissionState offset
					transmissionState.offset = 0;

					//Increment packet iterator
					packetItt += packetSize;
				}

				//Keep receiving data forever
				if (tcpClient.Connected)
				{
					stream.BeginRead(transmissionState.data, 0, NUUtilities.MTU,
						new AsyncCallback(EndReliableReceive), transmissionState);
				}
			}
			catch (Exception ex)
			{
				if (!transmissionState.client.connected)
					return;

				Debug.LogError("Error receiving reliable Packet from client (id " +
					transmissionState.client + "): " + ex.ToString());

				//Hold Guid because it's lost on Disconnect()
				Guid guid = transmissionState.client.guid;

				//Disconnect client
				transmissionState.client.Disconnect();

				//Release slot
				freeSlots.Add(transmissionState.client.guid);

				lock (timeOutQueueLock)
				{
					timeOutQueue.Enqueue(guid);
				}
			}
		}

		//When finished unreliably receiving data from the client
		private static void EndUnreliableReceive(IAsyncResult asyncResult)
		{
			//Extract UnreliableReceivingBuffer from AsyncState
			UdpTransmissionState transmissionState = (UdpTransmissionState)asyncResult.AsyncState;
			Socket udpClient = transmissionState.socket;

			try
			{
				//End receiving data
				int receivedSize = udpClient.EndReceive(asyncResult);

				if (receivedSize > 0)
				{
					//Create packet from raw data
					Packet packet = new Packet(transmissionState.data, receivedSize);

					//Treat packet accordingly
					TreatPacket(ref packet, transmissionState.client);
				}

				//Keep receiving data forever
				udpClient.BeginReceive(transmissionState.data, 0, NUUtilities.MTU, SocketFlags.None,
					EndUnreliableReceive, transmissionState);
			}
			catch (Exception ex)
			{
				if (!transmissionState.client.connected)
					return;

				Debug.LogError("Error receiving unreliable Packet from client (id " +
					transmissionState.client + "): " + ex.ToString());
			}
		}

		private static void EndBroadcastSend(IAsyncResult asyncResult)
		{
			Socket broadcaster = (Socket)asyncResult.AsyncState;
			try
			{
				broadcaster.EndSendTo(asyncResult);
			}
			catch (Exception ex)
			{
				Debug.LogError("Error while sending broadcast message: " + ex.Message);
			}
		}

		//When received a broadcast message
		private static void EndBroadcastReceive(IAsyncResult asyncResult)
		{
			//Extract UnreliableReceivingBuffer from AsyncState
			BroadcastTransmissionState broadcastState = (BroadcastTransmissionState)asyncResult.AsyncState;
			Socket broadcaster = broadcastState.broadcaster;

			try
			{
				//Get data
				int receivedSize = broadcaster.EndReceiveFrom(asyncResult, ref broadcastState.senderEp);

				//Treat message
				if (receivedSize > 0)
				{
					try
					{
						//Respond to player request with server info if no callback registered
						if (m_onBroadcastPacketReceived != null)
						{
							Packet receivedPacket = new Packet(broadcastState.data, broadcastState.data.Length);
							Packet response = m_onBroadcastPacketReceived(receivedPacket);
							if (response.data.Length > 0)
							{
								if (response.flag != Packet.TypeFlag.BROADCAST)
								{
									response.OverridePacketFlag(Packet.TypeFlag.BROADCAST);
								}
								broadcaster.BeginSendTo(response.data, 0, response.data.Length, SocketFlags.None,
									broadcastState.senderEp, new AsyncCallback(EndBroadcastSend), broadcaster);
							}
						}
						else
						{
							Packet response = new Packet(NUUtilities.GetBytes(port), messageFlag: Packet.TypeFlag.BROADCAST);
							broadcaster.BeginSendTo(response.data, 0, response.data.Length, SocketFlags.None,
								broadcastState.senderEp, new AsyncCallback(EndBroadcastSend), broadcaster);
						}
					}
					catch (Exception ex)
					{
						if (ex.GetType() == typeof(ObjectDisposedException) || broadcaster == null || !broadcaster.IsBound)
							return;

						Debug.LogError("Error while responding broadcast message: " + ex.Message);
					}
				}

				//Keep receiving data forever
				broadcaster.BeginReceiveFrom(broadcastState.data, 0, broadcastState.data.Length, SocketFlags.None,
					ref broadcastState.senderEp, new AsyncCallback(EndBroadcastReceive), broadcastState);
			}
			catch (Exception ex)
			{
				if (ex.GetType() == typeof(ObjectDisposedException) || broadcaster == null || !broadcaster.IsBound)
					return;

				Debug.LogError("Error while receiving broadcast message: " + ex.Message);
			}
		}

		private static void TreatPacket(ref Packet packet, NUClientInfo senderClient)
		{
			if ((packet.flag ^ Packet.TypeFlag.DATA) == 0)
			{
				//Check if this is a sequential Packet
				if (packet.id >= 0)
				{
					object seqQueueLock;
					if (!seqDataQueueLock.TryGetValue(senderClient.guid, out seqQueueLock))
					{
						return;
					}

					lock (seqQueueLock)
					{
						LastPacketID lastId;
						if (!lastPacketIds.TryGetValue(senderClient.guid, out lastId))
						{
							return;
						}

						List<PacketClientID> seqList;
						if (!seqDataQueue.TryGetValue(senderClient.guid, out seqList))
						{
							return;
						}

						PacketClientID packetWithId = new PacketClientID(senderClient.guid, ref packet);

						//Check if it's dependency packet has been received
						if (packet.id == 0 && lastId.id != int.MaxValue)
						{
							seqList.Add(packetWithId);
						}
						else if (packet.id != (lastId.id + 1) && packet.id != 0)
						{
							seqList.Add(packetWithId);
						}
						else //If it does, enqueue it and all packets on hold
						{
							lock (dataQueueLock)
							{
								dataQueue.Enqueue(packetWithId);
								if (lastId.id == int.MaxValue) { lastId.id = -1; }
								lastId.id++;

								int counter = 0;
								PacketClientID nextPacket;
								List<PacketClientID> packetsToAdd = new List<PacketClientID>();
								while (counter < seqList.Count)
								{
									nextPacket = seqList[counter];
									if (nextPacket.packet.id == 0 && lastId.id != int.MaxValue)
									{
										counter++;
										continue;
									}
									else if (nextPacket.packet.id != (lastId.id + 1) && nextPacket.packet.id != 0)
									{
										counter++;
										continue;
									}

									packetsToAdd.Add(nextPacket);
									seqList.RemoveAt(counter);
									if (lastId.id == int.MaxValue) { lastId.id = -1; }
									lastId.id++;

									//Ensure it will go over all elements again
									counter = 0;
								}

								//Add all packets in proper order
								foreach (PacketClientID p in packetsToAdd)
								{
									dataQueue.Enqueue(p);
								}
							}
						}
					}
					return;
				}

				//Enqueue proper callbacks
				lock (dataQueueLock)
				{
					dataQueue.Enqueue(new PacketClientID(senderClient.guid, ref packet));
				}
			}
			else if ((packet.flag ^ Packet.TypeFlag.DCONNECT) == 0)
			{
				//Hold Guid because it's lost on disconnection
				Guid guid = senderClient.guid;

				//Remove from clients list
				lock (clientsLock)
				{
					clients.Remove(senderClient.guid);
				}

				//Disconnect client on main thread
				senderClient.Disconnect();

				//Release slot
				freeSlots.Add(guid);

				//Free Sequential Packet Structures
				seqDataQueue.Remove(guid);
				seqDataQueueLock.Remove(guid);
				lastPacketIds.Remove(guid);

				lock (discQueueLock)
				{
					disconnectionQueue.Enqueue(guid);
				}
			}
			else if ((packet.flag ^ Packet.TypeFlag.PING) == 0)
			{
				//Record ping value
				senderClient.EndPingMeasurement(stopwatch.ElapsedMilliseconds);
			}
			else if ((packet.flag ^ Packet.TypeFlag.GUID) == 0)
			{
				//Check if Client is already connected
				Guid guid = NUUtilities.GetGuid(packet.GetCleanData());

				//Reconnection Test
				bool reconnection = true;
				NUClientInfo client;
				lock (clientsLock)
				{
					if (clients.TryGetValue(guid, out client) && !client.connected)
					{
						//Override GUID with Client one
						Debug.Log("This Client is reconnecting!");

						//Hold old UDP port offset to use again
						senderClient.portOffset = client.portOffset;

						//Disconnect old sockets and null stuff
						client.Disconnect();

						//Override client connection with current one but update GUID
						senderClient.guid = guid;
						clients[guid] = client = senderClient;
					}
					else //New Client
					{
						client = senderClient;
						//Debug.Log("New Client! Sending connect info:\n" +
						//    "GUID: " + client.id + "; Port Offset: " + client.portOffset);

						//Add Client into the hashmap
						clients.Add(client.guid, client);

						//Mark this connection as a new connection
						reconnection = false;
					}
				}

				//Setup Last Packet ID
				object seqQueueLock;
				if (!seqDataQueueLock.TryGetValue(senderClient.guid, out seqQueueLock))
				{
					seqQueueLock = new object();
				}
				lock (seqQueueLock)
				{
					LastPacketID lastId;
					if (!lastPacketIds.TryGetValue(client.guid, out lastId))
					{
						lastId = new LastPacketID(packet.id);
						lastPacketIds.Add(client.guid, lastId);
					}
					lastId.id = packet.id;
				}

				//Send GUID and Port Offset to Client
				Packet guidPacket = new Packet(NUUtilities.GetGuidAndPortBuffer(client.guid, (ushort)client.portOffset),
					null, Packet.TypeFlag.GUID, NUUtilities.GeneratePacketId(client.guid));
				TcpTransmissionState guidState = new TcpTransmissionState(0, ref client.tcpClient, client, null);
				client.tcpClient.GetStream().BeginWrite(guidPacket.data, 0, guidPacket.data.Length,
					EndReliableSend, guidState);

				//============================================
				//==========Setup Unreliable channel==========
				//============================================
				IPEndPoint localClientEP = (IPEndPoint)client.tcpClient.Client.LocalEndPoint;
				IPEndPoint clientEP = (IPEndPoint)client.tcpClient.Client.RemoteEndPoint;

				//Bind UDP Client to local endpoint with new port
				client.udpClient.Bind(new IPEndPoint(localClientEP.Address, localClientEP.Port + client.portOffset));
				client.udpClient.Connect(clientEP); //Connect it to the client address

				UdpTransmissionState udpReceivingState = new UdpTransmissionState(NUUtilities.MTU, ref client.udpClient, client);
				client.udpClient.BeginReceive(udpReceivingState.data, 0, NUUtilities.MTU, SocketFlags.None,
					new AsyncCallback(EndUnreliableReceive), udpReceivingState);

				//Set client as connected
				client.connected = true;

				//Handle callbacks on proper time!
				//Didn't perform this calls before because of logic flow!
				if (reconnection)
				{
					lock (reconnQueueLock)
					{
						reconnectionQueue.Enqueue(client.guid);
					}
				}
				else
				{
					lock (connQueueLock)
					{
						connectionQueue.Enqueue(client.guid);
					}
				}
			}
			else if ((packet.flag ^ Packet.TypeFlag.MPARTDATA) == 0)
			{
				//Get MultiPart packet Hash and ID
				Hash hash;
				byte[] packetData = packet.GetCleanData();
				int partId = NUUtilities.GetHashAndPartId(packetData, out hash);

				//Check if there is a multipart packet registration
				MultiPartBuffer multiPartBuffer;
				lock (multiPartLock)
				{
					//Create a new multipart packet registration if not already
					if (!multiPartBuffers.TryGetValue(hash, out multiPartBuffer))
					{
						byte[] bufferSizeData = new byte[4];
						Array.Copy(packetData, 20, bufferSizeData, 0, 4);
						int bufferSize = NUUtilities.GetInt32(bufferSizeData);

						byte[] packetCountData = new byte[4];
						Array.Copy(packetData, 24, packetCountData, 0, 4);
						int packetCount = NUUtilities.GetInt32(packetCountData);

						multiPartBuffer = new MultiPartBuffer(packetCount, bufferSize, hash);
						multiPartBuffers.Add(hash, multiPartBuffer);
						//Debug.Log("New MultiPart Packet: " + hash);
					}

					//Merge received data
					multiPartBuffer.MergePacketData(partId, packetData);

					//Debug.LogFormat("MultiPart Packet ({0}) received part{1}: {2}", hash, partId, packet.hash);

					//Assemble Original Packet and Enqueu it!
					if (multiPartBuffer.remainingSize == 0)
					{
						Packet originalPacket;
						if (multiPartBuffer.GetOriginalPacket(out originalPacket))
						{
							Guid guid = senderClient.guid;
							//Debug.LogFormat("Multipart Packet Received from Guid {0}!", guid);
							lock (dataQueueLock)
							{
								dataQueue.Enqueue(new PacketClientID(guid, ref originalPacket));
							}
							multiPartBuffers.Remove(hash);
						}
					}
				}
			}
		}

		/// <summary>
		/// Closes the server and disconnect all clients
		/// </summary>
		public static void Shutdown()
		{
			if (started)
			{
				Debug.Log("Server shutting down...");

				//Clear queues
				lock (connQueueLock) { connectionQueue.Clear(); }
				lock (reconnQueueLock) { reconnectionQueue.Clear(); }
				lock (timeOutQueueLock) { timeOutQueue.Clear(); }
				lock (discQueueLock) { disconnectionQueue.Clear(); }
				lock (dataQueueLock) { dataQueue.Clear(); }

				//Clear Multipart Control structure
				lock (multiPartLock) { multiPartBuffers.Clear(); }

				//Stop from listening to new connections
				listener.Stop();

				//Stop broadcasting socket
				broadcaster.Close();

				//Destroy Server Component
				GameObject.Destroy(serverComponent.gameObject);
				serverComponent = null;

				//Disconnect all clients
				lock (clientsLock)
				{
					Packet discPacket = new Packet("Server Shutdown...", GetConnectedClients(), Packet.TypeFlag.DCONNECT);
					foreach (var kv in clients)
					{
						/*Send disconnect packet with blockable function call
						 * to make sure it ends before application exit!*/
						NUClientInfo client = kv.Value;
						if (client.connected)
						{
							client.tcpClient.Client.Send(discPacket.data);
							client.Disconnect();

							//Do proper callback directly (this is the users thread!)
							if (m_onClientDisconnected != null)
								m_onClientDisconnected(kv.Key);
						}
					}
				}

				//Clear the slots list
				freeSlots.Clear();

				//Stop stopwatch
				stopwatch.Stop();
				stopwatch = null;

				//Reset started flag
				started = false;

				Debug.Log("Server closed!");
			}
		}

		//----------------------------------------------------------
		//--------------------Auxiliar Functions--------------------
		//----------------------------------------------------------

		/// <summary>
		/// Returns an array of client ids from the clients who are connected.
		/// </summary>
		/// <returns>Array of client ids (<see cref="Guid"/>).</returns>
		public static Guid[] GetConnectedClients()
		{
			if (!started)
			{
				Debug.LogError("Can't get connected clients of offline server!");
				return null;
			}

			List<Guid> clientIds;
			lock (clientsLock)
			{
				clientIds = new List<Guid>(clients.Count);
				foreach (KeyValuePair<Guid, NUClientInfo> kv in clients)
				{
					if (kv.Value.Connected())
						clientIds.Add(kv.Key);
				}
			}


			return clientIds.ToArray();
		}

		/// <summary>
		/// Get <see cref="NUClientInfo"/> from a given client with ID <paramref name="clientId"/>.
		/// </summary>
		/// <param name="clientId">The <see cref="Guid"/> of the client whose info is to be returned.</param>
		/// <param name="clientInfo">The output variable with the client info for the given <see cref="Guid"/>.</param>
		/// <returns>If a client with such <paramref name="clientId"/> was found.</returns>
		public static bool GetClientInfo(Guid clientId, out NUClientInfo clientInfo)
		{
			bool found = false;
			lock (clientsLock)
			{
				found = clients.TryGetValue(clientId, out clientInfo);
			}
			return found;
		}

		internal static void PingAllClients()
		{
			if (!started)
				return;

			Guid[] connectedClients = GetConnectedClients();

			lock (clientsLock)
			{
				for (int i = 0; i < connectedClients.Length; i++)
				{
					Guid id = connectedClients[i];
					Packet pingPacket = new Packet(clients[id].ping, new Guid[] { id }, Packet.TypeFlag.PING);
					SendUnreliable(pingPacket, () =>
					{
						//Debug.Log("Should ping...");
						clients[id].StartPingMeasurement(stopwatch.ElapsedMilliseconds);
					});
				}
			}
		}

		internal static void ProcessQueues()
		{
			//Callbacks Sanitize
			m_onBroadcastPacketReceived = (BroadcastPacketCallback)NUUtilities.SanitizeDelegate(
				m_onBroadcastPacketReceived);
			m_onClientConnected = NUUtilities.SanitizeAction(
				m_onClientConnected);
			m_onClientDisconnected = NUUtilities.SanitizeAction(
				m_onClientDisconnected);
			m_onClientPacketReceived = NUUtilities.SanitizeAction(
				m_onClientPacketReceived);
			m_onClientReconnected = NUUtilities.SanitizeAction(
				m_onClientReconnected);
			m_onClientTimedOut = NUUtilities.SanitizeAction(
				m_onClientTimedOut);
			m_onServerStarted = NUUtilities.SanitizeAction(
				m_onServerStarted);

			if (!started)
				return;

			lock (timeOutQueueLock)
			{
				while (timeOutQueue.Count > 0)
				{
					Guid id = timeOutQueue.Dequeue();

					//Do proper callback
					if (m_onClientTimedOut != null)
						m_onClientTimedOut(id);
				}
			}

			lock (discQueueLock)
			{
				while (disconnectionQueue.Count > 0)
				{
					Guid id = disconnectionQueue.Dequeue();

					//Do proper callback
					if (m_onClientDisconnected != null)
						m_onClientDisconnected(id);
				}
			}

			lock (connQueueLock)
			{
				while (connectionQueue.Count > 0)
				{
					Guid id = connectionQueue.Dequeue();

					//Do proper callback
					if (m_onClientConnected != null)
						m_onClientConnected(id);
				}
			}

			lock (reconnectionQueue)
			{
				while (reconnectionQueue.Count > 0)
				{
					Guid id = reconnectionQueue.Dequeue();

					//Do proper callback
					if (m_onClientReconnected != null)
						m_onClientReconnected(id);
				}
			}

			lock (dataQueueLock)
			{
				while (dataQueue.Count > 0)
				{
					PacketClientID packetAndId = dataQueue.Dequeue();

					//Do proper callback
					if (m_onClientPacketReceived != null)
						m_onClientPacketReceived(packetAndId.id, packetAndId.packet);
				}
			}
		}

	}

}
