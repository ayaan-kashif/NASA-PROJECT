using System;
using System.Collections;
using System.Globalization;
using UnityEngine;
using UnityEngine.Networking;

namespace TMinus.Mission
{
    /// <summary>A place on Earth the player can point the satellite at.</summary>
    public sealed class Region
    {
        public string id, name;
        public double minLat, minLon, maxLat, maxLon;
        public Region(string id, string name, double minLat, double minLon, double maxLat, double maxLon)
        { this.id = id; this.name = name; this.minLat = minLat; this.minLon = minLon; this.maxLat = maxLat; this.maxLon = maxLon; }
    }

    /// <summary>
    /// Real-data target (audit F26): pick a region in the briefing and the debrief shows a real NASA picture of it, fetched live from
    /// NASA Worldview Snapshots (MODIS Terra true colour, 250 m per pixel at best) and shown at your camera's detail. The Lake Manchar
    /// floods stay bundled so the mission works offline. This needs an internet connection. The address was tried against the live service on 14 November 2026 and returns a JPEG (a first version with a WRAP parameter was refused).
    /// </summary>
    public static class LiveImagery
    {
        public const string DefaultId = "manchar";
        const string PrefKey = "tminus.targetRegion";
        public const string Layer = "MODIS_Terra_CorrectedReflectance_TrueColor";
        public const string Date = "2022-08-28";   // the same flood season as the bundled images, so the comparison is fair
        public const double SourceResolutionM = 250;

        public static readonly Region[] Regions =
        {
            new Region(DefaultId, "Lake Manchar, Sindh (bundled, works offline)", 0, 0, 0, 0),
            new Region("karachi", "Karachi and the Indus delta", 23.5, 66.0, 25.8, 68.4),
            new Region("nile_delta", "Nile delta, Egypt", 30.0, 30.0, 32.5, 33.0),
            new Region("aral", "Aral Sea, Central Asia", 43.5, 58.0, 46.5, 62.0),
            new Region("lake_chad", "Lake Chad, Africa", 12.0, 13.0, 14.8, 15.8),
            new Region("amazon_mouth", "Amazon river mouth, Brazil", -2.0, -52.0, 1.0, -48.0),
            new Region("karakoram", "Karakoram glaciers, Pakistan", 35.0, 75.0, 37.5, 78.0),
        };

        public static Region ById(string id)
        {
            foreach (var r in Regions) if (r.id == id) return r;
            return Regions[0];
        }

        public static Region Selected => ById(PlayerPrefs.GetString(PrefKey, DefaultId));

        public static void Select(string id)
        {
            PlayerPrefs.SetString(PrefKey, ById(id).id);
            PlayerPrefs.Save();
        }

        public static bool IsBundled(Region r) => r.id == DefaultId;

        /// <summary>The NASA Worldview snapshot address for the region (EPSG:4326 boxes are written south, west, north, east).</summary>
        public static string Url(Region r, int pixels)
        {
            var c = CultureInfo.InvariantCulture;
            return "https://wvs.earthdata.nasa.gov/api/v1/snapshot?REQUEST=GetSnapshot"
                 + "&LAYERS=" + Layer + "&CRS=EPSG:4326&TIME=" + Date
                 + "&BBOX=" + r.minLat.ToString(c) + "," + r.minLon.ToString(c) + "," + r.maxLat.ToString(c) + "," + r.maxLon.ToString(c)
                 + "&FORMAT=image/jpeg&WIDTH=" + pixels + "&HEIGHT=" + pixels;
        }

        /// <summary>Downloads the picture. done(null) when there is no connection or the service says no.</summary>
        public static IEnumerator Fetch(Region r, int pixels, Action<Texture2D> done)
        {
            using (var req = UnityWebRequestTexture.GetTexture(Url(r, pixels)))
            {
                req.timeout = 20;
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success) { done(null); yield break; }
                Texture2D tex = null;
                try { tex = DownloadHandlerTexture.GetContent(req); } catch (Exception) { /* not an image: treated as no picture */ }
                done(tex);
            }
        }
    }
}
