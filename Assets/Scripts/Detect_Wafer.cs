using UnityEngine;
using System.Collections;
using UnityEngine.Events;

public class Detect_Wafer : MonoBehaviour
{
    public int targetCount = 3;
    public float delaySeconds = 1.5f;
    public WaferSpawner waferSpawner;

    public UnityEvent onTargetReached;
    public int Count => _count;

    int _count = 0;
    bool _isProcessing = false; // prevents multiple triggers

    void OnTriggerEnter(Collider other)
    {
        if (_isProcessing) return;

        if (!other.CompareTag("Wafer")) return;

        _isProcessing = true;
        StartCoroutine(HandleWafer(other.gameObject));
        
    }

    IEnumerator HandleWafer(GameObject wafer)
    {
        _count++;
        Debug.Log($"Wafer count: {_count}/{targetCount}");

        var rb = wafer.GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;

        wafer.GetComponent<Renderer>().material.color = Color.red;
        yield return new WaitForSeconds(delaySeconds);

        Destroy(wafer);

        if (_count < targetCount && waferSpawner != null)
        {
            waferSpawner.SpawnWithDelay();
            _isProcessing = false;
            yield break;
        }

        onTargetReached?.Invoke();
    }

    public void ResetCount()
    {
        _count = 0;
        _isProcessing = false;
    }

}
