using UnityEngine;
using System.IO;
using System.Text;

public class PressureCsvLogger : MonoBehaviour
{
    public ReceiverLatest receiver;

    [Header("File")]
    public string folderName = "PressureLogs";
    public string fileName = $"pressure_{System.DateTime.Now:yyyyMMdd_HHmmss}.csv";

    [Header("Logging")]
    public bool enableLogging = true;
    public bool logOnlyWhenConnected = true;
    public float logHz = 20f;
    [Min(1)] public int flushEveryRows = 20;

    string _filePath;
    bool _headerWritten = false;
    float _nextTime;
    StreamWriter _writer;
    int _pendingRows;
    readonly StringBuilder _rowBuilder = new StringBuilder(160);

    void Start()
    {
        // Create folder if needed
        string folderPath = Path.Combine(Application.persistentDataPath, folderName);
        Directory.CreateDirectory(folderPath);

        _filePath = Path.Combine(folderPath, fileName);
        Debug.Log("Logging CSV to: " + _filePath);
    }

    void Update()
    {
        if (!enableLogging) return;
        if (Time.time < _nextTime) return;
        _nextTime = Time.time + 1f / logHz;

        if (receiver == null) return;
        if (logOnlyWhenConnected && !receiver.connected) return;

        // Expect 2 heatmaps of 9 each
        if (receiver.heatmapA == null || receiver.heatmapB == null) return;
        if (receiver.heatmapA.Length < 9 || receiver.heatmapB.Length < 9) return;

        WriteRow(receiver.heatmapA, receiver.heatmapB);
    }

    void WriteRow(float[] a, float[] b)
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
            sb.Append("timestamp");

            for (int i = 0; i < 9; i++) sb.Append($",A{i}");
            for (int i = 0; i < 9; i++) sb.Append($",B{i}");

            sb.AppendLine();
            _writer.Write(sb.ToString());
            sb.Clear();
            _headerWritten = true;
        }

        // Timestamp
        sb.Append(System.DateTime.Now.ToString("HH:mm:ss.fff"));

        for (int i = 0; i < 9; i++) sb.Append("," + a[i].ToString("F4"));
        for (int i = 0; i < 9; i++) sb.Append("," + b[i].ToString("F4"));

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

    void FlushWriter()
    {
        if (_writer == null)
            return;

        _writer.Flush();
        _pendingRows = 0;
    }
}
