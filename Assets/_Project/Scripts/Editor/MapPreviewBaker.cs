using System.IO;
using NeonBrawl;
using UnityEditor;
using UnityEngine;

namespace NeonBrawl.EditorTools
{
    /// <summary>
    /// Renders every map in high resolution for the map cards (home screen, map list, online room).
    /// The old card was built from tiny tile and prop cut-outs stretched up, so it looked soft; this
    /// shoots the real map with the game camera at 2x and scales it down, so it is sharp on any phone.
    /// Menu: Neon Brawl ▸ Art ▸ Bake Map Previews (enters Play mode, takes one shot per map, stops).
    /// Output: Resources/MapPreviews/map_N.png (800x384, ASTC 6x6 on Android, ~135 KB each).
    /// </summary>
    public static class MapPreviewBaker
    {
        private const string Folder = "Assets/_Project/Resources/MapPreviews";
        private const int W = 800, H = 384;
        private const string Key = "nb.bakemaps";

        private static int _map = -1;
        private static int _wait;

        [MenuItem("Neon Brawl/Art/Bake Map Previews")]
        public static void Bake()
        {
            SessionState.SetBool(Key, true);
            if (!EditorApplication.isPlaying) EditorApplication.isPlaying = true;
            else Begin();
        }

        [InitializeOnLoadMethod]
        private static void Hook()
        {
            EditorApplication.playModeStateChanged += s =>
            {
                if (s == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key, false)) Begin();
            };
        }

        private static void Begin()
        {
            _map = -1;
            _wait = 90; // let the camp intro settle
            EditorApplication.update -= Step;
            EditorApplication.update += Step;
        }

        private static void Step()
        {
            if (!EditorApplication.isPlaying) { EditorApplication.update -= Step; return; }
            if (--_wait > 0) return;
            var app = Object.FindAnyObjectByType<GameApp>();
            if (app == null || app.Context == null) { _wait = 10; return; }
            var ctx = app.Context;
            if (_map >= 0) Shoot(ctx, _map);
            _map++;
            if (_map >= MapLayouts.All.Length)
            {
                EditorApplication.update -= Step;
                SessionState.SetBool(Key, false);
                AssetDatabase.Refresh();
                Import();
                Debug.Log("[MapPreviewBaker] baked " + MapLayouts.All.Length + " map previews into " + Folder);
                EditorApplication.isPlaying = false;
                return;
            }
            app.PreviewMap(_map);
            _wait = 120; // bots spread out, fx settle
        }

        private static void Shoot(GameContext ctx, int index)
        {
            var match = ctx.Match;
            var cam = ctx.Camera != null ? ctx.Camera : Camera.main;
            if (match == null || cam == null) { Debug.LogWarning("[MapPreviewBaker] no match for map " + index); return; }
            var map = match.Map;
            Vector3 oldPos = cam.transform.position;
            float oldSize = cam.orthographicSize;
            var oldTarget = cam.targetTexture;
            // a wide look across the middle of the map
            cam.transform.position = new Vector3(map.Width * 0.5f, map.Height * 0.5f, oldPos.z);
            cam.orthographicSize = Mathf.Min(map.Height * 0.3f, 6.4f);
            var rt = RenderTexture.GetTemporary(W * 2, H * 2, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 1;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = oldTarget;
            cam.transform.position = oldPos;
            cam.orthographicSize = oldSize;

            // 2x down to 1x with a box filter: crisp edges, no shimmer
            var big = new Texture2D(W * 2, H * 2, TextureFormat.RGBA32, false);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            big.ReadPixels(new Rect(0, 0, W * 2, H * 2), 0, 0);
            big.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            var src = big.GetPixels32();
            var dst = new Color32[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int i0 = (y * 2) * W * 2 + x * 2, i1 = i0 + W * 2;
                    Color32 a = src[i0], b = src[i0 + 1], c = src[i1], d = src[i1 + 1];
                    dst[y * W + x] = new Color32((byte)((a.r + b.r + c.r + d.r) >> 2), (byte)((a.g + b.g + c.g + d.g) >> 2), (byte)((a.b + b.b + c.b + d.b) >> 2), 255);
                }
            Object.DestroyImmediate(big);
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            tex.SetPixels32(dst);
            tex.Apply();
            Directory.CreateDirectory(Folder);
            File.WriteAllBytes(Folder + "/map_" + index + ".png", tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            Debug.Log("[MapPreviewBaker] map " + index + " (" + map.Name + ")");
        }

        [MenuItem("Neon Brawl/Art/Fix Map Preview Import")]
        public static void Import()
        {
            for (int i = 0; i < MapLayouts.All.Length; i++)
            {
                string path = Folder + "/map_" + i + ".png";
                var imp = AssetImporter.GetAtPath(path) as TextureImporter;
                if (imp == null) continue;
                imp.textureType = TextureImporterType.Sprite;
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.mipmapEnabled = false;
                imp.alphaSource = TextureImporterAlphaSource.None;
                imp.filterMode = FilterMode.Bilinear;
                imp.maxTextureSize = 1024;
                imp.textureCompression = TextureImporterCompression.CompressedHQ;
                imp.npotScale = TextureImporterNPOTScale.None;
                var android = imp.GetPlatformTextureSettings("Android");
                android.overridden = true;
                android.maxTextureSize = 1024;
                android.format = TextureImporterFormat.ASTC_6x6;
                android.compressionQuality = 100;
                imp.SetPlatformTextureSettings(android);
                imp.SaveAndReimport();
            }
        }
    }
}
