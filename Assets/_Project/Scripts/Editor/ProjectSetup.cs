using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace NeonBrawl.EditorTools
{
    /// <summary>
    /// One-click project setup: physics layer names, player settings (landscape, Android IL2CPP ARM64,
    /// app id), the GameConfig tuning asset and the Game scene. Safe to run again.
    /// </summary>
    public static class ProjectSetup
    {
        public const string ScenePath = "Assets/_Project/Scenes/Game.unity";
        public const string ConfigPath = "Assets/_Project/Resources/GameConfig.asset";
        public const string AppId = "com.mohitgames.neonbrawl";

        [MenuItem("Neon Brawl/Setup Project")]
        public static void SetupAll()
        {
            SetupLayers();
            SetupPlayerSettings();
            SetupSizeSettings();
            CreateConfig();
            CreateScene();
            AssetDatabase.SaveAssets();
            Debug.Log("[NeonBrawl] Project setup complete. Open " + ScenePath + " and press Play.");
        }

        public static void SetupLayers()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0) return;
            var so = new SerializedObject(assets[0]);
            var layers = so.FindProperty("layers");
            SetLayer(layers, Layers.Walls, "Walls");
            SetLayer(layers, Layers.Props, "Props");
            SetLayer(layers, Layers.Fighters, "Fighters");
            SetLayer(layers, Layers.Corpses, "Corpses");
            SetLayer(layers, Layers.Water, "Water");
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetLayer(SerializedProperty layers, int index, string name)
        {
            var p = layers.GetArrayElementAtIndex(index);
            if (string.IsNullOrEmpty(p.stringValue) || p.stringValue == name) p.stringValue = name;
            else Debug.LogWarning($"[NeonBrawl] Layer {index} is already '{p.stringValue}', expected '{name}'.");
        }

        public static void SetupPlayerSettings()
        {
            PlayerSettings.companyName = "MohitGames";
            PlayerSettings.productName = "Neon Brawl";
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.Android.bundleVersionCode = 1;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, AppId);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, AppId);

            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;

            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

            PlayerSettings.runInBackground = true;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.resizableWindow = true;
        }

        public const string RendererPath = "Assets/Settings/Renderer2D.asset";

        /// <summary>Uses the painted U8 app icon (from the art catalog) as the default icon for every platform.</summary>
        [MenuItem("Neon Brawl/Art/Apply App Icon")]
        public static void ApplyAppIcon()
        {
            var cat = AssetDatabase.LoadAssetAtPath<ArtCatalog>("Assets/_Project/Resources/ArtCatalog.asset");
            if (cat == null || cat.AppIcon == null) { Debug.LogWarning("[NeonBrawl] No app icon in the art catalog yet."); return; }
            var tex = cat.AppIcon.texture;
            var path = AssetDatabase.GetAssetPath(tex);
            if (AssetImporter.GetAtPath(path) is TextureImporter ti && ti.textureCompression != TextureImporterCompression.Uncompressed)
            {
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.SaveAndReimport();
            }
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { tex }, IconKind.Any);
            AssetDatabase.SaveAssets();
            Debug.Log("[NeonBrawl] App icon set from " + path);
        }

        /// <summary>
        /// Small-APK settings: strong code stripping (Assets/_Project/link.xml keeps our code and the
        /// Input System), size-first IL2CPP, R8 for Java code, no splash, no post-processing data.
        /// </summary>
        [MenuItem("Neon Brawl/Apply Small Build Settings")]
        public static void SetupSizeSettings()
        {
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.High);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Standalone, ManagedStrippingLevel.High);
            PlayerSettings.stripEngineCode = true;
            PlayerSettings.SetIl2CppCodeGeneration(NamedBuildTarget.Android, Il2CppCodeGeneration.OptimizeSize);
            PlayerSettings.Android.minifyRelease = true;
            PlayerSettings.SplashScreen.show = false;

            // The game uses no Volumes, so the post-processing data (film grain textures, UberPost
            // shader, SMAA tables) is dead weight. Same as unticking "Post-processing" on the renderer.
            var renderer = AssetDatabase.LoadMainAssetAtPath(RendererPath);
            if (renderer != null)
            {
                var so = new SerializedObject(renderer);
                var pp = so.FindProperty("m_PostProcessData");
                if (pp != null && pp.objectReferenceValue != null)
                {
                    pp.objectReferenceValue = null;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(renderer);
                }
            }
        }

        public static void CreateConfig()
        {
            if (AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath) != null) return;
            var cfg = ScriptableObject.CreateInstance<GameConfig>();
            cfg.ResetToDefaults();
            AssetDatabase.CreateAsset(cfg, ConfigPath);
        }

        public static void CreateScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            camGo.transform.position = new Vector3(25f, 17f, -10f);
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 6.5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.07f, 0.06f, 0.05f);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 50f;
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<UniversalAdditionalCameraData>();

            var lightGo = new GameObject("Global Light 2D");
            var light = lightGo.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.intensity = 1f;
            light.color = new Color(1f, 0.96f, 0.88f);
            Lights.ApplyAllLayers(light);

            // 2D game: no skybox reflections (saves a baked cubemap in the build).
            RenderSettings.skybox = null;
            RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = null;

            var appGo = new GameObject("GameApp");
            var app = appGo.AddComponent<GameApp>();
            var so = new SerializedObject(app);
            so.FindProperty("_camera").objectReferenceValue = cam;
            so.FindProperty("_globalLight").objectReferenceValue = light;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }
    }
}
