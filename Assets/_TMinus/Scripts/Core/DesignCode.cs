using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using Newtonsoft.Json;

namespace TMinus.Core
{
    /// <summary>Everything needed to rebuild a player's build: mission + CubeSat + rocket.</summary>
    [Serializable]
    public sealed class DesignBundle
    {
        public int v = 1;
        public string missionId;
        public SpacecraftDesign spacecraft;
        public VehicleDesign vehicle;
    }

    /// <summary>Shareable design codes: JSON -> GZip -> Base64 (Prompt 9). Paste a code to load a friend's build.</summary>
    public static class DesignCode
    {
        public const string Prefix = "TM1-";

        public static string Encode(DesignBundle bundle)
        {
            var json = JsonConvert.SerializeObject(bundle, Formatting.None);
            using (var ms = new MemoryStream())
            {
                using (var gz = new GZipStream(ms, CompressionLevel.Optimal, true))
                {
                    var bytes = Encoding.UTF8.GetBytes(json);
                    gz.Write(bytes, 0, bytes.Length);
                }
                return Prefix + Convert.ToBase64String(ms.ToArray());
            }
        }

        public static bool TryDecode(string code, out DesignBundle bundle, out string error)
        {
            bundle = null; error = null;
            try
            {
                code = code?.Trim() ?? "";
                if (code.StartsWith(Prefix)) code = code.Substring(Prefix.Length);
                var data = Convert.FromBase64String(code);
                using (var ms = new MemoryStream(data))
                using (var gz = new GZipStream(ms, CompressionMode.Decompress))
                using (var reader = new StreamReader(gz, Encoding.UTF8))
                    bundle = JsonConvert.DeserializeObject<DesignBundle>(reader.ReadToEnd());
                if (bundle == null) { error = "Empty design code."; return false; }
                bundle.spacecraft ??= new SpacecraftDesign();
                bundle.vehicle ??= new VehicleDesign();
                return true;
            }
            catch (Exception ex)
            {
                error = "That isn't a valid design code (" + ex.GetType().Name + ").";
                return false;
            }
        }

        public static DesignBundle FromState(GameState s) =>
            new DesignBundle { missionId = s.CurrentMissionId, spacecraft = s.Spacecraft, vehicle = s.Vehicle };

        public static void Apply(DesignBundle b, GameState s)
        {
            if (!string.IsNullOrEmpty(b.missionId)) s.CurrentMissionId = b.missionId;
            s.Spacecraft = b.spacecraft;
            s.Vehicle = b.vehicle;
        }
    }
}
