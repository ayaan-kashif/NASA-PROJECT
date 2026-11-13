using System.IO;
using NUnit.Framework;
using TMinus.Assembly;
using TMinus.Core;
using TMinus.Core.Data;
using TMinus.EditorTools;
using UnityEditor;
using UnityEngine;

namespace TMinus.Tests
{
    [TestFixture]
    public sealed class ImportTests
    {
        DataRegistry _data;
        RulesDef _rules;

        [SetUp]
        public void Setup()
        {
            _data = DataRegistry.LoadFromResources();
            _rules = _data.GetRules();
        }

        [Test]
        public void PrefabPartProviderHookSetsSuccessfully()
        {
            PrefabPartProvider.Initialize();
            Assert.That(ProceduralPartFactory.PrefabProvider, Is.Not.Null, "PrefabProvider hook should be set.");
        }

        [Test]
        public void ModelValidatorIdentifiesMissingParts()
        {
            var partDef = _data.GetPart("pf_interstage");
            Assert.That(partDef, Is.Not.Null);

            var result = ModelValidatorWindow.ValidatePart(partDef, _rules);
            // In a fresh setup without Blender FBX yet, it notes that procedural is used
            Assert.That(result.partId, Is.EqualTo("pf_interstage"));
            Assert.That(result.expectedBolts, Is.EqualTo(12));
        }

        [Test]
        public void PartRegistersSocketsOnAwake()
        {
            var go = new GameObject("TestPart");
            var part = go.AddComponent<Part>();
            part.partId = "test_part";

            var snapTop = new GameObject("SNAP_Top");
            snapTop.transform.SetParent(go.transform);

            var snapBottom = new GameObject("SNAP_Bottom");
            snapBottom.transform.SetParent(go.transform);

            var bolt1 = new GameObject("BOLT_01");
            bolt1.transform.SetParent(go.transform);

            var port1 = new GameObject("PORT_PWR_IN");
            port1.transform.SetParent(go.transform);

            part.RegisterSockets();

            Assert.That(part.SnapNodes.Count, Is.EqualTo(2));
            Assert.That(part.BoltHoles.Count, Is.EqualTo(1));
            Assert.That(part.Ports.Count, Is.EqualTo(1));

            Object.DestroyImmediate(go);
        }

        [Test]
        public void PrefabGeneratedCorrectlyWithComponents()
        {
            string testDir = TMinus.EditorTools.ImportPostprocessor.PrefabDir;
            if (!Directory.Exists(testDir)) Directory.CreateDirectory(testDir);

            string testPrefabPath = $"{testDir}/test_part_mock.prefab";
            var root = new GameObject("test_part_mock");
            var part = root.AddComponent<Part>();
            part.partId = "test_part_mock";

            var snap = new GameObject("SNAP_Top");
            snap.transform.SetParent(root.transform);

            var bolt = new GameObject("BOLT_01");
            bolt.transform.SetParent(root.transform);

            part.RegisterSockets();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, testPrefabPath);
            Object.DestroyImmediate(root);

            Assert.That(prefab, Is.Not.Null);
            var loadedPart = prefab.GetComponent<Part>();
            Assert.That(loadedPart, Is.Not.Null);
            Assert.That(loadedPart.partId, Is.EqualTo("test_part_mock"));

            // Clean up test prefab
            AssetDatabase.DeleteAsset(testPrefabPath);
        }

        [Test]
        public void FactoryUsesModelPrefab_WithDataDimensions()
        {
            string dir = TMinus.EditorTools.ImportPostprocessor.PrefabDir;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            string path = $"{dir}/pf_interstage.prefab";
            bool existed = File.Exists(path);
            Assume.That(existed, Is.False, "A real pf_interstage model exists; skip so it isn't overwritten.");
            var root = new GameObject("pf_interstage");
            var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.transform.SetParent(root.transform);
            body.transform.localPosition = new Vector3(0, 0.5f, 0);
            new GameObject("SNAP_Bottom").transform.SetParent(root.transform);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            try
            {
                PrefabPartProvider.Initialize();
                var go = ProceduralPartFactory.Create("pf_interstage", TMinus.Core.DataRegistry.LoadFromResources());
                var part = go.GetComponent<Part>();
                Assert.That(part.transform.Find("Cylinder"), Is.Not.Null, "the model prefab should be used, not the procedural shape");
                Assert.That(part.stackLength, Is.EqualTo(1.0f).Within(1e-4), "stack length comes from parts.json");
                Assert.That(part.diameterM, Is.EqualTo(1.2f).Within(1e-4));
                Assert.That(part.SnapNodes.Count, Is.EqualTo(1));
                Object.DestroyImmediate(go);
            }
            finally { AssetDatabase.DeleteAsset(path); }
        }
    }
}
