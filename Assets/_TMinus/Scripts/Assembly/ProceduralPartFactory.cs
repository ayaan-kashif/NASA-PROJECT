using System.Collections.Generic;
using TMinus.Core;
using TMinus.Core.Data;
using UnityEngine;
using M = TMinus.Core.MaterialLibrary;

namespace TMinus.Assembly
{
    /// <summary>
    /// Builds a placeholder for any part id: cylinders, cones, bells and shells with the same
    /// socket names a Blender model would have. Prompt 16 makes it prefer a real prefab.
    /// </summary>
    public static class ProceduralPartFactory
    {
        /// <summary>Optional hook (Prompt 16): return a prefab instance for a part id, or null.</summary>
        public static System.Func<string, Transform, GameObject> PrefabProvider;

        public const string PayloadCubeSat = "payload_cubesat";

        public static GameObject Create(string partId, DataRegistry data, Transform parent = null)
        {
            var fromPrefab = PrefabProvider?.Invoke(partId, parent);
            if (fromPrefab != null) return fromPrefab;

            var def = data?.GetPart(partId);
            var go = new GameObject(partId);
            if (parent != null) go.transform.SetParent(parent, false);
            var part = go.AddComponent<Part>();
            part.partId = partId == PayloadCubeSat ? "frame_3u" : partId;
            part.Def = def;

            if (partId == PayloadCubeSat) BuildCubeSat(part);
            else if (def == null) BuildBox(part, 0.5f, M.MetalGrey);
            else
            {
                switch (def.category)
                {
                    case "rocket_stage":
                    case "saturn_stage":
                        if (partId == "saturnv_spacecraft") BuildApollo(part);
                        else if (partId == "saturnv_iu") BuildRing(part, def, M.MetalGrey);
                        else BuildStage(part, def, FindStage(data, partId));
                        break;
                    case "interstage": BuildInterstage(part, def); break;
                    case "avionics_ring": BuildRing(part, def, M.BandBlack); break;
                    case "payload_adapter": BuildAdapter(part, def); break;
                    case "fairing": BuildFairingHalf(part, def, partId.EndsWith("left")); break;
                    default: BuildBox(part, 0.08f, M.BoardGreen); break;
                }
            }

            AddBoltHoles(part, data);
            part.RegisterSockets();
            part.SetInternalsVisible(false);
            return go;
        }

        static StageDef FindStage(DataRegistry data, string partId)
        {
            if (data == null) return null;
            foreach (var r in data.Rockets)
                foreach (var s in r.stages)
                    if (s.partId == partId) return s;
            return null;
        }

        // ------------------------------------------------------------------ shapes

        static void BuildStage(Part part, PartDef def, StageDef stage)
        {
            float d = (float)(stage?.diameterM ?? def.diameterM), L = (float)(stage?.lengthM ?? def.lengthM);
            float r = d / 2;
            part.diameterM = d; part.stackLength = L; part.isShell = true;

            if (part.partId == "saturnv_sivb")
            {
                // Tapered aft section sits on the 10 m S-II.
                const float taper = 5f;
                AddMesh(part, "Shell_Taper", MeshBuilder.Frustum(5f, r, taper, 48, false), M.BodyWhite, 0);
                AddMesh(part, "Shell", MeshBuilder.Cylinder(r, L - taper, 48), M.BodyWhite, taper);
            }
            else AddMesh(part, "Shell", MeshBuilder.Cylinder(r, L, 48), M.BodyWhite, 0);

            float band = Mathf.Max(0.1f, L * 0.04f);
            AddMesh(part, "Band_Top", MeshBuilder.Cylinder(r * 1.005f, band, 48, false), M.BandBlack, L - band);
            AddMesh(part, "Band_Bottom", MeshBuilder.Cylinder(r * 1.005f, band, 48, false), M.BandBlack, 0);

            // Tanks, only visible in X-ray: fuel below, oxidiser above.
            AddMesh(part, "INT_Tank_Fuel", MeshBuilder.Cylinder(r * 0.88f, L * 0.36f, 32), M.MetalGrey, L * 0.06f);
            AddMesh(part, "INT_Tank_Oxidiser", MeshBuilder.Cylinder(r * 0.88f, L * 0.46f, 32), M.InsulationOrange, L * 0.47f);

            int n = Mathf.Max(1, stage?.engineCount ?? 1);
            float exit = n == 1 ? r * 0.42f : n <= 5 ? r * 0.3f : r * 0.2f;
            float bellLen = exit * 2.4f;
            foreach (var pos in EnginePositions(n, r))
            {
                var bell = AddMesh(part, "Engine", MeshBuilder.EngineBell(exit, bellLen), M.EngineDark, -bellLen);
                bell.transform.localPosition = new Vector3(pos.x, -bellLen, pos.y);
            }
            part.bellDepth = bellLen;

            AddSnap(part, "SNAP_Bottom", 0);
            AddSnap(part, "SNAP_Top", L);
        }

        static IEnumerable<Vector2> EnginePositions(int n, float r)
        {
            if (n == 1) { yield return Vector2.zero; yield break; }
            bool center = n == 5 || n == 9;
            int ring = center ? n - 1 : n;
            float rr = r * (n <= 5 ? 0.55f : 0.65f);
            for (int i = 0; i < ring; i++)
            {
                float a = Mathf.PI * 2 * i / ring + Mathf.PI / 4;
                yield return new Vector2(Mathf.Cos(a) * rr, Mathf.Sin(a) * rr);
            }
            if (center) yield return Vector2.zero;
        }

        static void BuildInterstage(Part part, PartDef def)
        {
            float r = (float)def.diameterM / 2, L = (float)def.lengthM;
            part.diameterM = (float)def.diameterM; part.stackLength = L; part.isShell = true;
            AddMesh(part, "Shell", MeshBuilder.Lathe(new[] { new Vector2(r, 0), new Vector2(r, L) }, 48, doubleSided: true), M.BandBlack, 0);
            AddSnap(part, "SNAP_Bottom", 0);
            AddSnap(part, "SNAP_Top", L);
        }

        static void BuildRing(Part part, PartDef def, string mat)
        {
            float r = (float)def.diameterM / 2, L = (float)def.lengthM;
            part.diameterM = (float)def.diameterM; part.stackLength = L;
            AddMesh(part, "Ring", MeshBuilder.Cylinder(r * 1.01f, L, 48), mat, 0);
            AddMesh(part, "Rib", MeshBuilder.Torus(r * 1.01f, L * 0.12f), M.MetalGrey, L * 0.38f);
            for (int i = 0; i < 6; i++) // avionics boxes, visible in X-ray
            {
                float a = Mathf.PI * 2 * i / 6;
                var box = AddBox(part, "INT_Avionics_" + i, new Vector3(r * 0.25f, L * 0.6f, r * 0.18f), M.BoardGreen);
                box.transform.localPosition = new Vector3(Mathf.Cos(a) * r * 0.7f, L * 0.5f, Mathf.Sin(a) * r * 0.7f);
            }
            AddSnap(part, "SNAP_Bottom", 0);
            AddSnap(part, "SNAP_Top", L);
        }

        static void BuildAdapter(Part part, PartDef def)
        {
            float rb = 0.6f, L = (float)def.lengthM;
            part.diameterM = (float)def.diameterM; part.stackLength = L;
            AddMesh(part, "Cone", MeshBuilder.Frustum(rb, rb * 0.45f, L, 40), M.MetalGrey, 0);
            var disp = AddBox(part, "Dispenser", new Vector3(0.14f, 0.38f, 0.14f), M.MetalGrey);
            disp.transform.localPosition = new Vector3(0, L + 0.19f, 0);
            AddSnap(part, "SNAP_Bottom", 0);
            AddSnap(part, "SNAP_Top", L);
            AddSnap(part, "SNAP_Payload", L + 0.02f);
        }

        static void BuildFairingHalf(Part part, PartDef def, bool left)
        {
            float r = (float)def.diameterM / 2, L = (float)def.lengthM;
            part.diameterM = (float)def.diameterM; part.stackLength = 0; part.isShell = true;
            AddMesh(part, "Shell", MeshBuilder.Ogive(r, L * 0.45f, L * 0.55f, 40, 180, left ? 90 : -90), M.BodyWhite, 0);
            AddSnap(part, "SNAP_Bottom", 0);
        }

        static void BuildApollo(Part part)
        {
            part.diameterM = 6.6f; part.stackLength = 25f;
            AddMesh(part, "SLA", MeshBuilder.Frustum(3.3f, 1.95f, 8.5f, 48), M.BodyWhite, 0);
            AddMesh(part, "ServiceModule", MeshBuilder.Cylinder(1.95f, 7.5f, 48), M.MetalGrey, 8.5f);
            AddMesh(part, "CommandModule", MeshBuilder.Frustum(1.95f, 0.4f, 3.5f, 48), M.MetalGrey, 16f);
            AddMesh(part, "EscapeTower", MeshBuilder.Cylinder(0.3f, 5.5f, 16), M.GantryRed, 19.5f);
            AddMesh(part, "INT_LunarModule", MeshBuilder.Cylinder(2.1f, 6.5f, 8), M.InsulationOrange, 1f);
            AddSnap(part, "SNAP_Bottom", 0);
        }

        static void BuildCubeSat(Part part)
        {
            part.diameterM = 0.14f; part.stackLength = 0.34f;
            var body = AddBox(part, "Frame", new Vector3(0.1f, 0.34f, 0.1f), M.MetalGrey);
            body.transform.localPosition = new Vector3(0, 0.17f, 0);
            for (int i = 0; i < 4; i++)
            {
                var panel = AddBox(part, "SolarPanel_" + i, new Vector3(0.002f, 0.3f, 0.09f), M.BoardGreen);
                float a = Mathf.PI / 2 * i;
                panel.transform.localPosition = new Vector3(Mathf.Cos(a) * 0.051f, 0.17f, Mathf.Sin(a) * 0.051f);
                panel.transform.localRotation = Quaternion.Euler(0, -a * Mathf.Rad2Deg, 0);
            }
            AddSnap(part, "SNAP_Bottom", 0);
        }

        static void BuildBox(Part part, float size, string mat)
        {
            part.stackLength = size; part.diameterM = size;
            var box = AddBox(part, "Body", Vector3.one * size, mat);
            box.transform.localPosition = new Vector3(0, size / 2, 0);
        }

        /// <summary>BOLT_01..N on the bottom flange of the upper part of each joint in rules.json.</summary>
        static void AddBoltHoles(Part part, DataRegistry data)
        {
            var rules = data?.GetRules();
            if (rules == null) return;
            foreach (var j in rules.boltJoints)
            {
                if (j.upperPartId != part.partId) continue;
                var root = new GameObject("Joint_" + j.id).transform;
                root.SetParent(part.transform, false);
                bool half = part.partId.Contains("fairing");
                float start = part.partId.EndsWith("left") ? 90 : -90;
                // Bolts sit on an external flange ring so the player can reach them.
                float r = Mathf.Max(0.05f, part.diameterM / 2 * 1.06f);
                var flangeMesh = half
                    ? MeshBuilder.Lathe(new[] { new Vector2(part.diameterM / 2, 0), new Vector2(r * 1.04f, 0), new Vector2(r * 1.04f, 0.03f), new Vector2(part.diameterM / 2, 0.03f) }, 24, 180, start, doubleSided: true, name: "flange")
                    : MeshBuilder.Lathe(new[] { new Vector2(part.diameterM / 2, 0), new Vector2(r * 1.04f, 0), new Vector2(r * 1.04f, 0.03f), new Vector2(part.diameterM / 2, 0.03f) }, 48, doubleSided: true, name: "flange");
                var flange = new GameObject("Flange");
                flange.transform.SetParent(root, false);
                flange.AddComponent<MeshFilter>().sharedMesh = flangeMesh;
                flange.AddComponent<MeshRenderer>().sharedMaterial = M.Get(M.MetalGrey);
                for (int i = 0; i < j.boltCount; i++)
                {
                    float deg = half ? start + 180f * (i + 0.5f) / j.boltCount : 360f * i / j.boltCount;
                    float a = deg * Mathf.Deg2Rad;
                    var b = new GameObject($"BOLT_{i + 1:00}").transform;
                    b.SetParent(root, false);
                    b.localPosition = new Vector3(Mathf.Cos(a) * r, 0.03f, Mathf.Sin(a) * r);
                    b.localRotation = Quaternion.identity; // bolt axis = local up
                }
            }
        }

        // ------------------------------------------------------------------ helpers

        static GameObject AddMesh(Part part, string name, Mesh mesh, string mat, float y)
        {
            var go = new GameObject(name);
            go.transform.SetParent(part.transform, false);
            go.transform.localPosition = new Vector3(0, y, 0);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = M.Get(mat);
            if (!name.StartsWith("INT_")) go.AddComponent<MeshCollider>().sharedMesh = mesh;
            return go;
        }

        static GameObject AddBox(Part part, string name, Vector3 size, string mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(part.transform, false);
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = M.Get(mat);
            if (name.StartsWith("INT_")) Object.Destroy(go.GetComponent<Collider>());
            return go;
        }

        static void AddSnap(Part part, string name, float y)
        {
            var t = new GameObject(name).transform;
            t.SetParent(part.transform, false);
            t.localPosition = new Vector3(0, y, 0);
            t.localRotation = Quaternion.LookRotation(name == "SNAP_Bottom" ? Vector3.down : Vector3.up);
        }
    }
}
