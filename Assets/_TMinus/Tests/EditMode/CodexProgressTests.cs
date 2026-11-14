using System.Linq;
using NUnit.Framework;
using TMinus.Core;
using UnityEngine;

namespace TMinus.Tests
{
    public class CodexProgressTests
    {
        static System.Collections.Generic.List<QuizItem> Quiz()
        {
            var asset = Resources.Load<TextAsset>("Data/quiz");
            Assert.IsNotNull(asset, "Resources/Data/quiz.json must exist");
            return CodexProgress.Parse(asset.text);
        }

        [Test]
        public void EveryTopicHasAQuickCheck_AndEveryQuickCheckBelongsToATopic()
        {
            var quiz = Quiz();
            var codexIds = TestData.Registry.Codex.Select(c => c.id).ToList();
            foreach (var id in codexIds) Assert.IsTrue(quiz.Any(q => q.id == id), "no quick check for topic " + id);
            foreach (var q in quiz) Assert.IsTrue(codexIds.Contains(q.id), "quick check for unknown topic " + q.id);
            Assert.AreEqual(quiz.Count, quiz.Select(q => q.id).Distinct().Count(), "one quick check per topic");
        }

        [Test]
        public void EachQuickCheckHasOneRightAnswerAndDifferentWrongOnes()
        {
            foreach (var q in Quiz())
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(q.q), q.id);
                Assert.GreaterOrEqual(q.wrong.Count, 2, q.id);
                var all = q.wrong.Concat(new[] { q.correct }).ToList();
                Assert.AreEqual(all.Count, all.Distinct().Count(), q.id + ": answers must differ");
            }
        }

        [Test]
        public void OptionOrderIsStableAndContainsTheCorrectAnswer()
        {
            var item = Quiz().First();
            var a = CodexProgress.Options(item); var b = CodexProgress.Options(item);
            CollectionAssert.AreEqual(a, b);
            Assert.IsTrue(a.Contains(item.correct));
            Assert.AreEqual(item.wrong.Count + 1, a.Count);
        }

        [Test]
        public void BrokenQuizJsonGivesAnEmptyListInsteadOfThrowing()
        {
            Assert.IsEmpty(CodexProgress.Parse("{ not json"));
            Assert.IsEmpty(CodexProgress.Parse(null));
        }
    }
}
