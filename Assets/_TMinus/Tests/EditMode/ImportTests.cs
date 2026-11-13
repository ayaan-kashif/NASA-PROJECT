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
            string testDir = "Assets/_TMinus/Prefabs/Parts";
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
    }
}
