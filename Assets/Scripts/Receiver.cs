using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System;

public class Receiver : MonoBehaviour
{
    [Header("Network")]
    public int listenPort = 3333;

    [Header("Data")]
    public float force_L_N = 0f;
    public float force_R_N = 0f;
    public bool connected = false;

    UdpClient _udp;
    Thread _thread;
    volatile bool _running;

    void Start()
    {
        Application.runInBackground = true; // keep receiving when not focused
        _udp = new UdpClient(listenPort);
        _udp.Client.ReceiveTimeout = 2000;  // 2s
        _running = true;
        _thread = new Thread(ReceiveLoop) { IsBackground = true };
        _thread.Start();
        Debug.Log($"UDP listening on port {listenPort}");
    }

    void ReceiveLoop()
    {
        IPEndPoint any = new IPEndPoint(IPAddress.Any, 0);
        while (_running)
        {
            try
            {
                byte[] data = _udp.Receive(ref any);
                if (data == null || data.Length < 8) continue;

                // Check header 'F','S','R','1'
                if (data.Length >= 12 && data[0]=='F' && data[1]=='S' && data[2]=='R' && data[3]=='2')
                {
                    float f_l = BitConverter.ToSingle(data, 4);
                    float f_r = BitConverter.ToSingle(data, 8);
                    force_L_N = f_l;
                    force_R_N = f_r;
                    connected = true;
                }
            }
            catch (SocketException) { /* timeout, loop */ }
            catch (Exception ex) { Debug.LogWarning("UDP recv error: " + ex.Message); }
        }
    }

    void OnDestroy()
    {
        _running = false;
        try { _udp?.Close(); } catch {}
        if (_thread != null && _thread.IsAlive) _thread.Join(200);
    }
}
