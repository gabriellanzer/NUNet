using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization.Formatters.Binary;
using StackTrace = System.Diagnostics.StackTrace;
using StackFrame = System.Diagnostics.StackFrame;

using UnityEngine;

namespace NUNet
{
	//Utilities used all around the NUNet code
	public static class NUUtilities
	{
		private static byte[,] hexaToBytesMap = new byte[23, 23];

		static NUUtilities()
		{
			// Initialize hexa chars to bytes decode table
			for (int i = 0; i < 23; i++)
			{
				if (i > 9 && i < 17)
				{
					continue;
				}

				for (int j = 0; j < 23; j++)
				{
					if (j > 9 && j < 17)
					{
						continue;
					}

					string ijChars = new string(new[] { (char)(i + 48), (char)(j + 48) });
					hexaToBytesMap[i, j] = Convert.ToByte(ijChars, 16);
				}
			}
		}

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

		public static UInt32 GetUIntFromIpAddress(IPAddress address)
		{
			if (address.AddressFamily != AddressFamily.InterNetwork)
			{
				Debug.LogError("Cannot convert an address famility that is not IPv4. Sorry :<");
				return 0;
			}
			byte[] bytes = address.GetAddressBytes();
			return (uint)((bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3]);
		}

		public static IPAddress GetIpAddressFromUInt32(UInt32 UIntAddress)
		{
			byte[] bytes = new byte[] {
					(byte)((UIntAddress >> 24) & 0xff),
					(byte)((UIntAddress >> 16) & 0xff),
					(byte)((UIntAddress >> 8) & 0xff),
					(byte)((UIntAddress >> 0) & 0xff)
				};
			return new IPAddress(bytes);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int GetInt32(byte[] bytes)
		{
			if (!BitConverter.IsLittleEndian)
				Array.Reverse(bytes);
			return BitConverter.ToInt32(bytes, 0);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int GetInt32(byte[] bytes, ref int offset)
		{
			if (!BitConverter.IsLittleEndian)
				Array.Reverse(bytes, offset, sizeof(int));
			int val = BitConverter.ToInt32(bytes, offset);
			if (!BitConverter.IsLittleEndian)
				Array.Reverse(bytes, offset, sizeof(int));
			offset += sizeof(int);
			return val;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint GetUInt32(byte[] bytes)
		{
			if (!BitConverter.IsLittleEndian)
				Array.Reverse(bytes);
			return BitConverter.ToUInt32(bytes, 0);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint GetUInt32(byte[] bytes, ref int offset)
		{
			if (!BitConverter.IsLittleEndian)
				Array.Reverse(bytes, offset, sizeof(uint));
			uint val = BitConverter.ToUInt32(bytes, offset);
			if (!BitConverter.IsLittleEndian)
				Array.Reverse(bytes, offset, sizeof(uint));
			offset += sizeof(uint);
			return val;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float GetSingle(byte[] bytes)
		{
			if (!BitConverter.IsLittleEndian)
				Array.Reverse(bytes);
			return BitConverter.ToSingle(bytes, 0);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float GetSingle(byte[] bytes, ref int offset)
		{
			if (!BitConverter.IsLittleEndian)
				Array.Reverse(bytes, offset, sizeof(float));
			float val = BitConverter.ToSingle(bytes, offset);
			if (!BitConverter.IsLittleEndian)
				Array.Reverse(bytes, offset, sizeof(float));
			offset += sizeof(float);
			return val;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static short GetInt16(byte[] bytes)
		{
			if (!BitConverter.IsLittleEndian)
				Array.Reverse(bytes);
			return BitConverter.ToInt16(bytes, 0);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static short GetInt16(byte[] bytes, ref int offset)
		{
			if (!BitConverter.IsLittleEndian)
				Array.Reverse(bytes, offset, sizeof(short));
			short val = BitConverter.ToInt16(bytes, offset);
			if (!BitConverter.IsLittleEndian)
				Array.Reverse(bytes, offset, sizeof(short));
			offset += sizeof(short);
			return val;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool GetBool(byte[] bytes)
		{
			if (!BitConverter.IsLittleEndian)
				Array.Reverse(bytes);
			return BitConverter.ToBoolean(bytes, 0);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool GetBool(byte[] bytes, ref int offset)
		{
			if (!BitConverter.IsLittleEndian)
				Array.Reverse(bytes, offset, sizeof(bool));
			bool val = BitConverter.ToBoolean(bytes, offset);
			if (!BitConverter.IsLittleEndian)
				Array.Reverse(bytes, offset, sizeof(bool));
			offset += sizeof(bool);
			return val;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ushort GetUInt16(byte[] bytes)
		{
			if (!BitConverter.IsLittleEndian)
				Array.Reverse(bytes);
			return BitConverter.ToUInt16(bytes, 0);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ushort GetUInt16(byte[] bytes, ref int offset)
		{
			if (!BitConverter.IsLittleEndian)
				Array.Reverse(bytes, offset, sizeof(ushort));
			ushort val = BitConverter.ToUInt16(bytes, offset);
			if (!BitConverter.IsLittleEndian)
				Array.Reverse(bytes, offset, sizeof(ushort));
			offset += sizeof(ushort);
			return val;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Vector3 GetVector3(byte[] bytes)
		{
			int offset = 0;
			return GetVector3(bytes, ref offset);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Vector3 GetVector3(byte[] bytes, ref int offset)
		{
			return new Vector3(
				GetSingle(bytes, ref offset),
				GetSingle(bytes, ref offset),
				GetSingle(bytes, ref offset)
				);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Quaternion GetQuaternion(byte[] bytes)
		{
			int offset = 0;
			return GetQuaternion(bytes, ref offset);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Quaternion GetQuaternion(byte[] bytes, ref int offset)
		{
			return new Quaternion(
				GetSingle(bytes, ref offset),
				GetSingle(bytes, ref offset),
				GetSingle(bytes, ref offset),
				GetSingle(bytes, ref offset)
				); ;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte[] GetBytes(bool value)
		{
			byte[] boolBytes = BitConverter.GetBytes(value);
			if (!BitConverter.IsLittleEndian)
				Array.Reverse(boolBytes);
			return boolBytes;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializeToBuffer(bool value, byte[] buffer, ref int offset)
		{
			byte[] boolBytes = GetBytes(value);
			boolBytes.CopyTo(buffer, offset);
			offset += sizeof(bool);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte[] GetBytes(int value)
		{
			byte[] intBytes = BitConverter.GetBytes(value);
			if (!BitConverter.IsLittleEndian)
				Array.Reverse(intBytes);
			return intBytes;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializeToBuffer(int value, byte[] buffer, ref int offset)
		{
			byte[] intBytes = GetBytes(value);
			intBytes.CopyTo(buffer, offset);
			offset += sizeof(int);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte[] GetBytes(uint value)
		{
			byte[] uintBytes = BitConverter.GetBytes(value);
			if (!BitConverter.IsLittleEndian)
				Array.Reverse(uintBytes);
			return uintBytes;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializeToBuffer(uint value, byte[] buffer, ref int offset)
		{
			byte[] uintBytes = GetBytes(value);
			uintBytes.CopyTo(buffer, offset);
			offset += sizeof(uint);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte[] GetBytes(float value)
		{
			byte[] floatBytes = BitConverter.GetBytes(value);
			if (!BitConverter.IsLittleEndian)
				Array.Reverse(floatBytes);
			return floatBytes;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializeToBuffer(float value, byte[] buffer, ref int offset)
		{
			byte[] floatBytes = GetBytes(value);
			floatBytes.CopyTo(buffer, offset);
			offset += sizeof(float);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte[] GetBytes(short value)
		{
			byte[] shortBytes = BitConverter.GetBytes(value);
			if (!BitConverter.IsLittleEndian)
				Array.Reverse(shortBytes);
			return shortBytes;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializeToBuffer(short value, byte[] buffer, ref int offset)
		{
			byte[] shortBytes = GetBytes(value);
			shortBytes.CopyTo(buffer, offset);
			offset += sizeof(short);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte[] GetBytes(ushort value)
		{
			byte[] ushortBytes = BitConverter.GetBytes(value);
			if (!BitConverter.IsLittleEndian)
				Array.Reverse(ushortBytes);
			return ushortBytes;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializeToBuffer(ushort value, byte[] buffer, ref int offset)
		{
			byte[] ushortBytes = GetBytes(value);
			ushortBytes.CopyTo(buffer, offset);
			offset += sizeof(ushort);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte[] GetBytes(string msg)
		{
			byte[] msgData = Encoding.ASCII.GetBytes(msg);
			if (!BitConverter.IsLittleEndian)
				Array.Reverse(msgData);
			return msgData;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializeToBuffer(string msg, byte[] buffer, ref int offset)
		{
			byte[] msgData = GetBytes(msg);
			msgData.CopyTo(buffer, offset);
			offset += msgData.Length;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte[] GetBytes(object obj)
		{
			BinaryFormatter bf = new BinaryFormatter();
			using (var ms = new MemoryStream())
			{
				bf.Serialize(ms, obj);
				return ms.ToArray();
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializeToBuffer(object obj, byte[] buffer, ref int offset)
		{
			byte[] objData = GetBytes(obj);
			objData.CopyTo(buffer, offset);
			offset += objData.Length;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte[] GetBytes(Vector3 vector)
		{
			int offset = 0;
			byte[] buffer = new byte[3 * sizeof(float)];
			SerializeToBuffer(vector, buffer, ref offset);
			return buffer;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializeToBuffer(Vector3 vector, byte[] buffer, ref int offset)
		{
			GetBytes(vector.x).CopyTo(buffer, offset + sizeof(float) * 0);
			GetBytes(vector.y).CopyTo(buffer, offset + sizeof(float) * 1);
			GetBytes(vector.z).CopyTo(buffer, offset + sizeof(float) * 2);
			offset += sizeof(float) * 3;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte[] GetBytes(Quaternion quaternion)
		{
			int offset = 0;
			byte[] buffer = new byte[4 * sizeof(float)];
			SerializeToBuffer(quaternion, buffer, ref offset);
			return buffer;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializeToBuffer(Quaternion quaternion, byte[] buffer, ref int offset)
		{
			GetBytes(quaternion.x).CopyTo(buffer, offset + sizeof(float) * 0);
			GetBytes(quaternion.y).CopyTo(buffer, offset + sizeof(float) * 1);
			GetBytes(quaternion.z).CopyTo(buffer, offset + sizeof(float) * 2);
			GetBytes(quaternion.w).CopyTo(buffer, offset + sizeof(float) * 3);
			offset += sizeof(float) * 4;
		}

		public static Guid GetGuid(byte[] buffer)
		{
			if (buffer.Length < 16)
			{
				Debug.LogErrorFormat("Buffer doesn't contain a GUID, because it's length is " + buffer.Length);
				return Guid.Empty;
			}
			byte[] guidBytes = new byte[16];
			buffer.CopyTo(guidBytes, buffer.Length - 16);

			return new Guid(guidBytes);
		}

		public static Guid GetGuid(byte[] buffer, ref int offset)
		{
			byte[] guidBuf = new byte[16];
			Array.Copy(buffer, offset, guidBuf, 0, 16);
			offset += 16;
			return new Guid(guidBuf);
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

		public static byte[] GetHashBytes(string hashMsg)
		{
			int length = hashMsg.Length / 2;
			byte[] bytes = new byte[length];
			for (int i = 0; i < length; i++)
			{
				bytes[i] = hexaToBytesMap[hashMsg[i * 2 + 0] - 48, hashMsg[i * 2 + 1] - 48];
			}
			return bytes;
		}


		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte[] GetGuidAndPortBuffer(Guid guid, ushort port)
		{
			byte[] buffer = new byte[18];

			//Port goes first
			BitConverter.GetBytes(port).CopyTo(buffer, 0);

			//Guid goes next
			guid.ToByteArray().CopyTo(buffer, 2);

			return buffer;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Action SanitizeAction(Action action)
		{
			return (Action)SanitizeDelegate(action);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Action<T1> SanitizeAction<T1>(Action<T1> action)
		{
			return (Action<T1>)SanitizeDelegate(action);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
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

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
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

		public const int MaxPacketSize = (MTU - PacketHeaderSize);

		public const int MaxMultiPartPacketSize = (MTU - MultiPartDataHeaderSize - PacketHeaderSize);

		public static readonly char[] invalidCallbackChars = new[] { '<', '>' };

	}
}
