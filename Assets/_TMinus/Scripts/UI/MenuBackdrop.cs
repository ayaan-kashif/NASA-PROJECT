using TMinus.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using M = TMinus.Core.MaterialLibrary;

namespace TMinus.UI
{
    /// <summary>
    /// Live 3D backdrop for the main menu (built at runtime, no scene edits): NASA Blue Marble Earth turning under
    /// a star field with an atmosphere glow, and the Pathfinder rocket (Blender models, if present) slowly rotating.
    /// </summary>
    public sealed class MenuBackdrop : MonoBehaviour
    {
        Transform _earth, _rocket;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Hook()
        {
            SceneManager.sceneLoaded += (s, m) => { if (s.name == SceneNames.MainMenu) Spawn(); };
            if (SceneManager.GetActiveScene().name == SceneNames.MainMenu) Spawn();
        }

        static void Spawn()
        {
            if (FindFirstObjectByType<MenuBackdrop>() != null) return;
            new GameObject("[Menu Backdrop]").AddComponent<MenuBackdrop>();   // lives and dies with the menu scene
        }

        void Start()
        {
            var cam = Camera.main;
            if (cam == null) return;
            cam.transform.SetPositionAndRotation(new Vector3(0, 9, -30), Quaternion.Euler(-4, 0, 0));
            cam.fieldOfView = 40; cam.nearClipPlane = 0.3f; cam.farClipPlane = 3000;
            var stars = Resources.Load<Material>("Looks/Sky_Stars");
            if (stars != null) { RenderSettings.skybox = stars; cam.clearFlags = CameraClearFlags.Skybox; }
            RenderSettings.fog = false;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.16f, 0.18f, 0.24f);

            var sun = FindFirstObjectByType<Light>();
            if (sun != null) { sun.transform.rotation = Quaternion.Euler(18, -55, 0); sun.intensity = 2.2f; sun.color = new Color(1f, 0.97f, 0.92f); sun.shadows = LightShadows.Soft; }

            // Earth: big, low and to the right, so its lit limb curves behind the rocket.
            var tex = Resources.Load<Texture2D>("Earth/BlueMarble") ?? Resources.Load<Texture2D>("Earth/EarthProcedural");
            _earth = Ball("Earth", 420f, new Vector3(120, -215, 330));
            var em = new Material(M.Get(M.BodyWhite)) { name = "MenuEarth" };
            em.SetTexture("_BaseMap", tex); em.SetTextureScale("_BaseMap", Vector2.one); em.SetColor("_BaseColor", Color.white);
            em.SetTexture("_BumpMap", null); em.DisableKeyword("_NORMALMAP"); em.SetFloat("_Smoothness", 0.25f); em.SetFloat("_Metallic", 0f);
            _earth.GetComponent<Renderer>().sharedMaterial = em;
            _earth.rotation = Quaternion.Euler(-28, 150, 12);
            var atmo = Resources.Load<Material>("Looks/Atmosphere");
            if (atmo != null)
            {
                var shell = Ball("Atmosphere", 420f * 1.012f, _earth.position);
                var am = new Material(atmo);
                if (sun != null) am.SetVector("_SunDir", -sun.transform.forward);
                shell.GetComponent<Renderer>().sharedMaterial = am;
            }

            // Rocket: stack the model prefabs by the lengths in the data.
            var data = GameState.Instance != null ? GameState.Instance.Data : null;
            _rocket = new GameObject("MenuRocket").transform;
            _rocket.SetParent(transform, false);
            float y = 0; bool any = false;
            foreach (var id in new[] { "pf_stage1", "pf_interstage", "pf_stage2", "pf_avionics_ring", "pf_payload_adapter" })
            {
                any |= Add(id, y);
                y += (float)(data?.GetPart(id)?.lengthM ?? 0);
            }
            float fairingBase = y - (float)(data?.GetPart("pf_payload_adapter")?.lengthM ?? 0.4);
            Add("pf_fairing_left", fairingBase); Add("pf_fairing_right", fairingBase);
            _rocket.position = new Vector3(7.5f, -1.5f, 0);
            _rocket.rotation = Quaternion.Euler(0, 0, -8);
            if (!any) Destroy(_rocket.gameObject);
        }

        bool Add(string id, float y)
        {
            var prefab = Resources.Load<GameObject>("Prefabs/Parts/" + id);
            if (prefab == null) return false;
            var go = Instantiate(prefab, _rocket);
            go.transform.localPosition = new Vector3(0, y, 0);
            foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c);
            foreach (var t in go.GetComponentsInChildren<Transform>()) if (t.name.StartsWith("INT_")) t.gameObject.SetActive(false);
            return true;
        }

        Transform Ball(string name, float diameter, Vector3 pos)
        {
            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = _sphere ??= SmoothSphere(128, 64);
            go.AddComponent<MeshRenderer>();
            go.transform.SetParent(transform, false);
            go.transform.position = pos; go.transform.localScale = Vector3.one * diameter;
            go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        static Mesh _sphere;

        /// <summary>Unit-diameter UV sphere, fine enough that a planet-sized limb stays round (Unity's built-in one facets).</summary>
        static Mesh SmoothSphere(int lon, int lat)
        {
            var v = new System.Collections.Generic.List<Vector3>(); var uv = new System.Collections.Generic.List<Vector2>(); var t = new System.Collections.Generic.List<int>();
            for (int y = 0; y <= lat; y++)
                for (int x = 0; x <= lon; x++)
                {
                    float a = Mathf.PI * y / lat, b = 2 * Mathf.PI * x / lon;
                    v.Add(new Vector3(Mathf.Sin(a) * Mathf.Cos(b), Mathf.Cos(a), Mathf.Sin(a) * Mathf.Sin(b)) * 0.5f);
                    uv.Add(new Vector2(1f - (float)x / lon, 1f - (float)y / lat));
                }
            for (int y = 0; y < lat; y++)
                for (int x = 0; x < lon; x++)
                {
                    int i = y * (lon + 1) + x;
                    t.Add(i); t.Add(i + 1); t.Add(i + lon + 1); t.Add(i + 1); t.Add(i + lon + 2); t.Add(i + lon + 1);
                }
            var m = new Mesh { name = "SmoothSphere" };
            m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(t, 0);
            var n = v.ConvertAll(p => p.normalized); m.SetNormals(n);
            m.RecalculateTangents(); m.RecalculateBounds();
            return m;
        }

        void Update()
        {
            if (_earth != null) _earth.Rotate(Vector3.up, 0.6f * Time.unscaledDeltaTime, Space.Self);
            if (_rocket != null) _rocket.Rotate(Vector3.up, 6f * Time.unscaledDeltaTime, Space.Self);
        }
    }
}
