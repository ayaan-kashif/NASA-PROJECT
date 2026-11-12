using System;
using System.Collections.Generic;
using TMinus.Core.Data;

namespace TMinus.Core
{
    public enum BoltState { Missing, Inserted, Torqued, UnderTorqued, OverTorqued }

    public struct JointStatus : IJointStatus
    {
        public string jointId;
        public double integrity;      // 0..1
        public int boltCount, missing, inserted, torqued, under, over, sequenceErrors;
        public double maxQkPa;
        /// <summary>Dynamic pressure this joint survives: maxQkPa x integrity.</summary>
        public double FailureQkPa => maxQkPa * integrity;
        string IJointStatus.JointId => jointId;
        double IJointStatus.FailureQkPa => FailureQkPa;
        public override string ToString() =>
            $"{jointId}: integrity {integrity:0.00}, {torqued}/{boltCount} torqued, {missing} missing, {under} under, {over} over, {sequenceErrors} sequence errors";
    }

    /// <summary>Star-pattern order, torque passes and joint integrity. Pure maths shared by VAB, HOUSTON and flight.</summary>
    public static class BoltMath
    {
        public static readonly double[] PassFractions = { 0.3, 0.7, 1.0 };
        /// <summary>A bolt is "loose" (for warping) below this share of target torque.</summary>
        public const double LooseFraction = 0.3;
        /// <summary>A bolt counts as fully torqued (for warping) at this share of target.</summary>
        public const double FullFraction = 0.95;
        public const double OverPenalty = 0.15;
        /// <summary>Warping costs up to 35% (3 or more out-of-sequence pulls), so a clockwise job lands near 65% and fails at max-Q.</summary>
        public const double MaxWarpPenalty = 0.35;

        public static double WarpPenalty(int sequenceErrors) => MaxWarpPenalty * Math.Min(1.0, sequenceErrors / 3.0);

        /// <summary>0-based tightening order. N divisible by 4: k, k+N/2, k+N/4, k+3N/4. Else alternate opposite holes.</summary>
        public static int[] StarOrder(int n)
        {
            var order = new List<int>(n);
            if (n <= 0) return order.ToArray();
            if (n % 4 == 0)
            {
                for (int k = 0; k < n / 4; k++)
                {
                    order.Add(k); order.Add(k + n / 2); order.Add(k + n / 4); order.Add(k + 3 * n / 4);
                }
            }
            else
            {
                int half = (n + 1) / 2;
                for (int i = 0; i < half; i++)
                {
                    order.Add(i);
                    if (i + half < n) order.Add(i + half);
                }
            }
            return order.ToArray();
        }

        public static string StarOrderText(int n)
        {
            var o = StarOrder(n);
            var parts = new string[o.Length];
            for (int i = 0; i < o.Length; i++) parts[i] = (o[i] + 1).ToString();
            return string.Join("-", parts);
        }
    }

    /// <summary>Live state of one bolted joint. Saved into VehicleDesign.bolts / jointWarps.</summary>
    public sealed class BoltJointState
    {
        public readonly BoltJointDef def;
        public readonly bool[] present;
        public readonly double[] torque;
        public int sequenceErrors;
        int _sequence;

        public BoltJointState(BoltJointDef def)
        {
            this.def = def;
            present = new bool[def.boltCount];
            torque = new double[def.boltCount];
        }

        public int Count => def.boltCount;
        public double Target => def.torqueNm;
        double Tol => def.tolerancePct / 100.0;

        public BoltState StateOf(int i)
        {
            if (!present[i]) return BoltState.Missing;
            double t = torque[i];
            if (t > Target * (1 + Tol)) return BoltState.OverTorqued;
            if (t >= Target * (1 - Tol)) return BoltState.Torqued;
            if (t <= 0) return BoltState.Inserted;
            return BoltState.UnderTorqued;
        }

        public void Insert(int i)
        {
            if (present[i]) return;
            present[i] = true;
            torque[i] = 0;
        }

        public void Remove(int i)
        {
            present[i] = false;
            torque[i] = 0;
            if (AllMissing()) sequenceErrors = 0; // a fully stripped joint starts fresh
        }

        bool AllMissing()
        {
            foreach (var p in present) if (p) return false;
            return true;
        }

        /// <summary>
        /// Tighten bolt i to `nm` (a wrench never loosens). Returns the neighbours that were still loose
        /// if this pull fully torqued the bolt too early (the joint warps), else an empty list.
        /// </summary>
        public List<int> ApplyTorque(int i, double nm)
        {
            var looseNeighbours = new List<int>();
            if (!present[i] || nm <= torque[i]) return looseNeighbours;
            bool wasFull = torque[i] >= Target * BoltMath.FullFraction;
            torque[i] = nm;
            _sequence++;

            if (!wasFull && nm >= Target * BoltMath.FullFraction)
            {
                foreach (var n in Neighbours(i))
                    if (!present[n] || torque[n] < Target * BoltMath.LooseFraction) looseNeighbours.Add(n);
                if (looseNeighbours.Count > 0) sequenceErrors++;
            }
            return looseNeighbours;
        }

        IEnumerable<int> Neighbours(int i)
        {
            if (Count < 3) yield break;
            yield return (i + 1) % Count;
            yield return (i - 1 + Count) % Count;
        }

        /// <summary>1, 2 or 3 = the pass in progress; 4 = all passes done.</summary>
        public int CurrentPass()
        {
            for (int p = 0; p < BoltMath.PassFractions.Length; p++)
            {
                double need = Target * BoltMath.PassFractions[p] * (1 - Tol);
                for (int i = 0; i < Count; i++)
                    if (!present[i] || torque[i] < need) return p + 1;
            }
            return 4;
        }

        public double PassTargetNm(int pass) => Target * BoltMath.PassFractions[Math.Min(Math.Max(pass, 1), 3) - 1];

        /// <summary>Next bolt (0-based) in star order that needs work this pass, or -1 when finished.</summary>
        public int NextBolt()
        {
            int pass = CurrentPass();
            if (pass > 3) return -1;
            double need = PassTargetNm(pass) * (1 - Tol);
            foreach (var i in BoltMath.StarOrder(Count))
                if (!present[i] || torque[i] < need) return i;
            return -1;
        }

        public JointStatus GetStatus()
        {
            var s = new JointStatus { jointId = def.id, boltCount = Count, maxQkPa = def.maxQkPa, sequenceErrors = sequenceErrors };
            for (int i = 0; i < Count; i++)
            {
                switch (StateOf(i))
                {
                    case BoltState.Missing: s.missing++; break;
                    case BoltState.Inserted: s.inserted++; break;
                    case BoltState.Torqued: s.torqued++; break;
                    case BoltState.UnderTorqued: s.under++; break;
                    case BoltState.OverTorqued: s.over++; break;
                }
            }
            double share = Count > 0 ? (double)s.torqued / Count : 0;
            s.integrity = Math.Max(0, Math.Min(1, share - BoltMath.WarpPenalty(sequenceErrors) - BoltMath.OverPenalty * s.over));
            return s;
        }

        public bool HasAnyBolt()
        {
            foreach (var p in present) if (p) return true;
            return false;
        }

        // ------------------------------------------------------------------ save / load

        public void SaveTo(VehicleDesign design)
        {
            design.bolts.RemoveAll(b => b.jointId == def.id);
            for (int i = 0; i < Count; i++)
                if (present[i]) design.bolts.Add(new BoltRecord { jointId = def.id, boltIndex = i, torqueNm = torque[i], sequence = _sequence });
            design.jointWarps.RemoveAll(w => w.jointId == def.id);
            if (sequenceErrors > 0) design.jointWarps.Add(new JointWarp { jointId = def.id, count = sequenceErrors });
        }

        public static BoltJointState LoadFrom(BoltJointDef def, VehicleDesign design)
        {
            var s = new BoltJointState(def);
            if (design == null) return s;
            foreach (var b in design.bolts)
            {
                if (b.jointId != def.id || b.boltIndex < 0 || b.boltIndex >= s.Count) continue;
                s.present[b.boltIndex] = true;
                s.torque[b.boltIndex] = b.torqueNm;
                s._sequence = Math.Max(s._sequence, b.sequence);
            }
            foreach (var w in design.jointWarps) if (w.jointId == def.id) s.sequenceErrors = w.count;
            return s;
        }

        /// <summary>Status of every joint in rules.json for a saved vehicle (used by flight and the checklist).</summary>
        public static List<JointStatus> AllStatuses(RulesDef rules, VehicleDesign design)
        {
            var list = new List<JointStatus>();
            foreach (var j in rules.boltJoints) list.Add(LoadFrom(j, design).GetStatus());
            return list;
        }
    }
}
