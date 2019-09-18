

using UnityEngine;

using NUNet;

public class PlayerBehaviour : MonoBehaviour
{


    private void Start()
    {

    }

    private void Update()
    {
        if(!NUClient.connected)
            return;

        if (Input.GetKeyDown(KeyCode.Space))
        {
            NUClient.SendReliable(new Packet("Jmp"));
        }

        //Stream Player Input
        Vector3 input = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical"));
        input *= (Time.deltaTime * 5.0f);

        string inpMsg = "Inp|";
        inpMsg += input.x.ToString("R") + ":" + input.y.ToString("R") + ":" + input.z.ToString("R");
        Packet inpPacket = new Packet(inpMsg);
        NUClient.SendUnreliable(inpPacket);
    }
}
