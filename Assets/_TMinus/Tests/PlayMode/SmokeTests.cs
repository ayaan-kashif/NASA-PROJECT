using System.Collections;
using NUnit.Framework;
using TMinus.Core;
using TMinus.Flight;
using TMinus.Mission;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TMinus.Tests.PlayMode
{
    /// <summary>
    /// Scene-level checks that the EditMode tests cannot do (audit A-27): every room loads without an error in the Console, and the
    /// whole loop works from a ready build to a debrief. Run with Window > General > Test Runner > PlayMode.
    /// </summary>
    public class SmokeTests
    {
        static readonly string[] Rooms =
        {
            SceneNames.MainMenu, SceneNames.MissionBriefing, SceneNames.IntegrationBay, SceneNames.VAB,
            SceneNames.AvionicsBay, SceneNames.Flight, SceneNames.Debrief
        };

        [UnityTest]
        public IEnumerator EveryRoomLoadsWithoutErrors()
        {
            foreach (var room in Rooms)
            {
                SceneManager.LoadScene(room);
                yield return null;
                yield return new WaitForSeconds(1.0f);
                Assert.AreEqual(room, SceneManager.GetActiveScene().name);
                LogAssert.NoUnexpectedReceived();   // warnings are fine; errors and exceptions fail the test
            }
        }

        [UnityTest]
        public IEnumerator AReadyBuildReachesOrbitAndEarnsThreeStars()
        {
            SceneManager.LoadScene(SceneNames.MainMenu);
            yield return null;
            DemoBuilds.Apply(GameState.Instance, padReady: true);
            SceneManager.LoadScene(SceneNames.Flight);
            yield return null;
            yield return new WaitForSeconds(0.5f);

            var flight = FlightController.Instance;
            Assert.IsNotNull(flight, "the Flight scene needs a FlightController");
            flight.Launch(3);
            FlightDebug.RunToEnd(null);

            var record = GameState.Instance.Flight;
            Assert.IsTrue(record.orbitAchieved, "outcome " + record.outcome + ", " + record.failureCode + " <- " + record.failureCause);
            Assert.IsTrue(record.deployed, "the CubeSat must be released");

            var ops = DebriefDebug.RunMission();
            Assert.GreaterOrEqual(ops.imagesDownlinked, 20);
            Assert.GreaterOrEqual(DebriefDebug.LastScore.stars, 3, "score " + DebriefDebug.LastScore.total);
        }

        [UnityTest]
        public IEnumerator ALaunchWithSafeArmOnSafeAbortsAndNamesThePlug()
        {
            SceneManager.LoadScene(SceneNames.MainMenu);
            yield return null;
            DemoBuilds.Apply(GameState.Instance);        // a complete build, but nobody armed it at the pad
            SceneManager.LoadScene(SceneNames.Flight);
            yield return null;
            yield return new WaitForSeconds(0.5f);

            FlightController.Instance.Launch(3);
            FlightDebug.RunToEnd(null);

            var record = GameState.Instance.Flight;
            Assert.AreEqual("ignition_failure", record.failureCode);
            Assert.AreEqual("av_safe_arm", record.failureCause);
        }
    }
}
