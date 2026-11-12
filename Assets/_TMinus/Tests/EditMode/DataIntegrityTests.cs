using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMinus.Core;
using TMinus.Core.Data;
using UnityEngine;

namespace TMinus.Tests
{
    /// <summary>Checks the real files in Assets/_TMinus/Resources/Data.</summary>
    public class DataIntegrityTests
    {
        static DataRegistry _r;

        static DataRegistry R
        {
            get
            {
                if (_r != null) return _r;
                var dir = Path.Combine(Application.dataPath, "_TMinus/Resources/Data");
                var files = Directory.GetFiles(dir, "*.json")
                    .ToDictionary(Path.GetFileNameWithoutExtension, File.ReadAllText, StringComparer.OrdinalIgnoreCase);
                _r = DataRegistry.LoadFromText(files);
                return _r;
            }
        }

        [Test]
        public void AllFilesLoadWithoutErrors()
        {
            Assert.IsEmpty(R.Errors, string.Join("\n", R.Errors));
            Assert.Greater(R.Parts.Count, 30);
            Assert.AreEqual(2, R.Rockets.Count);
            Assert.GreaterOrEqual(R.Missions.Count, 5);
            Assert.IsFalse(R.GetRules().IsEmpty);
            Assert.IsNotNull(R.GetMission("floodwatch_1"));
        }

        [Test]
        public void ReferencedIdsExist()
        {
            var missing = new List<string>();
            void Need(bool ok, string what) { if (!ok) missing.Add(what); }

            foreach (var rocket in R.Rockets)
            {
                foreach (var p in rocket.partIds) Need(R.GetPart(p) != null, $"rocket {rocket.id} part {p}");
                foreach (var s in rocket.stages)
                    if (!string.IsNullOrEmpty(s.partId)) Need(R.GetPart(s.partId) != null, $"stage {s.id} part {s.partId}");
                foreach (var c in rocket.components)
                {
                    if (!string.IsNullOrEmpty(c.partId)) Need(R.GetPart(c.partId) != null, $"component {c.id} part {c.partId}");
                    if (!string.IsNullOrEmpty(c.dropWithStage)) Need(rocket.GetStage(c.dropWithStage) != null, $"component {c.id} stage {c.dropWithStage}");
                }
            }

            var rules = R.GetRules();
            foreach (var m in R.Missions)
            {
                foreach (var rk in m.rocketOptions) Need(R.GetRocket(rk) != null, $"mission {m.id} rocket {rk}");
                foreach (var site in m.launchSites) Need(rules.GetLaunchSite(site) != null, $"mission {m.id} site {site}");
                foreach (var c in m.codexIds) Need(R.GetCodex(c) != null, $"mission {m.id} codex {c}");
                foreach (var e in m.randomEvents)
                    if (!string.IsNullOrEmpty(e.codexId)) Need(R.GetCodex(e.codexId) != null, $"event {e.id} codex {e.codexId}");
            }

            foreach (var p in R.Parts)
            {
                if (!string.IsNullOrEmpty(p.codexId)) Need(R.GetCodex(p.codexId) != null, $"part {p.id} codex {p.codexId}");
                foreach (var req in p.requires) Need(R.GetPart(req) != null, $"part {p.id} requires {req}");
            }

            var categories = new HashSet<string>(R.Parts.Select(p => p.category));
            foreach (var kv in rules.assemblyOrder)
                foreach (var step in kv.Value)
                    Need(step == "payload" || R.GetPart(step) != null || categories.Contains(step), $"assemblyOrder {kv.Key} step {step}");

            foreach (var j in rules.boltJoints)
            {
                Need(R.GetPart(j.lowerPartId) != null, $"joint {j.id} lower {j.lowerPartId}");
                Need(R.GetPart(j.upperPartId) != null, $"joint {j.id} upper {j.upperPartId}");
                Need(j.boltCount > 0 && j.torqueNm > 0, $"joint {j.id} bolt data");
            }
            foreach (var ch in rules.pyroChannels) Need(R.GetPart(ch.targetPartId) != null, $"pyro {ch.channel} target");
            Need(R.GetPart(rules.safeArm.partId) != null, "safeArm part");
            foreach (var inline in rules.inlineRules) Need(R.GetPart(inline.partId) != null, $"inline {inline.partId}");

            foreach (var cable in rules.cableTypes)
                if (!string.IsNullOrEmpty(cable.codexId)) Need(R.GetCodex(cable.codexId) != null, $"cable {cable.id} codex");
            foreach (var rc in rules.requiredConnections)
                Need(R.GetCodex(rc.codexId) != null, $"required {rc.category} codex {rc.codexId}");
            foreach (var j in rules.boltJoints) Need(R.GetCodex(j.codexId) != null, $"joint {j.id} codex");

            Assert.IsEmpty(missing, "Missing references:\n" + string.Join("\n", missing));
        }

        [Test]
        public void DevicesHaveTheirRequiredPorts()
        {
            var rules = R.GetRules();
            var missing = new List<string>();

            foreach (var rc in rules.requiredConnections)
                foreach (var part in R.Parts.Where(p => p.category == rc.category))
                    foreach (var type in rc.portTypes)
                        if (!part.HasPortType(type)) missing.Add($"{part.id} lacks {type} (required for {rc.category})");

            foreach (var link in rules.avionicsWiring)
            {
                CheckEnd(link.from, link, missing, out var fromType);
                CheckEnd(link.to, link, missing, out var toType);
                if (fromType != null && toType != null)
                {
                    var cable = rules.CableFor(fromType, toType);
                    if (cable == null || cable.id != link.cableType)
                        missing.Add($"{link.id}: no {link.cableType} cable joins {fromType} -> {toType}");
                }
            }

            var dep = R.GetPart("dep_switch");
            Assert.AreEqual(2, dep.ports.Count(p => p.type == "BATT"), "dep_switch needs two BATT ports to sit inline");
            Assert.IsEmpty(missing, string.Join("\n", missing));
        }

        static void CheckEnd(string address, AvionicsLinkDef link, List<string> missing, out string portType)
        {
            portType = null;
            var parts = address.Split('/');
            var part = parts.Length == 2 ? R.GetPart(parts[0]) : null;
            var port = part?.GetPort(parts[1]);
            if (port == null) missing.Add($"{link.id}: {address} not found");
            else portType = port.type;
        }

        [Test]
        public void SaturnV_LiftoffMassInRealRange()
        {
            var sv = R.GetRocket("saturn_v");
            double m = sv.LiftoffMassKg(sv.referencePayloadKg);
            Assert.That(m, Is.InRange(sv.liftoffMassMinKg, sv.liftoffMassMaxKg), $"liftoff mass {m:N0} kg");
            Assert.AreEqual(111, sv.heightM);
        }

        [Test]
        public void SaturnV_VacuumThrustMatchesIspRatio()
        {
            // Same mass flow at sea level and vacuum: F_vac / F_sl = Isp_vac / Isp_sl.
            var s1 = R.GetRocket("saturn_v").GetStage("s_ic");
            Assert.AreEqual(s1.thrustSlKN * s1.ispVac / s1.ispSl, s1.thrustVacKN, s1.thrustVacKN * 0.001);
        }

        [Test]
        public void Pathfinder_MatchesBrief()
        {
            var pf = R.GetRocket("pathfinder");
            var s1 = pf.GetStage("pf_s1");
            Assert.AreEqual(9, s1.engineCount);
            Assert.AreEqual(9500, s1.propMassKg);
            Assert.AreEqual(2, pf.stages.Count);
            Assert.AreEqual(150, pf.components.Sum(c => c.massKg), 1e-6); // 60 + 25 + 15 + 50
            Assert.IsTrue(pf.illustrative);
        }

        [Test]
        public void CodexEntriesRespectLimits()
        {
            Assert.LessOrEqual(R.Codex.Count, 30);
            foreach (var c in R.Codex)
            {
                Assert.LessOrEqual(Words(c.shortText), 40, $"{c.id} short is too long");
                Assert.LessOrEqual(Words(c.longText), 120, $"{c.id} long is too long");
                Assert.IsFalse(string.IsNullOrEmpty(c.title), $"{c.id} title");
                Assert.IsFalse(string.IsNullOrEmpty(c.sourceUrl), $"{c.id} sourceUrl");
            }
        }

        static int Words(string s) =>
            string.IsNullOrWhiteSpace(s) ? 0 : s.Split((char[])null, StringSplitOptions.RemoveEmptyEntries).Length;
    }
}
