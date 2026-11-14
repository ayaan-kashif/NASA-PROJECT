using System.Linq;
using NUnit.Framework;
using TMinus.Core;
using UnityEngine;

namespace TMinus.Tests
{
    public class StringsTests
    {
        [Test]
        public void TheEnglishTableLoadsWithNoEmptyText()
        {
            var asset = Resources.Load<TextAsset>("Data/strings");
            Assert.IsNotNull(asset, "Resources/Data/strings.json must exist");
            var en = Strings.Parse(asset.text, "en");
            Assert.GreaterOrEqual(en.Count, 10);
            foreach (var kv in en) Assert.IsFalse(string.IsNullOrWhiteSpace(kv.Value), kv.Key);
            foreach (var key in new[] { "pause.resume", "pause.mainMenu", "pause.paused", "pause.controls" }) Assert.IsTrue(en.ContainsKey(key), key);
        }

        [Test]
        public void EveryStringsGetCallInTheScriptsMatchesTheTable()
        {
            // tools/extract_strings.py moves text into the table; this catches a key that was typed wrong or text that drifted apart.
            var en = Strings.Parse(Resources.Load<TextAsset>("Data/strings").text, "en");
            var call = new System.Text.RegularExpressions.Regex("Strings\\.Get\\(\"([^\"]+)\", \"((?:[^\"\\\\]|\\\\.)*)\"\\)");
            var problems = new System.Collections.Generic.List<string>();
            int calls = 0;
            foreach (var file in System.IO.Directory.GetFiles(Application.dataPath + "/_TMinus/Scripts", "*.cs", System.IO.SearchOption.AllDirectories))
                foreach (System.Text.RegularExpressions.Match m in call.Matches(System.IO.File.ReadAllText(file)))
                {
                    calls++;
                    string key = m.Groups[1].Value, inCode = m.Groups[2].Value.Replace("\\\"", "\"").Replace("\\\\", "\\");
                    if (!en.TryGetValue(key, out var inTable)) problems.Add(System.IO.Path.GetFileName(file) + ": key '" + key + "' is not in strings.json");
                    else if (inTable != inCode) problems.Add(System.IO.Path.GetFileName(file) + ": '" + key + "' differs from the table");
                }
            Assert.GreaterOrEqual(calls, 60, "the extraction should have moved dozens of texts");
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        [Test]
        public void AMissingKeyShowsTheFallbackNotAnEmptyButton()
        {
            Assert.AreEqual("Resume it", Strings.Get("no.such.key", "Resume it"));
            Assert.AreEqual("Hello Ayesha", Strings.Format("no.such.format", "Hello {0}", "Ayesha"));
        }

        [Test]
        public void ABrokenTableGivesAnEmptyMapInsteadOfThrowing()
        {
            Assert.IsEmpty(Strings.Parse("{ not json", "en"));
            Assert.IsEmpty(Strings.Parse(null, "en"));
            Assert.IsEmpty(Strings.Parse("{\"fr\":{\"a\":\"b\"}}", "en"));
        }
    }
}
