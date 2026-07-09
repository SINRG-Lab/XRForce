using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

public class Detect_Wafer : MonoBehaviour
{
    public int targetCount = 3;
    public float delaySeconds = 1.5f;
    public WaferSpawner waferSpawner;

    public UnityEvent onTargetReached;
    public int Count => GetSceneCount();

    static readonly Dictionary<string, int> SceneCounts = new();
    static readonly HashSet<int> AcceptedWafers = new();

    bool _isProcessing = false; // prevents multiple triggers

    void Awake()
    {
        if (waferSpawner == null)
            waferSpawner = FindFirstObjectByType<WaferSpawner>();
    }

    void OnTriggerEnter(Collider other)
    {
        if (_isProcessing) return;

        if (!other.CompareTag("Wafer")) return;

        int waferId = other.gameObject.GetInstanceID();
        if (AcceptedWafers.Contains(waferId)) return;

        AcceptedWafers.Add(waferId);
        _isProcessing = true;
        StartCoroutine(HandleWafer(other.gameObject));
        
    }

    IEnumerator HandleWafer(GameObject wafer)
    {
        int count = GetSceneCount() + 1;
        SetSceneCount(count);

        var tweezerDetection = wafer.GetComponent<Detect_Tweezer>();
        if (tweezerDetection != null)
            tweezerDetection.MarkAccepted();

        TransferMetricsLogger.RecordWaferAccepted(count);
        Debug.Log($"Wafer count: {count}/{targetCount}");

        var rb = wafer.GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;

        wafer.GetComponent<Renderer>().material.color = Color.red;
        yield return new WaitForSeconds(delaySeconds);

        Destroy(wafer);

        if (count < targetCount)
        {
            if (waferSpawner == null)
                waferSpawner = FindFirstObjectByType<WaferSpawner>();

            if (waferSpawner != null)
            {
                waferSpawner.SpawnWithDelay();
            }
            else
            {
                Debug.LogWarning(
                    $"Wafer count is {count}/{targetCount}, but no WaferSpawner is assigned or found. Task will not complete yet.",
                    this);
            }

            _isProcessing = false;
            yield break;
        }

        TransferMetricsLogger.RecordTaskCompleted(count);
        onTargetReached?.Invoke();
    }

    public void ResetCount()
    {
        SetSceneCount(0);
        AcceptedWafers.Clear();
        _isProcessing = false;
        TransferMetricsLogger.ResetTaskProgress();
    }

    int GetSceneCount()
    {
        SceneCounts.TryGetValue(SceneKey(), out int count);
        return count;
    }

    void SetSceneCount(int count)
    {
        SceneCounts[SceneKey()] = count;
    }

    string SceneKey()
    {
        return gameObject.scene.IsValid()
            ? gameObject.scene.path
            : SceneManager.GetActiveScene().path;
    }
}
