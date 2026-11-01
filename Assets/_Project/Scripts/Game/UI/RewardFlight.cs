using System;
using UnityEngine;
using UnityEngine.UI;

namespace NeonBrawl
{
    /// <summary>
    /// Coins or gems that burst out of a point and swoop into a wallet pill. Each arrival calls back (count-up,
    /// tick sound) and punches the target. Pieces are pooled; a flight allocates only its small record.
    /// Lives on a full-screen layer that should be the last sibling so the pieces fly over everything.
    /// </summary>
    public sealed class RewardFlight : MonoBehaviour
    {
        private const int MaxPieces = 40;

        private sealed class Flight
        {
            public RectTransform Target;
            public int Count, Arrived;
            public Action<int, int> OnArrive;
            public Action OnDone;
            public float Punch;
        }

        private sealed class Piece
        {
            public RectTransform Rt;
            public Image Img;
            public Vector2 A, B, C, D;
            public float T, Delay, Dur, Spin;
            public Flight Owner;
        }

        private RectTransform _layer;
        private Piece[] _pieces;
        private readonly Flight[] _flights = new Flight[4];

        public static RewardFlight Create(RectTransform parent)
        {
            var layer = UIKit.Rect(parent, "RewardFlight");
            UIKit.Stretch(layer);
            var f = layer.gameObject.AddComponent<RewardFlight>();
            f._layer = layer;
            f._pieces = new Piece[MaxPieces];
            for (int i = 0; i < MaxPieces; i++)
            {
                var img = UIKit.Image(layer, "Piece", null, Color.white);
                img.preserveAspect = true;
                img.enabled = false;
                UIKit.Place(img.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(70f, 70f));
                f._pieces[i] = new Piece { Rt = img.rectTransform, Img = img };
            }
            return f;
        }

        public bool Busy
        {
            get
            {
                for (int i = 0; i < _flights.Length; i++) if (_flights[i] != null) return true;
                return false;
            }
        }

        /// <summary>
        /// Sends <paramref name="count"/> icons from a world-space point (any UI element's position) to
        /// <paramref name="target"/>. <paramref name="onArrive"/> gets (arrived, count).
        /// </summary>
        public void Fly(Sprite icon, Vector3 fromWorld, RectTransform target, int count, Action<int, int> onArrive, Action onDone)
        {
            int slot = -1;
            for (int i = 0; i < _flights.Length; i++) if (_flights[i] == null) { slot = i; break; }
            if (slot < 0 || target == null) { onDone?.Invoke(); return; }
            var flight = new Flight { Target = target, OnArrive = onArrive, OnDone = onDone };
            Vector2 from = _layer.InverseTransformPoint(fromWorld);
            Vector2 to = _layer.InverseTransformPoint(target.position);
            int made = 0;
            for (int i = 0; i < _pieces.Length && made < count; i++)
            {
                var p = _pieces[i];
                if (p.Owner != null) continue;
                float ang = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                Vector2 burst = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * UnityEngine.Random.Range(110f, 240f);
                p.A = from;
                p.B = from + burst;
                p.C = Vector2.Lerp(from + burst, to, 0.55f) + new Vector2(UnityEngine.Random.Range(-140f, 140f), UnityEngine.Random.Range(60f, 200f));
                p.D = to;
                p.T = 0f;
                p.Delay = made * 0.045f;
                p.Dur = UnityEngine.Random.Range(0.65f, 0.85f);
                p.Spin = UnityEngine.Random.Range(-240f, 240f);
                p.Owner = flight;
                p.Img.sprite = icon;
                p.Img.enabled = false;
                p.Rt.anchoredPosition = from;
                made++;
            }
            flight.Count = made;
            if (made == 0) { onDone?.Invoke(); return; }
            _flights[slot] = flight;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < _pieces.Length; i++)
            {
                var p = _pieces[i];
                if (p.Owner == null) continue;
                p.T += dt;
                float t = (p.T - p.Delay) / p.Dur;
                if (t < 0f) continue;
                if (!p.Img.enabled) p.Img.enabled = true;
                if (t >= 1f)
                {
                    var f = p.Owner;
                    p.Owner = null;
                    p.Img.enabled = false;
                    f.Arrived++;
                    f.Punch = 1f;
                    f.OnArrive?.Invoke(f.Arrived, f.Count);
                    continue;
                }
                // fast burst out, brief hang, then accelerate into the wallet
                float e = t < 0.3f ? (1f - Mathf.Pow(1f - t / 0.3f, 3f)) * 0.3f : 0.3f + Mathf.Pow((t - 0.3f) / 0.7f, 2f) * 0.7f;
                p.Rt.anchoredPosition = Bezier(p.A, p.B, p.C, p.D, e);
                float s = t < 0.12f ? Mathf.Lerp(0.3f, 1.15f, t / 0.12f) : Mathf.Lerp(1.15f, 0.62f, (t - 0.12f) / 0.88f);
                p.Rt.localScale = new Vector3(s, s, 1f);
                p.Rt.localRotation = Quaternion.Euler(0f, 0f, p.Spin * t);
            }

            for (int i = 0; i < _flights.Length; i++)
            {
                var f = _flights[i];
                if (f == null) continue;
                if (f.Punch > 0f)
                {
                    f.Punch = Mathf.Max(0f, f.Punch - dt * 5f);
                    float s = 1f + f.Punch * 0.22f;
                    if (f.Target != null) f.Target.localScale = new Vector3(s, s, 1f);
                }
                if (f.Arrived >= f.Count && f.Punch <= 0f)
                {
                    _flights[i] = null;
                    if (f.Target != null) f.Target.localScale = Vector3.one;
                    f.OnDone?.Invoke();
                }
            }
        }

        private static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float t)
        {
            float u = 1f - t;
            return u * u * u * a + 3f * u * u * t * b + 3f * u * t * t * c + t * t * t * d;
        }
    }
}
