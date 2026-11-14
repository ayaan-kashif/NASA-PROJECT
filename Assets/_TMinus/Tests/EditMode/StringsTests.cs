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
