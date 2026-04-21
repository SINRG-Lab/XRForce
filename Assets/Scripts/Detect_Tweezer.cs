using UnityEngine;
using System.Collections.Generic;

public class Detect_Tweezer : MonoBehaviour
{
    [Header("Pinch sources (your tweezer tips)")]
    public Collider rightTip;
    public Collider leftTip;
    public bool tipsAreTriggers = false;

    [Header("Auto Resolve")]
    public bool autoResolveReferences = true;
    public string targetParentName = "Tweezers";
    public string leftTipName = "Stopper_Left";
    public string rightTipName = "Stopper_Right";
    public bool logAutoResolveFailures = false;

    [Header("Pinch Validation")]
    public bool usePinchValidation = true;
    [Tooltip("Require the tweezer contacts to land on opposite sides of the object. More negative is stricter.")]
    [Range(-1f, 1f)] public float oppositeSideDotThreshold = 0.35f;
    [Tooltip("Allow the object's center to sit this many object radii away from the contact line.")]
    [Min(0f)] public float centerlineToleranceMultiplier = 1.5f;
    [Tooltip("Allow the object's center to sit slightly beyond the tip segment while still counting as pinched.")]
    [Min(0f)] public float centerBetweenTipsAllowanceMultiplier = 0.5f;
    [Tooltip("Allow the tip gap to be at most this multiple of the object's projected diameter.")]
    [Min(0.1f)] public float maxTipGapMultiplier = 2.2f;

    [Header("Parenting")]
    public Transform targetParent;
    public bool keepWorldPoseOnParent = true;
    public bool keepWorldPoseOnUnparent = true;
    public bool makeKinematicWhileParented = true;
    public bool zeroLocalPoseAfterParent = false;
    public bool requireTipOverlapToHold = true;

    private readonly HashSet<Collider> _rightContacts = new();
    private readonly HashSet<Collider> _leftContacts = new();

    private Transform _originalParent;
    private Rigidbody _rb;
    private bool _isParented;
    private CollisionDetectionMode _savedCCD;
    private Collider _selfCollider;

    void Awake()
    {
        _selfCollider = GetComponent<Collider>();
        _originalParent = transform.parent;
        _rb = GetComponent<Rigidbody>();

        if (_selfCollider) _selfCollider.contactOffset = 0.0001f;

        if (_rb)
            _savedCCD = _rb.collisionDetectionMode;

        TryResolveReferences();
    }

    void FixedUpdate()
    {
        TryResolveReferences();

        if (!_isParented || !_selfCollider || !rightTip || !leftTip) return;

        bool rightStillTouching = IsTipTouchingNow(rightTip);
        bool leftStillTouching  = IsTipTouchingNow(leftTip);

        if (!rightStillTouching) _rightContacts.Clear();
        if (!leftStillTouching) _leftContacts.Clear();

        bool bothTouching = rightStillTouching && leftStillTouching;

        if (!bothTouching || !IsValidPinch())
            UnparentNow();
    }

    void OnCollisionEnter(Collision c)
    {
        if (!tipsAreTriggers) HandleEnter(c.collider);
    }

    void OnCollisionExit(Collision c)
    {
        if (!tipsAreTriggers) HandleExit(c.collider);
    }

    void OnTriggerEnter(Collider other)
    {
        if (tipsAreTriggers) HandleEnter(other);
    }

    void OnTriggerExit(Collider other)
    {
        if (tipsAreTriggers) HandleExit(other);
    }

    void HandleEnter(Collider other)
    {
        if (IsRightTip(other)) _rightContacts.Add(other);
        if (IsLeftTip(other)) _leftContacts.Add(other);
        TryUpdateParenting();
    }

    void HandleExit(Collider other)
    {
        if (IsRightTip(other)) _rightContacts.Remove(other);
        if (IsLeftTip(other)) _leftContacts.Remove(other);
        TryUpdateParenting();
    }

    bool IsRightTip(Collider c)
    {
        if (!rightTip) return false;
        return c == rightTip || c.transform.IsChildOf(rightTip.transform);
    }

    bool IsLeftTip(Collider c)
    {
        if (!leftTip) return false;
        return c == leftTip || c.transform.IsChildOf(leftTip.transform);
    }

    void TryUpdateParenting()
    {
        TryResolveReferences();

        bool bothTouching = _rightContacts.Count > 0 && _leftContacts.Count > 0;
        bool validPinch = bothTouching && IsValidPinch();

        if (validPinch && !_isParented) ParentNow();
        else if (!validPinch && _isParented) UnparentNow();
    }

    void ParentNow()
    {
        TryResolveReferences();

        if (!targetParent) return;

        if (_rb && makeKinematicWhileParented)
        {
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.isKinematic = true;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }

        transform.SetParent(targetParent, keepWorldPoseOnParent);

        if (zeroLocalPoseAfterParent)
        {
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }

        _isParented = true;
    }

    void UnparentNow()
    {
        transform.SetParent(_originalParent, keepWorldPoseOnUnparent);

        _rightContacts.Clear();
        _leftContacts.Clear();

        if (_rb && makeKinematicWhileParented)
        {
            _rb.isKinematic = false;
            _rb.collisionDetectionMode = _savedCCD;
        }

        _isParented = false;
    }

    public void ConfigurePinchSources(Collider left, Collider right, Transform parent)
    {
        if (left) leftTip = left;
        if (right) rightTip = right;
        if (parent) targetParent = parent;
    }

    public bool TryResolveReferences()
    {
        if (!autoResolveReferences)
            return HasRequiredReferences();

        Transform resolvedParent = targetParent ? targetParent : FindTransformByName(targetParentName);
        if (resolvedParent)
            targetParent = resolvedParent;

        if (!leftTip && targetParent)
            leftTip = FindColliderInChildrenByName(targetParent, leftTipName);

        if (!rightTip && targetParent)
            rightTip = FindColliderInChildrenByName(targetParent, rightTipName);

        if (!leftTip)
            leftTip = FindColliderByName(leftTipName);

        if (!rightTip)
            rightTip = FindColliderByName(rightTipName);

        bool hasReferences = HasRequiredReferences();
        if (!hasReferences && logAutoResolveFailures)
        {
            Debug.LogWarning(
                $"[{nameof(Detect_Tweezer)}] Failed to resolve references on {name}. " +
                $"leftTip={leftTip != null}, rightTip={rightTip != null}, targetParent={targetParent != null}",
                this);
        }

        return hasReferences;
    }

    bool HasRequiredReferences()
    {
        return leftTip && rightTip && targetParent;
    }

    bool IsTipTouchingNow(Collider tip)
    {
        if (!tip || !_selfCollider)
            return false;

        if (!requireTipOverlapToHold)
            return tip.bounds.Intersects(_selfCollider.bounds);

        if (!tip.enabled || !_selfCollider.enabled || !tip.gameObject.activeInHierarchy || !_selfCollider.gameObject.activeInHierarchy)
            return false;

        if (!tip.bounds.Intersects(_selfCollider.bounds))
            return false;

        return Physics.ComputePenetration(
            tip, tip.transform.position, tip.transform.rotation,
            _selfCollider, _selfCollider.transform.position, _selfCollider.transform.rotation,
            out _, out _);
    }

    bool IsValidPinch()
    {
        if (!usePinchValidation)
            return true;

        if (_selfCollider == null || rightTip == null || leftTip == null)
            return false;

        Vector3 objectCenter = _selfCollider.bounds.center;
        Vector3 leftTipCenter = leftTip.bounds.center;
        Vector3 rightTipCenter = rightTip.bounds.center;

        Vector3 tipAxis = rightTipCenter - leftTipCenter;
        float tipGap = tipAxis.magnitude;
        if (tipGap <= Mathf.Epsilon)
            return false;

        Vector3 tipAxisNormalized = tipAxis / tipGap;
        float projectedRadius = GetProjectedRadius(_selfCollider.bounds.extents, tipAxisNormalized);
        float maxTipGap = Mathf.Max(projectedRadius * 2f * maxTipGapMultiplier, 0.001f);
        if (tipGap > maxTipGap)
            return false;

        float centerProjection = Vector3.Dot(objectCenter - leftTipCenter, tipAxisNormalized);
        float projectionAllowance = Mathf.Max(projectedRadius * centerBetweenTipsAllowanceMultiplier, 0.001f);
        if (centerProjection < -projectionAllowance || centerProjection > tipGap + projectionAllowance)
            return false;

        float centerlineDistance = DistancePointToSegment(objectCenter, leftTipCenter, rightTipCenter);
        float maxCenterlineDistance = Mathf.Max(projectedRadius * centerlineToleranceMultiplier, 0.001f);
        return centerlineDistance <= maxCenterlineDistance;
    }

    static Vector3 GetSurfaceDirection(Vector3 objectCenter, Vector3 contactPoint, Vector3 fallbackPoint)
    {
        Vector3 dir = contactPoint - objectCenter;
        if (dir.sqrMagnitude > 1e-8f)
            return dir.normalized;

        dir = fallbackPoint - objectCenter;
        return dir.sqrMagnitude > 1e-8f ? dir.normalized : Vector3.zero;
    }

    static float GetProjectedRadius(Vector3 extents, Vector3 axis)
    {
        axis = new Vector3(Mathf.Abs(axis.x), Mathf.Abs(axis.y), Mathf.Abs(axis.z));
        return Vector3.Dot(extents, axis);
    }

    static float DistancePointToSegment(Vector3 point, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        float abSqrMag = ab.sqrMagnitude;
        if (abSqrMag <= Mathf.Epsilon)
            return Vector3.Distance(point, a);

        float t = Mathf.Clamp01(Vector3.Dot(point - a, ab) / abSqrMag);
        Vector3 closestPoint = a + ab * t;
        return Vector3.Distance(point, closestPoint);
    }

    static Transform FindTransformByName(string targetName)
    {
        if (string.IsNullOrWhiteSpace(targetName))
            return null;

        Transform[] transforms = FindObjectsOfType<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            if (transforms[i].name == targetName)
                return transforms[i];
        }

        return null;
    }

    static Collider FindColliderByName(string targetName)
    {
        if (string.IsNullOrWhiteSpace(targetName))
            return null;

        Collider[] colliders = FindObjectsOfType<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i].name == targetName)
                return colliders[i];
        }

        return null;
    }

    static Collider FindColliderInChildrenByName(Transform root, string targetName)
    {
        if (!root || string.IsNullOrWhiteSpace(targetName))
            return null;

        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i].name == targetName)
                return colliders[i];
        }

        return null;
    }
}
