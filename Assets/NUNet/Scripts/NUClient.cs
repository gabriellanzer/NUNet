
//STD Includes
using System;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Threading;
using Stopwatch = System.Diagnostics.Stopwatch;

//Unity Includes
using UnityEngine;

//NUNet Includes
using NUNet.Internal;
using UnityEngine.UIElements;

namespace NUNet
{
	public static class NUClient
	{
		//Client instance
		public static NUClientInfo client;

		//Client Interface Calls
		public static Guid guid { get { return client.guid; } private set { } }

		//Transmission callbacks
		private static Action m_onConnected;
		public static Action onConnected
		{
			get { return m_onConnected; }
			set { m_onConnected = NUUtilities.SanitizeAction(value); }
		}

		private static Action m_onConnectionFailed;
		public static Action onConnectionFailed
		{
			get { return m_onConnectionFailed; }
			set { m_onConnectionFailed = NUUtilities.SanitizeAction(value); }
		}

		private static Action m_onDisconnected;
		public static Action onDisconnected
		{
			get { return m_onDisconnected; }
			set { m_onDisconnected = NUUtilities.SanitizeAction(value); }
		}

		private static Action<string> m_onServerDisconnected;
		public static Action<string> onServerDisconnected
		{
			get { return m_onServerDisconnected; }
			set { m_onServerDisconnected = NUUtilities.SanitizeAction(value); }
		}

		private static Action m_onConnectionTimeout;
		public static Action onConnectionTimeout
		{
			get { return m_onConnectionTimeout; }
			set { m_onConnectionTimeout = NUUtilities.SanitizeAction(value); }
		}

		private static Action<Packet> m_onPacketReceived;
		public static Action<Packet> onPacketReceived
		{
			get { return m_onPacketReceived; }
			set { m_onPacketReceived = NUUtilities.SanitizeAction(value); }
		}

		private static Action<BroadcastPacket> m_onBroadcastResponse;
		public static Action<BroadcastPacket> onBroadcastResponse
		{
			get { return m_onBroadcastResponse; }
			set { m_onBroadcastResponse = NUUtilities.SanitizeAction(value); }
		}

		//Pool of received Packets
		private static readonly object dataQueueLock;
		private static Queue<Packet> dataQueue;
		private static readonly object seqDataQueueLock;
		private static List<Packet> seqDataList;
		private static readonly object broadcastDataQueueLock;
		private static Queue<BroadcastPacket> broadcastDataQueue;

		//Flags for thread-safe operations
		private static bool calledDisconnect;
		private static bool serverDisconnected;
		private static bool hasDisconnected;
		private static bool hasConnected;

		//Disconnection messages
		private static string serverDisconnectMsg = string.Empty;

		//Index of the last received sequential packet
		private static int lastPacketId;

		//Multipart Control Structure
		private static readonly object multiPartLock;
		private static Dictionary<Hash, MultiPartBuffer> multiPartBuffers;

		//UDP Socket handler for Broadcasting operations
		private static Socket broadcaster = null;
		private static UInt32 broadcastStartRange;
		private static UInt32 broadcastFinalRange;
		private static ushort broadcastServerPort = 0;

		//MonoBehaviour instance of client component
		private static NUClientComponent clientComponent;

		static NUClient()
		{
			client = null;
			m_onConnected = null;
			m_onConnectionTimeout = null;
			m_onPacketReceived = null;
			dataQueue = null;
			seqDataList = null;
			broadcastDataQueue = null;
			hasDisconnected = false;
			hasConnected = false;
			dataQueueLock = new object();
			seqDataQueueLock = new object();
			broadcastDataQueueLock = new object();
			multiPartBuffers = null;
			multiPartLock = new object();
			clientComponent = null;
			broadcaster = null;
			broadcastServerPort = 0;
			lastPacketId = -1;
		}

		/// <summary>
		/// Connection flag
		/// </summary>
		public static bool connected
		{
			get
			{
				if (client != null)
					return client.connected;
				else
					return false;
			}
			private set { }
		}

		/// <summary>
		/// Ping related by the server
		/// </summary>
		public static int ping
		{
			get { return client.ping; }
			private set { }
		}

		/// <summary>
		/// Connect to server on given IP string and port (default 25565)
		/// </summary>
		/// <param name="address"></param>
		/// <param name="port"></param>
		/// <param name="updateHook"></param>
		public static void Connect(string address, ushort port = 25565, Action updateHook = null,
			int reservedBufferedPackets = NUUtilities.MaxBufferedPackets)
		{
			IPAddress ipAddress;
			if (IPAddress.TryParse(address, out ipAddress))
			{
				Connect(ipAddress, port, updateHook, reservedBufferedPackets);
			}
			else
			{
				Debug.LogError("Could not parse IP " + address + "!");
			}
		}

		/// <summary>
		/// Connect to server on given IP (default loopback) and port (default 25565)
		/// </summary>
		/// <param name="address"></param>
		/// <param name="port"></param>
		/// <param name="updateHook"></param>
		public static void Connect(IPAddress address = null, ushort port = 25565, Action updateHook = null,
			int reservedBufferedPackets = NUUtilities.MaxBufferedPackets)
		{
			if (!connected)
			{
				//Loopback address if none are given
				if (address == null)
					address = IPAddress.Loopback;

				//Create tcpClient
				TcpClient tcpClient = new TcpClient(AddressFamily.InterNetwork);
				tcpClient.ReceiveBufferSize = 1048576;  //1MB
				tcpClient.SendBufferSize = 1048576;     //1MB

				Debug.Log("Trying to connect to server at " + address.ToString() + ":" + port);

				//Initialize data queues
				dataQueue = new Queue<Packet>(reservedBufferedPackets);
				seqDataList = new List<Packet>();
				lastPacketId = -1;
				multiPartBuffers = new Dictionary<Hash, MultiPartBuffer>(reservedBufferedPackets, Hash.comparer);

				//Hook on updateHook otherwise instantiate NUClientComponent
				if (updateHook != null)
				{
					updateHook += ProcessQueues;
				}
				else
				{
					//Create MonoBehaviour instance if it doesn't exists
					if (clientComponent == null)
					{
						GameObject clientObject = new GameObject("NUClientObject");
						//clientObject.hideFlags = HideFlags.HideAndDontSave;
						GameObject.DontDestroyOnLoad(clientObject);
						clientComponent = clientObject.AddComponent<NUClientComponent>();
						//clientObject.hideFlags = HideFlags.HideInInspector;
					}
				}

				try
				{
					//Begin connecting to server
					tcpClient.BeginConnect(address, port, new AsyncCallback(EndConnect), tcpClient);
				}
				catch (Exception ex)
				{
					if (clientComponent != null)
						GameObject.DestroyImmediate(clientComponent);

					Debug.LogError("Error while connecting to server: " + ex.ToString());
				}
			}
			else
			{
				Debug.LogError("Already Connected to server!");
			}
		}

		/// <summary>
		/// Disconnects from server
		/// </summary>
		public static void Disconnect()
		{
			if (!connected)
				return;

			//Send blockable reliable message (because the app might be about to quit!)
			try
			{
				Packet packet = new Packet("Disconnecting!", null, Packet.TypeFlag.DCONNECT);
				client.tcpClient.GetStream().Write(packet.data, 0, packet.data.Length);
			}
			catch (Exception ex)
			{
				Debug.LogError("Error sending Disconnect Message to Server: " + ex.ToString());
			}

			calledDisconnect = true;
			hasDisconnected = true;
		}

		private static void EndConnect(IAsyncResult asyncResult)
		{
			//Extract TcpClient from AsyncState
			TcpClient tcpClient = (TcpClient)asyncResult.AsyncState;

			//Try to connect to the given EndPoint
			try
			{
				//Finish connecting
				tcpClient.EndConnect(asyncResult);

				//Get Client EndPoint
				IPEndPoint localEndPoint = (IPEndPoint)tcpClient.Client.LocalEndPoint;
				IPEndPoint remoteEndPoint = (IPEndPoint)tcpClient.Client.RemoteEndPoint;

				//Create UdpClient
				Socket udpClient = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp)
				{
					ExclusiveAddressUse = true
				};
				udpClient.Bind(localEndPoint); //Bind it locally to the TCP Address

				//Create NUClientInfo if it doesn't exists already
				if (client == null)
				{
					client = new NUClientInfo(Guid.Empty, localEndPoint.Address, ref tcpClient, ref udpClient);
				}
				else
				{
					//Override information with last client id but current addresses and sockets
					client = new NUClientInfo(client.guid, localEndPoint.Address, ref tcpClient, ref udpClient);
				}

				//Debug.Log("Connected to server (" + remoteEndPoint.Address + ":" + remoteEndPoint.Port + ")!" +
				//    "\nSending GUID " + client.id + " and Waiting for response...");

				//Send Current GUID - Null if this is not a reconnect
				Packet guidPacket = new Packet(client.guid.ToByteArray(), null, Packet.TypeFlag.GUID, NUUtilities.GeneratePacketId());
				TcpTransmissionState transmissionState = new TcpTransmissionState(guidPacket.data.Length, ref tcpClient, client, null);
				tcpClient.GetStream().BeginWrite(guidPacket.data, 0, guidPacket.data.Length,
					new AsyncCallback(EndReliableSend), transmissionState);

				//Start receiving messages from server
				TcpTransmissionState tcpTransmissionState = new TcpTransmissionState(
					NUUtilities.MTU, ref tcpClient, client, null);
				tcpClient.GetStream().BeginRead(tcpTransmissionState.data, 0, NUUtilities.MTU,
					new AsyncCallback(EndReliableReceive), tcpTransmissionState);
				UdpTransmissionState udpTransmissionState = new UdpTransmissionState(
					NUUtilities.MTU, ref udpClient, client, null);
				udpClient.BeginReceive(udpTransmissionState.data, 0, NUUtilities.MTU, SocketFlags.None,
					new AsyncCallback(EndUnreliableReceive), udpTransmissionState);

				Debug.Log("Connected to Server!");

			}
			catch (Exception ex)
			{
				//Setup disconnection flag
				hasDisconnected = true;

				Debug.LogError("Could not connect to Server! " + ex.ToString());

				return;
			}
		}

		/// <summary>
		/// Setup the broadcast channel for the given adapter address and port.
		/// </summary>
		/// <param name="adapterAddress">The Network Adapter address to be used for broadcasting.
		/// See <see cref="NUUtilities.ListIPv4Addresses()"/> for valid sources.</param>
		/// <param name="broadcastPort">The port in which the broadcast will happen.</param>
		public static void SetupBroadcast(IPAddress adapterAddress = null, ushort broadcastServerPort = 56552, Action updateHook = null,
			int reservedBufferedPackets = NUUtilities.MaxBufferedPackets)
		{
			//Loopback address if none are given
			if (adapterAddress == null)
				adapterAddress = IPAddress.Loopback;
			NUClient.broadcastServerPort = broadcastServerPort;

			// Setup broadcast ranges and receiving queue
			IPAddress subnet = NUUtilities.GetSubnetMaskFromIPv4(adapterAddress);
			UInt32 subnetInt = NUUtilities.GetUIntFromIpAddress(subnet);
			UInt32 addressInt = NUUtilities.GetUIntFromIpAddress(adapterAddress);
			broadcastStartRange = (addressInt & subnetInt) + 1;
			broadcastFinalRange = (addressInt | (~subnetInt)) - 1;
			lock (broadcastDataQueueLock)
			{
				broadcastDataQueue = new Queue<BroadcastPacket>(reservedBufferedPackets);
			}
			IPEndPoint broadcastEp = new IPEndPoint(adapterAddress, 0);
			broadcaster = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp)
			{
				ExclusiveAddressUse = true
			};
			broadcaster.Bind(broadcastEp);
			BroadcastTransmissionState broadcastState = new BroadcastTransmissionState(
					NUUtilities.MTU, ref broadcaster, null);
			EndPoint broadcastSenderEp = broadcastState.senderEp;
			broadcaster.BeginReceiveFrom(broadcastState.data, 0, NUUtilities.MTU, SocketFlags.None,
				ref broadcastSenderEp, new AsyncCallback(EndBroadcastReceive), broadcastState);

			//Hook on updateHook otherwise instantiate NUClientComponent
			if (updateHook != null)
			{
				updateHook += ProcessQueues;
			}
			else
			{
				//Create MonoBehaviour instance if it doesn't exists
				if (clientComponent == null)
				{
					GameObject clientObject = new GameObject("NUClientObject");
					//clientObject.hideFlags = HideFlags.HideAndDontSave;
					GameObject.DontDestroyOnLoad(clientObject);
					clientComponent = clientObject.AddComponent<NUClientComponent>();
					//clientObject.hideFlags = HideFlags.HideInInspector;
				}
			}

		}

		/// <summary>
		/// Broadcast a Packet (whose destination doesn't matter)
		/// </summary>
		/// <param name="emptyDestinationPacket">An packet whose destination doesn't matter</param>
		public static void Broadcast(Packet emptyDestinationPacket, Action callback = null)
		{
			if (broadcaster == null)
			{
				Debug.LogWarning("Broadcast not configured! Using default settings...\n" +
					"Call NUClient.SetupBroadcast() with corrent parameters.");
				SetupBroadcast();
			}

			if (emptyDestinationPacket.data == null)
			{
				Debug.LogError("Data to be sent is null!");
				return;
			}

			if (emptyDestinationPacket.data.Length == 0)
			{
				Debug.LogError("Data to be sent is empty!");
				return;
			}

			// Set Broadcast Flag if not already
			if (emptyDestinationPacket.flag != Packet.TypeFlag.BROADCAST)
			{
				emptyDestinationPacket.OverridePacketFlag(Packet.TypeFlag.BROADCAST);
			}

			BroadcastTransmissionState transmissionState = new BroadcastTransmissionState(
				emptyDestinationPacket.data.Length, ref broadcaster, callback);

			// Send a broadcast transmission to the full range of IPs range
			IPEndPoint receivingEp = (IPEndPoint)broadcaster.LocalEndPoint;
			for (uint ip = broadcastStartRange + 1; ip < broadcastFinalRange; ip++)
			{
				IPAddress broadcastIp = NUUtilities.GetIpAddressFromUInt32(ip);
				IPEndPoint broadcastEp = new IPEndPoint(broadcastIp, broadcastServerPort);

				broadcaster.BeginSendTo(emptyDestinationPacket.data, 0, emptyDestinationPacket.data.Length,
					SocketFlags.None, broadcastEp, new AsyncCallback(EndBroadcastSend), transmissionState);
			}
		}

		//When finished broadcast send
		private static void EndBroadcastSend(IAsyncResult asyncResult)
		{
			//Extract UnreliableTransmissionState from Async State
			BroadcastTransmissionState transmissionState = (BroadcastTransmissionState)asyncResult.AsyncState;
			Socket broadcaster = transmissionState.broadcaster;

			try
			{
				broadcaster.EndSend(asyncResult);

				//Callback of transmission state
				if (transmissionState.callback != null)
					transmissionState.callback();
			}
			catch (Exception ex)
			{
				if (ex.GetType() == typeof(ObjectDisposedException) || broadcaster == null || !broadcaster.IsBound)
					return;

				Debug.LogError("Error sending unreliable packet to server: " + ex);
			}
		}

		private static void EndBroadcastReceive(IAsyncResult asyncResult)
		{
			//Extract UDPClient from Async State
			BroadcastTransmissionState broadcastState = (BroadcastTransmissionState)asyncResult.AsyncState;
			Socket broadcaster = broadcastState.broadcaster;

			try
			{
				//End receiving data
				int receivedSize = broadcaster.EndReceiveFrom(asyncResult, ref broadcastState.senderEp);

				if (receivedSize > 0)
				{
					//Get Message flag and process accordingly
					Packet receivedPacket = new Packet(broadcastState.data, receivedSize);

					//Ignore packets that are not valid in our broadcast routines
					if (receivedPacket.flag == Packet.TypeFlag.BROADCAST)
					{
						BroadcastPacket broadcastPacket = new BroadcastPacket(receivedPacket, 
							(IPEndPoint)broadcastState.senderEp);
						lock (broadcastDataQueueLock)
						{
							broadcastDataQueue.Enqueue(broadcastPacket);
						}
					}
				}

				//Keep receiving data
				broadcaster.BeginReceiveFrom(broadcastState.data, 0, NUUtilities.MTU, SocketFlags.None,
					ref broadcastState.senderEp, new AsyncCallback(EndBroadcastReceive), broadcastState);

			}
			catch (Exception ex)
			{
				if (ex.GetType() == typeof(ObjectDisposedException) || broadcaster == null || !broadcaster.IsBound)
					return;

				Debug.LogError("Error occurred receiving broadcast packet: " + ex.Message);

				//Keep receiving data (because one connection error should not eliminate other responses)
				broadcaster.BeginReceiveFrom(broadcastState.data, 0, NUUtilities.MTU, SocketFlags.None,
					ref broadcastState.senderEp, new AsyncCallback(EndBroadcastReceive), broadcastState);
			}
		}

		public static void FinishBroadcast()
		{
			broadcaster.Close();
			lock (broadcastDataQueueLock)
			{
				if (broadcastDataQueue != null)
				{
					broadcastDataQueue.Clear();
				}
				broadcastDataQueue = null;
			}
		}

		private static void EndReliableReceive(IAsyncResult asyncResult)
		{
			//Extract TransmissionState from Async State
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
					TreatPacket(ref packet);

					//Reset transmissionState offset
					transmissionState.offset = 0;

					//Increment packet iterator
					packetItt += packetSize;
				}

				//Keep receiving data
				if (tcpClient.Connected)
				{
					stream.BeginRead(transmissionState.data, 0, NUUtilities.MTU,
						new AsyncCallback(EndReliableReceive), transmissionState);
				}
				else
				{
					hasDisconnected = true;
				}
			}
			catch (Exception ex)
			{
				if (!client.connected || !tcpClient.Connected)
					return;

				Debug.LogError("Error occurred receiving reliable packet from server: " + ex.ToString());

				//Do proper disconnection handling
				hasDisconnected = true;
			}
		}

		private static void EndUnreliableReceive(IAsyncResult asyncResult)
		{
			//Extract UDPClient from Async State
			UdpTransmissionState transmissionState = (UdpTransmissionState)asyncResult.AsyncState;
			Socket udpClient = transmissionState.socket;

			try
			{
				//End receiving data
				int receivedSize = udpClient.EndReceive(asyncResult);

				if (receivedSize > 0)
				{
					//Get Message flag and process accordingly
					Packet receivedPacket = new Packet(transmissionState.data, receivedSize);

					//Do proper handling
					TreatPacket(ref receivedPacket);

					//Debug.Log("Message of type " + receivedPacket.messageFlag + " received from server - " + receivedPacket.ToString());
				}

				//Keep receiving data
				udpClient.BeginReceive(transmissionState.data, 0, NUUtilities.MTU, SocketFlags.None,
					new AsyncCallback(EndUnreliableReceive), transmissionState);

			}
			catch (Exception ex)
			{
				if (!client.connected || !client.tcpClient.Connected || !client.udpClient.Connected)
					return;

				Debug.LogError("Error occurred receiving unreliable packet from server: " + ex.Message);
			}
		}

		public static void SendReliable(Packet packet, Action callback = null)
		{
			if (!connected || !client.tcpClient.Connected)
			{
				Debug.LogError("Can't send Packet while not connected!");
				return;
			}

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

			try
			{
				TcpTransmissionState transmissionState = new TcpTransmissionState(packet.data.Length,
					ref client.tcpClient, client, callback);
				client.tcpClient.GetStream().BeginWrite(packet.data, 0, packet.data.Length,
					new AsyncCallback(EndReliableSend), transmissionState);
			}
			catch (Exception ex)
			{
				if (!client.connected || !client.tcpClient.Connected)
					return;

				Debug.LogError("Error sending reliable Packet to Server: " + ex.ToString());

				//Do proper disconnection handling
				hasDisconnected = true;

			}
		}

		/// <summary>
		/// Send a Packet through a reliable but latency prone way with sequence guarantee.
		/// <para>This call might have more latency then <see cref="SendReliable(Packet, Action)"/>.</para>
		/// </summary>
		/// <param name="packet"></param>
		public static void SendReliableSequenced(Packet packet, Action callback = null)
		{
			if (packet.id == -1)
			{
				packet.OverrideID(NUUtilities.GeneratePacketId());
			}

			SendReliable(packet, callback);
		}

		public static void SendUnreliable(Packet packet, Action callback = null)
		{
			if (!connected || !client.tcpClient.Connected)
			{
				Debug.LogError("Can't send Packet while not connected!");
				return;
			}

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

			if (packet.data.Length > NUUtilities.MTU)
			{
				Debug.LogErrorFormat("Cannot send Packet with size ({0}) bigger then {1} bytes" +
					" through unreliable channel! Use SendReliable instead.",
					packet.data.Length, (NUUtilities.MTU - 1));
				return;
			}

			try
			{
				UdpTransmissionState transmissionState = new UdpTransmissionState(packet.data.Length,
					ref client.udpClient, client, callback);
				client.udpClient.BeginSend(packet.data, 0, packet.data.Length, SocketFlags.None,
					new AsyncCallback(EndUnreliableSend), transmissionState);
			}
			catch (Exception ex)
			{
				if (!client.connected || !client.tcpClient.Connected)
					return;

				Debug.LogError("Error sending unreliable Packet to Server: " + ex.ToString());
			}
		}

		private static void EndReliableSend(IAsyncResult asyncResult)
		{
			//Extract ReliableTransmissionState from Async State
			TcpTransmissionState transmissionState = (TcpTransmissionState)asyncResult.AsyncState;
			TcpClient tcpClient = transmissionState.tcpClient;
			try
			{
				NetworkStream stream = tcpClient.GetStream();
				stream.EndWrite(asyncResult);

				//Callback of transmission state
				if (transmissionState.callback != null)
					transmissionState.callback();
			}
			catch (Exception ex)
			{
				if (!client.connected || !client.tcpClient.Connected)
					return;

				Debug.LogError("Error sending reliable packet to server: " + ex);
			}
		}

		private static void EndUnreliableSend(IAsyncResult asyncResult)
		{
			//Extract UnreliableTransmissionState from Async State
			UdpTransmissionState transmissionState = (UdpTransmissionState)asyncResult.AsyncState;
			Socket udpClient = transmissionState.socket;

			try
			{
				udpClient.EndSend(asyncResult);

				//Callback of transmission state
				if (transmissionState.callback != null)
					transmissionState.callback();
			}
			catch (Exception ex)
			{
				if (!client.connected || !client.tcpClient.Connected)
					return;

				Debug.LogError("Error sending unreliable packet to server: " + ex);
			}
		}

		/// <summary>
		/// Processa all available sequential packets regarding the <see cref="lastPacketId"/>
		/// who are to be enqueued for the packet <see cref="dataQueue"/>.
		/// </summary>
		/// <returns>The list of Packets to be added to the <see cref="dataQueue"/>.</returns>
		private static List<Packet> ProcessSequentialPackets()
		{
			int counter = 0;
			Packet nextPacket;
			List<Packet> packetsToAdd = new List<Packet>();
			while (counter < seqDataList.Count)
			{
				nextPacket = seqDataList[counter];
				if (nextPacket.id == 0 && lastPacketId != int.MaxValue)
				{
					counter++;
					continue;
				}
				else if (nextPacket.id != (lastPacketId + 1) && nextPacket.id != 0)
				{
					counter++;
					continue;
				}

				packetsToAdd.Add(nextPacket);
				seqDataList.RemoveAt(counter);
				if (lastPacketId == int.MaxValue) { lastPacketId = -1; }
				lastPacketId++;

				//Ensure it will go over all elements again
				counter = 0;
			}
			return packetsToAdd;
		}

		private static void TreatPacket(ref Packet packet)
		{
			if ((packet.flag ^ Packet.TypeFlag.DATA) == 0)
			{
				//Check if this is a sequential Packet
				if (packet.id >= 0)
				{
					lock (seqDataQueueLock)
					{
						//Treat out-of-order server packets that arrive
						//before GUID transaction is done
						if (lastPacketId == -1)
						{
							seqDataList.Add(packet);
							return;
						}

						//Check if it's dependency packet has been received
						if (packet.id == 0 && lastPacketId != int.MaxValue)
						{
							seqDataList.Add(packet);
						}
						else if (packet.id != (lastPacketId + 1) && packet.id != 0)
						{
							seqDataList.Add(packet);
						}
						else //If it does, enqueue it and all packets on hold
						{
							lock (dataQueueLock)
							{
								dataQueue.Enqueue(packet);
							}
							if (lastPacketId == int.MaxValue) { lastPacketId = -1; }
							lastPacketId++;

							//Add all packets in proper order
							List<Packet> packetsToAdd = ProcessSequentialPackets();
							lock (dataQueueLock)
							{
								foreach (Packet p in packetsToAdd)
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
					dataQueue.Enqueue(packet);
				}
			}
			else if ((packet.flag ^ Packet.TypeFlag.GUID) == 0)
			{
				//Setup Last Packet ID
				lock (seqDataQueueLock)
				{
					lastPacketId = packet.id;

					//Process out-of-order messages
					List<Packet> packetsToAdd = ProcessSequentialPackets();
					lock (dataQueueLock)
					{
						foreach (Packet p in packetsToAdd)
						{
							dataQueue.Enqueue(p);
						}
					}
				}

				//Set GUID given from the server
				int udpPortOffset = NUUtilities.GetGuidAndPort(packet.GetCleanData(), out client.guid);

				//Debug.LogFormat("Received GUID ({0}) and Port ({1}) from server!", client.guid, udpPortOffset);

				//Connect UDP client to correct port
				IPEndPoint remoteEndPoint = (IPEndPoint)client.tcpClient.Client.RemoteEndPoint;
				IPEndPoint endPoint = new IPEndPoint(remoteEndPoint.Address, remoteEndPoint.Port + udpPortOffset);
				client.udpClient.Connect(endPoint);
				client.connected = true;

				//Set hasConnected flag
				hasConnected = true;
			}
			else if ((packet.flag ^ Packet.TypeFlag.DCONNECT) == 0)
			{
				serverDisconnectMsg = packet.GetMessageData();
				//Set hasDisconnected flag
				hasDisconnected = true;
				serverDisconnected = true;
			}
			else if ((packet.flag ^ Packet.TypeFlag.PING) == 0)
			{
				//Update ping value
				client.OverridePing(NUUtilities.GetInt32(packet.GetCleanData()));
				Packet pingPacket = new Packet("", null, Packet.TypeFlag.PING);
				if (connected) SendUnreliable(pingPacket);
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
					}
				}

				//Merge received data
				multiPartBuffer.MergePacketData(partId, packetData);

				//Assemble Original Packet and Enqueu it!
				if (multiPartBuffer.remainingSize == 0)
				{
					Packet originalPacket;
					if (multiPartBuffer.GetOriginalPacket(out originalPacket))
					{
						//Debug.LogFormat("Multipart Packet Received from Server!");
						lock (dataQueueLock)
						{
							dataQueue.Enqueue(originalPacket);
						}
						lock (multiPartLock)
						{
							multiPartBuffers.Remove(hash);
						}
					}
				}
			}
		}

		internal static void ProcessQueues()
		{
			m_onBroadcastResponse = NUUtilities.SanitizeAction(m_onBroadcastResponse);
			m_onConnected = NUUtilities.SanitizeAction(m_onConnected);
			m_onConnectionFailed = NUUtilities.SanitizeAction(m_onConnectionFailed);
			m_onDisconnected = NUUtilities.SanitizeAction(m_onDisconnected);
			m_onServerDisconnected = NUUtilities.SanitizeAction(m_onServerDisconnected);
			m_onConnectionTimeout = NUUtilities.SanitizeAction(m_onConnectionTimeout);
			m_onPacketReceived = NUUtilities.SanitizeAction(m_onPacketReceived);

			if (hasDisconnected)
			{
				//Reset flag
				hasDisconnected = false;

				bool connectionFailed = !connected;

				//Destroy client component object
				if (clientComponent != null)
				{
					GameObject.Destroy(clientComponent.gameObject);
					clientComponent = null;
				}

				//Clear dataQueue
				dataQueue.Clear();

				//Clear sequential structures
				seqDataList.Clear();
				lastPacketId = -1;

				//Clear MultiPart Buffers
				multiPartBuffers.Clear();

				//Disconnect client instance
				if (client != null)
					client.Disconnect();

				//Do proper callbacks
				if (calledDisconnect)
				{
					calledDisconnect = false;
					if (m_onDisconnected != null)
						m_onDisconnected();
					return;
				}
				if (serverDisconnected)
				{
					serverDisconnected = false;
					if (m_onServerDisconnected != null)
						m_onServerDisconnected(serverDisconnectMsg);
				}
				if (connectionFailed)
				{
					if (m_onConnectionFailed != null)
						m_onConnectionFailed();
					return;
				}
				else
				{
					if (m_onConnectionTimeout != null)
						m_onConnectionTimeout();
				}
			}

			if (hasConnected)
			{
				//Reset flag
				hasConnected = false;

				//Do proper callback
				if (m_onConnected != null)
					m_onConnected();
			}

			//Process broadcast callbacks
			if (broadcaster != null)
			{
				//Process broadcast callbacks
				lock (broadcastDataQueueLock)
				{
					while (broadcastDataQueue.Count > 0)
					{
						BroadcastPacket packet = broadcastDataQueue.Dequeue();

						//Do proper callback
						if (m_onBroadcastResponse != null)
							m_onBroadcastResponse(packet);
					}
				}
			}

			if (!connected)
				return;

			//Process packet callbacks
			lock (dataQueueLock)
			{
				while (dataQueue.Count > 0)
				{
					Packet packet = dataQueue.Dequeue();

					//Do proper callback
					if (m_onPacketReceived != null)
						m_onPacketReceived(packet);
				}
			}
		}
	}

}
