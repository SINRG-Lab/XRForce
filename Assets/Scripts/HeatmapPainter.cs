using UnityEngine;

public class HeatmapPainter : MonoBehaviour
{
    public ReceiverLatest rx;
    public Renderer[] cells = new Renderer[9];   // 0..8 top-left -> bottom-right
    public Gradient gradient;

    public float minValue = 0f;
    public float maxValue = 20f;   // since you are sending Force (N), not ADC (adjust!)

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
            float t = Mathf.InverseLerp(minValue, maxValue, v);
            if (cells[i] == null) continue;

            var block = _blocks[i];
            Color color = gradient.Evaluate(t);
            cells[i].GetPropertyBlock(block);
            block.SetColor(ColorId, color);
            block.SetColor(BaseColorId, color);
            cells[i].SetPropertyBlock(block);
        }
    }
}
