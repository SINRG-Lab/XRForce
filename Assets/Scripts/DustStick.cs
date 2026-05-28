using UnityEngine;

public class DustStick : MonoBehaviour
{
    [Header("Stick Settings")]
    public Transform surface;                 // wafer transform
    public float stickForce = 30f;            // how strongly it stays on wafer
    public float breakAwayImpulse = 2.0f;     // if pushed harder than this -> detach
    public float maxStickDistance = 0.01f;    // keep it close to surface
    public bool destroyWhenRemoved = true;
    [Min(0f)] public float destroyDelay = 0f;

    Rigidbody _rb;
    bool _stuck = true;
    bool _removedFromSurface;
    DustSpawner _owner;

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
    }

    public void Initialize(Transform surfaceTransform, DustSpawner owner)
    {
        surface = surfaceTransform;
        _owner = owner;
        _stuck = true;
        _removedFromSurface = false;
    }

    void FixedUpdate()
    {
        if (!_stuck || surface == null || _rb == null || _rb.isKinematic) return;

        // keep dust near the surface plane (wafer "up" direction)
        Vector3 toSurface = surface.position - _rb.position;

        // Pull towards wafer (simple adhesion)
        _rb.AddForce(toSurface.normalized * stickForce, ForceMode.Acceleration);

        // If it drifts too far (or got blown), stop sticking
        if (toSurface.magnitude > maxStickDistance)
        {
            Detach();
        }
    }

    // Called by blower to break adhesion more deterministically
    public void Detach()
    {
        if (_removedFromSurface)
            return;

        _stuck = false;
        _removedFromSurface = true;

        if (_rb != null)
            _rb.isKinematic = false;

        transform.SetParent(null, true);
        _owner?.NotifyDustRemoved(this);

        if (destroyWhenRemoved)
            Destroy(gameObject, destroyDelay);
    }

    public void Clean()
    {
        Detach();
    }

    // Optional: if any strong collision happens, detach
    void OnCollisionEnter(Collision c)
    {
        if (c.impulse.magnitude > breakAwayImpulse) Detach();
    }
}
