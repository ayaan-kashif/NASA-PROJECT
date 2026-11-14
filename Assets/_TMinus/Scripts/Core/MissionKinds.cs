using TMinus.Core.Data;

namespace TMinus.Core
{
    /// <summary>What a mission is about, so the screens that assume a CubeSat can step aside for a flight-only mission (Moonshot).</summary>
    public static class MissionKinds
    {
        /// <summary>True when the mission is about building and operating a 3U CubeSat (FloodWatch-1, its variants, the Sandbox).</summary>
        public static bool IsCubeSat(MissionDef mission) =>
            mission == null || string.IsNullOrEmpty(mission.payloadType) || mission.payloadType == "cubesat_3u";
    }
}
