using UnityEngine;

public class WaferBlendController : MonoBehaviour
{
    [Header("Renderer + Shader Property")]
    [SerializeField] private Renderer _renderer;
    [SerializeField] private string _blendProperty = "_Blend"; // float 0..1 on your shader

    [Header("Cleaning")]
    [SerializeField] private float _cleanRatePerSecond = 0.25f; // how fast it cleans at full air strength
    [SerializeField] private bool _startFromSavedValue = true;

    // Optional: save permanently between play sessions
    [SerializeField] private bool _saveToPlayerPrefs = true;
    [SerializeField] private string _saveKey = "WaferCleanBlend";

    private MaterialPropertyBlock _mpb;
    private int _blendId;
    private float _blend; // 0..1 (permanent state)

    void Awake()
    {
        if (_renderer == null) _renderer = GetComponentInChildren<Renderer>();
        _mpb = new MaterialPropertyBlock();
        _blendId = Shader.PropertyToID(_blendProperty);

        if (_startFromSavedValue && _saveToPlayerPrefs && PlayerPrefs.HasKey(_saveKey))
        {
            _blend = Mathf.Clamp01(PlayerPrefs.GetFloat(_saveKey));
        }

        Apply();
    }

    /// Call this every frame while blowing
    public void AddCleaning(float airStrength01)
    {
        if (_blend >= 1f) return;

        float add = _cleanRatePerSecond * Mathf.Clamp01(airStrength01) * Time.deltaTime;
        _blend = Mathf.Clamp01(_blend + add);

        Apply();

        if (_saveToPlayerPrefs)
        {
            PlayerPrefs.SetFloat(_saveKey, _blend);
            PlayerPrefs.Save();
        }
    }

    public float CurrentBlend => _blend;

    private void Apply()
    {
        _renderer.GetPropertyBlock(_mpb);
        _mpb.SetFloat(_blendId, _blend);
        _renderer.SetPropertyBlock(_mpb);
    }

    [ContextMenu("Reset Clean State")]
    public void ResetClean()
    {
        _blend = 0f;
        Apply();
        if (_saveToPlayerPrefs)
        {
            PlayerPrefs.DeleteKey(_saveKey);
            PlayerPrefs.Save();
        }
    }
}
