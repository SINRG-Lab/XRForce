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

    class TaskState
    {
        public bool TaskActive;
        public float TaskStartRealtime;
        public DateTime TaskStartLocal;
        public int CompletedTransfers;
        public int TotalBreakCount;
        public int TotalSlipCount;

        public bool TransferActive;
        public int TransferIndex;
        public int SpawnedAttempts;
        public int BreakCount;
        public int SlipCount;
        public int GrabCount;
        public float TransferStartRealtime;
        public DateTime TransferStartLocal;

        public int PressureSampleCount;
        public float SumAbsErrorA;
        public float SumAbsErrorB;
        public float FinalAbsErrorA;
        public float FinalAbsErrorB;
        public int WithinToleranceCountA;
        public int WithinToleranceCountB;
        public bool HasPreviousPressureResult;
        public float PreviousMeanAbsErrorA;
        public float PreviousMeanAbsErrorB;
    }

    static readonly Dictionary<string, TaskState> StatesByTask = new Dictionary<string, TaskState>();
    static readonly StringBuilder RowBuilder = new StringBuilder(512);
    static readonly char[] CsvQuoteChars = { ',', '"', '\n', '\r' };
    static StreamWriter _transferWriter;
    static StreamWriter _taskWriter;
    static StreamWriter _gripWriter;
    static string _sessionStamp;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        DisposeWriters();
        StatesByTask.Clear();
        _sessionStamp = null;
    }

    public static void SetUserId(string userId)
    {
        PlayerPrefs.SetString(UserIdPlayerPrefsKey, string.IsNullOrWhiteSpace(userId) ? "unknown_user" : userId.Trim());
        PlayerPrefs.Save();
    }

    public static void GetCurrentContext(out string userId, out string taskName, out int transferIndex)
    {
        userId = GetUserId();
        taskName = GetTaskName();
        TaskState state = GetState(taskName);
        transferIndex = state.TransferActive ? state.TransferIndex : state.CompletedTransfers + 1;
    }

    public static void ResetTaskProgress()
    {
        string taskName = GetTaskName();
        TaskState state = GetState(taskName);

        state.TaskActive = true;
        state.TaskStartRealtime = Time.realtimeSinceStartup;
        state.TaskStartLocal = DateTime.Now;
        state.CompletedTransfers = 0;
        state.TotalBreakCount = 0;
        state.TotalSlipCount = 0;
        state.HasPreviousPressureResult = false;
        BeginTransfer(state, 1);
    }

    public static void RecordWaferSpawned()
    {
        TaskState state = GetActiveState();
        EnsureTaskStarted(state);

        if (!state.TransferActive)
            BeginTransfer(state, state.CompletedTransfers + 1);

        state.SpawnedAttempts++;
    }

    public static void RecordWaferBroken()
    {
        TaskState state = GetActiveState();
        EnsureTaskAndTransferStarted(state);

        if (state.SpawnedAttempts < 1)
            state.SpawnedAttempts = 1;

        state.BreakCount++;
        state.TotalBreakCount++;
    }

    public static void RecordGripStarted(
        Vector3 waferWorldPosition,
        Vector3 leftContactLocal,
        Vector3 rightContactLocal,
        Vector3 leftTipWorld,
        Vector3 rightTipWorld)
    {
        TaskState state = GetActiveState();
        EnsureTaskAndTransferStarted(state);
        state.GrabCount++;

        WriteGripEvent(
            "grab",
            state,
            waferWorldPosition,
            leftContactLocal,
            rightContactLocal,
            leftTipWorld,
            rightTipWorld);
    }

    public static void RecordGripReleased(
        Vector3 waferWorldPosition,
        Vector3 leftContactLocal,
        Vector3 rightContactLocal,
        Vector3 leftTipWorld,
        Vector3 rightTipWorld,
        bool countAsSlip)
    {
        TaskState state = GetActiveState();
        EnsureTaskAndTransferStarted(state);

        if (countAsSlip)
        {
            state.SlipCount++;
            state.TotalSlipCount++;
        }

        WriteGripEvent(
            countAsSlip ? "slip" : "release",
            state,
            waferWorldPosition,
            leftContactLocal,
            rightContactLocal,
            leftTipWorld,
            rightTipWorld);
    }

    public static void RecordPressureSample(
        float appliedPressureA,
        float appliedPressureB,
        float idealPressureA,
        float idealPressureB,
        float tolerance)
    {
        TaskState state = GetActiveState();
        if (!state.TransferActive)
            return;

        float absErrorA = Mathf.Abs(appliedPressureA - idealPressureA);
        float absErrorB = Mathf.Abs(appliedPressureB - idealPressureB);

        state.PressureSampleCount++;
        state.SumAbsErrorA += absErrorA;
        state.SumAbsErrorB += absErrorB;
        state.FinalAbsErrorA = absErrorA;
        state.FinalAbsErrorB = absErrorB;

        if (absErrorA <= tolerance)
            state.WithinToleranceCountA++;
        if (absErrorB <= tolerance)
            state.WithinToleranceCountB++;
    }

    public static void RecordWaferAccepted(int transferIndex)
    {
        string taskName = GetTaskName();
        TaskState state = GetState(taskName);
        EnsureTaskAndTransferStarted(state);

        state.TransferIndex = transferIndex;
        if (state.SpawnedAttempts < 1)
            state.SpawnedAttempts = 1;

        float durationSeconds = Mathf.Max(0f, Time.realtimeSinceStartup - state.TransferStartRealtime);
        WriteTransferRow(taskName, state, durationSeconds);
        state.CompletedTransfers = Mathf.Max(state.CompletedTransfers, transferIndex);
        state.TransferActive = false;
    }

    public static void RecordTaskCompleted(int completedTransfers)
    {
        string taskName = GetTaskName();
        TaskState state = GetState(taskName);
        EnsureTaskStarted(state);
        state.CompletedTransfers = Mathf.Max(state.CompletedTransfers, completedTransfers);

        float taskDurationSeconds = Mathf.Max(0f, Time.realtimeSinceStartup - state.TaskStartRealtime);
        WriteTaskRow(taskName, state, taskDurationSeconds);
        state.TaskActive = false;
    }

    public static void Flush()
    {
        _transferWriter?.Flush();
        _taskWriter?.Flush();
        _gripWriter?.Flush();
    }

    static TaskState GetActiveState()
    {
        return GetState(GetTaskName());
    }

    static TaskState GetState(string taskName)
    {
        if (!StatesByTask.TryGetValue(taskName, out TaskState state))
        {
            state = new TaskState();
            StatesByTask.Add(taskName, state);
        }

        return state;
    }

    static void EnsureTaskStarted(TaskState state)
    {
        if (state.TaskActive)
            return;

        state.TaskActive = true;
        state.TaskStartRealtime = Time.realtimeSinceStartup;
        state.TaskStartLocal = DateTime.Now;
    }

    static void EnsureTaskAndTransferStarted(TaskState state)
    {
        EnsureTaskStarted(state);
        if (!state.TransferActive)
            BeginTransfer(state, state.CompletedTransfers + 1);
    }

    static void BeginTransfer(TaskState state, int transferIndex)
    {
        state.TransferActive = true;
        state.TransferIndex = transferIndex;
        state.SpawnedAttempts = 0;
        state.BreakCount = 0;
        state.SlipCount = 0;
        state.GrabCount = 0;
        state.TransferStartRealtime = Time.realtimeSinceStartup;
        state.TransferStartLocal = DateTime.Now;
        state.PressureSampleCount = 0;
        state.SumAbsErrorA = 0f;
        state.SumAbsErrorB = 0f;
        state.FinalAbsErrorA = 0f;
        state.FinalAbsErrorB = 0f;
        state.WithinToleranceCountA = 0;
        state.WithinToleranceCountB = 0;
    }

    static void WriteTransferRow(string taskName, TaskState state, float durationSeconds)
    {
        EnsureWriters();

        float meanAbsErrorA = Divide(state.SumAbsErrorA, state.PressureSampleCount);
        float meanAbsErrorB = Divide(state.SumAbsErrorB, state.PressureSampleCount);
        float withinTolerancePercentA = Percent(state.WithinToleranceCountA, state.PressureSampleCount);
        float withinTolerancePercentB = Percent(state.WithinToleranceCountB, state.PressureSampleCount);
        float improvementA = state.HasPreviousPressureResult ? state.PreviousMeanAbsErrorA - meanAbsErrorA : 0f;
        float improvementB = state.HasPreviousPressureResult ? state.PreviousMeanAbsErrorB - meanAbsErrorB : 0f;

        var sb = RowBuilder;
        sb.Clear();
        AppendCsvText(sb, GetUserId(), true);
        AppendCsvText(sb, taskName);
        AppendCsvValue(sb, state.TransferIndex);
        AppendCsvValue(sb, state.SpawnedAttempts);
        AppendCsvValue(sb, state.BreakCount);
        AppendCsvValue(sb, state.SlipCount);
        AppendCsvValue(sb, state.GrabCount);
        AppendCsvValue(sb, durationSeconds);
        AppendCsvValue(sb, state.PressureSampleCount);
        AppendCsvValue(sb, meanAbsErrorA);
        AppendCsvValue(sb, meanAbsErrorB);
        AppendCsvValue(sb, state.FinalAbsErrorA);
        AppendCsvValue(sb, state.FinalAbsErrorB);
        AppendCsvValue(sb, withinTolerancePercentA);
        AppendCsvValue(sb, withinTolerancePercentB);
        AppendCsvValue(sb, improvementA);
        AppendCsvValue(sb, improvementB);
        AppendCsvText(sb, FormatDate(state.TransferStartLocal));
        AppendCsvText(sb, FormatDate(DateTime.Now));
        AppendCsvText(sb, SceneManager.GetActiveScene().path);
        sb.AppendLine();

        _transferWriter.Write(sb.ToString());
        _transferWriter.Flush();

        if (state.PressureSampleCount > 0)
        {
            state.HasPreviousPressureResult = true;
            state.PreviousMeanAbsErrorA = meanAbsErrorA;
            state.PreviousMeanAbsErrorB = meanAbsErrorB;
        }
    }

    static void WriteTaskRow(string taskName, TaskState state, float taskDurationSeconds)
    {
        EnsureWriters();

        var sb = RowBuilder;
        sb.Clear();
        AppendCsvText(sb, GetUserId(), true);
        AppendCsvText(sb, taskName);
        AppendCsvValue(sb, state.CompletedTransfers);
        AppendCsvValue(sb, state.TotalBreakCount);
        AppendCsvValue(sb, state.TotalSlipCount);
        AppendCsvValue(sb, taskDurationSeconds);
        AppendCsvText(sb, FormatDate(state.TaskStartLocal));
        AppendCsvText(sb, FormatDate(DateTime.Now));
        AppendCsvText(sb, SceneManager.GetActiveScene().path);
        sb.AppendLine();

        _taskWriter.Write(sb.ToString());
        _taskWriter.Flush();
    }

    static void WriteGripEvent(
        string eventType,
        TaskState state,
        Vector3 waferWorldPosition,
        Vector3 leftContactLocal,
        Vector3 rightContactLocal,
        Vector3 leftTipWorld,
        Vector3 rightTipWorld)
    {
        EnsureWriters();

        var sb = RowBuilder;
        sb.Clear();
        AppendCsvText(sb, GetUserId(), true);
        AppendCsvText(sb, GetTaskName());
        AppendCsvValue(sb, state.TransferIndex);
        AppendCsvValue(sb, state.GrabCount);
        AppendCsvText(sb, eventType);
        AppendCsvText(sb, FormatDate(DateTime.Now));
        AppendCsvValue(sb, Time.realtimeSinceStartup);
        AppendVector(sb, waferWorldPosition);
        AppendVector(sb, leftContactLocal);
        AppendVector(sb, rightContactLocal);
        AppendVector(sb, leftTipWorld);
        AppendVector(sb, rightTipWorld);
        AppendCsvValue(sb, Vector3.Distance(leftTipWorld, rightTipWorld));
        sb.AppendLine();

        _gripWriter.Write(sb.ToString());
        _gripWriter.Flush();
    }

    static void EnsureWriters()
    {
        if (_transferWriter != null)
            return;

        string folderPath = Path.Combine(Application.persistentDataPath, FolderName);
        Directory.CreateDirectory(folderPath);
        _sessionStamp = string.IsNullOrEmpty(_sessionStamp) ? DateTime.Now.ToString("yyyyMMdd_HHmmss") : _sessionStamp;

        string transferPath = Path.Combine(folderPath, $"transfer_metrics_{_sessionStamp}.csv");
        string taskPath = Path.Combine(folderPath, $"task_metrics_{_sessionStamp}.csv");
        string gripPath = Path.Combine(folderPath, $"grip_events_{_sessionStamp}.csv");

        _transferWriter = CreateWriter(
            transferPath,
            "user_id,task_name,transfer_index,spawned_attempts,break_count,slip_count,grab_count,duration_seconds,pressure_sample_count,mean_abs_error_a,mean_abs_error_b,final_abs_error_a,final_abs_error_b,within_tolerance_percent_a,within_tolerance_percent_b,error_improvement_a,error_improvement_b,start_local,end_local,scene_path");

        _taskWriter = CreateWriter(
            taskPath,
            "user_id,task_name,completed_transfers,total_break_count,total_slip_count,task_duration_seconds,start_local,end_local,scene_path");

        _gripWriter = CreateWriter(
            gripPath,
            "user_id,task_name,transfer_index,grip_index,event_type,timestamp_local,time_seconds,wafer_world_x,wafer_world_y,wafer_world_z,left_contact_local_x,left_contact_local_y,left_contact_local_z,right_contact_local_x,right_contact_local_y,right_contact_local_z,left_tip_world_x,left_tip_world_y,left_tip_world_z,right_tip_world_x,right_tip_world_y,right_tip_world_z,tip_gap");

        Debug.Log("Transfer metrics CSV: " + transferPath);
        Debug.Log("Task metrics CSV: " + taskPath);
        Debug.Log("Grip events CSV: " + gripPath);
    }

    static StreamWriter CreateWriter(string path, string header)
    {
        var writer = new StreamWriter(path, true, new UTF8Encoding(false));
        if (new FileInfo(path).Length == 0)
            writer.WriteLine(header);
        return writer;
    }

    static void DisposeWriters()
    {
        _transferWriter?.Dispose();
        _taskWriter?.Dispose();
        _gripWriter?.Dispose();
        _transferWriter = null;
        _taskWriter = null;
        _gripWriter = null;
    }

    static float Divide(float value, int count)
    {
        return count > 0 ? value / count : 0f;
    }

    static float Percent(int value, int count)
    {
        return count > 0 ? value * 100f / count : 0f;
    }

    static string FormatDate(DateTime value)
    {
        return value.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
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

    static void AppendVector(StringBuilder sb, Vector3 value)
    {
        AppendCsvValue(sb, value.x);
        AppendCsvValue(sb, value.y);
        AppendCsvValue(sb, value.z);
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
