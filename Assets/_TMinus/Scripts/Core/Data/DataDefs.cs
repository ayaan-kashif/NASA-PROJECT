using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TMinus.Core.Data
{
    // Minimal typed records. Prompt 2 adds the full fields; anything not declared yet
    // is kept in `extra`, so new JSON fields never break loading.

    public abstract class DefBase
    {
        public string id;
        public string name;
        public string source;
        public bool illustrative;
        public bool needsReview;

        [JsonExtensionData] public IDictionary<string, JToken> extra = new Dictionary<string, JToken>();
    }

    public sealed class PartDef : DefBase
    {
        public string category;
        public double massKg;
        public double costK;
    }

    public sealed class RocketDef : DefBase
    {
        public bool real;
    }

    public sealed class MissionDef : DefBase
    {
        public string objective;
        public string priority;
    }

    public sealed class CodexEntry : DefBase
    {
        public string title;
        [JsonProperty("short")] public string shortText;
        [JsonProperty("long")] public string longText;
        public string sourceUrl;
    }

    /// <summary>rules.json is one object; until Prompt 2 types it, it is exposed raw.</summary>
    public sealed class RulesDef
    {
        public JObject raw = new JObject();
        public bool IsEmpty => raw == null || !raw.HasValues;
    }
}
