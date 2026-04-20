using Oculus.Interaction.HandGrab;
using UnityEngine;
using UnityEngine.Events;

namespace Oculus.Interaction.Demo
{
    public class AirBlower : MonoBehaviour, IHandGrabUseDelegate
    {
        [Header("Input")]
        [SerializeField] private Transform _trigger;
        [SerializeField] private Transform _nozzle;
        [SerializeField] private AnimationCurve _triggerRotationCurve;
        [SerializeField] private SnapAxis _axis = SnapAxis.X;

        [SerializeField, Range(0f, 1f)]
        private float _startThreshold = 0.1f;   // start blowing above this

        [SerializeField, Range(0f, 1f)]
        private float _stopThreshold = 0.05f;   // stop blowing below this (hysteresis)

        [SerializeField] private float _triggerSpeed = 6f;
        [SerializeField] private AnimationCurve _strengthCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Air Output")]
        [SerializeField] private LayerMask _affectedLayers = ~0;
        [SerializeField, Tooltip("Max distance of the air cone")]
        private float _maxDistance = 3f;

        [SerializeField, Tooltip("Cone angle (deg). Bigger = wider air cone.")]
        private float _coneAngle = 25f;

        [SerializeField, Tooltip("Force at full strength (N). Applied as acceleration (mass-independent).")]
        private float _maxForce = 20f;

        [SerializeField, Tooltip("Extra upward lift to make light objects feel like they 'flutter' (optional).")]
        private float _lift = 0.0f;

        [SerializeField, Tooltip("How many colliders we can affect per frame.")]
        private int _maxHits = 24;

        [SerializeField, Tooltip("Use Acceleration so force doesn't depend on object mass.")]
        private ForceMode _forceMode = ForceMode.Acceleration;

        [Header("Optional FX")]
        [SerializeField] private ParticleSystem _airVFX;
        [SerializeField] private AudioSource _airSFX;

        [Header("Events")]
        [SerializeField] private UnityEvent WhenBlowStart;
        [SerializeField] private UnityEvent WhenBlowStop;

        private Collider[] _hits;
        private bool _blowing;

        private float _dampedUseStrength;
        private float _lastUseTime;

        private void Awake()
        {
            _hits = new Collider[Mathf.Max(1, _maxHits)];
        }

        private void UpdateTriggerRotation(float progress)
        {
            float value = _triggerRotationCurve.Evaluate(progress);
            Vector3 angles = _trigger.localEulerAngles;

            if ((_axis & SnapAxis.X) != 0) angles.x = value;
            if ((_axis & SnapAxis.Y) != 0) angles.y = value;
            if ((_axis & SnapAxis.Z) != 0) angles.z = value;

            _trigger.localEulerAngles = angles;
        }

        public void BeginUse()
        {
            _dampedUseStrength = 0f;
            _lastUseTime = Time.realtimeSinceStartup;
        }

        public void EndUse()
        {
            StopBlowing();
        }

        public float ComputeUseStrength(float strength)
        {
            // Smooth like WaterSpray
            float delta = Time.realtimeSinceStartup - _lastUseTime;
            _lastUseTime = Time.realtimeSinceStartup;

            if (strength > _dampedUseStrength)
                _dampedUseStrength = Mathf.Lerp(_dampedUseStrength, strength, _triggerSpeed * delta);
            else
                _dampedUseStrength = strength;

            float progress = _strengthCurve.Evaluate(_dampedUseStrength);

            UpdateTriggerRotation(progress);
            UpdateBlowing(progress);

            return progress;
        }

        private void UpdateBlowing(float progress)
        {
            // Start/stop with hysteresis so it doesn’t flicker around the threshold
            if (!_blowing && progress >= _startThreshold)
            {
                _blowing = true;
                WhenBlowStart?.Invoke();
                if (_airVFX) _airVFX.Play(true);
                if (_airSFX && !_airSFX.isPlaying) _airSFX.Play();
            }
            else if (_blowing && progress <= _stopThreshold)
            {
                StopBlowing();
            }

            if (_blowing)
            {
                ApplyAir(progress);
            }
        }

        private void StopBlowing()
        {
            if (!_blowing) return;

            _blowing = false;
            WhenBlowStop?.Invoke();

            if (_airVFX) _airVFX.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (_airSFX && _airSFX.isPlaying) _airSFX.Stop();
        }

        private void ApplyAir(float strength01)
        {
            if (_nozzle == null) return;

            Vector3 origin = _nozzle.position;
            Vector3 fwd = _nozzle.forward;

            // Broadphase: grab nearby colliders (sphere is fine; we’ll filter to a cone)
            float maxRadiusAtEnd = Mathf.Tan(Mathf.Deg2Rad * _coneAngle * 0.5f) * _maxDistance;
            int count = Physics.OverlapSphereNonAlloc(
                origin, maxRadiusAtEnd, _hits, _affectedLayers, QueryTriggerInteraction.Ignore);

            float forceBase = _maxForce * Mathf.Clamp01(strength01);

            for (int i = 0; i < count; i++)
            {
                Collider col = _hits[i];
                if (!col) continue;

                Rigidbody rb = col.attachedRigidbody;
                if (rb == null || rb.isKinematic) continue;

                // Use the rigidbody COM (more stable than ClosestPoint for cone checks)
                Vector3 to = rb.worldCenterOfMass - origin;

                // Forward distance along nozzle direction
                float forwardDist = Vector3.Dot(to, fwd);

                // Reject anything behind the nozzle or beyond max distance
                if (forwardDist <= 0f || forwardDist > _maxDistance) continue;

                // Perpendicular distance from the cone axis
                Vector3 lateral = to - fwd * forwardDist;
                float lateralDist = lateral.magnitude;

                // Cone radius grows with distance
                float allowedRadius = Mathf.Tan(Mathf.Deg2Rad * _coneAngle * 0.5f) * forwardDist;

                // Reject if outside cone
                if (lateralDist > allowedRadius) continue;

                // Optional distance falloff (stronger near nozzle)
                float distanceFade = 1f - (forwardDist / _maxDistance);

                // Direction + optional lift
                Vector3 dir = fwd;
                if (_lift != 0f) dir = (dir + Vector3.up * _lift).normalized;

                rb.AddForce(dir * (forceBase * distanceFade), _forceMode);
            }
        }
    }
}
