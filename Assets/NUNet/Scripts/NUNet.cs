
//STD Includes
using System;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Security.Cryptography;

//Unity Includes
using UnityEngine;

//TODO: Sync Custom Callbacks with RPC attributes
namespace NUNet
{
	//Packet data (a message)
	public struct Packet : IComparable
	{
		public enum TypeFlag
		{
			DATA = 0x00,        //Default Data Packet
			GUID = 0x01,        //Guid Handshakes
			PING = 0x02,        //Ping processing
			DCONNECT = 0x03,    //Disconnections
			MPARTDATA = 0x04,   //Multipart Data Packet
			BROADCAST = 0x05,   //Broadcast Data Packet
		}

		/// <summary>
		/// The Packet Flag
		/// </summary>
		public TypeFlag flag { get; private set; }

		/// <summary>
		/// Raw Packet data.
		/// </summary>
		public byte[] data { get; private set; }

		/// <summary>
		/// Entire packet size, used for TCP stream splitting.
		/// </summary>
		public int packetSize { get; private set; }

		/// <summary>
		/// Packet id, used for sequential message sending.
		/// </summary>
		public int id { get; private set; }

		/// <summary>
		/// The size of the data without protocol flag.
		/// </summary>
		public int cleanSize { get; private set; }

		/// <summary>
		/// Array of <see cref="Guid"/>s for the recipients of this packet.
		/// </summary>
		public Guid[] destinationIDs { get; private set; }

		/// <summary>
		/// Hash of this packet bytes.
		/// </summary>
		public Hash hash { get; private set; }

		/// <summary>
		/// Takes a Packet Raw data (with flag!) and builds a Packet structure from it.
		/// </summary>
		/// <param name="rawBuffer">The received buffer with trash.</param>
		/// <param name="index">The position to start copying from raw.</param>
		/// <param namoe="size">The packet's buffer correct size.</param>
		public Packet(byte[] rawBuffer, int index, int size)
		{
			//Copy packet data
			this.data = new byte[size];
			Array.Copy(rawBuffer, index, this.data, 0, size);

			//Set clean size data
			this.cleanSize = size - NUUtilities.PacketHeaderSize;

			//Set empty destination IDs
			this.destinationIDs = new Guid[0];

			//Read packet size
			this.packetSize = BitConverter.ToInt32(this.data, 0);

			//Get Hash from raw data
			byte[] hashData = new byte[16];
			Array.Copy(this.data, 4, hashData, 0, 16);
			Hash dataHash = new Hash(hashData, true);

			//Make sure hash data in buffer is clear
			//to compare with raw data Hash
			for (int i = 0; i < 16; i++)
			{
				this.data[4 + i] = (byte)0;
			}

			//Generate hash data
			this.hash = new Hash(this.data);
			if (!Hash.comparer.Equals(dataHash, this.hash))
			{
				Debug.LogErrorFormat("Corrupted Packet! Hash doesn't match {0} != {1}", hash, dataHash);
				throw new Exception("Corrupted Packet!");
			}

			//Read message flag
			this.flag = (TypeFlag)this.data[20];

			//Read packet id
			this.id = BitConverter.ToInt32(this.data, 21);
		}

		/// <summary>
		/// Takes a Packet Raw data (with flag!) and builds a Packet structure from it.
		/// </summary>
		/// <param name="rawBuffer">The received buffer with trash.</param>
		/// <param namoe="size">The packet's buffer correct size.</param>
		public Packet(byte[] rawBuffer, int size) : this(rawBuffer, 0, size) { }

		/// <summary>
		/// Take clean packet data, packet destinations and packet message flag and build a Packet class.
		/// </summary>
		/// <param name="data"></param>
		/// <param name="destinationIDs"></param>
		/// <param name="messageFlag"></param>
		public Packet(byte[] data, Guid[] destinationIDs = null, TypeFlag messageFlag =
			TypeFlag.DATA, int packetId = -1)
		{
			//Allocate data array
			this.data = new byte[data.Length + NUUtilities.PacketHeaderSize];

			//Copy array data
			Array.Copy(data, 0, this.data, NUUtilities.PacketHeaderSize, data.Length);

			//Set packet size
			this.packetSize = this.data.Length;
			BitConverter.GetBytes(this.packetSize).CopyTo(this.data, 0);

			//Set message flag
			this.data[20] = (byte)messageFlag;
			this.flag = messageFlag;

			//Set packet id
			this.id = packetId;
			BitConverter.GetBytes(this.id).CopyTo(this.data, 21);

			//Set clean size
			this.cleanSize = data.Length;

			//Destionation info
			if (destinationIDs == null) destinationIDs = new Guid[0];
			this.destinationIDs = destinationIDs;

			//Make sure hash data in buffer is clear
			//to enable comparations on the receiver
			for (int i = 0; i < 16; i++)
			{
				this.data[4 + i] = (byte)0;
			}

			//Generate hash data
			this.hash = new Hash(this.data);

			//Copy to packet buffer
			this.hash.data.CopyTo(this.data, 4);
		}

		/// <summary>
		/// Take clean packet data, packet destinations and packet message flag and build a Packet class.
		/// </summary>
		/// <param name="data"></param>
		/// <param name="destinationID"></param>
		/// <param name="messageFlag"></param>
		public Packet(byte[] data, Guid destinationID, TypeFlag messageFlag = TypeFlag.DATA, int packetId = -1) :
			this(data, new[] { destinationID }, messageFlag, packetId)
		{ }

		/// <summary>
		/// Take clean packet data, packet destinations and packet message flag and build a Packet class.
		/// </summary>
		/// <param name="data"></param>
		/// <param name="destinationIDs"></param>
		/// <param name="messageFlag"></param>
		public Packet(char[] data, Guid[] destinationIDs = null, TypeFlag messageFlag = TypeFlag.DATA, int packetId = -1) :
			this(Encoding.ASCII.GetBytes(data), destinationIDs, messageFlag, packetId)
		{ }

		/// <summary>
		/// Take clean packet data, packet destinations and packet message flag and build a Packet class.
		/// </summary>
		/// <param name="data"></param>
		/// <param name="destinationID"></param>
		/// <param name="messageFlag"></param>
		public Packet(char[] data, Guid destinationID, TypeFlag messageFlag = TypeFlag.DATA, int packetId = -1) :
			this(Encoding.ASCII.GetBytes(data), new[] { destinationID }, messageFlag, packetId)
		{ }

		/// <summary>
		/// Take a string message, packet destinations and packet message flag and build a Packet class.
		/// </summary>
		/// <param name="msg"></param>
		/// <param name="destinationIDs"></param>
		/// <param name="messageFlag"></param>
		public Packet(string msg, Guid[] destinationIDs = null, TypeFlag messageFlag = TypeFlag.DATA, int packetId = -1) :
			this(Encoding.ASCII.GetBytes(msg), destinationIDs, messageFlag, packetId)
		{ }

		/// <summary>
		/// Take a string message, packet destinations and packet message flag and build a Packet class.
		/// </summary>
		/// <param name="msg"></param>
		/// <param name="destinationID"></param>
		/// <param name="messageFlag"></param>
		public Packet(string msg, Guid destinationID, TypeFlag messageFlag = TypeFlag.DATA, int packetId = -1) :
			this(Encoding.ASCII.GetBytes(msg), new[] { destinationID }, messageFlag, packetId)
		{ }

		/// <summary>
		/// Take an int value, packet destinations and packet message flag and build a Packet class
		/// </summary>
		/// <param name="obj"></param>
		/// <param name="destinationIDs"></param>
		/// <param name="messageFlag"></param>
		public Packet(int value, Guid[] destinationIDs = null, TypeFlag messageFlag = TypeFlag.DATA, int packetId = -1) :
			this(NUUtilities.GetBytes(value), destinationIDs, messageFlag, packetId)
		{ }

		/// <summary>
		/// Take an int value, packet destinations and packet message flag and build a Packet class
		/// </summary>
		/// <param name="obj"></param>
		/// <param name="destinationID"></param>
		/// <param name="messageFlag"></param>
		public Packet(int value, Guid destinationID, TypeFlag messageFlag = TypeFlag.DATA, int packetId = -1) :
			this(NUUtilities.GetBytes(value), new[] { destinationID }, messageFlag, packetId)
		{ }

		/// <summary>
		/// Take any object to serialize as byte array, packet destinations and packet message flag and build a Packet class
		/// </summary>
		/// <param name="obj"></param>
		/// <param name="destinationIDs"></param>
		/// <param name="messageFlag"></param>
		public Packet(object obj, Guid[] destinationIDs = null, TypeFlag messageFlag = TypeFlag.DATA, int packetId = -1) :
			this(NUUtilities.GetBytes(obj), destinationIDs, messageFlag, packetId)
		{ }

		/// <summary>
		/// Take any object to serialize as byte array, packet destinations and packet message flag and build a Packet class
		/// </summary>
		/// <param name="obj"></param>
		/// <param name="destinationID"></param>
		/// <param name="messageFlag"></param>
		public Packet(object obj, Guid destinationID, TypeFlag messageFlag = TypeFlag.DATA, int packetId = -1) :
			this(NUUtilities.GetBytes(obj), new[] { destinationID }, messageFlag, packetId)
		{ }

		public void OverrideDestination(Guid[] guid)
		{
			if (guid == null)
			{
				destinationIDs = new Guid[0];
				return;
			}

			destinationIDs = guid;
		}

		public void OverrideID(int packetId)
		{
			//Override ID and set buffer header info
			this.id = packetId;
			BitConverter.GetBytes(this.id).CopyTo(this.data, 21);

			//Make sure hash data in buffer is clear
			//to enable comparations on the receiver
			for (int i = 0; i < 16; i++)
			{
				this.data[4 + i] = (byte)0;
			}

			//Recalculate Hash Code
			this.hash = new Hash(this.data);

			//Copy to packet buffer
			this.hash.data.CopyTo(this.data, 4);
		}

		public void OverridePacketFlag(TypeFlag messageFlag)
		{
			//Override message flag
			this.data[20] = (byte)messageFlag;
			this.flag = messageFlag;

			//Make sure hash data in buffer is clear
			//to enable comparations on the receiver
			for (int i = 0; i < 16; i++)
			{
				this.data[4 + i] = (byte)0;
			}

			//Recalculate Hash Code
			this.hash = new Hash(this.data);

			//Copy to packet buffer
			this.hash.data.CopyTo(this.data, 4);
		}

		public byte[] GetCleanData()
		{
			byte[] cleanData = new byte[this.cleanSize];
			Array.Copy(this.data, NUUtilities.PacketHeaderSize, cleanData, 0, this.cleanSize);
			return cleanData;
		}

		public string GetMessageData()
		{
			return Encoding.ASCII.GetString(this.data, NUUtilities.PacketHeaderSize, this.cleanSize);
		}

		public override string ToString()
		{
			string msg = "Packet Destinations: ";
			for (int i = 0; i < destinationIDs.Length; i++)
			{
				msg += destinationIDs[i] + "; ";
			}
			if (destinationIDs.Length == 0)
				msg += "None; ";
			msg += "Packet Type: " + this.flag + "; ";
			msg += "Packet Size: " + this.cleanSize;
			msg += "\n" + this.hash.ToString();
			return msg;
		}

		public int CompareTo(object o)
		{
			Packet e = (Packet)o;
			return Hash.comparer.Equals(hash, e.hash) ? 0 : 1;
		}

	}

	//Broadcast Packet data (a Packet with origin)
	public struct BroadcastPacket
	{
		public readonly Packet packet;
		public readonly IPAddress senderIp;
		public readonly int senderPort;

		public BroadcastPacket(Packet packet, IPEndPoint senderEp)
		{
			this.packet = packet;
			this.senderIp = senderEp.Address;
			this.senderPort = senderEp.Port;
		}
	}

	public struct WebPacket
	{
		public readonly int status;
		public readonly string response;
		public readonly Dictionary<string, string> headers;
		public readonly string body;

		private static readonly byte[] headersTerminator = Encoding.ASCII.GetBytes("\r\n\r\n");

		public WebPacket(byte[] data)
		{
			//Get Headers terminator index
			int headersIndex = -1;
			for (int i = 0; i < data.Length - headersTerminator.Length; i++)
			{
				int diffMask = 0;
				for (int j = 0; j < headersTerminator.Length; j++)
				{
					diffMask |= (data[i + j] ^ headersTerminator[j]);
				}

				if (diffMask == 0)
				{
					headersIndex = i;
					break;
				}
			}

			string headers = Encoding.ASCII.GetString(data, 0, headersIndex);
			Debug.Log("Response Header:\n" + headers);
			string body = Encoding.ASCII.GetString(data, headersIndex + 4, data.Length - (headersIndex + 4));
			Debug.Log("Response Body:\n" + body);

			//Split header lines
			string[] headerEntries = headers.Split('\n');

			//Get code and response value
			int spaceLoc = headerEntries[0].IndexOf(' ');
			string codeAndResp = headerEntries[0].Substring(spaceLoc + 1);
			spaceLoc = codeAndResp.IndexOf(' ');
			string codeStr = codeAndResp.Remove(spaceLoc);
			status = int.Parse(codeStr);
			response = codeAndResp.Substring(spaceLoc + 1).TrimEnd('\r');

			this.headers = new Dictionary<string, string>(headerEntries.Length - 1);
			for (int i = 1; i < headerEntries.Length; i++)
			{
				string[] values = headerEntries[i].Split(':');
				this.headers.Add(values[0], values[1].TrimEnd('\r').TrimStart(' '));
			}

			this.body = body;
		}

		public override string ToString()
		{
			string packet = "Status: ";
			packet += status + " - " + response + "\n";
			packet += "\nHeaders:\n";
			foreach (var kv in headers)
			{
				packet += kv.Key + ":" + kv.Value + "\n";
			}
			packet += "\nBody:\n";
			packet += body;
			return packet;
		}
	}

	public class MultiPartBuffer
	{
		public int receivedSize { get; private set; }

		public int remainingSize { get { return data.Length - receivedSize; } private set { } }

		public readonly Hash hash;

		private byte[] data;

		public readonly int packetsCount;

		public bool[] receivedPackets { get; private set; }

		public MultiPartBuffer(int amountOfPackets, int bufferSize, Hash hash)
		{
			receivedSize = 0;
			this.hash = hash;
			data = new byte[bufferSize];
			packetsCount = amountOfPackets;
			receivedPackets = new bool[amountOfPackets];
		}

		public void MergePacketData(int partId, byte[] partData)
		{
			if (receivedPackets[partId] == true)
			{
				Debug.LogError("Trying to merge duplicated multipart packet! Ignoring...");
				return;
			}

			//Set received packet flag
			receivedPackets[partId] = true;

			//Copy Original Packet Data without MultiPart Header
			int rawDataSize = partData.Length - NUUtilities.MultiPartDataHeaderSize;
			int copyLocation = partId * NUUtilities.MaxMultiPartPacketSize;
			//Debug.LogFormat("Copying part {0} data from {1} until {2}", partId, copyLocation, copyLocation+rawDataSize);
			Array.Copy(partData, NUUtilities.MultiPartDataHeaderSize, this.data, copyLocation, rawDataSize);
			receivedSize += rawDataSize;
		}

		public bool GetOriginalPacket(out Packet packet)
		{
			bool receivedAllPackets = true;
			for (int i = 0; i < receivedPackets.Length; i++)
			{
				if (receivedPackets[i] == false)
				{
					Debug.LogError("Multipart Packet not ready, " +
						"missing Packet " + i + "!");
				}
				receivedAllPackets &= receivedPackets[i];
			}

			if (!receivedAllPackets)
			{
				packet = new Packet();
				Debug.LogError("Haven't received all packets yet!");
				return false;
			}

			packet = new Packet(data, data.Length);
			return true;
		}

	}

	public struct Hash
	{
		public static readonly IEqualityComparer<Hash> comparer = new HashComparer();

		public readonly byte[] data;

		public Hash(byte[] data)
		{
			MD5 md5Hash = MD5.Create();
			this.data = md5Hash.ComputeHash(data);
		}

		public Hash(string message) : this(Encoding.ASCII.GetBytes(message)) { }

		public override string ToString()
		{
			StringBuilder sb = new StringBuilder(32);
			for (int i = 0; i < data.Length; i++)
			{
				sb.Append(data[i].ToString("X2"));
			}
			return sb.ToString();
		}

		public Hash(byte[] data, bool raw = false)
		{
			if (raw)
			{
				this.data = data;
			}
			else
			{
				MD5 md5Hash = MD5.Create();
				this.data = md5Hash.ComputeHash(data);
			}
		}

		public sealed class HashComparer : IEqualityComparer<Hash>
		{
			public bool Equals(Hash x, Hash y)
			{
				int test = 0;
				for (int i = 0; i < 16; i++)
				{
					test |= (x.data[i] ^ y.data[i]);
				}
				return test == 0;
			}

			//public bool Equals(Hash x, Hash y)
			//{
			//	for (int i = 0; i < 16; i++)
			//	{
			//		if (x.data[i] != y.data[i])
			//			return false;
			//	}
			//	return true;
			//}

			public int GetHashCode(Hash obj)
			{
				int hashCode = 0;
				for (int i = 0; i < obj.data.Length; i++)
				{
					hashCode ^= obj.data[i];
				}
				return hashCode;
			}
		}
	}

}

//Internal NUNet Stuff
namespace NUNet.Internal
{
	//NUClient structure
	public class NUClientInfo
	{
		private bool m_connected;   //Connection flag
		public bool connected       //Connection flag threadsafe operations
		{
			get { lock (lockObject) { return m_connected; } }
			set { lock (lockObject) { m_connected = value; } }
		}
		public IPAddress address;   //Remote IPAddress
		public TcpClient tcpClient; //TCP Client instance
		public Socket udpClient;    //UDP Socket instance
		public Guid guid;           //Global Unique Identifier
		public int ping;            //Last ping measurement
		public long lastPingTime;   //Last time ping was measured
		public int portOffset;      //UDP Port offset to listener port number
		private object lockObject;  //ThreadLock

		public NUClientInfo(Guid id, IPAddress address, ref TcpClient tcpClient, ref Socket udpClient, int ping = 0, long lastPingTime = 0, int portOffset = 0)
		{
			this.lockObject = new object();
			lock (lockObject)
			{
				this.guid = id;
				this.address = address;
				this.tcpClient = tcpClient;
				this.udpClient = udpClient;
				this.connected = false;
				this.ping = ping;
				this.lastPingTime = lastPingTime;
				this.portOffset = portOffset;
			}
		}

		public void Disconnect()
		{
			lock (lockObject)
			{
				m_connected = false;
				ping = 0;
				lastPingTime = 0;
				if (tcpClient != null)
				{
					if (tcpClient.Connected)
					{
						tcpClient.Close();
					}
				}
				if (udpClient != null)
				{
					if (udpClient.Connected)
					{
						udpClient.Shutdown(SocketShutdown.Both);
						udpClient.Close();
					}
				}
				guid = Guid.Empty;
				tcpClient = null;
				udpClient = null;
			}
		}

		public override string ToString()
		{
			return "Network Client GUID: " + guid + ", Connected: " + connected + "\nIP Address: " + address +
			", Ping: " + ping;
		}

		internal int GetCurrentPing(long currentTime)
		{
			return (int)(currentTime - this.lastPingTime);
		}

		internal void EndPingMeasurement(long currentTime)
		{
			this.ping = (int)(currentTime - this.lastPingTime);
		}

		internal void StartPingMeasurement(long currentTime)
		{
			this.lastPingTime = currentTime;
		}

		internal void OverridePing(int ping)
		{
			this.ping = ping;
		}

		public bool Connected()
		{
			lock (lockObject)
			{
				return this.m_connected;
			}
		}
	}

	//Auxiliar Structures
	public struct TcpTransmissionState
	{
		public int offset;
		public byte[] data;
		public TcpClient tcpClient;
		public readonly NUClientInfo client;
		public readonly Action callback;

		public TcpTransmissionState(int bufferSize, ref TcpClient tcpClient, NUClientInfo client, Action callback = null)
		{
			this.offset = 0;
			this.data = new byte[bufferSize];
			this.tcpClient = tcpClient;
			this.client = client;
			this.callback = callback;
		}
	}

	public struct UdpTransmissionState
	{
		public byte[] data;
		public Socket socket;
		public readonly NUClientInfo client;
		public readonly Action callback;

		public UdpTransmissionState(int bufferSize, ref Socket socket, NUClientInfo client, Action callback = null)
		{
			this.data = new byte[bufferSize];
			this.socket = socket;
			this.client = client;
			this.callback = callback;
		}
	}

	public struct BroadcastTransmissionState
	{
		public byte[] data;
		public Socket broadcaster;
		public EndPoint senderEp;
		public Action callback;

		public BroadcastTransmissionState(int bufferSize, ref Socket broadcaster, Action callback = null)
		{
			this.data = new byte[bufferSize];
			this.broadcaster = broadcaster;
			this.senderEp = new IPEndPoint(IPAddress.Any, 0);
			this.callback = callback;
		}
	}

	internal class LastPacketID
	{
		public int id;

		public LastPacketID(int lastPacketId)
		{
			this.id = lastPacketId;
		}
	}

	internal struct PacketClientID
	{
		public readonly Guid id;
		public readonly Packet packet;

		public PacketClientID(Guid id, ref Packet packet)
		{
			this.id = id;
			this.packet = packet;
		}
	}

	internal class NUServerComponent : MonoBehaviour
	{
		private void LateUpdate()
		{
			NUServer.ProcessQueues();
			NUServer.PingAllClients();
		}

		private void OnApplicationQuit()
		{
			NUServer.Shutdown();
		}
	}

	internal class NUClientComponent : MonoBehaviour
	{
		private void LateUpdate()
		{
			NUClient.ProcessQueues();
		}

		private void OnApplicationQuit()
		{
			NUClient.Disconnect();
			NUClient.FinishBroadcast();
		}
	}

	internal class NUWebComponent : MonoBehaviour
	{
		private void LateUpdate()
		{
			NUWeb.ProcessQueues();
		}

		private void OnApplicationQuit()
		{
			NUWeb.Disconnect();
		}
	}
}
