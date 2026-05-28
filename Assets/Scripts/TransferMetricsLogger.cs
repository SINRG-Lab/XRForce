using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class TransferMetricsLogger
{
    public const string UserIdPlayerPrefsKey = "XRForce.UserId";

    const string FolderName = "TransferLogs";

    class TransferState
    {
        public bool Active;
        public int TransferIndex;
        public int SpawnedAttempts;
        public int BreakCount;
        public float StartRealtime;
        public DateTime StartLocal;
    }

    static readonly Dictionary<string, TransferState> StatesByTask = new Dictionary<string, TransferState>();
    static readonly StringBuilder RowBuilder = new StringBuilder(256);
    static string _filePath;
    static StreamWriter _writer;
    static bool _headerWritten;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _writer?.Dispose();
        _writer = null;
        _filePath = null;
        _headerWritten = false;
        StatesByTask.Clear();
    }

    public static void SetUserId(string userId)
    {
        PlayerPrefs.SetString(UserIdPlayerPrefsKey, string.IsNullOrWhiteSpace(userId) ? "unknown_user" : userId.Trim());
        PlayerPrefs.Save();
    }

    public static void ResetTaskProgress()
    {
        string taskName = GetTaskName();
        TransferState state = GetState(taskName);
        state.Active = true;
        state.TransferIndex = 1;
        state.SpawnedAttempts = 0;
        state.BreakCount = 0;
        state.StartRealtime = Time.realtimeSinceStartup;
        state.StartLocal = DateTime.Now;
    }

    public static void RecordWaferSpawned()
    {
        string taskName = GetTaskName();
        TransferState state = GetState(taskName);

        if (!state.Active)
        {
            state.Active = true;
            state.TransferIndex++;
            state.SpawnedAttempts = 0;
            state.BreakCount = 0;
            state.StartRealtime = Time.realtimeSinceStartup;
            state.StartLocal = DateTime.Now;
        }

        state.SpawnedAttempts++;
    }

    public static void RecordWaferBroken()
    {
        string taskName = GetTaskName();
        TransferState state = GetState(taskName);

        if (!state.Active)
        {
            state.Active = true;
            state.TransferIndex++;
            state.SpawnedAttempts = 1;
            state.StartRealtime = Time.realtimeSinceStartup;
            state.StartLocal = DateTime.Now;
        }

        if (state.SpawnedAttempts < 1)
            state.SpawnedAttempts = 1;

        state.BreakCount++;
    }

    public static void RecordWaferAccepted(int transferIndex)
    {
        string taskName = GetTaskName();
        TransferState state = GetState(taskName);

        if (!state.Active)
        {
            state.Active = true;
            state.TransferIndex = transferIndex;
            state.SpawnedAttempts = 1;
            state.StartRealtime = Time.realtimeSinceStartup;
            state.StartLocal = DateTime.Now;
        }

        state.TransferIndex = transferIndex;
        if (state.SpawnedAttempts < 1)
            state.SpawnedAttempts = 1;

        float durationSeconds = Mathf.Max(0f, Time.realtimeSinceStartup - state.StartRealtime);
        WriteTransferRow(taskName, state, durationSeconds);

        state.Active = false;
        state.SpawnedAttempts = 0;
        state.BreakCount = 0;
    }

    public static void Flush()
    {
        _writer?.Flush();
    }

    static TransferState GetState(string taskName)
    {
        if (!StatesByTask.TryGetValue(taskName, out TransferState state))
        {
            state = new TransferState();
            StatesByTask.Add(taskName, state);
        }

        return state;
    }

    static void WriteTransferRow(string taskName, TransferState state, float durationSeconds)
    {
        EnsureWriter();

        var sb = RowBuilder;
        sb.Clear();

        AppendCsvText(sb, GetUserId(), firstColumn: true);
        AppendCsvText(sb, taskName);
        AppendCsvValue(sb, state.TransferIndex);
        AppendCsvValue(sb, state.SpawnedAttempts);
        AppendCsvValue(sb, state.BreakCount);
        AppendCsvValue(sb, durationSeconds);
        AppendCsvText(sb, state.StartLocal.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture));
        AppendCsvText(sb, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture));
        AppendCsvValue(sb, Time.realtimeSinceStartup);
        AppendCsvText(sb, SceneManager.GetActiveScene().path);
        sb.AppendLine();

        _writer.Write(sb.ToString());
        _writer.Flush();
    }

    static void EnsureWriter()
    {
        if (_writer != null)
            return;

        string folderPath = Path.Combine(Application.persistentDataPath, FolderName);
        Directory.CreateDirectory(folderPath);

        _filePath = Path.Combine(folderPath, $"transfer_metrics_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
        _writer = new StreamWriter(_filePath, append: true, new UTF8Encoding(false));

        if (!_headerWritten)
        {
            _writer.WriteLine("user_id,task_name,transfer_index,spawned_attempts,break_count,duration_seconds,start_local,end_local,end_time_seconds,scene_path");
            _headerWritten = true;
        }

        Debug.Log("Logging transfer metrics CSV to: " + _filePath);
    }

    static string GetUserId()
    {
        string userId = PlayerPrefs.GetString(UserIdPlayerPrefsKey, "");
        return string.IsNullOrWhiteSpace(userId) ? "unknown_user" : userId.Trim();
    }

    static string GetTaskName()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        return string.IsNullOrEmpty(sceneName) ? "unknown_task" : sceneName;
    }

    static void AppendCsvValue(StringBuilder sb, int value)
    {
        sb.Append(',');
        sb.Append(value.ToString(CultureInfo.InvariantCulture));
    }

    static void AppendCsvValue(StringBuilder sb, float value)
    {
        sb.Append(',');
        sb.Append(value.ToString("F4", CultureInfo.InvariantCulture));
    }

    static void AppendCsvText(StringBuilder sb, string value, bool firstColumn = false)
    {
        if (!firstColumn)
            sb.Append(',');

        if (string.IsNullOrEmpty(value))
            return;

        bool mustQuote = value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0;
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
