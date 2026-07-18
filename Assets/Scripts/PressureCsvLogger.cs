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

    [Header("Pressure Error")]
    [Tooltip("Ideal mean pressure for sensor array A, in the same units as the received values.")]
    public float idealPressureA = 0.15f;
    [Tooltip("Ideal mean pressure for sensor array B, in the same units as the received values.")]
    public float idealPressureB = 0.15f;
    [Min(0f)] public float pressureTolerance = 0.05f;

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

        string folderPath = LogRunSession.GetSubfolderPath(folderName);

        _filePath = Path.Combine(folderPath, GetSessionFileName());
        _headerWritten = File.Exists(_filePath) && new FileInfo(_filePath).Length > 0;
        EnsureWriterAndHeader();
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
        EnsureWriterAndHeader();

        var sb = _rowBuilder;
        sb.Clear();

        float meanA = CalculateMean(source.heatmapA);
        float meanB = CalculateMean(source.heatmapB);
        float errorA = meanA - idealPressureA;
        float errorB = meanB - idealPressureB;
        float absErrorA = Mathf.Abs(errorA);
        float absErrorB = Mathf.Abs(errorB);
        TransferMetricsLogger.GetCurrentContext(out string userId, out string taskName, out int transferIndex);

        sb.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture));
        AppendCsvValue(sb, Time.realtimeSinceStartup);
        AppendCsvText(sb, userId);
        AppendCsvText(sb, taskName);
        AppendCsvValue(sb, transferIndex);
        AppendCsvValue(sb, source.connected ? 1 : 0);
        AppendCsvValue(sb, source.lastPacketAgeSec);
        AppendCsvValue(sb, source.packetSequence);
        AppendCsvValue(sb, source.channelCount);
        AppendCsvText(sb, source.lastHeader);
        AppendCsvText(sb, source.lastSender);
        AppendCsvValue(sb, meanA);
        AppendCsvValue(sb, meanB);
        AppendCsvValue(sb, idealPressureA);
        AppendCsvValue(sb, idealPressureB);
        AppendCsvValue(sb, errorA);
        AppendCsvValue(sb, errorB);
        AppendCsvValue(sb, absErrorA);
        AppendCsvValue(sb, absErrorB);
        AppendCsvValue(sb, absErrorA <= pressureTolerance ? 1 : 0);
        AppendCsvValue(sb, absErrorB <= pressureTolerance ? 1 : 0);

        for (int i = 0; i < 9; i++) AppendCsvValue(sb, source.heatmapA[i]);
        for (int i = 0; i < 9; i++) AppendCsvValue(sb, source.heatmapB[i]);

        TransferMetricsLogger.RecordPressureSample(
            meanA,
            meanB,
            idealPressureA,
            idealPressureB,
            pressureTolerance);

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

    void EnsureWriterAndHeader()
    {
        if (_writer == null)
        {
            _writer = new StreamWriter(_filePath, true, new UTF8Encoding(false));
            _writer.AutoFlush = false;
        }

        if (_headerWritten)
            return;

        var sb = _rowBuilder;
        sb.Clear();
        sb.Append("timestamp_local,time_seconds,user_id,task_name,transfer_index,connected,packet_age_seconds,packet_sequence,channel_count,last_header,last_sender,mean_a,mean_b,ideal_a,ideal_b,error_a,error_b,abs_error_a,abs_error_b,within_tolerance_a,within_tolerance_b");

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
        _writer.Flush();
        sb.Clear();
        _headerWritten = true;
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

    static float CalculateMean(float[] values)
    {
        float sum = 0f;
        for (int i = 0; i < 9; i++)
            sum += values[i];

        return sum / 9f;
    }
}
