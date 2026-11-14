using System;
using System.Text;
using Newtonsoft.Json;

namespace TMinus.Core
{
    /// <summary>A crew member's GO / NO-GO, made on their own computer and sent back to the host as a short text code.</summary>
    [Serializable]
    public sealed class CrewVote
    {
        public int v = 1;
        public string role;        // a CrewRole name
        public bool go;
        public string name;        // the person's name, shown to the host
        public string designHash;  // which design this vote is about
        public int openItems;      // how many of their checklist items were still FAIL
    }

    /// <summary>
    /// Remote crew without a server (audit F25). The host shows a pad code (the design code); each teammate pastes it on their own
    /// computer, sees only their role's checklist items, votes, and copies back a vote code. The host pastes the vote codes in and the
    /// GO / NO-GO poll fills itself. It is asynchronous (copy and paste in any chat), not live: a live version needs a realtime server.
    /// </summary>
    public static class CrewCodes
    {
        public const string VotePrefix = "TMV1-";
        const int MaxVoteChars = 4096;

        /// <summary>A short fingerprint of the design a vote is about (FNV-1a over the design's JSON), so a vote for an older build is noticed.</summary>
        public static string DesignHash(DesignBundle bundle)
        {
            string json = JsonConvert.SerializeObject(bundle, Formatting.None);
            unchecked
            {
                ulong h = 14695981039346656037UL;
                foreach (char c in json) { h ^= c; h *= 1099511628211UL; }
                return h.ToString("x16");
            }
        }

        public static string EncodeVote(CrewVote vote) =>
            VotePrefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(vote, Formatting.None)));

        public static bool TryDecodeVote(string code, out CrewVote vote, out string error)
        {
            vote = null; error = null;
            try
            {
                code = (code ?? "").Trim();
                if (!code.StartsWith(VotePrefix) || code.Length > MaxVoteChars) { error = "That is not a crew vote code (it should start with " + VotePrefix + ")."; return false; }
                vote = JsonConvert.DeserializeObject<CrewVote>(Encoding.UTF8.GetString(Convert.FromBase64String(code.Substring(VotePrefix.Length))));
                if (vote == null || !Enum.TryParse(vote.role, out CrewRole _)) { error = "That vote code has no valid role."; vote = null; return false; }
                vote.name = Clip(vote.name, 40);
                return true;
            }
            catch (Exception)
            {
                error = "That vote code is damaged. Copy it again from your teammate.";
                vote = null;
                return false;
            }
        }

        public static bool Matches(CrewVote vote, DesignBundle bundle) => vote != null && vote.designHash == DesignHash(bundle);

        static string Clip(string s, int max) => string.IsNullOrEmpty(s) ? "" : s.Length > max ? s.Substring(0, max) : s;
    }
}
