using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace NeonBrawl.EditorTools
{
    /// <summary>
    /// Bakes the high-resolution UI kit (<see cref="UiArt"/>: panels, icons, rewards) into PNG sprites and
    /// points the art catalog at them, replacing the blurry 30–60 px pieces cut from the collage.
    /// Sizes on screen stay the same: the pixels-per-unit of each new sprite is matched to the old one.
    /// </summary>
    public static class UiArtBaker
    {
        private const string Dir = "Assets/_Project/Art/UIHD";
        private const string CatalogPath = "Assets/_Project/Resources/ArtCatalog.asset";

        [MenuItem("Neon Brawl/Art/Bake HD UI Kit")]
        public static void Bake()
        {
            var cat = AssetDatabase.LoadAssetAtPath<ArtCatalog>(CatalogPath);
            if (cat == null) { Debug.LogError("[NeonBrawl] ArtCatalog not found at " + CatalogPath); return; }
            Directory.CreateDirectory(Dir);
            cat.Panels = BakeSet("panel", UiArt.PanelCount, UiArt.PaintPanel, cat.Panels, true);
            cat.Icons = BakeSet("icon", UiArt.IconCount, UiArt.PaintIcon, cat.Icons, false);
            cat.Rewards = BakeSet("reward", UiArt.RewardCount, UiArt.PaintReward, cat.Rewards, false);
            EditorUtility.SetDirty(cat);
            AssetDatabase.SaveAssets();
            Debug.Log("[NeonBrawl] HD UI kit baked: " + UiArt.PanelCount + " panels, " + UiArt.IconCount + " icons, " + UiArt.RewardCount + " rewards");
        }

        private static Sprite[] BakeSet(string name, int count, Action<SdfCanvas, int> paint, Sprite[] old, bool panel)
        {
            int n = UiArt.Cell;
            var result = new Sprite[count];
            for (int i = 0; i < count; i++)
            {
                var px = new Color[n * n];
                paint(new SdfCanvas(px, n, 0, 0, n), i);
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
                tex.SetPixels(px);
                tex.Apply();
                string path = $"{Dir}/{name}_{i:00}.png";
                File.WriteAllBytes(path, tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

                Sprite before = old != null && i < old.Length ? old[i] : null;
                var ti = (TextureImporter)AssetImporter.GetAtPath(path);
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.alphaIsTransparency = true;
                ti.mipmapEnabled = true;            // shown smaller than 256 px: mips keep edges smooth, not jagged
                ti.filterMode = FilterMode.Trilinear;
                ti.maxTextureSize = n;
                ti.textureCompression = TextureImporterCompression.CompressedHQ;
                if (panel)
                {
                    // keep the frame as thick on screen as before: same border-size-to-ppu ratio
                    float oldBorder = before != null ? Mathf.Max(1f, before.border.x) : 8f;
                    float oldPpu = before != null ? before.pixelsPerUnit : 96f;
                    ti.spritePixelsPerUnit = oldPpu * UiArt.PanelBorder / oldBorder;
                    ti.spriteBorder = new Vector4(UiArt.PanelBorder, UiArt.PanelBorder, UiArt.PanelBorder, UiArt.PanelBorder);
                }
                else
                {
                    // same size in the world as the old piece (coins and gems also fly around the camp)
                    float oldSize = before != null ? Mathf.Max(before.bounds.size.x, before.bounds.size.y) : 0.4f;
                    ti.spritePixelsPerUnit = n / Mathf.Max(0.05f, oldSize);
                }
                var s = new TextureImporterSettings();
                ti.ReadTextureSettings(s);
                s.spriteMeshType = SpriteMeshType.FullRect;
                s.spriteAlignment = (int)SpriteAlignment.Center;
                ti.SetTextureSettings(s);
                ti.SaveAndReimport();
                result[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
            return result;
        }
    }
}
