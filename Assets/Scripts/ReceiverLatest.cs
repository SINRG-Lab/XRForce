using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System;

public class ReceiverLatest : MonoBehaviour
{
    [Header("Network")]
    public int listenPort = 3333;

    [Header("Data")]
    [Tooltip("Latest packet values. Read the first channelCount entries.")]
    public float[] channels = new float[64];
    public int channelCount = 0;

    [Header("Heatmap A (3x3): channels[0..8]")]
    public float[] heatmapA = new float[9];

    [Header("Heatmap B (3x3): channels[9..17]")]
    public float[] heatmapB = new float[9];

    [Header("Status")]
    public bool connected = false;
    public string lastHeader = "";   // e.g., "FSR(18)"
    public string lastSender = "";
    public float lastPacketAgeSec = 999f;
    public uint packetSequence = 0;

    UdpClient _udp;
    Thread _thread;
    volatile bool _running;

    readonly object _lock = new object();
    readonly float[] _latestChannels = new float[64];
    float[] _latestHeatA = new float[9];
    float[] _latestHeatB = new float[9];
    volatile bool _hasNew = false;
    int _latestChannelCount = 0;

    int _lastPacketTickMs = -1;

    void Start()
    {
        Application.runInBackground = true;

        heatmapA = new float[9];
        heatmapB = new float[9];
        channels = new float[64];
        _latestHeatA = new float[9];
        _latestHeatB = new float[9];

        _udp = new UdpClient(listenPort);
        _udp.Client.ReceiveTimeout = 2000;

        _running = true;
        _thread = new Thread(ReceiveLoop) { IsBackground = true };
        _thread.Start();

        Debug.Log($"UDP listening on port {listenPort}");
    }

    void Update()
    {
        if (_hasNew)
        {
            lock (_lock)
            {
                if (channelCount > _latestChannelCount)
                    Array.Clear(channels, _latestChannelCount, channelCount - _latestChannelCount);

                Array.Copy(_latestChannels, channels, _latestChannelCount);
                channelCount = _latestChannelCount;
                Array.Copy(_latestHeatA, heatmapA, 9);
                Array.Copy(_latestHeatB, heatmapB, 9);
                packetSequence++;
                _hasNew = false;
            }
        }

        if (_lastPacketTickMs != -1)
        {
            int now = Environment.TickCount;
            int deltaMs = unchecked(now - _lastPacketTickMs);
            lastPacketAgeSec = deltaMs / 1000f;
            connected = lastPacketAgeSec < 2.0f;
        }
        else
        {
            connected = false;
            lastPacketAgeSec = 999f;
        }
    }

    void ReceiveLoop()
    {
        IPEndPoint any = new IPEndPoint(IPAddress.Any, 0);

        while (_running)
        {
            try
            {
                byte[] data = _udp.Receive(ref any);
                if (data == null || data.Length < 4) continue;

                if (data[0] != (byte)'F' || data[1] != (byte)'S' || data[2] != (byte)'R')
                    continue;

                // New protocol: data[3] is BINARY count (e.g., 18)
                int count = data[3];
                int needed = 4 + count * 4;
                if (count < 1 || count > 64) continue;
                if (data.Length < needed) continue;

                lock (_lock)
                {
                    for (int i = 0; i < count; i++)
                        _latestChannels[i] = BitConverter.ToSingle(data, 4 + i * 4);

                    for (int i = count; i < _latestChannels.Length; i++)
                        _latestChannels[i] = 0f;

                    for (int i = 0; i < 9; i++)
                        _latestHeatA[i] = (i < count) ? _latestChannels[i] : 0f;

                    for (int i = 0; i < 9; i++)
                    {
                        int src = 9 + i;
                        _latestHeatB[i] = (src < count) ? _latestChannels[src] : 0f;
                    }

                    _latestChannelCount = count;
                    _hasNew = true;
                }

                lastHeader = $"FSR({count})";
                lastSender = any.Address.ToString();
                _lastPacketTickMs = Environment.TickCount;
            }
            catch (SocketException) { /* timeout */ }
            catch (Exception ex)
            {
                Debug.LogWarning("UDP recv error: " + ex.Message);
            }
        }
    }

    void OnDestroy()
    {
        _running = false;
        try { _udp?.Close(); } catch { }
        if (_thread != null && _thread.IsAlive) _thread.Join(200);
    }
}
