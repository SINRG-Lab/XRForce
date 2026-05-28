using UnityEngine;
using System;
using System.Globalization;
using System.IO;
using System.Text;

public class PressureCsvLogger : MonoBehaviour
{
    public ReceiverLatest receiver;

    [Header("File")]
    public string folderName = "PressureLogs";
    public string fileName = "pressure_log.csv";
    public bool appendTimestampToFileName = true;

    [Header("Logging")]
    public bool enableLogging = true;
    public bool logOnlyWhenConnected = true;
    public bool logOnlyNewPackets = true;
    [Min(0.1f)]
    public float logHz = 20f;
    [Min(1)] public int flushEveryRows = 20;

    string _filePath;
    bool _headerWritten = false;
    float _nextTime;
    StreamWriter _writer;
    int _pendingRows;
    uint _lastLoggedPacketSequence;
    readonly StringBuilder _rowBuilder = new StringBuilder(256);
    static readonly char[] CsvQuoteChars = { ',', '"', '\n', '\r' };

    void Start()
    {
        if (receiver == null)
            receiver = FindReceiver();

        if (receiver == null)
        {
            Debug.LogWarning($"{nameof(PressureCsvLogger)} could not find a {nameof(ReceiverLatest)} in the scene.", this);
            enabled = false;
            return;
        }

        string folderPath = Path.Combine(Application.persistentDataPath, folderName);
        Directory.CreateDirectory(folderPath);

        _filePath = Path.Combine(folderPath, GetSessionFileName());
        _headerWritten = File.Exists(_filePath) && new FileInfo(_filePath).Length > 0;
        Debug.Log("Logging pressure CSV to: " + _filePath, this);
    }

    void LateUpdate()
    {
        if (!enableLogging) return;
        if (Time.time < _nextTime) return;
        _nextTime = Time.time + 1f / Mathf.Max(0.1f, logHz);

        if (receiver == null) return;
        if (logOnlyWhenConnected && !receiver.connected) return;
        if (logOnlyNewPackets && receiver.packetSequence == _lastLoggedPacketSequence) return;

        if (receiver.heatmapA == null || receiver.heatmapB == null) return;
        if (receiver.heatmapA.Length < 9 || receiver.heatmapB.Length < 9) return;

        WriteRow(receiver);
        _lastLoggedPacketSequence = receiver.packetSequence;
    }

    void WriteRow(ReceiverLatest source)
    {
        if (_writer == null)
        {
            _writer = new StreamWriter(_filePath, true, new UTF8Encoding(false));
            _writer.AutoFlush = false;
        }

        var sb = _rowBuilder;
        sb.Clear();

        // Header once
        if (!_headerWritten)
        {
            sb.Append("timestamp_local,time_seconds,connected,packet_age_seconds,packet_sequence,channel_count,last_header,last_sender");

            for (int i = 0; i < 9; i++)
            {
                sb.Append(",A");
                sb.Append(i);
            }

            for (int i = 0; i < 9; i++)
            {
                sb.Append(",B");
                sb.Append(i);
            }

            sb.AppendLine();
            _writer.Write(sb.ToString());
            sb.Clear();
            _headerWritten = true;
        }

        sb.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture));
        AppendCsvValue(sb, Time.realtimeSinceStartup);
        AppendCsvValue(sb, source.connected ? 1 : 0);
        AppendCsvValue(sb, source.lastPacketAgeSec);
        AppendCsvValue(sb, source.packetSequence);
        AppendCsvValue(sb, source.channelCount);
        AppendCsvText(sb, source.lastHeader);
        AppendCsvText(sb, source.lastSender);

        for (int i = 0; i < 9; i++) AppendCsvValue(sb, source.heatmapA[i]);
        for (int i = 0; i < 9; i++) AppendCsvValue(sb, source.heatmapB[i]);

        sb.AppendLine();
        _writer.Write(sb.ToString());
        _pendingRows++;

        if (_pendingRows >= flushEveryRows)
            FlushWriter();
    }

    void OnDestroy()
    {
        FlushWriter();
        _writer?.Dispose();
    }

    void OnDisable()
    {
        FlushWriter();
    }

    void OnApplicationPause(bool isPaused)
    {
        if (isPaused)
            FlushWriter();
    }

    void OnApplicationQuit()
    {
        FlushWriter();
    }

    void FlushWriter()
    {
        if (_writer == null)
            return;

        _writer.Flush();
        _pendingRows = 0;
    }

    string GetSessionFileName()
    {
        string resolvedFileName = string.IsNullOrWhiteSpace(fileName) ? "pressure_log.csv" : fileName.Trim();

        if (!appendTimestampToFileName)
            return resolvedFileName;

        string extension = Path.GetExtension(resolvedFileName);
        string nameWithoutExtension = Path.GetFileNameWithoutExtension(resolvedFileName);

        if (string.IsNullOrEmpty(extension))
            extension = ".csv";

        return $"{nameWithoutExtension}_{DateTime.Now:yyyyMMdd_HHmmss}{extension}";
    }

    static ReceiverLatest FindReceiver()
    {
#if UNITY_2023_1_OR_NEWER
        return FindFirstObjectByType<ReceiverLatest>();
#else
        return FindObjectOfType<ReceiverLatest>();
#endif
    }

    static void AppendCsvValue(StringBuilder sb, float value)
    {
        sb.Append(',');
        sb.Append(value.ToString("F4", CultureInfo.InvariantCulture));
    }

    static void AppendCsvValue(StringBuilder sb, int value)
    {
        sb.Append(',');
        sb.Append(value.ToString(CultureInfo.InvariantCulture));
    }

    static void AppendCsvValue(StringBuilder sb, uint value)
    {
        sb.Append(',');
        sb.Append(value.ToString(CultureInfo.InvariantCulture));
    }

    static void AppendCsvText(StringBuilder sb, string value)
    {
        sb.Append(',');

        if (string.IsNullOrEmpty(value))
            return;

        bool mustQuote = value.IndexOfAny(CsvQuoteChars) >= 0;
        if (!mustQuote)
        {
            sb.Append(value);
            return;
        }

        sb.Append('"');
        for (int i = 0; i < value.Length; i++)
        {
            if (value[i] == '"')
                sb.Append("\"\"");
            else
                sb.Append(value[i]);
        }
        sb.Append('"');
    }
}
