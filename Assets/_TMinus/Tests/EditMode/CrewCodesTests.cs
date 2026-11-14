using NUnit.Framework;
using TMinus.Core;

namespace TMinus.Tests
{
    public class CrewCodesTests
    {
        static DesignBundle Design()
        {
            var input = ChecklistTests.Perfect();
            return new DesignBundle { missionId = "floodwatch_1", spacecraft = input.Spacecraft, vehicle = input.Vehicle };
        }

        [Test]
        public void AVoteSurvivesTheTripThroughAChatMessage()
        {
            var vote = new CrewVote { role = CrewRole.ScienceBudget.ToString(), go = false, name = "Ayesha", designHash = CrewCodes.DesignHash(Design()), openItems = 2 };
            string code = CrewCodes.EncodeVote(vote);
            StringAssert.StartsWith(CrewCodes.VotePrefix, code);
            Assert.IsTrue(CrewCodes.TryDecodeVote(code, out var back, out var error), error);
            Assert.AreEqual(vote.role, back.role);
            Assert.IsFalse(back.go);
            Assert.AreEqual("Ayesha", back.name);
            Assert.AreEqual(2, back.openItems);
        }

        [Test]
        public void AVoteOnlyCountsForTheBuildItWasMadeFor()
        {
            var design = Design();
            var vote = new CrewVote { role = CrewRole.FlightDirector.ToString(), go = true, designHash = CrewCodes.DesignHash(design) };
            Assert.IsTrue(CrewCodes.Matches(vote, design));
            design.vehicle.armed = !design.vehicle.armed;   // the host changed something after sending the pad code
            Assert.IsFalse(CrewCodes.Matches(vote, design));
        }

        [Test]
        public void TheSameDesignAlwaysGivesTheSameHash()
        {
            Assert.AreEqual(CrewCodes.DesignHash(Design()), CrewCodes.DesignHash(Design()));
        }

        [Test]
        public void GarbageAndUnknownRolesAreRefused()
        {
            Assert.IsFalse(CrewCodes.TryDecodeVote("hello", out _, out var e1));
            StringAssert.Contains("TMV1-", e1);
            Assert.IsFalse(CrewCodes.TryDecodeVote("TMV1-%%%%", out _, out _));
            string bad = CrewCodes.EncodeVote(new CrewVote { role = "Captain", go = true });
            Assert.IsFalse(CrewCodes.TryDecodeVote(bad, out _, out _), "a role that does not exist is refused");
            Assert.IsFalse(CrewCodes.TryDecodeVote(null, out _, out _));
        }

        [Test]
        public void ARemoteVoteCountsAsATeamRoleInTheScore()
        {
            // The poll text the host stores for a remote vote still starts with the role name, which the team score reads.
            var crew = TMinus.Mission.PrelaunchChecklist.AssignRoles(new[] { "A", "B", "C", "D" });
            var poll = new System.Collections.Generic.List<string>
            {
                "Flight Director: GO (remote, Sara)", "Propulsion & Structures: GO", "Spacecraft & Comms: GO (remote, Omar)", "Science & Budget: GO"
            };
            Assert.AreEqual(1.0, TMinus.Mission.MissionScoring.CollaborationFraction(crew, poll), 1e-9);
        }
    }
}
