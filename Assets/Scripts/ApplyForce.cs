using UnityEngine;

public class ApplyForce : MonoBehaviour
{
    [Header("Physics")]
    [Tooltip("Force magnitude in Newtons.")]
    public float forceNewtons_L = 0f;
    public float forceNewtons_R = 0f;

    [Header("Pushers")]
    public Rigidbody pusher_Left;
    public Rigidbody pusher_Right;

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

    public Receiver receiver; 

    void Awake() => rb = GetComponent<Rigidbody>();

    void Start()
    {
        // if (applyOnStartOnce)
        //     ApplyForceOnce();
    }

    void FixedUpdate()
    {
        if (!applyContinuously) return;

        forceNewtons_L = receiver.force_L_N;
        forceNewtons_R = receiver.force_R_N;

        Vector3 dir = GetDir();
        Vector3 f_l   = dir * forceNewtons_L;
        Vector3 f_r   = -dir * forceNewtons_R;

        // If you intend an *exact* N only for this step using Impulse, convert N → N·s:
        // if (forceMode == ForceMode.Impulse) f *= Time.fixedDeltaTime;

        pusher_Left.AddForce(f_l, forceMode);
        pusher_Right.AddForce(f_r, forceMode);
    }

    /// Call this from other scripts to give a single shove.
    // public void ApplyForceOnce()
    // {
    //     Vector3 dir = GetDir();
    //     Vector3 f   = dir * forceNewtons;

    //     if (forceMode == ForceMode.Impulse) f *= Time.fixedDeltaTime;
    //     rb.AddForce(f, forceMode);
    // }

    private Vector3 GetDir()
    {
        Vector3 d = direction.sqrMagnitude > 0f ? direction.normalized : Vector3.forward;
        return useLocalSpace ? transform.TransformDirection(d) : d;
    }
}
