using UnityEngine;

public class HeatmapPainter : MonoBehaviour
{
    public ReceiverLatest rx;
    public Renderer[] cells = new Renderer[9];   // 0..8 top-left -> bottom-right

    [Header("Color Mapping")]
    public Color zeroPressureColor = Color.white;
    public Color targetPressureColor = Color.green;
    public Color overPressureColor = Color.red;
    [Min(0.001f)] public float targetPressure = 3f;
    [Min(0.001f)] public float redPressure = 10.5f;

    public enum Source { HeatmapA, HeatmapB }
    public Source source = Source.HeatmapA;

    static readonly int ColorId = Shader.PropertyToID("_Color");
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    MaterialPropertyBlock[] _blocks;

    void Awake()
    {
        if (cells == null)
            cells = new Renderer[9];

        _blocks = new MaterialPropertyBlock[cells.Length];
        for (int i = 0; i < cells.Length; i++)
            _blocks[i] = new MaterialPropertyBlock();
    }

    void Update()
    {
        if (rx == null || cells == null || cells.Length < 9) return;

        rx.FlushLatestPacket();

        float[] values = (source == Source.HeatmapA) ? rx.heatmapA : rx.heatmapB;
        if (values == null || values.Length < 9) return;

        for (int i = 0; i < 9; i++)
        {
            float v = values[i];
            if (cells[i] == null) continue;

            var block = _blocks[i];
            Color color = EvaluatePressureColor(v);
            cells[i].GetPropertyBlock(block);
            block.SetColor(ColorId, color);
            block.SetColor(BaseColorId, color);
            cells[i].SetPropertyBlock(block);
        }
    }

    Color EvaluatePressureColor(float pressure)
    {
        float clampedPressure = Mathf.Max(0f, pressure);

        if (clampedPressure <= targetPressure)
        {
            float t = Mathf.InverseLerp(0f, targetPressure, clampedPressure);
            return Color.Lerp(zeroPressureColor, targetPressureColor, t);
        }

        if (redPressure <= targetPressure)
            return overPressureColor;

        float overTargetT = Mathf.InverseLerp(targetPressure, redPressure, clampedPressure);
        return Color.Lerp(targetPressureColor, overPressureColor, overTargetT);
    }
}
