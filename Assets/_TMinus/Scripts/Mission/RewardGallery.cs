using System;
using System.Collections.Generic;
using TMinus.Core;
using TMinus.Core.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace TMinus.Mission
{
    /// <summary>
    /// Debrief science reward: the real Landsat images of Lake Manchar (2022 floods), shown the way the player's
    /// camera would have seen them. Coarser GSD = blockier picture; fewer images returned = fewer dates unlocked.
    /// </summary>
    public static class RewardGallery
    {
        public const double LandsatGsdM = 30;   // Landsat 8/9 OLI multispectral pixel
        public const int TileWidthPx = 300;      // on-screen tile width

        /// <summary>Pixels across the tile your camera resolves: Landsat detail scaled by 30 m / your GSD (never sharper than the source).</summary>
        public static int ResolvedWidth(double gsdM, int tileWidth = TileWidthPx)
        {
            if (gsdM <= 0) return 0;
            double w = tileWidth * Math.Min(1.0, LandsatGsdM / gsdM);
            return Math.Max(8, (int)Math.Round(w));
        }

        /// <summary>How many of the dated images unlock: one per third of the image goal returned (at least one if any came home).</summary>
        public static int UnlockedCount(double imageShare, int imagesDownlinked, int total)
        {
            if (imagesDownlinked <= 0 || total <= 0) return 0;
            return Math.Max(1, Math.Min(total, (int)Math.Floor(imageShare * total + 1e-9)));
        }

        public static VisualElement Build(MissionDef mission, OpsReport ops)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row; row.style.flexWrap = Wrap.Wrap; row.style.marginTop = 6;
            var images = mission?.rewardImages ?? new List<RewardImageDef>();
            if (images.Count == 0) { row.Add(new Label("No reward images for this mission.")); return row; }

            int unlocked = UnlockedCount(ops.rewardImageShare, ops.imagesDownlinked, images.Count);
            int resolved = ResolvedWidth(ops.rewardGsdM);
            for (int i = 0; i < images.Count; i++) row.Add(Tile(images[i], i < unlocked, resolved, ops.rewardGsdM));
            return row;
        }

        static VisualElement Tile(RewardImageDef def, bool unlocked, int resolvedWidth, double gsdM)
        {
            var tile = new VisualElement();
            tile.style.width = TileWidthPx; tile.style.marginRight = 12; tile.style.marginBottom = 8;
            var src = string.IsNullOrEmpty(def.resource) ? null : Resources.Load<Texture2D>(def.resource);
            var pic = new Image { scaleMode = ScaleMode.ScaleToFit };
            pic.style.width = TileWidthPx; pic.style.height = TileWidthPx * 0.93f;
            pic.style.backgroundColor = new Color(0.06f, 0.07f, 0.09f);
            if (unlocked && src != null && resolvedWidth > 0) pic.image = Degrade(src, resolvedWidth);
            tile.Add(pic);

            string state = !unlocked ? "LOCKED: not downlinked. Return more images to unlock this date."
                         : src == null ? "Image missing."
                         : $"Seen at {gsdM:0.#} m/pixel ({resolvedWidth} px across){(gsdM <= LandsatGsdM ? ", as sharp as Landsat" : $", Landsat sees {LandsatGsdM:0} m")}";
            tile.Add(new Label($"{def.date}  ·  {def.title}"));
            var note = new Label(state); note.AddToClassList("muted"); note.style.whiteSpace = WhiteSpace.Normal; tile.Add(note);
            return tile;
        }

        static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void HookRelease() => UnityEngine.SceneManagement.SceneManager.sceneUnloaded += _ => Release();

        /// <summary>Destroy the downsampled copies (audit A-31): they were kept for the whole session.</summary>
        public static void Release()
        {
            foreach (var t in Cache.Values) if (t != null) UnityEngine.Object.Destroy(t);
            Cache.Clear();
        }

        /// <summary>Downsample on the GPU (works on non-readable textures and WebGL), then show with point filtering so pixels read as blocks.</summary>
        public static Texture2D Degrade(Texture2D src, int width)
        {
            width = Mathf.Clamp(width, 8, src.width);
            int height = Mathf.Max(8, Mathf.RoundToInt(width * src.height / (float)src.width));
            string key = src.name + ":" + width;
            if (Cache.TryGetValue(key, out var hit) && hit != null) return hit;

            var rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
            var prev = RenderTexture.active;
            Graphics.Blit(src, rt);
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, name = key };
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            Cache[key] = tex;
            return tex;
        }
    }
}
