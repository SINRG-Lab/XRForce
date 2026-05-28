using UnityEngine;

[DisallowMultipleComponent]
public class TempAirBlower : MonoBehaviour
{
    [Header("Activation")]
    public bool blowContinuously = false;
    public KeyCode holdKey = KeyCode.C;
    public bool editorOnly = true;

    [Header("Air Output")]
    public Transform nozzle;
    public LayerMask affectedLayers = ~0;
    [Min(0f)] public float maxDistance = 2f;
    [Range(1f, 180f)] public float coneAngle = 25f;
    [Min(0f)] public float maxForce = 3f;
    public float lift = 0f;
    [Min(1)] public int maxHits = 128;
    public ForceMode forceMode = ForceMode.Force;

    Collider[] _hits;

    void Awake()
    {
        _hits = new Collider[Mathf.Max(1, maxHits)];

        if (nozzle == null)
            nozzle = transform;
    }

    void Update()
    {
        if (editorOnly && !Application.isEditor)
            return;

        if (!blowContinuously && !Input.GetKey(holdKey))
            return;

        ApplyAir(1f);
    }

    [ContextMenu("Blow Once")]
    public void BlowOnce()
    {
        ApplyAir(1f);
    }

    void ApplyAir(float strength01)
    {
        if (nozzle == null)
            return;

        if (_hits == null || _hits.Length != Mathf.Max(1, maxHits))
            _hits = new Collider[Mathf.Max(1, maxHits)];

        Vector3 origin = nozzle.position;
        Vector3 fwd = nozzle.forward;

        int count = Physics.OverlapSphereNonAlloc(
            origin + fwd * (maxDistance * 0.5f),
            maxDistance,
            _hits,
            affectedLayers,
            QueryTriggerInteraction.Collide);

        float forceBase = maxForce * Mathf.Clamp01(strength01);

        for (int i = 0; i < count; i++)
        {
            Collider col = _hits[i];
            if (col == null)
                continue;

            if (IsInAirCone(col.bounds.center, origin, fwd))
            {
                var dust = col.GetComponentInParent<DustStick>();
                if (dust != null)
                {
                    dust.Clean();
                    continue;
                }

                var dustSpawner = col.GetComponentInParent<DustSpawner>();
                if (dustSpawner != null && dustSpawner.ActiveDustCount > 0)
                {
                    dustSpawner.ClearDust();
                    continue;
                }
            }

            Rigidbody rb = col.attachedRigidbody;
            if (rb == null || rb.isKinematic)
                continue;

            Vector3 to = rb.worldCenterOfMass - origin;
            float forwardDist = Vector3.Dot(to, fwd);

            if (forwardDist <= 0f || forwardDist > maxDistance)
                continue;

            Vector3 lateral = to - fwd * forwardDist;
            float allowedRadius = Mathf.Tan(Mathf.Deg2Rad * coneAngle * 0.5f) * forwardDist;

            if (lateral.magnitude > allowedRadius)
                continue;

            float distanceFade = 1f - (forwardDist / maxDistance);
            Vector3 dir = fwd;

            if (lift != 0f)
                dir = (dir + Vector3.up * lift).normalized;

            rb.AddForce(dir * (forceBase * distanceFade), forceMode);
        }
    }

    bool IsInAirCone(Vector3 point, Vector3 origin, Vector3 forward)
    {
        Vector3 to = point - origin;
        float forwardDist = Vector3.Dot(to, forward);

        if (forwardDist <= 0f || forwardDist > maxDistance)
            return false;

        Vector3 lateral = to - forward * forwardDist;
        float allowedRadius = Mathf.Tan(Mathf.Deg2Rad * coneAngle * 0.5f) * forwardDist;

        return lateral.magnitude <= allowedRadius;
    }
}
