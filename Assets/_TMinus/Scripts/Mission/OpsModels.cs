using System;
using System.Collections.Generic;
using TMinus.Core;

namespace TMinus.Mission
{
    [Serializable]
    public sealed class OpsPoint { public int day; public double stateOfCharge, storedMB, downlinkedMB; public int imagesTaken, imagesDownlinked; }
    [Serializable]
    public sealed class OpsReport
    {
        public bool powered, survived;
        public int daysAlive, imagesTaken, imagesDownlinked;
        public double storedMB, downlinkedMB, capacityWh, finalSoc;
        public double rewardGsdM, rewardImageShare, rewardImageQuality;
        public string powerFailureCause;
        public readonly List<OpsPoint> timeline = new List<OpsPoint>();
        public readonly List<string> failures = new List<string>();
        public string rewardCredit, rewardTitle, rewardPlaceholder;
    }
    [Serializable]
    public sealed class ScoreLine { public string id, label; public double points, maximum; }
    [Serializable]
    public sealed class MissionScore { public double total; public int stars; public readonly List<ScoreLine> breakdown = new List<ScoreLine>(); public readonly List<string> fixes = new List<string>(); }
}
