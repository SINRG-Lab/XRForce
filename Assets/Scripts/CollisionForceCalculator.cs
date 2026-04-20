using UnityEngine;
using System;

public class CollisionForceCalculator : MonoBehaviour
{
    [SerializeField] private GameObject waferBrokenPrefab;
    [SerializeField] private float breakSpeedThreshold = 1.0f;
    [SerializeField] private bool logCollisionsInDevelopment = false;
    public event Action<GameObject> OnBroken;
    bool _broken = false;

    void OnCollisionEnter(Collision c)
    {
        if (_broken) return;

        float hitSpeed = c.relativeVelocity.magnitude; // m/s

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (logCollisionsInDevelopment)
            Debug.Log($"Hit speed: {hitSpeed:F2} m/s");
#endif

        if (hitSpeed >= breakSpeedThreshold)
        {
            _broken = true;

            GameObject broken = null;
            if (waferBrokenPrefab != null)
            {
                broken = Instantiate(waferBrokenPrefab, transform.position, transform.rotation);
            }
            OnBroken?.Invoke(broken);

            Destroy(gameObject);
        }
    }
}
