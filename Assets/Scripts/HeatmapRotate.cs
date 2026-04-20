using UnityEngine;

public class HeatmapRotate : MonoBehaviour
{
    [Header("Driver (controls group rotation)")]
    public Transform driver;

    [Header("Children that must stay upright (no pitch/roll)")]
    public Transform[] images;

    void LateUpdate()
    {
        if (!driver) return;

        // Rotate the whole pair together (match driver)
        transform.rotation = driver.rotation;

        // Cancel pitch + roll for each image (keep only yaw)
        for (int i = 0; i < images.Length; i++)
        {
            if (!images[i]) continue;

            Vector3 e = images[i].rotation.eulerAngles;
            images[i].rotation = Quaternion.Euler(0f, e.y, 0f);
        }
    }
}
