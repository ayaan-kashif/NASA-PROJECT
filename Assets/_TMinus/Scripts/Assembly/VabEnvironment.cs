using UnityEngine;
using M = TMinus.Core.MaterialLibrary;

namespace TMinus.Assembly
{
    /// <summary>
    /// The assembly hall, built from boxes: grid floor, walls, launch stand and gantry tower.
    /// Called by the VAB scene builder (edit time). The stand and gantry resize per rocket.
    /// </summary>
    public sealed class VabEnvironment : MonoBehaviour
    {
        public Transform stand;
        public Transform gantry;

        // Big enough that the camera framing the 111 m Saturn V (about 200 m back) stays inside.
        public const float HallWidth = 600f, HallDepth = 600f, HallHeight = 260f;
        public const float StandHeight = 1.5f;

        public static VabEnvironment Build(Transform parent)
        {
            var root = new GameObject("[Environment]");
            root.transform.SetParent(parent, false);
            var env = root.AddComponent<VabEnvironment>();

            var floor = Box(root.transform, "Floor", new Vector3(HallWidth, 0.2f, HallDepth), new Vector3(0, -0.1f, 0), M.Concrete);
            floor.AddComponent<GridFloor>();

            Box(root.transform, "Wall_Back", new Vector3(HallWidth, HallHeight, 1), new Vector3(0, HallHeight / 2, HallDepth / 2), M.Wall);
            Box(root.transform, "Wall_Front", new Vector3(HallWidth, HallHeight, 1), new Vector3(0, HallHeight / 2, -HallDepth / 2), M.Wall);
            Box(root.transform, "Wall_Left", new Vector3(1, HallHeight, HallDepth), new Vector3(-HallWidth / 2, HallHeight / 2, 0), M.Wall);
            Box(root.transform, "Wall_Right", new Vector3(1, HallHeight, HallDepth), new Vector3(HallWidth / 2, HallHeight / 2, 0), M.Wall);
            Box(root.transform, "Ceiling", new Vector3(HallWidth, 1, HallDepth), new Vector3(0, HallHeight, 0), M.Wall);

            // Vertical stripes on the back wall give a sense of scale.
            for (int i = -14; i <= 14; i++)
            {
                Box(root.transform, "Wall_Rib_B" + i, new Vector3(2f, HallHeight, 1f), new Vector3(i * 20f, HallHeight / 2, HallDepth / 2 - 1f), M.BandBlack);
                Box(root.transform, "Wall_Rib_F" + i, new Vector3(2f, HallHeight, 1f), new Vector3(i * 20f, HallHeight / 2, -HallDepth / 2 + 1f), M.BandBlack);
            }

            var stand = new GameObject("LaunchStand").transform;
            stand.SetParent(root.transform, false);
            Box(stand, "Deck", new Vector3(1, StandHeight, 1), new Vector3(0, StandHeight / 2, 0), M.MetalGrey);
            env.stand = stand;

            var gantry = new GameObject("Gantry").transform;
            gantry.SetParent(root.transform, false);
            const float tower = 120f, w = 4f;
            for (int i = 0; i < 4; i++)
            {
                float x = (i % 2 == 0 ? -1 : 1) * w / 2, z = (i < 2 ? -1 : 1) * w / 2;
                Box(gantry, "Column_" + i, new Vector3(0.4f, tower, 0.4f), new Vector3(x, tower / 2, z), M.GantryRed);
            }
            for (float y = 6; y < tower; y += 8)
            {
                Box(gantry, "Beam_X_" + y, new Vector3(w, 0.3f, 0.3f), new Vector3(0, y, -w / 2), M.GantryRed);
                Box(gantry, "Beam_X2_" + y, new Vector3(w, 0.3f, 0.3f), new Vector3(0, y, w / 2), M.GantryRed);
                Box(gantry, "Beam_Z_" + y, new Vector3(0.3f, 0.3f, w), new Vector3(-w / 2, y, 0), M.GantryRed);
                Box(gantry, "Beam_Z2_" + y, new Vector3(0.3f, 0.3f, w), new Vector3(w / 2, y, 0), M.GantryRed);
            }
            env.gantry = gantry;
            env.FitTo(1.2f, 20f);
            return env;
        }

        /// <summary>Resize the stand and move the gantry to suit a rocket of this size.</summary>
        public void FitTo(float diameter, float height)
        {
            float deck = Mathf.Max(4f, diameter * 2.2f);
            if (stand != null) stand.localScale = new Vector3(deck, 1, deck);
            if (gantry != null)
            {
                gantry.localPosition = new Vector3(deck / 2 + 4f, 0, 0);
                float s = Mathf.Clamp(height / 100f, 0.25f, 1.1f);
                gantry.localScale = new Vector3(Mathf.Max(0.6f, s * 2f), s, Mathf.Max(0.6f, s * 2f));
            }
        }

        public float DeckTop => StandHeight;

        static GameObject Box(Transform parent, string name, Vector3 size, Vector3 pos, string mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localScale = size;
            go.transform.localPosition = pos;
            go.GetComponent<MeshRenderer>().sharedMaterial = M.Get(mat);
            go.isStatic = true;
            return go;
        }
    }

    /// <summary>Puts a faint generated grid on the floor material (no texture asset needed).</summary>
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class GridFloor : MonoBehaviour
    {
        static Texture2D _grid;

        void Awake()
        {
            if (_grid == null)
            {
                const int size = 128;
                _grid = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, name = "VAB_Grid" };
                var px = new Color32[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        bool major = x < 2 || y < 2;
                        bool minor = x % 32 == 0 || y % 32 == 0;
                        byte v = major ? (byte)150 : minor ? (byte)185 : (byte)205;
                        px[y * size + x] = new Color32(v, v, (byte)(v + 6), 255);
                    }
                _grid.SetPixels32(px);
                _grid.Apply(true);
            }
            var mpb = new MaterialPropertyBlock();
            var r = GetComponent<MeshRenderer>();
            r.GetPropertyBlock(mpb);
            mpb.SetTexture("_BaseMap", _grid);
            mpb.SetVector("_BaseMap_ST", new Vector4(VabEnvironment.HallWidth / 8f, VabEnvironment.HallDepth / 8f, 0, 0));
            r.SetPropertyBlock(mpb);
        }
    }
}
