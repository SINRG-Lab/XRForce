using UnityEngine;
using TMPro;

public class ApplyForce : MonoBehaviour
{
    public enum HeatmapSource { HeatmapA, HeatmapB }
    public enum DriveMode { AddForce, KinematicProxy }

    [Header("Physics")]
    [Tooltip("Computed average force in Newtons.")]
    public float forceNewtons_L = 0f;
    public float forceNewtons_R = 0f;

    [Header("Displays")]
    public TextMeshProUGUI force_value_text_left;
    public TextMeshProUGUI force_value_text_right;

    [Header("Pushers")]
    public Rigidbody pusher_Left;
    public Rigidbody pusher_Right;
    [Tooltip("Optional transform that defines the local direction space. If empty, the script uses the pushers' shared parent when possible.")]
    public Transform directionReference;

    [Tooltip("Direction to push. Will be normalized.")]
    public Vector3 direction = Vector3.forward;

    [Tooltip("Interpret 'direction' in this object's local space.")]
    public bool useLocalSpace = true;

    public ForceMode forceMode = ForceMode.Force;

    [Header("When to apply")]
    public bool applyContinuously = true;

    [Header("Input")]
    public ReceiverLatest receiver;   // uses heatmapA & heatmapB

    [Header("Sensor Mapping")]
    public HeatmapSource leftSource = HeatmapSource.HeatmapB;
    public HeatmapSource rightSource = HeatmapSource.HeatmapB;

    [Header("Drive")]
    public DriveMode driveMode = DriveMode.AddForce;
    [Min(0f)] public float proxyDistancePerForce = 0.01f;
    [Min(0f)] public float maxProxyOffset = 0.03f;

    Vector3 _leftStartLocalPosition;
    Vector3 _rightStartLocalPosition;

    void Start()
    {
        if (pusher_Left != null)
            _leftStartLocalPosition = GetReferenceLocalPosition(pusher_Left);

        if (pusher_Right != null)
            _rightStartLocalPosition = GetReferenceLocalPosition(pusher_Right);
    }

    void FixedUpdate()
    {
        if (!applyContinuously) return;
        if (receiver == null) return;

        receiver.FlushLatestPacket();

        forceNewtons_L = ReadAverageForce(leftSource);
        forceNewtons_R = ReadAverageForce(rightSource);

        Vector3 dir = GetDir();

        Vector3 f_l = dir * forceNewtons_L;
        Vector3 f_r = -dir * forceNewtons_R;

        ApplyForces(f_l, f_r);
        UpdateUI();
    }

    void ApplyForces(Vector3 leftForce, Vector3 rightForce)
    {
        if (driveMode == DriveMode.KinematicProxy)
        {
            ApplyProxyDrive();
            return;
        }

        if (pusher_Left != null)
            pusher_Left.AddForce(leftForce, forceMode);

        if (pusher_Right != null)
            pusher_Right.AddForce(rightForce, forceMode);
    }

    void UpdateUI()
    {
        if (force_value_text_left != null)
            force_value_text_left.text = forceNewtons_L.ToString("F2");

        if (force_value_text_right != null)
            force_value_text_right.text = forceNewtons_R.ToString("F2");
    }

    // ---------- Helpers ----------

    float ReadAverageForce(HeatmapSource source)
    {
        float[] values = source == HeatmapSource.HeatmapA ? receiver.heatmapA : receiver.heatmapB;
        if (values == null || values.Length == 0) return 0f;

        float sum = 0f;
        int count = 0;

        for (int i = 0; i < values.Length; i++)
        {
            sum += Mathf.Max(0f, values[i]);
            count++;
        }

        return count > 0 ? sum / count : 0f;
    }

    void ApplyProxyDrive()
    {
        Vector3 dir = GetDir();
        MoveProxy(pusher_Left, _leftStartLocalPosition, dir, forceNewtons_L);
        MoveProxy(pusher_Right, _rightStartLocalPosition, -dir, forceNewtons_R);
    }

    void MoveProxy(Rigidbody body, Vector3 startLocalPosition, Vector3 dir, float forceValue)
    {
        if (body == null)
            return;

        float offset = Mathf.Min(forceValue * proxyDistancePerForce, maxProxyOffset);
        Vector3 targetPosition = GetReferenceWorldPosition(body, startLocalPosition, dir * offset);
        body.MovePosition(targetPosition);
    }

    Vector3 GetReferenceLocalPosition(Rigidbody body)
    {
        Transform parent = body.transform.parent;
        return parent != null ? parent.InverseTransformPoint(body.position) : body.position;
    }

    Vector3 GetReferenceWorldPosition(Rigidbody body, Vector3 localPosition, Vector3 worldOffset)
    {
        Transform parent = body.transform.parent;
        return parent != null ? parent.TransformPoint(localPosition) + worldOffset : localPosition + worldOffset;
    }

    Vector3 GetDir()
    {
        Vector3 d = direction.sqrMagnitude > 0f
            ? direction.normalized
            : Vector3.forward;

        if (!useLocalSpace)
            return d;

        Transform reference = GetDirectionReference();
        return reference != null ? reference.TransformDirection(d) : transform.TransformDirection(d);
    }

    Transform GetDirectionReference()
    {
        if (directionReference != null)
            return directionReference;

        if (pusher_Left != null && pusher_Right != null)
        {
            Transform leftParent = pusher_Left.transform.parent;
            Transform rightParent = pusher_Right.transform.parent;

            if (leftParent != null && leftParent == rightParent)
                return leftParent;
        }

        if (pusher_Left != null && pusher_Left.transform.parent != null)
            return pusher_Left.transform.parent;

        if (pusher_Right != null && pusher_Right.transform.parent != null)
            return pusher_Right.transform.parent;

        return null;
    }
}
