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

    [Header("Dirt")]
    public bool spawnDirtOnWafer = false;
    public GameObject dustPrefab;
    [Min(1)] public int dustCount = 80;
    public Vector2 dustScaleRange = new Vector2(0.6f, 1.4f);
    public float dustSurfaceOffset = 0.0005f;
    public int cleanThreshold = 0;
    public bool requireCleanBeforeAccept = false;

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

        ConfigureDirt(wafer);
        ConfigureAcceptance(wafer);

        var detect = wafer.GetComponent<Detect_Tweezer>();
        if (detect == null) return;

        detect.ConfigurePinchSources(leftTip, rightTip, targetParent);

        if (autoResolveTweezers)
            detect.TryResolveReferences();
    }

    void ConfigureDirt(GameObject wafer)
    {
        if (!spawnDirtOnWafer || dustPrefab == null)
            return;

        var dustSpawner = wafer.GetComponent<DustSpawner>();
        if (dustSpawner == null)
            dustSpawner = wafer.AddComponent<DustSpawner>();

        dustSpawner.alignToNormal = true;
        dustSpawner.randomYaw = true;
        dustSpawner.clearExistingOnSpawn = true;
        dustSpawner.ConfigureAndSpawn(
            dustPrefab,
            dustCount,
            dustScaleRange,
            dustSurfaceOffset,
            cleanThreshold,
            wafer.transform);
    }

    void ConfigureAcceptance(GameObject wafer)
    {
        var condition = wafer.GetComponent<Detect_Wafer_Condition>();
        if (condition != null)
            condition.requireCleanWafer = requireCleanBeforeAccept;
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
