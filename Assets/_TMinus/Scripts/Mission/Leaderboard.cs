using System;
using System.Collections.Generic;
using UnityEngine;

namespace TMinus.Mission
{
    [Serializable] sealed class LeaderboardEntry { public string name, code; public double score; }
    [Serializable] sealed class LeaderboardList { public List<LeaderboardEntry> entries = new List<LeaderboardEntry>(); }
    static class Leaderboard
    {
        const string Key = "TMinus.LocalLeaderboard.v1";
        public static List<LeaderboardEntry> Load()
        {
            var json = PlayerPrefs.GetString(Key, "");
            return string.IsNullOrEmpty(json) ? new List<LeaderboardEntry>() : JsonUtility.FromJson<LeaderboardList>(json)?.entries ?? new List<LeaderboardEntry>();
        }
        public static void Save(List<LeaderboardEntry> rows)
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(new LeaderboardList { entries = rows }));
            PlayerPrefs.Save();
        }
    }
}
