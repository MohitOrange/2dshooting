using System.Collections.Generic;
using UnityEngine;

namespace NeonBrawl.Core
{
    /// <summary>
    /// A* on the map grid (8 directions, no corner cutting). All buffers are allocated once, so
    /// searches create no garbage. Perf: ~0.05–0.2 ms per search on a 50×34 map (estimate; measure
    /// with the "Bots.Path" profiler marker).
    /// </summary>
    public sealed class GridPathfinder
    {
        private const float Diagonal = 1.41421356f;

        private readonly int _w;
        private readonly int _h;
        private readonly bool[] _walk;
        private readonly float[] _g;
        private readonly int[] _parent;
        private readonly int[] _openStamp;
        private readonly int[] _closedStamp;
        private int[] _heap;
        private float[] _heapF;
        private int _heapCount;
        private int _stamp;
        private readonly List<int> _scratch = new List<int>(256);

        private static readonly int[] Dx = { 1, -1, 0, 0, 1, 1, -1, -1 };
        private static readonly int[] Dy = { 0, 0, 1, -1, 1, -1, 1, -1 };

        public GridPathfinder(int width, int height)
        {
            _w = width;
            _h = height;
            int n = width * height;
            _walk = new bool[n];
            _g = new float[n];
            _parent = new int[n];
            _openStamp = new int[n];
            _closedStamp = new int[n];
            _heap = new int[n * 2];
            _heapF = new float[n * 2];
        }

        public int Width => _w;
        public int Height => _h;

        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < _w && y < _h;

        public bool IsWalkable(int x, int y) => InBounds(x, y) && _walk[y * _w + x];

        public void SetWalkable(int x, int y, bool walkable)
        {
            if (InBounds(x, y)) _walk[y * _w + x] = walkable;
        }

        /// <summary>Finds a path of cells from start to goal (inclusive). Returns false if unreachable.</summary>
        public bool FindPath(Vector2Int start, Vector2Int goal, List<Vector2Int> outPath, int maxExpanded = 5000)
        {
            outPath.Clear();
            if (!IsWalkable(goal.x, goal.y)) goal = NearestWalkable(goal, 3);
            if (!IsWalkable(start.x, start.y)) start = NearestWalkable(start, 2);
            if (!IsWalkable(start.x, start.y) || !IsWalkable(goal.x, goal.y)) return false;

            _stamp++;
            _heapCount = 0;
            int s = start.y * _w + start.x;
            int t = goal.y * _w + goal.x;
            _g[s] = 0f;
            _parent[s] = -1;
            _openStamp[s] = _stamp;
            Push(s, Heuristic(start.x, start.y, goal.x, goal.y));

            int expanded = 0;
            while (_heapCount > 0)
            {
                int cur = Pop();
                if (_closedStamp[cur] == _stamp) continue; // stale heap entry
                _closedStamp[cur] = _stamp;
                if (cur == t) { Build(t, outPath); return true; }
                if (++expanded > maxExpanded) return false;

                int cx = cur % _w;
                int cy = cur / _w;
                for (int d = 0; d < 8; d++)
                {
                    int nx = cx + Dx[d];
                    int ny = cy + Dy[d];
                    if (!IsWalkable(nx, ny)) continue;
                    if (d >= 4 && (!IsWalkable(cx + Dx[d], cy) || !IsWalkable(cx, cy + Dy[d]))) continue; // no corner cutting
                    int ni = ny * _w + nx;
                    if (_closedStamp[ni] == _stamp) continue;
                    float ng = _g[cur] + (d >= 4 ? Diagonal : 1f);
                    if (_openStamp[ni] == _stamp && ng >= _g[ni]) continue;
                    _openStamp[ni] = _stamp;
                    _g[ni] = ng;
                    _parent[ni] = cur;
                    Push(ni, ng + Heuristic(nx, ny, goal.x, goal.y));
                }
            }
            return false;
        }

        /// <summary>Removes waypoints that can be skipped with a clear straight line.</summary>
        public void SmoothPath(List<Vector2Int> path, float agentRadius = 0.4f)
        {
            if (path.Count < 3) return;
            int write = 1;
            int anchor = 0;
            for (int i = 2; i < path.Count; i++)
            {
                if (!HasLineOfSight(path[anchor], path[i], agentRadius))
                {
                    path[write++] = path[i - 1];
                    anchor = i - 1;
                }
            }
            path[write++] = path[path.Count - 1];
            path.RemoveRange(write, path.Count - write);
        }

        /// <summary>True when a disc of the given radius can slide between the two cell centers.</summary>
        public bool HasLineOfSight(Vector2Int a, Vector2Int b, float radius = 0.4f)
        {
            float ax = a.x + 0.5f, ay = a.y + 0.5f, bx = b.x + 0.5f, by = b.y + 0.5f;
            float dx = bx - ax, dy = by - ay;
            float len = Mathf.Sqrt(dx * dx + dy * dy);
            if (len < 0.001f) return true;
            float px = -dy / len * radius, py = dx / len * radius;
            int steps = Mathf.CeilToInt(len / 0.2f);
            for (int i = 0; i <= steps; i++)
            {
                float f = (float)i / steps;
                float x = ax + dx * f, y = ay + dy * f;
                if (!IsWalkable(Mathf.FloorToInt(x), Mathf.FloorToInt(y))) return false;
                if (!IsWalkable(Mathf.FloorToInt(x + px), Mathf.FloorToInt(y + py))) return false;
                if (!IsWalkable(Mathf.FloorToInt(x - px), Mathf.FloorToInt(y - py))) return false;
            }
            return true;
        }

        public Vector2Int NearestWalkable(Vector2Int c, int maxRadius)
        {
            if (IsWalkable(c.x, c.y)) return c;
            for (int r = 1; r <= maxRadius; r++)
                for (int y = -r; y <= r; y++)
                    for (int x = -r; x <= r; x++)
                        if ((Mathf.Abs(x) == r || Mathf.Abs(y) == r) && IsWalkable(c.x + x, c.y + y))
                            return new Vector2Int(c.x + x, c.y + y);
            return c;
        }

        private static float Heuristic(int x0, int y0, int x1, int y1)
        {
            int dx = Mathf.Abs(x1 - x0), dy = Mathf.Abs(y1 - y0);
            int mn = dx < dy ? dx : dy;
            int mx = dx < dy ? dy : dx;
            return mx + (Diagonal - 1f) * mn;
        }

        private void Build(int t, List<Vector2Int> outPath)
        {
            _scratch.Clear();
            for (int c = t; c != -1; c = _parent[c]) _scratch.Add(c);
            for (int i = _scratch.Count - 1; i >= 0; i--)
                outPath.Add(new Vector2Int(_scratch[i] % _w, _scratch[i] / _w));
        }

        private void Push(int node, float f)
        {
            if (_heapCount == _heap.Length)
            {
                System.Array.Resize(ref _heap, _heap.Length * 2);
                System.Array.Resize(ref _heapF, _heapF.Length * 2);
            }
            int i = _heapCount++;
            while (i > 0)
            {
                int p = (i - 1) >> 1;
                if (_heapF[p] <= f) break;
                _heap[i] = _heap[p];
                _heapF[i] = _heapF[p];
                i = p;
            }
            _heap[i] = node;
            _heapF[i] = f;
        }

        private int Pop()
        {
            int result = _heap[0];
            int last = --_heapCount;
            if (last > 0)
            {
                int node = _heap[last];
                float f = _heapF[last];
                int i = 0;
                while (true)
                {
                    int l = i * 2 + 1;
                    if (l >= last) break;
                    int r = l + 1;
                    int c = r < last && _heapF[r] < _heapF[l] ? r : l;
                    if (_heapF[c] >= f) break;
                    _heap[i] = _heap[c];
                    _heapF[i] = _heapF[c];
                    i = c;
                }
                _heap[i] = node;
                _heapF[i] = f;
            }
            return result;
        }
    }
}
