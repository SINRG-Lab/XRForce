using UnityEngine;

public class ApplyForce : MonoBehaviour
{
    [Header("Physics")]
    [Tooltip("Force magnitude in Newtons.")]
    public float forceNewtons = 20f;

    [Tooltip("Direction to push. Will be normalized.")]
    public Vector3 direction = Vector3.forward;

    [Tooltip("Interpret 'direction' in this object's local space.")]
    public bool useLocalSpace = true;

    [Tooltip("Force = continuous N each step; Impulse = N·s applied once; Acceleration ignores mass; VelocityChange ignores mass (impulse).")]
    public ForceMode forceMode = ForceMode.Force;

    [Header("When to apply")]
    public bool applyContinuously = true;     // push every physics step
    public bool applyOnStartOnce = false;     // one-time shove in Start()

    private Rigidbody rb;

    void Awake() => rb = GetComponent<Rigidbody>();

    void Start()
    {
        if (applyOnStartOnce)
            ApplyForceOnce();
    }

    void FixedUpdate()
    {
        if (!applyContinuously) return;

        Vector3 dir = GetDir();
        Vector3 f   = dir * forceNewtons;

        // If you intend an *exact* N only for this step using Impulse, convert N → N·s:
        if (forceMode == ForceMode.Impulse) f *= Time.fixedDeltaTime;

        rb.AddForce(f, forceMode);
    }

    /// Call this from other scripts to give a single shove.
    public void ApplyForceOnce()
    {
        Vector3 dir = GetDir();
        Vector3 f   = dir * forceNewtons;

        if (forceMode == ForceMode.Impulse) f *= Time.fixedDeltaTime;
        rb.AddForce(f, forceMode);
    }

    private Vector3 GetDir()
    {
        Vector3 d = direction.sqrMagnitude > 0f ? direction.normalized : Vector3.forward;
        return useLocalSpace ? transform.TransformDirection(d) : d;
    }
}
