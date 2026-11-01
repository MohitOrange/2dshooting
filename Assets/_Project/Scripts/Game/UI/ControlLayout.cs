using System.Globalization;
using System.Text;
using UnityEngine;

namespace NeonBrawl
{
    /// <summary>The on-screen controls a player can move, resize and fade.</summary>
    public enum ControlId { Move, Fire, Reload, Swap, Grenade, Dash, Knife, Auto, Pickup }

    /// <summary>
    /// Where one control sits. The offset is measured from the nearest screen corner to the control's
    /// center (canvas units), so a layout keeps its feel on wide phones and on tablets.
    /// </summary>
    public struct ControlPlace
    {
        /// <summary>(0 or 1, 0 or 1): left/right, bottom/top.</summary>
        public Vector2 Corner;
        public Vector2 Offset;
        /// <summary>1 = normal size.</summary>
        public float Size;
        /// <summary>1 = fully visible.</summary>
        public float Alpha;
    }

    /// <summary>
    /// The player's touch layout: position, size and opacity of every stick and button, and whether the
    /// move stick follows the thumb. Saved in PlayerPrefs as one short text line.
    /// </summary>
    public sealed class ControlLayout
    {
        public const int Count = 9;
        public const float MinSize = 0.6f, MaxSize = 1.6f, MinAlpha = 0.25f, MaxAlpha = 1f;
        private const string Key = "nb.layout";
        private const string Version = "1";

        public static readonly string[] Names = { "MOVE STICK", "FIRE STICK", "RELOAD", "SWAP GUN", "GRENADE", "DASH", "KNIFE", "AUTO SHOOT", "PICK UP" };

        /// <summary>Unscaled size of each control (canvas units).</summary>
        public static readonly Vector2[] BaseSize =
        {
            new Vector2(290f, 290f), new Vector2(290f, 290f), new Vector2(126f, 126f), new Vector2(126f, 126f),
            new Vector2(134f, 134f), new Vector2(146f, 146f), new Vector2(112f, 112f), new Vector2(108f, 108f),
            new Vector2(260f, 150f),
        };

        // default spots: the move stick from the bottom-left corner, everything else from the bottom-right
        private static readonly Vector2[] DefaultOffset =
        {
            new Vector2(270f, 250f), new Vector2(-200f, 220f), new Vector2(-150f, 560f), new Vector2(-330f, 610f),
            new Vector2(-560f, 470f), new Vector2(-600f, 230f), new Vector2(-520f, 690f), new Vector2(-150f, 724f),
            new Vector2(-900f, 330f),
        };

        public readonly ControlPlace[] Items = new ControlPlace[Count];
        /// <summary>The move stick stays where it is drawn instead of jumping to your thumb.</summary>
        public bool FixedMove;

        public static ControlPlace Default(int i) => new ControlPlace
        {
            Corner = new Vector2(i == (int)ControlId.Move ? 0f : 1f, 0f),
            Offset = DefaultOffset[i],
            Size = 1f,
            Alpha = 1f,
        };

        public static ControlLayout Defaults()
        {
            var l = new ControlLayout();
            for (int i = 0; i < Count; i++) l.Items[i] = Default(i);
            return l;
        }

        public ControlLayout Clone()
        {
            var l = new ControlLayout { FixedMove = FixedMove };
            System.Array.Copy(Items, l.Items, Count);
            return l;
        }

        /// <summary>Center of a control measured from the bottom-left of an area of the given size.</summary>
        public static Vector2 Center(in ControlPlace p, Vector2 area) =>
            new Vector2(p.Corner.x > 0.5f ? area.x + p.Offset.x : p.Offset.x, p.Corner.y > 0.5f ? area.y + p.Offset.y : p.Offset.y);

        /// <summary>Stores a center (from the bottom-left) against the nearest corner.</summary>
        public static ControlPlace FromCenter(Vector2 center, Vector2 area, float size, float alpha)
        {
            var corner = new Vector2(center.x > area.x * 0.5f ? 1f : 0f, center.y > area.y * 0.5f ? 1f : 0f);
            return new ControlPlace
            {
                Corner = corner,
                Offset = new Vector2(corner.x > 0.5f ? center.x - area.x : center.x, corner.y > 0.5f ? center.y - area.y : center.y),
                Size = Mathf.Clamp(size, MinSize, MaxSize),
                Alpha = Mathf.Clamp(alpha, MinAlpha, MaxAlpha),
            };
        }

        // ------------------------------------------------------------------ save / load

        public static ControlLayout Load()
        {
            var l = Defaults();
            string s = PlayerPrefs.GetString(Key, "");
            if (string.IsNullOrEmpty(s)) return l;
            // "1|fixed|cornerX,cornerY,offX,offY,size,alpha;..." — anything unreadable keeps the default
            var parts = s.Split('|');
            if (parts.Length < 3 || parts[0] != Version) return l;
            l.FixedMove = parts[1] == "1";
            var items = parts[2].Split(';');
            for (int i = 0; i < Count && i < items.Length; i++)
            {
                var v = items[i].Split(',');
                if (v.Length < 6) continue;
                if (!F(v[0], out float cx) || !F(v[1], out float cy) || !F(v[2], out float ox) || !F(v[3], out float oy)
                    || !F(v[4], out float size) || !F(v[5], out float alpha)) continue;
                l.Items[i] = new ControlPlace
                {
                    Corner = new Vector2(cx > 0.5f ? 1f : 0f, cy > 0.5f ? 1f : 0f),
                    Offset = new Vector2(ox, oy),
                    Size = Mathf.Clamp(size, MinSize, MaxSize),
                    Alpha = Mathf.Clamp(alpha, MinAlpha, MaxAlpha),
                };
            }
            return l;
        }

        public void Save()
        {
            var sb = new StringBuilder(256);
            sb.Append(Version).Append('|').Append(FixedMove ? '1' : '0').Append('|');
            for (int i = 0; i < Count; i++)
            {
                var p = Items[i];
                if (i > 0) sb.Append(';');
                sb.Append(p.Corner.x > 0.5f ? '1' : '0').Append(',').Append(p.Corner.y > 0.5f ? '1' : '0').Append(',')
                  .Append(S(p.Offset.x)).Append(',').Append(S(p.Offset.y)).Append(',')
                  .Append(S(p.Size, "0.##")).Append(',').Append(S(p.Alpha, "0.##"));
            }
            PlayerPrefs.SetString(Key, sb.ToString());
        }

        private static bool F(string s, out float v) => float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v);
        private static string S(float v, string fmt = "0") => v.ToString(fmt, CultureInfo.InvariantCulture);
    }
}
