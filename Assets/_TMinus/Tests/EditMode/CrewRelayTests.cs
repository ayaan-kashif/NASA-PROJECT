using System.Linq;
using NUnit.Framework;
using TMinus.Core;

namespace TMinus.Tests
{
    public class CrewRelayTests
    {
        [Test]
        public void ARoomCodeIsLongRandomAndEasyToType()
        {
            string a = CrewRelay.NewRoomCode(), b = CrewRelay.NewRoomCode();
            Assert.AreNotEqual(a, b);
            Assert.IsTrue(CrewRelay.IsValid(a), a);
            Assert.AreEqual(14, a.Length, "three groups of four with two dashes");
            Assert.IsFalse(a.Any(c => "01OI".IndexOf(c) >= 0), "no look-alike characters: " + a);
        }

        [Test]
        public void TypingTheCodeLowercaseOrWithSpacesStillFindsTheSameRoom()
        {
            string code = "K7QF-3MXA-RD2T";
            Assert.AreEqual(CrewRelay.Topic(code), CrewRelay.Topic("k7qf 3mxa rd2t"));
            Assert.AreEqual("tminus-k7qf3mxard2t", CrewRelay.Topic(code));
            Assert.IsTrue(CrewRelay.IsValid("k7qf3mxard2t"));
        }

        [Test]
        public void ShortOrOddCodesAreNotRooms()
        {
            Assert.IsFalse(CrewRelay.IsValid(""));
            Assert.IsFalse(CrewRelay.IsValid("ABCD-EFGH"));
            Assert.IsFalse(CrewRelay.IsValid("0000-0000-0000"), "0 and O are not in the alphabet");
            Assert.IsFalse(CrewRelay.IsValid(null));
        }

        [Test]
        public void OnlyMessageLinesBecomeCrewMessages()
        {
            string reply =
                "{\"id\":\"aaa\",\"time\":1,\"event\":\"open\",\"topic\":\"t\"}\n" +
                "{\"id\":\"bbb\",\"time\":2,\"event\":\"message\",\"topic\":\"t\",\"message\":\"TMV1-abc\"}\n" +
                "not json at all\n" +
                "{\"id\":\"ccc\",\"time\":3,\"event\":\"keepalive\",\"topic\":\"t\"}\n" +
                "{\"id\":\"ddd\",\"time\":4,\"event\":\"message\",\"topic\":\"t\",\"message\":\"TMV1-def\"}";
            var messages = CrewRelay.ParseMessages(reply);
            Assert.AreEqual(2, messages.Count);
            Assert.AreEqual("bbb", messages[0].id);
            Assert.AreEqual("TMV1-def", messages[1].text);
            Assert.IsEmpty(CrewRelay.ParseMessages(null));
            Assert.IsEmpty(CrewRelay.ParseMessages(""));
        }

        [Test]
        public void ThePadCodeTravelsThroughTheRoomAndTheNewestOneWins()
        {
            string old = CrewRelay.PadMessage("TMD1-old");
            string fresh = CrewRelay.PadMessage("TMD1-new");
            StringAssert.StartsWith(CrewRelay.PadPrefix, fresh);
            var list = new System.Collections.Generic.List<RelayMessage>
            {
                new RelayMessage { id = "1", text = old }, new RelayMessage { id = "2", text = "TMV1-a-vote" }, new RelayMessage { id = "3", text = fresh },
                new RelayMessage { id = "4", text = "TMV1-another-vote" }
            };
            Assert.AreEqual("TMD1-new", CrewRelay.LatestPad(list));
            Assert.IsNull(CrewRelay.LatestPad(new System.Collections.Generic.List<RelayMessage>()));
            Assert.IsNull(CrewRelay.LatestPad(null));
            Assert.IsNull(CrewRelay.PadMessage(new string('x', CrewRelay.MaxPadLength + 1)), "too long to send: copied by hand instead");
        }
    }
}
