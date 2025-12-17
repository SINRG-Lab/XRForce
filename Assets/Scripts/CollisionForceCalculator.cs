using UnityEngine;

public class CollisionForceCalculator : MonoBehaviour
{
    [SerializeField] private GameObject wafer_broken;
    bool objectDestroyed = false;

    void OnCollisionEnter(Collision c)
    {
        float hitSpeed = c.relativeVelocity.magnitude; // m/s
        Debug.Log($"Hit speed: {hitSpeed:F2} m/s");

        if (hitSpeed > 1.0f && !objectDestroyed)
        {
            
            Destroy(gameObject);
            Instantiate(wafer_broken, transform.position, transform.rotation);
            objectDestroyed = true;
        }
    }
}
