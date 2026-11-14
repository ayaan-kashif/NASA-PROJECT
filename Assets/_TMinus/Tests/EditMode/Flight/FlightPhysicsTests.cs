using System;
using System.Collections.Generic;
using NUnit.Framework;
using TMinus.Core;
using TMinus.Core.Data;
using TMinus.Flight;

namespace TMinus.Tests.Flight
{
    public sealed class FlightPhysicsTests
    {
        DataRegistry LoadData() => DataRegistry.LoadFromResources();

        [Test]
        public void SaturnVLiftoffTwrMatchesBrief()
        {
            var rocket = LoadData().GetRocket("saturn_v");
            double twr = rocket.stages[0].thrustSlKN * 1000 /
                         (rocket.LiftoffMassKg(rocket.referencePayloadKg) * FlightConstants.G0);
            Assert.That(twr, Is.InRange(1.1, 1.25));
        }

        [Test]
        public void RocketEquationMatchesHandCalculation()
        {
            const double isp = 300, m0 = 12000, mf = 4000;
            double expected = isp * 9.80665 * Math.Log(3);
            double actual = FlightPhysics.RocketEquation(isp, m0, mf);
            Assert.That(Math.Abs(actual - expected) / expected, Is.LessThan(0.001));
        }

        [Test]
        public void SubTwrRocketRemainsHeldOnLaunchPad()
        {
            var vehicle = new FlightVehicle { id = "pad-hold-test" };
            vehicle.stages.Add(new FlightStage
            {
                id = "weak-stage", dryMassKg = 900, propMassKg = 100,
                thrustVacN = 5000, ispVac = 300, ispSeaLevel = 250, diameterM = 1
            });
            var sim = new FlightSim(vehicle, new LaunchOptions { payloadMassKg = 0 }) { RecordTelemetry = false };

            sim.RunForSimSeconds(10);

            Assert.That(sim.Phase, Is.EqualTo(FlightPhase.Pad));
            Assert.That(FlightPhysics.Altitude(sim.Position), Is.EqualTo(0).Within(1e-6));
        }

        [Test]
        public void Circular550KmOrbitStaysWithinTwoKmForTenOrbits()
        {
            double radius = FlightConstants.EarthRadiusM + 550000;
            double period = 2 * Math.PI * Math.Sqrt(radius * radius * radius / FlightConstants.Mu);
            var vehicle = new FlightVehicle { id = "circular-test" };
            vehicle.stages.Add(new FlightStage { id = "coast", dryMassKg = 1000, propMassKg = 0, diameterM = 1 });
            var sim = new FlightSim(vehicle, new LaunchOptions
            {
                initialAltitudeKm = 550, startInCircularOrbit = true, autopilot = false, payloadMassKg = 0
            }) { RecordTelemetry = false };
            sim.SetManualControl(0, 90);
            sim.RunForSimSeconds(period * 10);
            double altitudeKm = FlightPhysics.Altitude(sim.Position) / 1000;
            Assert.That(altitudeKm, Is.InRange(548, 552));
        }

        [Test]
        public void PathfinderReachesAtLeastFiveHundredKilometresOnAutopilot()
        {
            var data = LoadData();
            var sim = FlightRuntime.Create(data, new VehicleDesign
            {
                rocketId = "pathfinder", launchSiteId = "sonmiani", pitchKickDeg = 3
            }, new PerfectAvionicsReport(), null, null);
            sim.RecordTelemetry = false;
            sim.RunForSimSeconds(5000);
            double altitudeKm = FlightPhysics.Altitude(sim.Position) / 1000;
            Assert.That(altitudeKm, Is.GreaterThanOrEqualTo(500), $"phase={sim.Phase}, altitude={altitudeKm:0.0} km, max-Q={sim.Outcome.maxQPa / 1000:0.000} kPa");
            Assert.That(sim.Outcome.OrbitAchieved, Is.True, $"The flight ended in {sim.Phase} at {altitudeKm:0.0} km.");
        }

        [Test]
        public void JointFailureUsesIntegrityAdjustedQThresholdAndCause()
        {
            var data = LoadData();
            var rocket = data.GetRocket("pathfinder");
            var vehicle = FlightVehicleFactory.From(rocket, data.GetRules());
            var av = new FakeAvionics { separationOk = true };
            var joints = new List<IJointStatus> { new FakeJoint("j_s1_interstage", 0.01) };
            var sim = new FlightSim(vehicle, new LaunchOptions { payloadMassKg = 150 }, av, joints, null)
            { RecordTelemetry = false };
            sim.RunForSimSeconds(100);
            Assert.That(sim.Outcome.failureCode, Is.EqualTo("joint_failure"));
            Assert.That(sim.Outcome.failureCause, Is.EqualTo("j_s1_interstage"));
        }

        [Test]
        public void MissingImuLogsCauseAndAccumulatesPitchDrift()
        {
            var data = LoadData();
            var events = new EventLog();
            var sim = FlightRuntime.Create(data, new VehicleDesign { rocketId = "pathfinder", launchSiteId = "sonmiani" },
                new FakeAvionics { guidanceOk = false }, null, events);
            sim.Options.autopilot = false;
            sim.SetManualControl(1, 90); // full throttle: with 0 the rocket never lifts off and the sim ends at 2 s
            sim.RecordTelemetry = false;
            sim.RunForSimSeconds(10);
            Assert.That(sim.GuidanceErrorDeg, Is.EqualTo(5).Within(1e-8));
            bool found = false;
            foreach (var e in events.All)
                if (e.code == "guidance_drift" && e.causeId == "w_pdu_imu") found = true;
            Assert.That(found, Is.True, "The guidance fault should retain the avionics cable cause id.");
        }

        [Test]
        public void UnwiredStageSeparationLogsTheCausalCable()
        {
            var data = LoadData();
            var sim = FlightRuntime.Create(data, new VehicleDesign { rocketId = "pathfinder", launchSiteId = "sonmiani" },
                new FakeAvionics { separationOk = false }, null, new EventLog());
            sim.RecordTelemetry = false;
            sim.RunForSimSeconds(500);
            Assert.That(sim.Outcome.failureCode, Is.EqualTo("stuck_stage"));
            Assert.That(sim.Outcome.failureCause, Is.EqualTo("w_pyro2"));
        }

        [Test]
        public void ManualControlLiftsOffAndLeavesThePad()
        {
            // Audit A-02: with the autopilot off the phase used to stay "Pad", so the rocket was clamped to the ground.
            var data = LoadData();
            var sim = FlightRuntime.Create(data, new VehicleDesign { rocketId = "pathfinder", launchSiteId = "sonmiani" },
                new PerfectAvionicsReport(), null, new EventLog());
            sim.Options.autopilot = false;
            sim.SetManualControl(1, 90);
            sim.RecordTelemetry = false;
            sim.RunForSimSeconds(30);
            Assert.That(sim.Phase, Is.Not.EqualTo(FlightPhase.Pad));
            Assert.That(FlightPhysics.Altitude(sim.Position), Is.GreaterThan(1000));
        }

        [Test]
        public void ManualFlightWithTooLittleThrottleStaysOnThePad()
        {
            var data = LoadData();
            var sim = FlightRuntime.Create(data, new VehicleDesign { rocketId = "pathfinder", launchSiteId = "sonmiani" },
                new PerfectAvionicsReport(), null, new EventLog());
            sim.Options.autopilot = false;
            sim.SetManualControl(0.1, 90);   // 10% of 1.7 TWR is far below 1
            sim.RecordTelemetry = false;
            sim.RunForSimSeconds(10);
            Assert.That(sim.Phase, Is.EqualTo(FlightPhase.Pad));
        }

        [Test]
        public void SaturnVReachesTheParkingOrbitWithPropellantLeftForTheMoonBurn()
        {
            // The Moonshot mission. The profile in rockets.json was found with tools/sim (a JavaScript port of this solver), which gives about a
            // 185 x 175 km orbit with roughly 2,980 m/s left. If this test fails, the C# and the port disagree: compare them step by step.
            var data = LoadData();
            Assert.IsTrue(data.GetRocket("saturn_v").guidance.altitudeHold, "Saturn V needs the altitude-hold profile");
            Assert.IsNull(data.GetRocket("pathfinder").guidance, "Pathfinder keeps the original profile");

            var sim = FlightRuntime.Create(data, new VehicleDesign { rocketId = "saturn_v", launchSiteId = "cape_canaveral" },
                new PerfectAvionicsReport(), null, new EventLog(), "moonshot");
            sim.Options.pitchKickDeg = 3;
            sim.RunForSimSeconds(2400);

            Assert.That(sim.Phase, Is.EqualTo(FlightPhase.Orbit), "ended as " + sim.Phase + " " + sim.Outcome.failureCode);
            var last = sim.Outcome.telemetry[sim.Outcome.telemetry.Count - 1];
            Assert.That((last.periapsisM - FlightConstants.EarthRadiusM) / 1000, Is.GreaterThan(170), "periapsis in km");
            Assert.That(last.deltaVRemainingMs, Is.GreaterThan(2800), "propellant left for the Moon burn, m/s");
        }

        [Test]
        public void RehbarRocketClimbsToAboutOneHundredAndThirtyKilometresAndStopsThere()
        {
            // The Rehbar-I mission. tools/sim gives 130 to 133 km for any pitch kick from 1 to 6 degrees.
            var data = LoadData();
            foreach (double kick in new[] { 1.0, 3.0, 6.0 })
            {
                var sim = FlightRuntime.Create(data, new VehicleDesign { rocketId = "rehbar_nike_cajun", launchSiteId = "sonmiani" },
                    new PerfectAvionicsReport(), null, new EventLog(), "rehbar_1962");
                Assert.IsTrue(sim.Options.suborbital, "the mission has a target apogee, so the flight is a sounding rocket");
                sim.Options.pitchKickDeg = kick;
                sim.RunForSimSeconds(600);
                Assert.That(sim.Phase, Is.EqualTo(FlightPhase.Apogee), "kick " + kick + ": ended as " + sim.Phase + " " + sim.Outcome.failureCode);
                var last = sim.Outcome.telemetry[sim.Outcome.telemetry.Count - 1];
                Assert.That(last.altitudeM / 1000, Is.InRange(120, 145), "kick " + kick);
            }
        }

        [Test]
        public void PartOfAStepIsKeptSoNormalSpeedFlightAdvances()
        {
            // At 60 frames a second one frame is 0.0167 s, less than one 0.05 s step. Dropping the remainder froze x1 and x2 flights.
            var vehicle = new FlightVehicle { id = "advance-test" };
            vehicle.stages.Add(new FlightStage { id = "s", dryMassKg = 900, propMassKg = 100, thrustVacN = 5000, ispVac = 300, ispSeaLevel = 250, diameterM = 1 });
            var sim = new FlightSim(vehicle, new LaunchOptions { payloadMassKg = 0 }) { RecordTelemetry = false };
            for (int i = 0; i < 60; i++) sim.Advance(1.0 / 60.0, 1);
            Assert.That(sim.TimeS, Is.EqualTo(1.0).Within(0.06), "one real second at x1 is about one simulated second");

            var fast = new FlightSim(vehicle, new LaunchOptions { payloadMassKg = 0 }) { RecordTelemetry = false };
            for (int i = 0; i < 60; i++) fast.Advance(1.0 / 60.0, 4);
            Assert.That(fast.TimeS, Is.EqualTo(4.0).Within(0.1), "x4 is about four simulated seconds per real second");
        }

        sealed class FakeJoint : IJointStatus
        {
            public string JointId { get; }
            public double FailureQkPa { get; }
            public FakeJoint(string id, double failureQ) { JointId = id; FailureQkPa = failureQ; }
        }

        sealed class FakeAvionics : IAvionicsReport
        {
            public bool separationOk = true;
            public bool guidanceOk = true;
            public bool S1IgnitionOk => true;
            public bool StageSeparationOk => separationOk;
            public bool S2IgnitionOk => true;
            public bool GuidanceOk => guidanceOk;
            public bool FairingSeparationOk => true;
            public string CauseId(string consequence) => consequence == "stuck_stage" ? "w_pyro2" : consequence == "guidance_drift" ? "w_pdu_imu" : null;
        }
    }
}
