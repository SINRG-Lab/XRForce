using UnityEngine;

public class DustStick : MonoBehaviour
{
    [Header("Stick Settings")]
    public Transform surface;                 // wafer transform
    public float stickForce = 30f;            // how strongly it stays on wafer
    public float breakAwayImpulse = 2.0f;     // if pushed harder than this -> detach
    public float maxStickDistance = 0.01f;    // keep it close to surface

    Rigidbody _rb;
    bool _stuck = true;

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        if (!_stuck || surface == null) return;

        // keep dust near the surface plane (wafer "up" direction)
        Vector3 toSurface = surface.position - _rb.position;

        // Pull towards wafer (simple adhesion)
        _rb.AddForce(toSurface.normalized * stickForce, ForceMode.Acceleration);

        // If it drifts too far (or got blown), stop sticking
        if (toSurface.magnitude > maxStickDistance)
        {
            _stuck = false;
        }
    }

    // Called by blower to break adhesion more deterministically
    public void Detach()
    {
        _stuck = false;
    }

    // Optional: if any strong collision happens, detach
    void OnCollisionEnter(Collision c)
    {
        if (c.impulse.magnitude > breakAwayImpulse) _stuck = false;
    }
}
