using System;
using System.Collections.Generic;
using System.Linq;
using TMinus.Core;

namespace TMinus.Houston
{
    /// <summary>
    /// Finds the Space Guide (codex) entries a free-text question is about. Scores whole-word hits on each
    /// entry's id and title, so "what does the EPS do?" finds "Electrical power system (EPS)".
    /// </summary>
    public static class CodexMatcher
    {
        static readonly HashSet<string> Stop = new HashSet<string>
        { "the", "and", "for", "what", "why", "how", "does", "did", "are", "was", "with", "this", "that", "from", "have", "need", "into", "about", "your", "you", "can", "its", "use", "used" };

        static List<string> Words(string s) =>
            new string((s ?? "").ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : ' ').ToArray())
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Where(w => w.Length >= 2 && !Stop.Contains(w)).ToList();

        public static List<string> Match(string question, DataRegistry data, int max)
        {
            var result = new List<string>();
            if (data == null || string.IsNullOrWhiteSpace(question)) return result;
            var q = new HashSet<string>(Words(question));
            // Simple plural folding: "bolts" also matches "bolt".
            foreach (var w in q.ToList()) if (w.Length > 3 && w.EndsWith("s")) q.Add(w.Substring(0, w.Length - 1));
            var scored = new List<(string id, int score)>();
            foreach (var e in data.Codex)
            {
                var keys = new HashSet<string>(Words(e.title).Concat(Words((e.id ?? "").Replace('_', ' '))));
                int score = keys.Count(k => q.Contains(k) || (k.Length > 3 && k.EndsWith("s") && q.Contains(k.Substring(0, k.Length - 1))));
                if (score > 0) scored.Add((e.id, score));
            }
            foreach (var s in scored.OrderByDescending(x => x.score).ThenBy(x => x.id).Take(max)) result.Add(s.id);
            return result;
        }
    }
}
