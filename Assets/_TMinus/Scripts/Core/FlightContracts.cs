namespace TMinus.Core
{
    /// <summary>Flight-facing wiring state. Implementations may be live builds or all-OK stubs.</summary>
    public interface IAvionicsReport
    {
        bool S1IgnitionOk { get; }
        bool StageSeparationOk { get; }
        bool S2IgnitionOk { get; }
        bool GuidanceOk { get; }
        bool FairingSeparationOk { get; }
        string CauseId(string consequence);
    }

    /// <summary>Minimum joint data the flight solver needs, independent of bolt implementation.</summary>
    public interface IJointStatus
    {
        string JointId { get; }
        double FailureQkPa { get; }
    }

    /// <summary>Default build state for inspect-only vehicles and isolated physics tests.</summary>
    public sealed class PerfectAvionicsReport : IAvionicsReport
    {
        public bool S1IgnitionOk => true;
        public bool StageSeparationOk => true;
        public bool S2IgnitionOk => true;
        public bool GuidanceOk => true;
        public bool FairingSeparationOk => true;
        public string CauseId(string consequence) => null;
    }

    public sealed class PerfectJointStatus : IJointStatus
    {
        public string JointId => string.Empty;
        public double FailureQkPa => double.PositiveInfinity;
    }
}
