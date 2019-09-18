
//System Includes
using Guid = System.Guid;
using Array = System.Array;
using System.Collections.Generic;

//Unity Includes
using UnityEngine;
using UnityEngine.UI;

//Network Specifics
using NUNet;
using UnityEngine.SceneManagement;

public class NetworkAppManager : MonoBehaviour
{
	private List<Guid> connectedPlayers = new List<Guid>();
	private Dictionary<Guid, GameObject> playerObjects = new Dictionary<Guid, GameObject>();

	[SerializeField]
	private GameObject playerClientPrefab;

	[SerializeField]
	private GameObject playerServerPrefab;

	[SerializeField]
	private InputField inputField;

	[SerializeField]
	private GameObject guiCanvas;

	private void Awake()
	{
		//Register Players when they Connect
		NUServer.onClientConnected += ClientConnected;
		NUServer.onClientReconnected += ClientReconnected;

		//Remove'em when they Disconnect
		NUServer.onClientDisconnected += PlayerDisconnectFromServer;
		NUServer.onClientTimedOut += PlayerTimedOutFromServer;
		NUServer.onClientPacketReceived += ServerReceivedPacket;

		//If is Server and Client, register Player
		if (NUServer.started && NUClient.connected)
		{
			connectedPlayers.Add(NUClient.guid);
		}

		NUClient.onPacketReceived += ClientReceivedPacket;
		NUClient.onDisconnected += ClientDisconnected;
	}

	private void ClientConnected(Guid id)
	{
		connectedPlayers.Add(id);
		Packet firstPacket = new Packet("First", packetId: NUUtilities.GeneratePacketId());
		Packet secondPacket = new Packet("Second", packetId: NUUtilities.GeneratePacketId());
		Packet thirdPacket = new Packet("Third", packetId: NUUtilities.GeneratePacketId());
		Debug.LogError("Third");
		Debug.LogError("Second");
		Debug.LogError("First");
		thirdPacket.OverrideDestination(NUServer.GetConnectedClients());
		NUServer.SendReliableSequenced(thirdPacket);    //Send third first
		secondPacket.OverrideDestination(NUServer.GetConnectedClients());
		NUServer.SendReliableSequenced(secondPacket);   //Send second second
		firstPacket.OverrideDestination(NUServer.GetConnectedClients());
		NUServer.SendReliableSequenced(firstPacket);    //Then first third
	}

	private void ClientReconnected(Guid id)
	{
		connectedPlayers.Add(id);
	}

	private void ClientDisconnected()
	{
		SceneManager.LoadScene(0);
	}

	private void FixedUpdate()
	{
		if (NUServer.started) //Is Server!
		{
			//Send Game-state to everyone online
			Packet stateData = new Packet(GetStateMsg(), NUServer.GetConnectedClients());
			NUServer.SendUnreliable(stateData);
		}

		if (Input.GetKeyUp(KeyCode.O) && NUClient.connected)
		{
			byte[] reandomBuffer = new byte[8192];
			for (int i = 0; i < 8192; i++)
			{
				reandomBuffer[i] = (byte)(Random.Range(0, 255));
			}
			Packet multipartTestPacket = new Packet(reandomBuffer);
			NUClient.SendReliable(multipartTestPacket);
		}

		if (Input.GetKeyUp(KeyCode.I) && NUServer.started)
		{
			byte[] reandomBuffer = new byte[8192];
			for (int i = 0; i < 8192; i++)
			{
				reandomBuffer[i] = (byte)(Random.Range(0, 255));
			}
			Packet multipartTestPacket = new Packet(reandomBuffer,
				NUServer.GetConnectedClients(), Packet.TypeFlag.DATA);
			NUServer.SendReliable(multipartTestPacket);
		}

		if (Input.GetKeyUp(KeyCode.J))
		{
			Packet firstPacket = new Packet("First", packetId: NUUtilities.GeneratePacketId());
			Packet secondPacket = new Packet("Second", packetId: NUUtilities.GeneratePacketId());
			Packet thirdPacket = new Packet("Third", packetId: NUUtilities.GeneratePacketId());
			Debug.LogError("Third");
			Debug.LogError("Second");
			Debug.LogError("First");
			if (NUServer.started)
			{
				thirdPacket.OverrideDestination(NUServer.GetConnectedClients());
				NUServer.SendReliableSequenced(thirdPacket);    //Send third first
				secondPacket.OverrideDestination(NUServer.GetConnectedClients());
				NUServer.SendReliableSequenced(secondPacket);   //Send second second
				firstPacket.OverrideDestination(NUServer.GetConnectedClients());
				NUServer.SendReliableSequenced(firstPacket);    //Then first third
			}
			else if (NUClient.connected)
			{
				NUClient.SendReliableSequenced(thirdPacket);    //Send third first
				NUClient.SendReliableSequenced(secondPacket);   //Send second second
				NUClient.SendReliableSequenced(firstPacket);    //Then first third
			}
		}
	}

	public void Spawn()
	{
		if (inputField.text.Length == 0)
		{
			inputField.Select();
			return;
		}
		Packet spawnPacket = new Packet("Spw|" + inputField.text);
		NUClient.SendReliable(spawnPacket);
		guiCanvas.SetActive(false);
	}

	private void ServerReceivedPacket(Guid guid, Packet packet)
	{
		if (!connectedPlayers.Contains(guid))
			return;

		string msg = packet.GetMessageData();
		string[] args = msg.Split('|');
		if (args[0] == "Spw")
		{
			//Spawn Player on Random Position
			Vector3 spwnPos = Vector3.right * Random.Range(-5.0f, 5.0f);

			GameObject playerObj;
			if (NUClient.connected && guid == NUClient.guid) //Is Server Player
			{
				playerObj = GameObject.Instantiate(playerServerPrefab,
					spwnPos, Quaternion.identity);
				playerObj.AddComponent<PlayerBehaviour>();
			}
			else
			{
				playerObj = GameObject.Instantiate(playerServerPrefab,
					spwnPos, Quaternion.identity);
			}

			playerObj.name = "Player (" + args[1] + ")";
			playerObj.GetComponentInChildren<Text>().text = args[1];
			playerObjects.Add(guid, playerObj);

			string playerData = GetPlayersData();

			List<Guid> guids = new List<Guid>(playerObjects.Keys);
			NUServer.SendReliable(new Packet(playerData, guids.ToArray()));
			NUServer.SendReliable(new Packet(GetStateMsg(), new Guid[] { guid }));
		}
		else if (args[0] == "Inp")
		{
			string plData = args[1];
			GameObject playerObj;
			if (playerObjects.TryGetValue(guid, out playerObj))
			{
				string[] inpMsg = plData.Split(':');
				Vector3 input = new Vector3(
					float.Parse(inpMsg[0]),
					float.Parse(inpMsg[1]),
					float.Parse(inpMsg[2])
					);
				playerObj.transform.position += input;
			}
		}
		else if (args[0] == "Jmp")
		{
			GameObject playerObj;
			if (playerObjects.TryGetValue(guid, out playerObj))
			{
				//Can Jump check
				RaycastHit hit;
				Vector3 playerPos = playerObj.transform.position;
				if (Physics.Raycast(playerPos, Vector3.down, out hit))
				{
					if (hit.distance > 0.6f)
						return;

					Rigidbody rb = playerObj.GetComponent<Rigidbody>();
					rb.velocity = new Vector3(rb.velocity.x, 0, rb.velocity.z);
					rb.AddForce(Vector3.up * 5.0f, ForceMode.VelocityChange);
				}

			}
		}

		if (packet.id >= 0)
		{
			Debug.LogError(msg);
		}
	}

	private void ClientReceivedPacket(Packet packet)
	{
		if (NUServer.started) //Is Server and Client
			return;

		string msg = packet.GetMessageData();
		string[] args = msg.Split('|');
		if (args[0] == "Prs") //Player Profile Data
		{
			for (int i = 1; i < args.Length; i++)
			{
				string[] plData = args[i].Split(';');
				Guid guid = new Guid(plData[0]);

				GameObject playerObj;

				//Might be a reconnected player
				if (playerObjects.TryGetValue(guid, out playerObj))
				{
					playerObj.SetActive(true);
					continue;
				}

				playerObj = GameObject.Instantiate(playerClientPrefab);
				if (guid == NUClient.guid)
				{
					playerObj.AddComponent<PlayerBehaviour>();
				}
				playerObj.name = "Player (" + plData[1] + ")";
				playerObj.GetComponentInChildren<Text>().text = plData[1];
				playerObjects.Add(guid, playerObj);
			}
		}
		else if (args[0] == "Sta") //State Data
		{
			for (int i = 1; i < args.Length; i++)
			{
				string[] plData = args[i].Split(';');
				Guid guid = new Guid(plData[0]);
				GameObject playerObj;
				if (playerObjects.TryGetValue(guid, out playerObj))
				{
					string[] pos = plData[1].Split(':');
					Vector3 vPos = new Vector3(
						float.Parse(pos[0]),
						float.Parse(pos[1]),
						float.Parse(pos[2])
						);
					playerObj.transform.position = vPos;
					string[] rot = plData[2].Split(':');
					Quaternion qRot = new Quaternion(
						float.Parse(rot[0]),
						float.Parse(rot[1]),
						float.Parse(rot[2]),
						float.Parse(rot[3])
						);
					playerObj.transform.rotation = qRot;
				}
			}
		}
		else if (args[0] == "Dsc")
		{
			Guid guid = new Guid(args[1]);
			GameObject playerObj;
			if (playerObjects.TryGetValue(guid, out playerObj))
			{
				playerObj.SetActive(false);
			}
		}

		if (packet.id >= 0)
		{
			Debug.LogError(msg);
		}
	}

	private void PlayerDisconnectFromServer(Guid guid)
	{
		GameObject playerObject;
		if (playerObjects.TryGetValue(guid, out playerObject))
		{
			GameObject.Destroy(playerObject);
			playerObjects.Remove(guid);
		}

		NUServer.SendReliable(new Packet("Dsc|" + guid, NUServer.GetConnectedClients()));
		connectedPlayers.Remove(guid);
	}

	private void PlayerTimedOutFromServer(Guid guid)
	{
		GameObject playerObject;
		if (playerObjects.TryGetValue(guid, out playerObject))
		{
			playerObject.SetActive(false);
		}

		NUServer.SendReliable(new Packet("Dsc|" + guid, NUServer.GetConnectedClients()));
		connectedPlayers.Remove(guid);
	}

	private string GetPlayersData()
	{
		string playerData = "Prs";
		foreach (var player in playerObjects)
		{
			Vector3 pos = player.Value.transform.position;
			playerData += "|" + player.Key.ToString() + ";" + player.Value.name;
		}
		return playerData;
	}

	private string GetStateMsg()
	{
		string stateData = "Sta";
		foreach (var player in playerObjects)
		{
			Vector3 pos = player.Value.transform.position;
			stateData += "|" + player.Key.ToString() + ";" + pos.x.ToString("R") + ":" + pos.y.ToString("R") + ":" + pos.z.ToString("R");
			Quaternion rot = player.Value.transform.rotation;
			stateData += ";" + rot.x.ToString("R") + ":" + rot.y.ToString("R") + ":" + rot.z.ToString("R") + ":" + rot.w.ToString("R");
		}
		return stateData;
	}

	private byte[] GetStateData()
	{
		byte[] state = new byte[playerObjects.Count * (32/*GUID*/ + 12/*XYZ*/)];
		int i = 0;
		foreach (var player in playerObjects)
		{
			//Copy GUID Data
			Array.Copy(player.Key.ToByteArray(), 0, state, i * 44, 32);

			//Copy Position Data
			Vector3 pos = player.Value.transform.position;
			byte[] posData = NUUtilities.GetBytes(pos);
			Array.Copy(posData, 0, state, i * 44 + 32, 12);
		}
		return state;
	}

	private void OnApplicationQuit()
	{
		NUServer.Shutdown();
	}

}
