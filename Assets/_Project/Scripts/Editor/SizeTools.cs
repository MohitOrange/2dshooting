using System;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace NeonBrawl.EditorTools
{
    /// <summary>
    /// Keeps the shipped game small.
    /// After a Windows build: Vivox copies both its x64 and ARM64 voice libraries (16 MB each) into every
    /// PC build, but a player only ever loads the one matching its CPU, so the other folder is deleted.
    /// Menu "Physics SDKs": lists the 3D physics engines this Unity can ship (the game is 2D only).
    /// </summary>
    public sealed class SizeTools : IPostprocessBuildWithReport
    {
        public int callbackOrder => 100;

        public void OnPostprocessBuild(BuildReport report)
        {
            var s = report.summary;
            if (s.platform != BuildTarget.StandaloneWindows64 && s.platform != BuildTarget.StandaloneWindows) return;
            string dir = Path.GetDirectoryName(s.outputPath);
            string plugins = Path.Combine(dir, Path.GetFileNameWithoutExtension(s.outputPath) + "_Data", "Plugins");
            ushort machine = PeMachine(Path.Combine(dir, "UnityPlayer.dll"));
            string unused = machine == 0x8664 ? "ARM64" : machine == 0xAA64 ? "x86_64" : null;
            if (unused == null) return;
            string path = Path.Combine(plugins, unused);
            if (!Directory.Exists(path)) return;
            long bytes = 0;
            foreach (var f in Directory.GetFiles(path, "*", SearchOption.AllDirectories)) bytes += new FileInfo(f).Length;
            Directory.Delete(path, true);
            Debug.Log($"[NeonBrawl] removed unused {unused} plugins from the PC build ({bytes / (1024f * 1024f):0.0} MB)");
        }

        /// <summary>CPU a Windows .dll/.exe was built for (0x8664 = x64, 0xAA64 = ARM64), or 0.</summary>
        private static ushort PeMachine(string file)
        {
            try
            {
                using (var r = new BinaryReader(File.OpenRead(file)))
                {
                    r.BaseStream.Position = 0x3C;
                    int pe = r.ReadInt32();
                    r.BaseStream.Position = pe + 4;
                    return r.ReadUInt16();
                }
            }
            catch (Exception) { return 0; }
        }

        /// <summary>Id of Unity's "None" 3D physics SDK (from Log Physics SDKs).</summary>
        private const long NoPhysicsSdk = 3737844653;

        /// <summary>
        /// One-time project settings that shrink every build:
        /// - 3D physics SDK = None: the game only uses 2D physics (Box2D), so PhysX is left out.
        /// - Unity logo off (the splash is already off, but the logo texture still shipped).
        /// - PC: Direct3D 11 only (widest PC support for a 2D game). The D3D12 and DirectStorage DLLs
        ///   still ship: UnityPlayer.dll will not start without them (checked 2026-11-01).
        /// </summary>
        [MenuItem("Neon Brawl/Build/Apply Size Settings")]
        public static void ApplySizeSettings()
        {
            var dynamics = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/DynamicsManager.asset");
            if (dynamics.Length > 0)
            {
                var so = new SerializedObject(dynamics[0]);
                var id = so.FindProperty("m_CurrentBackendId");
                if (id != null && id.longValue != NoPhysicsSdk)
                {
                    id.longValue = NoPhysicsSdk;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            PlayerSettings.SplashScreen.showUnityLogo = false;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { UnityEngine.Rendering.GraphicsDeviceType.Direct3D11 });
            AssetDatabase.SaveAssets();
            Debug.Log("[NeonBrawl] size settings applied: physics SDK None, Unity logo off, PC Direct3D 11 only");
        }

        [MenuItem("Neon Brawl/Build/Log Physics SDKs")]
        public static void LogPhysicsSdks()
        {
            var sb = new StringBuilder("[NeonBrawl] 3D physics SDKs:");
            foreach (var m in typeof(Physics).GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                if (m.Name.Contains("Integration")) sb.Append("\n  sig: ").Append(m);
            // the managed overloads return spans, so read the native table through the pointer overload
            var infoType = typeof(Physics).Assembly.GetType("UnityEngine.IntegrationInfo");
            MethodInfo raw = null;
            foreach (var m in typeof(Physics).GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                if (m.Name == "GetIntegrationInfos" && m.GetParameters().Length == 2) raw = m;
            if (raw != null && infoType != null)
            {
                var args = new object[] { IntPtr.Zero, 0UL };
                raw.Invoke(null, args);
                var ptr = (IntPtr)args[0];
                ulong count = (ulong)args[1];
                int size = System.Runtime.InteropServices.Marshal.SizeOf(infoType);
                sb.Append("\n  count=").Append(count).Append(" size=").Append(size);
                for (ulong i = 0; i < count; i++)
                    sb.Append("\n  ").Append(Describe(System.Runtime.InteropServices.Marshal.PtrToStructure(new IntPtr(ptr.ToInt64() + (long)i * size), infoType)));
            }
            Debug.Log(sb.ToString());
            File.WriteAllText("Builds/physics_sdks.txt", sb.ToString());
        }

        private static MethodInfo NoArgs(string name)
        {
            foreach (var m in typeof(Physics).GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                if (m.Name == name && m.GetParameters().Length == 0) return m;
            return null;
        }

        private static string Describe(object o)
        {
            if (o == null) return "null";
            var sb = new StringBuilder();
            const BindingFlags all = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (var p in o.GetType().GetProperties(all))
                if (p.GetIndexParameters().Length == 0) sb.Append(p.Name).Append('=').Append(p.GetValue(o)).Append(' ');
            foreach (var f in o.GetType().GetFields(all)) sb.Append(f.Name).Append('=').Append(f.GetValue(o)).Append(' ');
            return sb.ToString();
        }
    }
}
