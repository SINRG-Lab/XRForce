using UnityEngine;
using System.Collections.Generic;

public class Detect_Tweezer : MonoBehaviour
{
    [Header("Pinch sources (your tweezer tips)")]
    public Collider rightTip;               // drag the right tip collider
    public Collider leftTip;                // drag the left  tip collider
    public bool tipsAreTriggers = false;    // set true if the tip colliders use IsTrigger

    [Header("Parenting")]
    public Transform targetParent;          // who to parent under when pinched
    public bool keepWorldPoseOnParent = true;
    public bool keepWorldPoseOnUnparent = true;
    public bool makeKinematicWhileParented = true;
    public bool zeroLocalPoseAfterParent = false; // snap to parent's origin when parented

    private readonly HashSet<Collider> _rightContacts = new();
    private readonly HashSet<Collider> _leftContacts  = new();

    private Transform _originalParent;
    private Rigidbody _rb;
    private bool _isParented;
    private bool _savedKinematic;
    private CollisionDetectionMode _savedCCD;

    void Awake()
    {
        GetComponent<Collider>().contactOffset = 0.0001f;  // try 1–2 mm

        _originalParent = transform.parent;
        _rb = GetComponent<Rigidbody>();
        if (_rb)
        {
            _savedKinematic = _rb.isKinematic;
            _savedCCD = _rb.collisionDetectionMode;
        }
    }

    // -------- Solid collider path --------
    void OnCollisionEnter(Collision c) { if (!tipsAreTriggers) HandleEnter(c.collider); }
    void OnCollisionExit (Collision c) { if (!tipsAreTriggers) HandleExit (c.collider); }

    // -------- Trigger path (use if tips are triggers) --------
    void OnTriggerEnter(Collider other) { if (tipsAreTriggers) HandleEnter(other); }
    void OnTriggerExit (Collider other) { if (tipsAreTriggers) HandleExit (other); }

    void HandleEnter(Collider other)
    {
        if (IsRightTip(other)) _rightContacts.Add(other);
        if (IsLeftTip (other)) _leftContacts.Add(other);
        TryUpdateParenting();
    }

    void HandleExit(Collider other)
    {
        if (IsRightTip(other)) _rightContacts.Remove(other);
        if (IsLeftTip (other)) _leftContacts.Remove(other);
        TryUpdateParenting();
    }

    bool IsRightTip(Collider c)
    {
        if (!rightTip) return false;
        // count the tip collider itself or any child collider under the tip’s transform
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
            _savedKinematic = _rb.isKinematic;
            _savedCCD = _rb.collisionDetectionMode;
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

        if (_rb && makeKinematicWhileParented)
        {
            _rb.isKinematic = _savedKinematic;
            _rb.collisionDetectionMode = _savedCCD;
        }

        _isParented = false;
    }
}
