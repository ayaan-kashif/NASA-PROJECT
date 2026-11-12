using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace TMinus.Flight
{
    /// <summary>Line chart of one telemetry value against time (Painter2D).</summary>
    public sealed class LineChart : VisualElement
    {
        readonly List<Vector2> _pts = new List<Vector2>();
        readonly Label _title, _value;
        readonly Color _color;
        float _maxX = 1, _maxY = 1;

        public LineChart(string title, Color color)
        {
            _color = color;
            AddToClassList("chart");
            _title = new Label(title); _title.AddToClassList("chart-title");
            _value = new Label(); _value.AddToClassList("chart-value");
            Add(_title); Add(_value);
            generateVisualContent += Draw;
        }

        public void Add(float x, float y, string valueText)
        {
            if (_pts.Count > 0 && x - _pts[_pts.Count - 1].x < 0.5f) { _value.text = valueText; return; }
            _pts.Add(new Vector2(x, y));
            _maxX = Mathf.Max(_maxX, x);
            _maxY = Mathf.Max(_maxY, y);
            _value.text = valueText;
            MarkDirtyRepaint();
        }

        void Draw(MeshGenerationContext ctx)
        {
            var r = contentRect;
            if (r.width < 10 || _pts.Count < 2) return;
            var p = ctx.painter2D;
            float l = 8, t = 34, w = r.width - 16, h = r.height - 44;
            p.strokeColor = new Color(1, 1, 1, 0.12f); p.lineWidth = 1;
            for (int i = 0; i <= 4; i++)
            {
                p.BeginPath(); p.MoveTo(new Vector2(l, t + h * i / 4)); p.LineTo(new Vector2(l + w, t + h * i / 4)); p.Stroke();
            }
            p.strokeColor = _color; p.lineWidth = 2.5f; p.lineJoin = LineJoin.Round;
            p.BeginPath();
            for (int i = 0; i < _pts.Count; i++)
            {
                var v = new Vector2(l + w * _pts[i].x / _maxX, t + h - h * _pts[i].y / (_maxY * 1.05f));
                if (i == 0) p.MoveTo(v); else p.LineTo(v);
            }
            p.Stroke();
        }
    }

    /// <summary>2D orbit map: Earth, the flown path, the predicted orbit (Kepler ellipse) and the rocket.</summary>
    public sealed class OrbitMap : VisualElement
    {
        public IList<Vec2d> path;
        public Vec2d pos, vel;
        public string caption = "";
        readonly Label _caption;

        public OrbitMap()
        {
            AddToClassList("orbit-map");
            _caption = new Label(); _caption.AddToClassList("map-caption");
            Add(_caption);
            generateVisualContent += Draw;
        }

        public void Refresh() { _caption.text = caption; MarkDirtyRepaint(); }

        void Draw(MeshGenerationContext ctx)
        {
            var r = contentRect;
            if (r.width < 10) return;
            var p = ctx.painter2D;
            var c = new Vector2(r.width / 2, r.height / 2);
            double R = FlightConstants.EarthRadiusM;
            double viewR = R * 1.45;
            float scale = (float)(Mathf.Min(r.width, r.height) * 0.48f / viewR);
            Vector2 S(Vec2d v) => c + new Vector2((float)v.x, -(float)v.y) * scale;

            p.fillColor = new Color(0.08f, 0.3f, 0.6f);
            p.BeginPath(); p.Arc(c, (float)(R * scale), 0, 360); p.Fill();
            p.strokeColor = new Color(0.5f, 0.75f, 1f, 0.35f); p.lineWidth = 6;
            p.BeginPath(); p.Arc(c, (float)(R * scale) + 3, 0, 360); p.Stroke();

            // Predicted orbit from the current state vector.
            double mu = FlightConstants.Mu, rmag = pos.Magnitude, v2 = vel.SqrMagnitude;
            if (rmag > 0)
            {
                double energy = v2 / 2 - mu / rmag;
                if (energy < 0)
                {
                    double a = -mu / (2 * energy);
                    double rv = pos.x * vel.x + pos.y * vel.y;
                    var e = new Vec2d((v2 - mu / rmag) * pos.x - rv * vel.x, (v2 - mu / rmag) * pos.y - rv * vel.y) / mu;
                    double ecc = e.Magnitude, w = System.Math.Atan2(e.y, e.x);
                    p.strokeColor = new Color(1f, 1f, 1f, 0.45f); p.lineWidth = 1.5f;
                    p.BeginPath();
                    for (int i = 0; i <= 180; i++)
                    {
                        double th = 2 * System.Math.PI * i / 180;
                        double rr = a * (1 - ecc * ecc) / (1 + ecc * System.Math.Cos(th - w));
                        if (rr < R) rr = R;
                        var pt = S(new Vec2d(rr * System.Math.Cos(th), rr * System.Math.Sin(th)));
                        if (i == 0) p.MoveTo(pt); else p.LineTo(pt);
                    }
                    p.Stroke();
                }
            }
            if (path != null && path.Count > 1)
            {
                p.strokeColor = new Color(1f, 0.48f, 0.1f); p.lineWidth = 3;
                p.BeginPath(); p.MoveTo(S(path[0]));
                for (int i = 1; i < path.Count; i++) p.LineTo(S(path[i]));
                p.LineTo(S(pos));
                p.Stroke();
            }
            p.fillColor = Color.white;
            p.BeginPath(); p.Arc(S(pos), 5, 0, 360); p.Fill();
        }
    }
}
