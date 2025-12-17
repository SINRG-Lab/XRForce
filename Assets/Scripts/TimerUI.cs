using UnityEngine;
using TMPro;

public class TimerUI : MonoBehaviour
{
    [Header ("UI Elements")]
    [SerializeField] private TMP_Text timerText;

    [Header ("Settings")]
    [SerializeField] private bool showMilliseconds = true;

    private float elapsed;
    private bool running;

    private void Start()
    {
        elapsed = 0f;
        running = false;
        UpdateTimerText(0f);
    }

    private void Update()
    {
        if (!running) return;

        elapsed += Time.deltaTime;
        UpdateTimerText(elapsed);
    }

    public void StartTimer()
    {
        running = true;
    }

    public void StopTimer()
    {
        running = false;
    }

    public void ResetTimer()
    {
        elapsed = 0f;
        UpdateTimerText(elapsed);
    }

    private void UpdateTimerText(float t)
    {
        int minutes = Mathf.FloorToInt(t / 60f);
        int seconds = Mathf.FloorToInt(t % 60f);

        if (showMilliseconds)
        {
            int ms = Mathf.FloorToInt((t * 100f) % 100f);
            timerText.text = $"{minutes:00}:{seconds:00}:{ms:00}";
        }
        else
        {
            timerText.text = $"{minutes:00}:{seconds:00}";
        }
    }
}
