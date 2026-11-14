using UnityEngine;

namespace TMinus.Core
{
    /// <summary>Which rooms' guided first build the player has finished or skipped (audit F2). Kept on this computer.</summary>
    public static class FirstBuildGuideFlags
    {
        public static readonly string[] Rooms = { SceneNames.IntegrationBay, SceneNames.VAB, SceneNames.AvionicsBay };

        static string Key(string room) => "tminus.guide.done." + room;

        public static bool IsDone(string room) => PlayerPrefs.GetInt(Key(room), 0) == 1;

        public static void MarkDone(string room)
        {
            PlayerPrefs.SetInt(Key(room), 1);
            PlayerPrefs.Save();
        }

        /// <summary>Settings > "Replay the guided first build".</summary>
        public static void ResetAll()
        {
            foreach (var r in Rooms) PlayerPrefs.DeleteKey(Key(r));
            PlayerPrefs.Save();
        }
    }
}
