using System;
using System.Collections;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace TMinus.Mission
{
    /// <summary>
    /// Picture card (audit F15): saves what the debrief screen shows as a PNG, to post or print. On a computer it is written into the
    /// game's data folder; in a browser the page offers it as a download. The screenshot must be taken after the frame is drawn, so this is a coroutine.
    /// </summary>
    public static class ShareCard
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void TMinusDownload(string name, byte[] data, int size);
#endif

        /// <summary>done(message): where the picture went, or why it could not be saved.</summary>
        public static IEnumerator Save(string missionId, Action<string> done)
        {
            yield return new WaitForEndOfFrame();
            Texture2D tex = null;
            try { tex = ScreenCapture.CaptureScreenshotAsTexture(); }
            catch (Exception) { /* handled below */ }
            if (tex == null) { done("Could not take the picture on this device."); yield break; }
            byte[] png;
            try { png = tex.EncodeToPNG(); }
            finally { UnityEngine.Object.Destroy(tex); }
            string name = "t-minus-" + Safe(missionId) + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".png";
#if UNITY_WEBGL && !UNITY_EDITOR
            TMinusDownload(name, png, png.Length);
            done("Picture saved to your downloads: " + name);
#else
            try
            {
                string path = Path.Combine(Application.persistentDataPath, name);
                File.WriteAllBytes(path, png);
                done("Picture saved: " + path);
            }
            catch (Exception e) { done("Could not save the picture: " + e.Message); }
#endif
        }

        static string Safe(string s)
        {
            if (string.IsNullOrEmpty(s)) return "mission";
            var sb = new System.Text.StringBuilder();
            foreach (char c in s) sb.Append(char.IsLetterOrDigit(c) || c == '_' ? c : '-');
            return sb.ToString();
        }
    }
}
