using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System;
using System.Text;

public class ReceiverLatest : MonoBehaviour
{
    [Header("Network")]
    public int listenPort = 3333;
    public bool announceToEsp = true;
    public int espDiscoveryPort = 13333;
    [Min(0.05f)] public float announceIntervalSeconds = 0.25f;
    [Min(0f)] public float announceDurationSeconds = 10f;
    public string announcePayload = "XRForce";

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
    [Tooltip("Counts every UDP pressure packet received on the background thread.")]
    public uint receivedPacketSequence = 0;
    [Tooltip("Measured receive rate from the background UDP thread.")]
    public float receivedPacketsPerSecond = 0f;
    [Tooltip("Packets skipped because multiple UDP packets arrived before Unity consumed the latest one.")]
    public uint packetsSkippedBeforeConsume = 0;
    [Header("Debug")]
    public bool logReceiveStats = false;
    [Min(0.1f)] public float logReceiveStatsInterval = 1f;

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
    uint _latestReceivedPacketSequence = 0;
    uint _lastConsumedReceivedPacketSequence = 0;
    int _receivedPacketsPendingForRate = 0;
    int _receivedPacketsInWindow = 0;
    float _receiveRateTimer = 0f;
    float _logReceiveStatsTimer = 0f;
    float _announceTimer = 0f;
    float _announceElapsed = 0f;
    byte[] _announcePayloadBytes;
    IPEndPoint _broadcastDiscoveryEndPoint;

    void Start()
    {
        Application.runInBackground = true;

        heatmapA = new float[9];
        heatmapB = new float[9];
        channels = new float[64];
        _latestHeatA = new float[9];
        _latestHeatB = new float[9];

        _udp = new UdpClient(listenPort);
        _udp.EnableBroadcast = true;
        _udp.Client.ReceiveTimeout = 2000;
        _announcePayloadBytes = Encoding.ASCII.GetBytes(string.IsNullOrEmpty(announcePayload) ? "XRForce" : announcePayload);
        _broadcastDiscoveryEndPoint = new IPEndPoint(IPAddress.Broadcast, espDiscoveryPort);

        _running = true;
        _thread = new Thread(ReceiveLoop) { IsBackground = true };
        _thread.Start();

        Debug.Log($"UDP listening on port {listenPort}");
    }

    void Update()
    {
        AnnounceToEspIfNeeded();
        FlushLatestPacket();
        UpdateReceiveRate();
        LogReceiveStatsIfEnabled();

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

    void AnnounceToEspIfNeeded()
    {
        if (!announceToEsp || _udp == null || _announcePayloadBytes == null)
            return;

        if (announceDurationSeconds > 0f && _announceElapsed >= announceDurationSeconds)
            return;

        _announceElapsed += Time.unscaledDeltaTime;
        _announceTimer -= Time.unscaledDeltaTime;

        if (_announceTimer > 0f)
            return;

        _announceTimer = announceIntervalSeconds;

        try
        {
            _udp.Send(_announcePayloadBytes, _announcePayloadBytes.Length, _broadcastDiscoveryEndPoint);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("ESP discovery announce failed: " + ex.Message, this);
        }
    }

    public bool FlushLatestPacket()
    {
        if (!_hasNew)
            return false;

        lock (_lock)
        {
            if (!_hasNew)
                return false;

            if (channelCount > _latestChannelCount)
                Array.Clear(channels, _latestChannelCount, channelCount - _latestChannelCount);

            Array.Copy(_latestChannels, channels, _latestChannelCount);
            channelCount = _latestChannelCount;
            Array.Copy(_latestHeatA, heatmapA, 9);
            Array.Copy(_latestHeatB, heatmapB, 9);
            packetSequence++;
            receivedPacketSequence = _latestReceivedPacketSequence;

            if (_lastConsumedReceivedPacketSequence != 0 &&
                _latestReceivedPacketSequence > _lastConsumedReceivedPacketSequence + 1)
            {
                packetsSkippedBeforeConsume += _latestReceivedPacketSequence - _lastConsumedReceivedPacketSequence - 1;
            }

            _lastConsumedReceivedPacketSequence = _latestReceivedPacketSequence;
            _hasNew = false;
            return true;
        }
    }

    void UpdateReceiveRate()
    {
        int receivedSinceLastUpdate;

        lock (_lock)
        {
            receivedSinceLastUpdate = _receivedPacketsPendingForRate;
            _receivedPacketsPendingForRate = 0;
        }

        _receivedPacketsInWindow += receivedSinceLastUpdate;
        _receiveRateTimer += Time.unscaledDeltaTime;

        if (_receiveRateTimer < 1f)
            return;

        receivedPacketsPerSecond = _receivedPacketsInWindow / _receiveRateTimer;
        _receivedPacketsInWindow = 0;
        _receiveRateTimer = 0f;
    }

    void LogReceiveStatsIfEnabled()
    {
        if (!logReceiveStats)
            return;

        _logReceiveStatsTimer += Time.unscaledDeltaTime;
        if (_logReceiveStatsTimer < logReceiveStatsInterval)
            return;

        _logReceiveStatsTimer = 0f;
        Debug.Log(
            $"Pressure UDP: receivedHz={receivedPacketsPerSecond:F1}, ageMs={lastPacketAgeSec * 1000f:F0}, " +
            $"receivedSeq={receivedPacketSequence}, consumedSeq={packetSequence}, skippedBeforeConsume={packetsSkippedBeforeConsume}");
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
                    _latestReceivedPacketSequence++;
                    _receivedPacketsPendingForRate++;
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
