using UnityEngine;
using UnityEngine.UIElements;

namespace TMinus.Assembly
{
    /// <summary>Circular torque gauge drawn with Painter2D: grey track, green target band, orange fill, preset tick.</summary>
    public sealed class TorqueGauge : VisualElement
    {
        public float Value, Target = 45, TolerancePct = 10, Preset = -1;
        public float Max => Target * 1.5f;

        readonly Label _text;
        const float StartDeg = 135, SweepDeg = 270;

        public TorqueGauge()
        {
            AddToClassList("torque-gauge");
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            style.width = 150; style.height = 150;
            _text = new Label { pickingMode = PickingMode.Ignore };
            _text.AddToClassList("gauge-text");
            Add(_text);
            generateVisualContent += Draw;
        }

        public void Set(float value, float target, float tolPct, float preset)
        {
            Value = value; Target = target; TolerancePct = tolPct; Preset = preset;
            float tol = tolPct / 100f;
            string state = value > target * (1 + tol) ? "OVER" : value >= target * (1 - tol) ? "ON TARGET" : preset > 0 && value >= preset * 0.98f ? "PASS SET" : "";
            _text.text = $"{value:0.0}\nN·m\n{state}";
            MarkDirtyRepaint();
        }

        float Angle(float nm) => StartDeg + SweepDeg * Mathf.Clamp01(nm / Max);

        void Draw(MeshGenerationContext ctx)
        {
            var p = ctx.painter2D;
            var c = new Vector2(contentRect.width / 2, contentRect.height / 2);
            float r = Mathf.Min(c.x, c.y) - 12;
            float tol = TolerancePct / 100f;

            p.lineCap = LineCap.Round;
            Arc(p, c, r, StartDeg, StartDeg + SweepDeg, new Color(1, 1, 1, 0.15f), 14);
            Arc(p, c, r, Angle(Target * (1 - tol)), Angle(Target * (1 + tol)), new Color(0.25f, 0.9f, 0.4f, 0.9f), 14);
            bool over = Value > Target * (1 + tol);
            Arc(p, c, r - 14, StartDeg, Angle(Value), over ? new Color(1f, 0.25f, 0.2f) : new Color(1f, 0.48f, 0.1f), 9);
            if (Preset > 0)
            {
                float a = Angle(Preset) * Mathf.Deg2Rad;
                p.strokeColor = Color.white;
                p.lineWidth = 3;
                p.BeginPath();
                p.MoveTo(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (r - 22));
                p.LineTo(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (r + 8));
                p.Stroke();
            }
        }

        static void Arc(Painter2D p, Vector2 c, float r, float fromDeg, float toDeg, Color color, float width)
        {
            if (toDeg <= fromDeg + 0.01f) return;
            p.strokeColor = color;
            p.lineWidth = width;
            p.BeginPath();
            p.Arc(c, r, UnityEngine.UIElements.Angle.Degrees(fromDeg), UnityEngine.UIElements.Angle.Degrees(toDeg), ArcDirection.Clockwise);
            p.Stroke();
        }
    }
}
