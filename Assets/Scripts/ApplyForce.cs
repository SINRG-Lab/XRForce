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
    [Min(1f)] public float weightedForcePower = 2.0f;

    [Header("Calibration")]
    public bool calibrateOnStart = true;
    [Min(0.05f)] public float calibrationDuration = 0.5f;
    public bool subtractBaseline = true;
    public float baselineLeft = 0f;
    public float baselineRight = 0f;

    [Header("Filtering")]
    [Min(0f)] public float startThreshold = 0.15f;
    [Min(0f)] public float stopThreshold = 0.08f;
    [Min(0f)] public float smoothingTime = 0.08f;
    [Min(0f)] public float maxAppliedForce = 2.5f;
    [Min(0f)] public float maxRiseRatePerSecond = 8f;

    [Header("Drive")]
    public DriveMode driveMode = DriveMode.AddForce;
    [Min(0f)] public float proxyDistancePerForce = 0.01f;
    [Min(0f)] public float maxProxyOffset = 0.03f;

    float _targetForceLeft;
    float _targetForceRight;
    bool _leftActive;
    bool _rightActive;

    bool _isCalibrating;
    float _calibrationTimer;
    float _baselineSumLeft;
    float _baselineSumRight;
    int _baselineSamples;

    Vector3 _leftStartLocalPosition;
    Vector3 _rightStartLocalPosition;

    void Start()
    {
        if (pusher_Left != null)
            _leftStartLocalPosition = GetReferenceLocalPosition(pusher_Left);

        if (pusher_Right != null)
            _rightStartLocalPosition = GetReferenceLocalPosition(pusher_Right);

        if (calibrateOnStart)
            BeginCalibration();
    }

    void FixedUpdate()
    {
        if (!applyContinuously) return;
        if (receiver == null) return;

        float dt = Time.fixedDeltaTime;

        float rawLeft = ReadWeightedForce(leftSource);
        float rawRight = ReadWeightedForce(rightSource);

        if (_isCalibrating)
        {
            _calibrationTimer += dt;
            _baselineSumLeft += rawLeft;
            _baselineSumRight += rawRight;
            _baselineSamples++;

            if (_calibrationTimer >= calibrationDuration)
            {
                if (_baselineSamples > 0)
                {
                    baselineLeft = _baselineSumLeft / _baselineSamples;
                    baselineRight = _baselineSumRight / _baselineSamples;
                }
                _isCalibrating = false;
            }

            forceNewtons_L = 0f;
            forceNewtons_R = 0f;
            _targetForceLeft = 0f;
            _targetForceRight = 0f;
            _leftActive = false;
            _rightActive = false;
            ApplyForces(Vector3.zero, Vector3.zero);
            UpdateUI();
            return;
        }

        float filteredLeft = subtractBaseline ? Mathf.Max(0f, rawLeft - baselineLeft) : rawLeft;
        float filteredRight = subtractBaseline ? Mathf.Max(0f, rawRight - baselineRight) : rawRight;

        _leftActive = UpdateActiveState(_leftActive, filteredLeft);
        _rightActive = UpdateActiveState(_rightActive, filteredRight);

        _targetForceLeft = _leftActive ? Mathf.Min(filteredLeft, maxAppliedForce) : 0f;
        _targetForceRight = _rightActive ? Mathf.Min(filteredRight, maxAppliedForce) : 0f;
        _targetForceLeft = LimitRiseRate(forceNewtons_L, _targetForceLeft, dt);
        _targetForceRight = LimitRiseRate(forceNewtons_R, _targetForceRight, dt);

        float smoothingFactor = GetSmoothingFactor(dt);
        forceNewtons_L = Mathf.Lerp(forceNewtons_L, _targetForceLeft, smoothingFactor);
        forceNewtons_R = Mathf.Lerp(forceNewtons_R, _targetForceRight, smoothingFactor);

        Vector3 dir = GetDir();

        Vector3 f_l = dir * forceNewtons_L;
        Vector3 f_r = -dir * forceNewtons_R;

        ApplyForces(f_l, f_r);
        UpdateUI();
    }

    public void BeginCalibration()
    {
        _isCalibrating = true;
        _calibrationTimer = 0f;
        _baselineSumLeft = 0f;
        _baselineSumRight = 0f;
        _baselineSamples = 0;
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

    float ReadWeightedForce(HeatmapSource source)
    {
        float[] values = source == HeatmapSource.HeatmapA ? receiver.heatmapA : receiver.heatmapB;
        return WeightedForce(values, weightedForcePower);
    }

    float WeightedForce(float[] values, float power = 2.0f)
    {
        if (values == null || values.Length == 0) return 0f;

        float num = 0f;
        float den = 0f;

        for (int i = 0; i < values.Length; i++)
        {
            float v = Mathf.Max(0f, values[i]); 
            float vp = Mathf.Pow(v, power);

            num += vp;
            den += Mathf.Pow(v, power - 1f);
        }

        return den > 0f ? num / den : 0f;
    }

    bool UpdateActiveState(bool isActive, float value)
    {
        if (isActive)
            return value > stopThreshold;

        return value >= startThreshold;
    }

    float GetSmoothingFactor(float dt)
    {
        if (smoothingTime <= 0f)
            return 1f;

        return 1f - Mathf.Exp(-dt / smoothingTime);
    }

    float LimitRiseRate(float current, float target, float dt)
    {
        if (maxRiseRatePerSecond <= 0f || target <= current)
            return target;

        return Mathf.Min(target, current + maxRiseRatePerSecond * dt);
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

        return useLocalSpace ? transform.TransformDirection(d) : d;
    }
}
