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
