using System.Collections.Generic;
using UnityEngine;

public class TweezersResettable : MonoBehaviour
{
    [System.Serializable]
    struct TransformState
    {
        public Transform transform;
        public Vector3 localPosition;
        public Quaternion localRotation;
    }

    [System.Serializable]
    struct RigidbodyState
    {
        public Rigidbody rigidbody;
        public bool isKinematic;
    }

    readonly List<TransformState> _transformStates = new();
    readonly List<RigidbodyState> _rigidbodyStates = new();
    bool _isCached;

    void Awake()
    {
        CacheInitialState();
    }

    [ContextMenu("Reset Pose")]
    public void ResetPose()
    {
        CacheInitialState();

        for (int i = 0; i < _rigidbodyStates.Count; i++)
        {
            Rigidbody body = _rigidbodyStates[i].rigidbody;
            if (!body) continue;

            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
        }

        for (int i = 0; i < _transformStates.Count; i++)
        {
            TransformState state = _transformStates[i];
            if (!state.transform) continue;

            state.transform.localPosition = state.localPosition;
            state.transform.localRotation = state.localRotation;
        }

        Physics.SyncTransforms();

        for (int i = 0; i < _rigidbodyStates.Count; i++)
        {
            RigidbodyState state = _rigidbodyStates[i];
            if (!state.rigidbody) continue;

            state.rigidbody.isKinematic = state.isKinematic;
            state.rigidbody.linearVelocity = Vector3.zero;
            state.rigidbody.angularVelocity = Vector3.zero;
            state.rigidbody.Sleep();
        }
    }

    void CacheInitialState()
    {
        if (_isCached)
            return;

        _transformStates.Clear();
        _rigidbodyStates.Clear();

        Transform[] transforms = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform current = transforms[i];
            _transformStates.Add(new TransformState
            {
                transform = current,
                localPosition = current.localPosition,
                localRotation = current.localRotation
            });
        }

        Rigidbody[] rigidbodies = GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < rigidbodies.Length; i++)
        {
            Rigidbody current = rigidbodies[i];
            _rigidbodyStates.Add(new RigidbodyState
            {
                rigidbody = current,
                isKinematic = current.isKinematic
            });
        }

        _isCached = true;
    }
}
