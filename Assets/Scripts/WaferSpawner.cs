using UnityEngine;
using System.Collections;

public class WaferSpawner : MonoBehaviour
{
    public GameObject waferPrefab;
    public Transform spawnPoint;
    public Collider leftTip;
    public Collider rightTip;
    public Transform targetParent;
    public bool autoResolveTweezers = true;
    public float spawnDelay = 2f;
    public float brokenCleanupDelay = 2f;
    int spawnCount = 0;
    bool isBusy = false;

    void Start()
    {
        SpawnWithDelay();
    }
    
    public void SpawnWithDelay(float delay = 0f)
    {
        if (!isBusy)
            StartCoroutine(SpawnRoutine(delay));
    }

    IEnumerator SpawnRoutine(float delay)
    {
        isBusy = true;

        yield return new WaitForSeconds(delay);

        Debug.Log("Spawning: " + waferPrefab.name, waferPrefab);

        GameObject wafer = Instantiate(waferPrefab, spawnPoint.position, spawnPoint.rotation);
        ConfigureSpawnedWafer(wafer);
        spawnCount++;

        // var morph = wafer.GetComponent<WaferMaterialMorph>();
        // if (morph != null) morph.enabled = true;

        var calc = wafer.GetComponent<CollisionForceCalculator>();
        if (calc != null)
            calc.OnBroken += HandleWaferBroken;

        isBusy = false;
    }

    void ConfigureSpawnedWafer(GameObject wafer)
    {
        if (!wafer) return;

        var detect = wafer.GetComponent<Detect_Tweezer>();
        if (detect == null) return;

        detect.ConfigurePinchSources(leftTip, rightTip, targetParent);

        if (autoResolveTweezers)
            detect.TryResolveReferences();
    }

    void HandleWaferBroken(GameObject brokenInstance)
    {
        if (brokenInstance != null)
        {
            Destroy(brokenInstance, brokenCleanupDelay);
        }

        SpawnWithDelay(spawnDelay);
    }
}
