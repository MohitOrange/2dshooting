using System.Collections.Generic;
using UnityEngine;

namespace NeonBrawl
{
    /// <summary>
    /// Classic top-down line of sight: everything the viewer cannot see past walls is darkened, and
    /// enemies there are hidden. Built as one mesh (a ring from the visibility edge outward), so it
    /// costs one draw call. The 240 rays only reach the screen edge and are skipped while the viewer
    /// stands still (walls never move). Perf: 0 B GC/frame; ~0.64 ms in the editor before, rays now
    /// about 2.5x shorter and none at all when standing still.
    /// </summary>
    public sealed class FogOfView : MonoBehaviour
    {
        private const int Rays = 240;
        private const float MaxDist = 34f;
        private const float Soft = 0.8f;
        private const float WallReveal = 0.45f;

        private Mesh _mesh;
        private MeshRenderer _renderer;
        private Vector3[] _verts;
        private Vector2[] _dirs;
        private Fighter _viewer;
        private List<Fighter> _fighters;
        private MatchController _match;
        private bool _enabled;
        private Camera _cam;
        private Vector2 _lastOrigin = new Vector2(float.MaxValue, float.MaxValue);
        private float _lastReach;

        public void Init(MatchController match, ArtLibrary art, bool enabled, bool night)
        {
            _match = match;
            _cam = Camera.main;
            _fighters = match.Fighters;
            _enabled = enabled;
            _verts = new Vector3[Rays * 3];
            _dirs = new Vector2[Rays];
            var colors = new Color32[Rays * 3];
            var tris = new int[Rays * 12];
            byte a = (byte)((night ? 0.97f : 0.6f) * 255f);
            for (int i = 0; i < Rays; i++)
            {
                float ang = i * Mathf.PI * 2f / Rays;
                _dirs[i] = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                colors[i * 3] = new Color32(0, 0, 0, 0);
                colors[i * 3 + 1] = new Color32(0, 0, 0, a);
                colors[i * 3 + 2] = new Color32(0, 0, 0, a);
                int j = (i + 1) % Rays;
                int a0 = i * 3, a1 = a0 + 1, a2 = a0 + 2, b0 = j * 3, b1 = b0 + 1, b2 = b0 + 2;
                int t = i * 12;
                tris[t] = a0; tris[t + 1] = a1; tris[t + 2] = b1;
                tris[t + 3] = a0; tris[t + 4] = b1; tris[t + 5] = b0;
                tris[t + 6] = a1; tris[t + 7] = a2; tris[t + 8] = b2;
                tris[t + 9] = a1; tris[t + 10] = b2; tris[t + 11] = b1;
            }
            _mesh = new Mesh { name = "LineOfSight" };
            _mesh.MarkDynamic();
            _mesh.vertices = _verts;
            _mesh.colors32 = colors;
            _mesh.triangles = tris;

            gameObject.AddComponent<MeshFilter>().sharedMesh = _mesh;
            _renderer = gameObject.AddComponent<MeshRenderer>();
            _renderer.sharedMaterial = art.VertexColor;
            _renderer.sortingOrder = SortOrder.Fog;
            _renderer.enabled = false;
        }

        public void SetViewer(Fighter viewer)
        {
            _viewer = viewer;
            _lastOrigin = new Vector2(float.MaxValue, float.MaxValue);
        }

        /// <summary>How far rays must go: the screen corner farthest from the viewer, plus a margin.</summary>
        private float Reach(Vector2 o)
        {
            if (_cam == null) _cam = Camera.main;
            if (_cam == null || !_cam.orthographic) return MaxDist;
            float h = _cam.orthographicSize, w = h * _cam.aspect;
            Vector2 c = _cam.transform.position;
            float dx = Mathf.Abs(c.x - o.x) + w, dy = Mathf.Abs(c.y - o.y) + h;
            return Mathf.Min(MaxDist, Mathf.Sqrt(dx * dx + dy * dy) + 2f);
        }

        private void LateUpdate()
        {
            if (_viewer == null || !_enabled)
            {
                _renderer.enabled = false;
                for (int i = 0; i < _fighters.Count; i++) _fighters[i].SetSeen(true);
                return;
            }

            Vector2 o = _viewer.Position;
            float reach = Reach(o);
            if ((o - _lastOrigin).sqrMagnitude > 0.0004f || reach > _lastReach + 0.5f)
            {
                _lastOrigin = o;
                _lastReach = reach;
                for (int i = 0; i < Rays; i++)
                {
                    Vector2 d = _dirs[i];
                    var hit = Physics2D.Raycast(o, d, reach, Layers.WallMask);
                    float dist = hit.collider != null ? Mathf.Min(hit.distance + WallReveal, reach) : reach;
                    _verts[i * 3] = new Vector3(o.x + d.x * dist, o.y + d.y * dist, 0f);
                    _verts[i * 3 + 1] = new Vector3(o.x + d.x * (dist + Soft), o.y + d.y * (dist + Soft), 0f);
                    _verts[i * 3 + 2] = new Vector3(o.x + d.x * (reach + 8f), o.y + d.y * (reach + 8f), 0f);
                }
                _mesh.SetVertices(_verts);
                _mesh.bounds = new Bounds(new Vector3(o.x, o.y, 0f), new Vector3(100f, 100f, 1f));
            }
            _renderer.enabled = true;

            for (int i = 0; i < _fighters.Count; i++)
            {
                var f = _fighters[i];
                if (f == _viewer) { f.SetSeen(true); continue; }
                bool seen = (f.Position - o).sqrMagnitude < MaxDist * MaxDist &&
                            (!_match.AreEnemies(_viewer, f) || Combat.HasLineOfSight(o, f.Position));
                f.SetSeen(seen);
            }
        }
    }
}
