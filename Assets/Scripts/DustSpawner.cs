using UnityEngine;

[DisallowMultipleComponent]
public class DustSpawner : MonoBehaviour
{
    [Header("Dust")]
    public GameObject dustPrefab;
    [Min(1)] public int count = 200;

    [Tooltip("Random scale multiplier for dust instances.")]
    public Vector2 scaleRange = new Vector2(0.6f, 1.4f);

    [Tooltip("Lift the dust slightly off the surface to avoid z-fighting.")]
    public float surfaceOffset = 0.0005f;

    [Tooltip("Parent spawned dust under this transform (defaults to this object).")]
    public Transform parentOverride;

    [Header("Spawn Options")]
    public bool alignToNormal = true;
    public bool randomYaw = true;

    Mesh _mesh;
    Vector3[] _verts;
    int[] _tris;
    Vector3[] _normals;
    float[] _cdf; // cumulative distribution of triangle areas
    float _totalArea;

    void Awake()
    {
        var mf = GetComponentInChildren<MeshFilter>();
        if (mf == null || mf.sharedMesh == null)
        {
            Debug.LogError($"[{nameof(DustSpawner)}] No MeshFilter with a mesh found under {name}");
            enabled = false;
            return;
        }

        _mesh = mf.sharedMesh;
        _verts = _mesh.vertices;
        _tris = _mesh.triangles;
        _normals = _mesh.normals;

        BuildTriangleAreaCDF();
    }

    void Start()
    {
        if (dustPrefab == null)
        {
            Debug.LogError($"[{nameof(DustSpawner)}] Dust Prefab is not assigned.");
            return;
        }

        Spawn();
    }

    [ContextMenu("Spawn Dust Now")]
    public void Spawn()
    {
        Transform parent = parentOverride != null ? parentOverride : transform;

        for (int i = 0; i < count; i++)
        {
            SamplePointOnMesh(out Vector3 localPos, out Vector3 localNormal);

            // Convert to world
            Vector3 worldPos = transform.TransformPoint(localPos);
            Vector3 worldNormal = transform.TransformDirection(localNormal).normalized;

            worldPos += worldNormal * surfaceOffset;

            Quaternion rot = Quaternion.identity;

            if (alignToNormal)
            {
                // Align "up" to normal; if your dust prefab forward matters, adjust here
                rot = Quaternion.FromToRotation(Vector3.up, worldNormal);
            }

            if (randomYaw)
            {
                rot = Quaternion.AngleAxis(Random.Range(0f, 360f), worldNormal) * rot;
            }

            var go = Instantiate(dustPrefab, worldPos, rot, parent);

            float s = Random.Range(scaleRange.x, scaleRange.y);
            go.transform.localScale = go.transform.localScale * s;
        }
    }

    void BuildTriangleAreaCDF()
    {
        int triCount = _tris.Length / 3;
        _cdf = new float[triCount];
        _totalArea = 0f;

        for (int t = 0; t < triCount; t++)
        {
            int i0 = _tris[t * 3 + 0];
            int i1 = _tris[t * 3 + 1];
            int i2 = _tris[t * 3 + 2];

            Vector3 a = _verts[i0];
            Vector3 b = _verts[i1];
            Vector3 c = _verts[i2];

            float area = Vector3.Cross(b - a, c - a).magnitude * 0.5f;
            _totalArea += area;
            _cdf[t] = _totalArea;
        }
    }

    void SamplePointOnMesh(out Vector3 localPos, out Vector3 localNormal)
    {
        // Pick a triangle by area
        float r = Random.Range(0f, _totalArea);
        int triIndex = System.Array.BinarySearch(_cdf, r);
        if (triIndex < 0) triIndex = ~triIndex;
        triIndex = Mathf.Clamp(triIndex, 0, _cdf.Length - 1);

        int i0 = _tris[triIndex * 3 + 0];
        int i1 = _tris[triIndex * 3 + 1];
        int i2 = _tris[triIndex * 3 + 2];

        // Random barycentric coords (uniform over triangle)
        float u = Random.value;
        float v = Random.value;
        if (u + v > 1f) { u = 1f - u; v = 1f - v; }

        Vector3 a = _verts[i0];
        Vector3 b = _verts[i1];
        Vector3 c = _verts[i2];

        localPos = a + (b - a) * u + (c - a) * v;

        // Interpolated normal (falls back to face normal if mesh has no normals)
        if (_normals != null && _normals.Length == _verts.Length)
        {
            Vector3 na = _normals[i0];
            Vector3 nb = _normals[i1];
            Vector3 nc = _normals[i2];
            localNormal = (na + (nb - na) * u + (nc - na) * v).normalized;
        }
        else
        {
            localNormal = Vector3.Cross(b - a, c - a).normalized;
        }
    }
}
