
using System.IO;
using System.Text;

using UnityEngine;

using NUNet;

public class WebAPITest : MonoBehaviour
{
    private void Awake()
    {
        NUWeb.onConnected += () =>
        {
            StringBuilder sw = new StringBuilder();
            sw.AppendLine("GET / HTTP/1.1");
            //sw.AppendLine("Host: 191.4.232.35");
            sw.AppendLine("Connection: keep-alive");
            sw.AppendLine("User-Agent: NUWebClient");
            sw.AppendLine("cache-control: no-cache");
            sw.AppendLine(); //REALLY NEEDED!!! OTHERWISE IT WONT WORK

            Debug.Log(sw.ToString());
            NUWeb.Send(sw.ToString());
        };

        NUWeb.onResponse += (WebPacket webPacket) =>
        {
            Debug.Log(webPacket);
        };
    }

    private void Start()
    {
        NUWeb.Connect("10.0.1.9");
    }

}
