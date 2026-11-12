using UnityEngine;

namespace TMinus.Wiring
{
    /// <summary>A cable drawn as a cubic Bezier that arcs over the bench; longer cables arc higher.</summary>
    [RequireComponent(typeof(LineRenderer))]
    public sealed class CableView : MonoBehaviour
    {
        public string cableId;
        public string cableType;
        public Vector3 from, to;
        const int Points = 24;

        LineRenderer _line;
        readonly Vector3[] _pts = new Vector3[Points];

        public static CableView Create(Transform parent, string cableType, bool ghost)
        {
            var go = new GameObject("Cable_" + cableType);
            go.transform.SetParent(parent, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.positionCount = Points;
            lr.useWorldSpace = true;
            lr.textureMode = LineTextureMode.Tile;
            lr.numCapVertices = 3;
            lr.numCornerVertices = 2;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.sharedMaterial = CableStyle.Material(cableType, ghost);
            lr.widthMultiplier = CableStyle.Width(cableType);
            lr.alignment = LineAlignment.View;
            var cv = go.AddComponent<CableView>();
            cv._line = lr;
            cv.cableType = cableType;
            return cv;
        }

        public void SetType(string type, bool ghost)
        {
            cableType = type;
            if (_line == null) _line = GetComponent<LineRenderer>();
            _line.sharedMaterial = CableStyle.Material(type, ghost);
            _line.widthMultiplier = CableStyle.Width(type);
        }

        public void SetEnds(Vector3 a, Vector3 b)
        {
            from = a; to = b;
            float len = Vector3.Distance(a, b);
            float lift = 0.03f + 0.18f * len;          // sag grows with length (here: arcs over the bench)
            Vector3 c1 = a + Vector3.up * lift + (b - a) * 0.15f;
            Vector3 c2 = b + Vector3.up * lift - (b - a) * 0.15f;
            for (int i = 0; i < Points; i++)
            {
                float t = i / (Points - 1f), u = 1 - t;
                _pts[i] = u * u * u * a + 3 * u * u * t * c1 + 3 * u * t * t * c2 + t * t * t * b;
            }
            if (_line == null) _line = GetComponent<LineRenderer>();
            _line.SetPositions(_pts);
            // Repeat the pattern every ~2 cm.
            if (_line.sharedMaterial != null) _line.sharedMaterial.mainTextureScale = new Vector2(1, 1);
            _line.textureScale = new Vector2(1f / Mathf.Max(0.005f, _line.widthMultiplier * 3f), 1);
        }

        /// <summary>Smallest screen distance from a point to this cable (for right-click and hover).</summary>
        public float ScreenDistance(Camera cam, Vector2 screen)
        {
            float best = float.MaxValue;
            Vector2 prev = cam.WorldToScreenPoint(_pts[0]);
            for (int i = 1; i < Points; i++)
            {
                Vector2 cur = cam.WorldToScreenPoint(_pts[i]);
                best = Mathf.Min(best, DistToSegment(screen, prev, cur));
                prev = cur;
            }
            return best;
        }

        static float DistToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float t = ab.sqrMagnitude < 1e-6f ? 0 : Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }

        public Vector3 Mid => _pts[Points / 2];
    }
}
