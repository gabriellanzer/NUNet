
//STD Includes
using System;
using System.IO;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Runtime.Serialization.Formatters.Binary;
using StackTrace = System.Diagnostics.StackTrace;
using StackFrame = System.Diagnostics.StackFrame;

//Unity Includes
using UnityEngine;
using System.Runtime.CompilerServices;

//TODO: Sync Custom Callbacks with RPC attributes
namespace NUNet
{
	//Utilities used all around the NUNet code
	public static class NUUtilities
	{
		public static List<IPAddress> ListIPv4Addresses()
		{
			IPAddress[] addresses = Dns.GetHostAddresses(Dns.GetHostName());
			List<IPAddress> ipv4Addresses = new List<IPAddress>();
			for (int i = 0; i < addresses.Length; i++)
			{
				if (addresses[i].AddressFamily == AddressFamily.InterNetwork)
					ipv4Addresses.Add(addresses[i]);
			}
			return ipv4Addresses;
		}

		public static List<IPAddress> ListIPv6Addresses()
		{
			IPAddress[] addresses = Dns.GetHostAddresses(Dns.GetHostName());
			List<IPAddress> ipv6Addresses = new List<IPAddress>();
			for (int i = 0; i < addresses.Length; i++)
			{
				if (addresses[i].AddressFamily == AddressFamily.InterNetworkV6)
					ipv6Addresses.Add(addresses[i]);
			}
			return ipv6Addresses;
		}

		public static IPAddress GetSubnetMaskFromIPv4(IPAddress adapterAddress)
		{
			foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
			{
				foreach (UnicastIPAddressInformation unicastIPAddressInfo in adapter.GetIPProperties().UnicastAddresses)
				{
					if (unicastIPAddressInfo.Address.AddressFamily == AddressFamily.InterNetwork)
					{
						if (adapterAddress.Equals(unicastIPAddressInfo.Address))
						{
							return unicastIPAddressInfo.IPv4Mask;
						}
					}
				}
			}
			Debug.LogErrorFormat("Can't find subnetmask for IP address '{0}'", adapterAddress);
			return new IPAddress(0);
		}

		public static IPAddress GetBroadcastFromIPv4(IPAddress adapterAddress)
		{
			if (adapterAddress.Equals(IPAddress.Loopback))
			{
				return IPAddress.Broadcast;
			}

			foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
			{
				foreach (UnicastIPAddressInformation unicastIPAddressInfo in adapter.GetIPProperties().UnicastAddresses)
				{
					if (unicastIPAddressInfo.Address.AddressFamily == AddressFamily.InterNetwork)
					{
						if (adapterAddress.Equals(unicastIPAddressInfo.Address))
						{
							byte[] addressBytes = adapterAddress.GetAddressBytes();
							byte[] maskBytes = unicastIPAddressInfo.Address.GetAddressBytes();
							for (int i = 0; i < maskBytes.Length; i++)
							{
								addressBytes[i] |= (byte)(maskBytes[i] ^ 255);
							}
							return new IPAddress(addressBytes);
						}
					}
				}
			}
			Debug.LogErrorFormat(string.Format("Can't find broadcast for IP address '{0}'", adapterAddress));
			return new IPAddress(0);
		}

		public static int GetInt32(byte[] bytes)
		{
			if (!BitConverter.IsLittleEndian)
				Array.Reverse(bytes);
			return BitConverter.ToInt32(bytes, 0);
		}

		public static uint GetUInt32(byte[] bytes)
		{
			if (!BitConverter.IsLittleEndian)
				Array.Reverse(bytes);
			return BitConverter.ToUInt32(bytes, 0);
		}

		public static float GetSingle(byte[] bytes)
		{
			if (!BitConverter.IsLittleEndian)
				Array.Reverse(bytes);
			return BitConverter.ToSingle(bytes, 0);
		}

		public static short GetInt16(byte[] bytes)
		{
			if (!BitConverter.IsLittleEndian)
				Array.Reverse(bytes);
			return BitConverter.ToInt16(bytes, 0);
		}

		public static ushort GetUInt16(byte[] bytes)
		{
			if (!BitConverter.IsLittleEndian)
				Array.Reverse(bytes);
			return BitConverter.ToUInt16(bytes, 0);
		}

		public static byte[] GetBytes(int value)
		{
			byte[] intBytes = BitConverter.GetBytes(value);
			if (!BitConverter.IsLittleEndian)
				Array.Reverse(intBytes);
			return intBytes;
		}

		public static byte[] GetBytes(uint value)
		{
			byte[] uintBytes = BitConverter.GetBytes(value);
			if (!BitConverter.IsLittleEndian)
				Array.Reverse(uintBytes);
			return uintBytes;
		}

		public static byte[] GetBytes(float value)
		{
			byte[] floatBytes = BitConverter.GetBytes(value);
			if (!BitConverter.IsLittleEndian)
				Array.Reverse(floatBytes);
			return floatBytes;
		}

		public static byte[] GetBytes(short value)
		{
			byte[] shortBytes = BitConverter.GetBytes(value);
			if (!BitConverter.IsLittleEndian)
				Array.Reverse(shortBytes);
			return shortBytes;
		}

		public static byte[] GetBytes(ushort value)
		{
			byte[] ushortBytes = BitConverter.GetBytes(value);
			if (!BitConverter.IsLittleEndian)
				Array.Reverse(ushortBytes);
			return ushortBytes;
		}

		public static byte[] GetBytes(string msg)
		{
			byte[] msgData = Encoding.ASCII.GetBytes(msg);
			if (!BitConverter.IsLittleEndian)
				Array.Reverse(msgData);
			return msgData;
		}

		public static byte[] GetBytes(object obj)
		{
			BinaryFormatter bf = new BinaryFormatter();
			using (var ms = new MemoryStream())
			{
				bf.Serialize(ms, obj);
				return ms.ToArray();
			}
		}

		public static Guid GetGuid(byte[] buffer)
		{
			byte[] guidBytes = new byte[16];
			if (buffer.Length < 16)
			{
				Debug.LogErrorFormat("Buffer doesn't contain a GUID, because it's length is " + buffer.Length);
				return Guid.Empty;
			}
			buffer.CopyTo(guidBytes, buffer.Length - 16);

			return new Guid(guidBytes);
		}

		public static int GetGuidAndPort(byte[] buffer, out Guid guid)
		{
			byte[] guidBytes = new byte[16];
			if (buffer.Length < 18)
			{
				Debug.LogErrorFormat("Buffer doesn't contain a GUID and Port, because it's length is " + buffer.Length);
				guid = Guid.Empty;
				return -1;
			}
			Array.Copy(buffer, buffer.Length - 16, guidBytes, 0, 16);
			guid = new Guid(guidBytes);
			return BitConverter.ToInt16(buffer, buffer.Length - 18);
		}

		public static int GetHashAndPartId(byte[] buffer, out Hash hash)
		{
			if (buffer.Length < 20)
			{
				Debug.LogError("Buffer cannot contain hash and part id because" +
					"it's length is less then 20: " + buffer.Length);
				hash = new Hash();
				return -1;
			}

			byte[] hashData = new byte[16];
			Array.Copy(buffer, 4, hashData, 0, 16);
			hash = new Hash(hashData, true);

			byte[] partIdData = new byte[4];
			Array.Copy(buffer, 0, partIdData, 0, 4);
			return GetInt32(partIdData);
		}

		public static byte[] GetGuidAndPortBuffer(Guid guid, ushort port)
		{
			byte[] buffer = new byte[18];

			//Port goes first
			BitConverter.GetBytes(port).CopyTo(buffer, 0);

			//Guid goes next
			guid.ToByteArray().CopyTo(buffer, 2);

			return buffer;
		}

		public static Action SanitizeAction(Action action)
		{
			return (Action)SanitizeDelegate(action);
		}

		public static Action<T1> SanitizeAction<T1>(Action<T1> action)
		{
			return (Action<T1>)SanitizeDelegate(action);
		}

		public static Action<T1, T2> SanitizeAction<T1, T2>(Action<T1, T2> action)
		{
			return (Action<T1, T2>)SanitizeDelegate(action);
		}

		public static Delegate SanitizeDelegate(Delegate callback)
		{
			if (callback == null)
				return null;

			Delegate[] delegates = callback.GetInvocationList();
			foreach (Delegate invocable in delegates)
			{
				//Log error if using callbacks
				string methodName = invocable.Method.Name;
				if (methodName.IndexOfAny(invalidCallbackChars) != -1)
				{
					StackTrace stack = new StackTrace(true);
					StackFrame frame = stack.GetFrame(2);
					Debug.LogErrorFormat("Cannot assign anonymous function as callback on file {0}, line {1}." +
						"\nThat is done to avoid them not being removed.", frame.GetFileName(),
						frame.GetFileLineNumber());
					callback = Delegate.Remove(callback, invocable);
					continue;
				}

				//Static functions may be used as they access static memory references
				if (invocable.Method.IsStatic)
					continue;

				//Check it it relies on an instance or not
				if (invocable.Target == null || invocable.Target.Equals(null))
				{
					callback = Delegate.Remove(callback, invocable);
				}
			}

			return callback;
		}

		public static Packet[] SplitPacket(Packet packet)
		{
			if (packet.data.Length <= MTU)
			{
				return new Packet[] { packet };
			}

			//Calculate Packets Count
			int dataLenght = packet.data.Length;
			int partPacketCount = 1 + ((dataLenght - 1) / MaxMultiPartPacketSize); //Int Ceil

			//Setup All Part Packets
			Packet[] partPackets = new Packet[partPacketCount];
			for (int i = 0; i < partPackets.Length; i++)
			{
				int partPacketSize = Mathf.Min(dataLenght, MaxMultiPartPacketSize);
				byte[] partPacketData = new byte[MultiPartDataHeaderSize + partPacketSize];

				//Header
				GetBytes(i).CopyTo(partPacketData, 0);                  //Append Packet Identifier
				packet.hash.data.CopyTo(partPacketData, 4);             //Append Origin Packet Hash
				GetBytes(packet.data.Length).CopyTo(partPacketData, 20);//Append Origin Packet Size
				GetBytes(partPacketCount).CopyTo(partPacketData, 24);   //Append Amount of Packets

				//Append Part Data
				Array.Copy(packet.data, i * MaxMultiPartPacketSize, partPacketData,
					MultiPartDataHeaderSize, partPacketSize);

				partPackets[i] = new Packet(partPacketData, packet.destinationIDs, Packet.TypeFlag.MPARTDATA);
				dataLenght -= partPacketSize;  //Update Remaining Data Length
			}

			//Debug.LogFormat("Generated {0} packets...", partPacketCount);

			return partPackets;
		}

		public static int GeneratePacketId()
		{
			return GeneratePacketId(Guid.Empty);
		}

		public static int GeneratePacketId(Guid guid)
		{
			Internal.LastPacketID packetId;
			if (!packetIds.TryGetValue(guid, out packetId))
			{
				packetId = new Internal.LastPacketID(-1);
				packetIds.Add(guid, packetId);
			}
			if (packetId.id == int.MaxValue) { packetId.id = -1; }
			packetId.id++;
			return packetId.id;
		}

		private static Dictionary<Guid, Internal.LastPacketID> packetIds = new Dictionary<Guid, Internal.LastPacketID>();

		public const int MTU = 1452;

		public const int MaxBufferedPackets = 2048;

		public const int PacketHeaderSize = 4/*Packet Size*/ + 16/*Packet Hash*/ + 1/*Packet Type Flag*/ + 4/*Packet ID*/;

		public const int MultiPartDataHeaderSize = 4/*Packet ID*/ + 16/*Origin Packet Hash*/ + 4/*Origin Packet Size*/ + 4/*Packets Count*/;

		public const int MaxMultiPartPacketSize = (MTU - MultiPartDataHeaderSize - PacketHeaderSize);

		public static readonly char[] invalidCallbackChars = new[] { '<', '>' };

	}

	//Packet data (a message)
	public struct Packet : IComparable
	{
		public enum TypeFlag
		{
			DATA = 0x00,        //Default Data Packet
			GUID = 0x01,        //Guid Handshakes
			PING = 0x02,        //Ping processing
			DCONNECT = 0x03,    //Disconnections
			MPARTDATA = 0x04    //Multipart Data Packet
		}

		/// <summary>
		/// The Packet Flag
		/// </summary>
		public readonly TypeFlag flag;

		/// <summary>
		/// Raw Packet data.
		/// </summary>
		public readonly byte[] data;

		/// <summary>
		/// Entire packet size, used for TCP stream splitting.
		/// </summary>
		public readonly int packetSize;

		/// <summary>
		/// Packet id, used for sequential message sending.
		/// </summary>
		public int id;

		/// <summary>
		/// The size of the data without protocol flag.
		/// </summary>
		public readonly int cleanSize;

		/// <summary>
		/// Array of <see cref="Guid"/>s for the recipients of this packet.
		/// </summary>
		public Guid[] destinationIDs { get; private set; }

		/// <summary>
		/// Hash of this packet bytes.
		/// </summary>
		public Hash hash { private set; get; }

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
		/// Takes a Packet Raw data (with flag!) and builds a Packet structure from it.
		/// </summary>
		/// <param name="rawBuffer">The received buffer.</param>
		public Packet(byte[] rawBuffer) : this(rawBuffer, 0, rawBuffer.Length) { }

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
		/// <param name="destinationIDs"></param>
		/// <param name="messageFlag"></param>
		public Packet(char[] data, Guid[] destinationIDs = null, TypeFlag messageFlag = TypeFlag.DATA, int packetId = -1) :
			this(Encoding.ASCII.GetBytes(data), destinationIDs, messageFlag, packetId)
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
		/// Take an int value, packet destinations and packet message flag and build a Packet class
		/// </summary>
		/// <param name="obj"></param>
		/// <param name="destinationIDs"></param>
		/// <param name="messageFlag"></param>
		public Packet(int value, Guid[] destinationIDs = null, TypeFlag messageFlag = TypeFlag.DATA, int packetId = -1) :
			this(NUUtilities.GetBytes(value), destinationIDs, messageFlag, packetId)
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
		public readonly IPAddress origin;

		public BroadcastPacket(Packet packet, IPAddress origin)
		{
			this.packet = packet;
			this.origin = origin;
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
				bool found = true;
				for (int j = 0; j < headersTerminator.Length; j++)
				{
					found &= (data[i + j] == headersTerminator[j]);
				}

				if (found)
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

			packet = new Packet(data);
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

		private sealed class HashComparer : IEqualityComparer<Hash>
		{
			//public bool Equals(Hash x, Hash y)
			//{
			//    int test = 0;
			//    for (int i = 0; i < 16; i++)
			//    {
			//        test |= (x.data[i] ^ y.data[i]);
			//    }
			//    return test == 0;
			//}

			public bool Equals(Hash x, Hash y)
			{
				for (int i = 0; i < 16; i++)
				{
					if (x.data[i] != y.data[i])
						return false;
				}
				return true;
			}

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
