using System.Collections.Generic;
using NUnit.Framework;
using TMinus.Assembly;

namespace TMinus.Tests
{
    public class AssemblyGraphTests
    {
        static AssemblyRules PathfinderRules(double stage2Diameter = 1.2)
        {
            var order = new List<string>
            {
                "pf_stage1", "pf_interstage", "pf_stage2", "pf_avionics_ring", "pf_payload_adapter",
                "payload", "pf_fairing_left", "pf_fairing_right"
            };
            AssemblyRules.PartInfo P(string name, string cat, double d) =>
                new AssemblyRules.PartInfo { name = name, category = cat, diameterM = d };
            var parts = new Dictionary<string, AssemblyRules.PartInfo>
            {
                ["pf_stage1"] = P("Stage 1", "rocket_stage", 1.2),
                ["pf_interstage"] = P("Interstage", "interstage", 1.2),
                ["pf_stage2"] = P("Stage 2", "rocket_stage", stage2Diameter),
                ["pf_avionics_ring"] = P("Avionics ring", "avionics_ring", 1.2),
                ["pf_payload_adapter"] = P("Payload adapter", "payload_adapter", 1.0),
                ["payload"] = P("CubeSat", "payload", 0.1),
                ["pf_fairing_left"] = P("Fairing L", "fairing", 1.2),
                ["pf_fairing_right"] = P("Fairing R", "fairing", 1.2),
            };
            var joints = new Dictionary<(string, string), string>
            {
                [("pf_stage1", "pf_interstage")] = "j1",
                [("pf_interstage", "pf_stage2")] = "j2",
            };
            return new AssemblyRules(order, parts, joints);
        }

        static AssemblyGraph NewGraph(AssemblyRules rules)
        {
            var g = new AssemblyGraph(rules);
            foreach (var id in rules.Order) g.AddNode(id, id);
            return g;
        }

        static void BuildAll(AssemblyGraph g)
        {
            Assert.IsTrue(g.TryAttach("pf_stage1", AssemblyGraph.StandId, "top", out var r), r);
            Assert.IsTrue(g.TryAttach("pf_interstage", "pf_stage1", "top", out r), r);
            Assert.IsTrue(g.TryAttach("pf_stage2", "pf_interstage", "top", out r), r);
            Assert.IsTrue(g.TryAttach("pf_avionics_ring", "pf_stage2", "top", out r), r);
            Assert.IsTrue(g.TryAttach("pf_payload_adapter", "pf_avionics_ring", "top", out r), r);
            Assert.IsTrue(g.TryAttach("payload", "pf_payload_adapter", "payload", out r), r);
            Assert.IsTrue(g.TryAttach("pf_fairing_left", "pf_payload_adapter", "fairing_left", out r), r);
            Assert.IsTrue(g.TryAttach("pf_fairing_right", "pf_payload_adapter", "fairing_right", out r), r);
        }

        [Test]
        public void FullBuildInOrder_Completes()
        {
            var g = NewGraph(PathfinderRules());
            BuildAll(g);
            Assert.IsTrue(g.IsComplete());
            Assert.AreEqual("j1", g.EdgeBelow("pf_interstage").jointId);
        }

        [Test]
        public void WrongOrder_IsRejectedWithReason()
        {
            var g = NewGraph(PathfinderRules());
            Assert.IsFalse(g.TryAttach("pf_stage2", AssemblyGraph.StandId, "top", out var reason));
            StringAssert.Contains("Wrong order", reason);

            g.TryAttach("pf_stage1", AssemblyGraph.StandId, "top", out _);
            Assert.IsFalse(g.TryAttach("pf_stage2", "pf_stage1", "top", out reason));
            StringAssert.Contains("Interstage", reason);
        }

        [Test]
        public void DiameterMismatch_IsRejected()
        {
            var g = NewGraph(PathfinderRules(stage2Diameter: 3.0));
            g.TryAttach("pf_stage1", AssemblyGraph.StandId, "top", out _);
            g.TryAttach("pf_interstage", "pf_stage1", "top", out _);
            Assert.IsFalse(g.TryAttach("pf_stage2", "pf_interstage", "top", out var reason));
            StringAssert.Contains("Diameter mismatch: 1.2 m vs 3.0 m", reason);
        }

        [Test]
        public void OnePartPerSocket_AndSocketKindsMatter()
        {
            var g = NewGraph(PathfinderRules());
            BuildAll(g);
            g.AddNode("payload_2", "payload");
            Assert.IsFalse(g.TryAttach("payload_2", "pf_payload_adapter", "payload", out var reason));
            StringAssert.Contains("already taken", reason);
        }

        [Test]
        public void Detach_RequiresNothingAbove()
        {
            var g = NewGraph(PathfinderRules());
            BuildAll(g);
            Assert.IsFalse(g.TryDetach("pf_stage2", out var reason));
            StringAssert.Contains("Remove first", reason);
            Assert.IsTrue(g.TryDetach("pf_fairing_left", out reason), reason);
        }

        [Test]
        public void Detach_BlockedWhileBolted()
        {
            var g = NewGraph(PathfinderRules());
            g.TryAttach("pf_stage1", AssemblyGraph.StandId, "top", out _);
            g.TryAttach("pf_interstage", "pf_stage1", "top", out _);
            g.JointBoltsClear = j => j != "j1";
            Assert.IsFalse(g.TryDetach("pf_interstage", out var reason));
            StringAssert.Contains("Unbolt", reason);
        }

        [Test]
        public void UndoRedo_RestoresGraphExactly()
        {
            var g = NewGraph(PathfinderRules());
            g.TryAttach("pf_stage1", AssemblyGraph.StandId, "top", out _);
            g.TryAttach("pf_interstage", "pf_stage1", "top", out _);
            var two = g.Snapshot();
            g.TryAttach("pf_stage2", "pf_interstage", "top", out _);
            var three = g.Snapshot();

            Assert.IsTrue(g.Undo());
            Assert.AreEqual(two, g.Snapshot());
            Assert.IsTrue(g.Redo());
            Assert.AreEqual(three, g.Snapshot());

            g.TryDetach("pf_stage2", out _);
            Assert.AreEqual(two, g.Snapshot());
            g.Undo();
            Assert.AreEqual(three, g.Snapshot());
            Assert.IsFalse(g.CanRedo == false && g.CanUndo == false);
        }
    }
}
