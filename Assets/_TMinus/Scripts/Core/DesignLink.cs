using System;
using UnityEngine;

namespace TMinus.Core
{
    /// <summary>
    /// Shareable links to a design for the web build (audit F16): the page address ends in "#d=" and the design code. A friend who opens
    /// that link finds the code waiting in the briefing. Only the web build knows its own address, so the desktop build has no links.
    /// </summary>
    public static class DesignLink
    {
        const string Marker = "#d=";

        public static bool Available => Application.platform == RuntimePlatform.WebGLPlayer && !string.IsNullOrEmpty(Application.absoluteURL);

        /// <summary>The page address with this design code attached, or null when not running in a browser.</summary>
        public static string Build(string designCode)
        {
            if (!Available || string.IsNullOrEmpty(designCode)) return null;
            string url = Application.absoluteURL;
            int hash = url.IndexOf('#');
            if (hash >= 0) url = url.Substring(0, hash);
            return url + Marker + Uri.EscapeDataString(designCode);
        }

        /// <summary>The design code in the page address, if any.</summary>
        public static bool TryRead(out string designCode)
        {
            designCode = null;
            string url = Application.absoluteURL;
            if (string.IsNullOrEmpty(url)) return false;
            int i = url.IndexOf(Marker, StringComparison.Ordinal);
            if (i < 0) return false;
            try { designCode = Uri.UnescapeDataString(url.Substring(i + Marker.Length)); }
            catch (UriFormatException) { return false; }
            return !string.IsNullOrWhiteSpace(designCode) && designCode.Length < 200000;
        }
    }
}
