using UnityEngine;
using System.Collections.Generic;

public class Detect_Tweezer : MonoBehaviour
{
    [Header("Pinch sources (your tweezer tips)")]
    public Collider rightTip;
    public Collider leftTip;
    public bool tipsAreTriggers = false;

    [Header("Parenting")]
    public Transform targetParent;
    public bool keepWorldPoseOnParent = true;
    public bool keepWorldPoseOnUnparent = true;
    public bool makeKinematicWhileParented = true;
    public bool zeroLocalPoseAfterParent = false;

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
    }

    void FixedUpdate()
    {
        if (!_isParented || !_selfCollider || !rightTip || !leftTip) return;

        bool rightStillTouching = rightTip.bounds.Intersects(_selfCollider.bounds);
        bool leftStillTouching  = leftTip.bounds.Intersects(_selfCollider.bounds);

        if (!rightStillTouching) _rightContacts.Clear();
        if (!leftStillTouching) _leftContacts.Clear();

        bool bothTouching = rightStillTouching && leftStillTouching;

        if (!bothTouching)
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
        bool bothTouching = _rightContacts.Count > 0 && _leftContacts.Count > 0;

        if (bothTouching && !_isParented) ParentNow();
        else if (!bothTouching && _isParented) UnparentNow();
    }

    void ParentNow()
    {
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
}