using UnityEditor;
using UnityEngine;

namespace NeonBrawl.EditorTools
{
    /// <summary>
    /// Import rules for the generated sound effects (Resources/Sfx, made by Tools/Sfx/gen-sfx.js):
    /// mono, ADPCM (cheap to decode, ~3.5x smaller), decompressed on load so rapid fire never stutters.
    /// </summary>
    public sealed class SfxImport : AssetPostprocessor
    {
        private const string Folder = "Assets/_Project/Resources/Sfx/";

        private void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(Folder)) return;
            var imp = (AudioImporter)assetImporter;
            imp.forceToMono = true;
            imp.loadInBackground = false;
            var s = imp.defaultSampleSettings;
            s.loadType = AudioClipLoadType.DecompressOnLoad;
            s.compressionFormat = AudioCompressionFormat.ADPCM;
            s.preloadAudioData = true;
            s.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            imp.defaultSampleSettings = s;
            imp.ClearSampleSettingOverride("Android");
        }

        [MenuItem("Neon Brawl/Audio/Reimport Sound Effects")]
        private static void Reimport()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { Folder.TrimEnd('/') }))
                AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
            Debug.Log("[Sfx] reimported");
        }
    }
}
