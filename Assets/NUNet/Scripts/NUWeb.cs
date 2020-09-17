
//System Includes
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Collections.Generic;

//Unity Includes
using UnityEngine;
using NUNet.Internal;

namespace NUNet
{
	public static class NUWeb
	{
		//Callbacks
		public static Action onConnected;
		public static Action onDisconnected;
		public static Action<WebPacket> onResponse;

		//Receiving Queue
		private static readonly object msgQueueLock;
		private static Queue<WebPacket> msgQueue;
		private static bool hasDisconnected;
		private static bool hasConnected;

		//Internal State Variables
		private static IPAddress conAddress;
		private static Socket webSocket;

		//MonoBehaviour instance of Unity component
		private static NUWebComponent component;

		//Returns wheather or not the Socket is connected to something
		public static bool connected { get { return webSocket.Connected; } private set { } }

		static NUWeb()
		{
			webSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			webSocket.ExclusiveAddressUse = false;
			webSocket.SendTimeout = 500;
			webSocket.ReceiveTimeout = 1000;
			msgQueueLock = new object();
			msgQueue = null;
			hasDisconnected = false;
			hasConnected = false;
			conAddress = new IPAddress(0);
		}

		public static void Connect(string address, Action updateHook = null, Action endHook = null)
		{
			try
			{
				IPAddress[] addresses = Dns.GetHostAddresses(address);
				if (address.Length > 0)
				{
					Connect(addresses[0], updateHook, endHook);
				}
				else
				{
					Debug.LogError("Address (" + address + ") could not be resolved...");
				}
			}
			catch (Exception ex)
			{
				Debug.LogError(ex.ToString());
			}
		}

		public static void Connect(IPAddress address, Action updateHook = null, Action endHook = null)
		{
			//Check for GameObject Hook
			if (updateHook != null && endHook != null)
			{
				updateHook += ProcessQueues;
				endHook += Disconnect;
			}
			else
			{
				if (component == null)
				{
					GameObject webObject = new GameObject("NUWebObject");
					//clientObject.hideFlags = HideFlags.HideAndDontSave;
					GameObject.DontDestroyOnLoad(webObject);
					component = webObject.AddComponent<NUWebComponent>();
					//clientObject.hideFlags = HideFlags.HideInInspector;
				}
			}

			try
			{
				if (!conAddress.Equals(address) && webSocket.Connected)
				{
					webSocket.Disconnect(true);
					hasDisconnected = true;
				}

				webSocket.BeginConnect(address, 80, EndConnect, address);
			}
			catch (Exception ex)
			{
				Debug.LogError(ex.ToString());
			}
		}

		private static void EndConnect(IAsyncResult asyncResult)
		{
			try
			{
				webSocket.EndConnect(asyncResult);
				conAddress = (IPAddress)asyncResult.AsyncState;

				//Set hasConnected flag for main thread callback
				hasConnected = true;

				//Create Message Queue
				lock (msgQueueLock)
				{
					msgQueue = new Queue<WebPacket>(NUUtilities.MaxBufferedPackets);
				}

				//Start Receiving
				byte[] receiveBuffer = new byte[NUUtilities.MTU];
				webSocket.BeginReceive(receiveBuffer, 0, NUUtilities.MTU, SocketFlags.None,
					EndReceive, receiveBuffer);
			}
			catch (Exception ex)
			{
				Debug.LogError(ex.ToString());
			}
		}

		public static void EndReceive(IAsyncResult asyncResult)
		{
			try
			{
				int size = webSocket.EndReceive(asyncResult);
				byte[] receiveBuffer = ((byte[])asyncResult.AsyncState);
				if (size > 0)
				{
					//Copy data to a Size Fitting Buffer
					byte[] data = new byte[size];
					Array.Copy(receiveBuffer, data, size);

					lock (msgQueueLock)
					{
						msgQueue.Enqueue(new WebPacket(data));
					}
				}

				//Keep receiving
				webSocket.BeginReceive(receiveBuffer, 0, NUUtilities.MTU, SocketFlags.None,
					EndReceive, receiveBuffer);
			}
			catch (Exception ex)
			{
				if (!webSocket.Connected)
					return;

				Debug.LogError(ex.ToString());
				webSocket.Disconnect(true);
				hasDisconnected = true;
			}
		}

		public static void Send(string msg)
		{
			try
			{
				byte[] bytes = Encoding.ASCII.GetBytes(msg);
				webSocket.BeginSend(bytes, 0, bytes.Length, SocketFlags.None, EndSend, null);
			}
			catch (Exception ex)
			{
				if (!webSocket.Connected)
					return;

				Debug.LogError(ex.ToString());
				webSocket.Disconnect(true);
				hasDisconnected = true;
			}
		}

		private static void EndSend(IAsyncResult asyncResult)
		{
			try
			{
				webSocket.EndSend(asyncResult);
			}
			catch (Exception ex)
			{
				Debug.LogError(ex.ToString());
				webSocket.Disconnect(true);
				hasDisconnected = true;
			}
		}

		internal static void ProcessQueues()
		{
			if (hasConnected)
			{
				//Reset flag
				hasConnected = false;

				//Do proper callbacks
				if (onConnected != null)
					onConnected();
			}

			if (hasDisconnected)
			{
				//Reset flag
				hasDisconnected = false;

				//Do proper callbacks
				if (onDisconnected != null)
					onDisconnected();

				lock (msgQueueLock)
				{
					msgQueue.Clear();
					msgQueue = null;
				}

				return;
			}

			if (msgQueue == null)
				return;

			lock (msgQueueLock)
			{
				while (msgQueue.Count > 0)
				{
					WebPacket msg = msgQueue.Dequeue();

					//Do proper callbacks
					if (onResponse != null)
						onResponse(msg);
				}
			}
		}

		public static void Disconnect()
		{
			if (webSocket.Connected)
			{
				webSocket.Disconnect(true);
			}

			lock (msgQueueLock)
			{
				msgQueue.Clear();
				msgQueue = null;
			}

			//Do proper callbacks
			if (onDisconnected != null)
				onDisconnected();

			//Clear all callbacks
			onConnected = null;
			onDisconnected = null;
			onResponse = null;
		}

	}
}
