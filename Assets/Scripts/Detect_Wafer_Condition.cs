using UnityEngine;
using System.Collections;
using TMPro;

public class Detect_Wafer_Condition : MonoBehaviour
{
    [Header("Countdown")]
    public float countdownTime = 10f;

    [Header("Trigger Tags")]
    public string requiredTag = "StartObject";     // must be inside trigger with this
    public string forbiddenTag = "BlockObject";    // must NOT be inside trigger with this

    [Header("Cleanliness")]
    public bool requireCleanWafer = false;

    [Header("UI (runtime find)")]
    public string countdownTextTag = "CountdownText"; // Tag your TMP text object with this

    TMP_Text countdownText;
    DustSpawner dustSpawner;

    int requiredCount = 0;
    int forbiddenCount = 0;

    Coroutine countdownRoutine;

    void Awake()
    {
        dustSpawner = GetComponent<DustSpawner>();
        var textObj = GameObject.FindGameObjectWithTag(countdownTextTag);
        if (textObj != null) countdownText = textObj.GetComponent<TMP_Text>();
        UpdateText(0f);
    }

    void Update()
    {
        if (requiredCount <= 0 && forbiddenCount <= 0)
            return;

        if (ConditionValid())
            TryStartCountdown();
        else
            StopCountdownIfRunning();
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(requiredTag)) requiredCount++;
        if (other.CompareTag(forbiddenTag)) forbiddenCount++;

        TryStartCountdown();
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(requiredTag)) requiredCount = Mathf.Max(0, requiredCount - 1);
        if (other.CompareTag(forbiddenTag)) forbiddenCount = Mathf.Max(0, forbiddenCount - 1);

        StopCountdownIfRunning(); // reset when condition breaks
    }

    bool ConditionValid()
    {
        if (requiredCount <= 0 || forbiddenCount > 0)
            return false;

        if (!requireCleanWafer)
            return true;

        if (dustSpawner == null)
            dustSpawner = GetComponent<DustSpawner>();

        return dustSpawner == null || dustSpawner.IsClean;
    }

    void TryStartCountdown()
    {
        if (ConditionValid() && countdownRoutine == null)
            countdownRoutine = StartCoroutine(Countdown());
    }

    void StopCountdownIfRunning()
    {
        if (!ConditionValid() && countdownRoutine != null)
        {
            StopCoroutine(countdownRoutine);
            countdownRoutine = null;
            UpdateText(0f);
            Debug.Log("Countdown reset");
        }
    }

    IEnumerator Countdown()
    {
        float t = countdownTime;

        while (t > 0f)
        {
            if (!ConditionValid())
            {
                UpdateText(0f);
                countdownRoutine = null;
                yield break;
            }

            UpdateText(t);
            t -= Time.deltaTime;
            yield return null;
        }

        UpdateText(0f);
        countdownRoutine = null;
        Debug.Log("✅ Countdown completed!");

        // TODO: trigger next action here
    }

    void UpdateText(float time)
    {
        if (countdownText == null) return;
        countdownText.text = time > 0f ? $"{time:F1}s" : "";
    }
}
