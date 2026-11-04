using UnityEngine;
using UnityEngine.UI;

namespace NeonBrawl
{
    /// <summary>
    /// Settings ▸ ABOUT: the developer card (the same "about me" as the other games): name, birthday,
    /// a short bio and the skills table, plus the game's version and the privacy policy.
    /// </summary>
    public static class AboutPage
    {
        public const string Name = "MOHIT";
        public const string Born = "24.03.2005";
        public const string Role = "GAME DEVELOPER";

        public const string Bio =
            "I'm a passionate technology enthusiast and game developer with a strong interest in Unity, Unreal Engine, " +
            "AI, and creative digital experiences. I love turning ideas into interactive projects, learning new " +
            "technologies, and building innovative solutions that combine creativity with technology.";

        public static readonly string[,] Skills =
        {
            { "GAME DEV", "Unity, Unreal Engine, 2D/3D games" },
            { "PROGRAMMING", "C#, JavaScript, Java, Python, PHP/Laravel" },
            { "AI / ML", "AI applications, ML projects" },
            { "WEB DEV", "React, Node.js, MongoDB, MySQL" },
            { "3D & DESIGN", "Blender, Maya, UI/UX" },
            { "BLOCKCHAIN", "Ethereum, Polygon, Arbitrum, Fuel" },
            { "CREATIVE TECH", "Video editing, short films, game concepts" },
        };

        public static void Build(RectTransform page, GameContext ctx)
        {
            var art = ctx.Art;
            Color accent = new Color(1f, 0.62f, 0.25f);

            // header: monogram, name, role and birthday
            var avatarGlow = UIKit.Image(page, "AvatarGlow", art.Glow, new Color(accent.r, accent.g, accent.b, 0.45f));
            UIKit.Place(avatarGlow.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(70f, -62f), new Vector2(190f, 190f));
            avatarGlow.gameObject.AddComponent<Pulse>().Amount = 0.08f;
            var ring = UIKit.Image(page, "AvatarRing", art.Circle, new Color(0.03f, 0.04f, 0.08f));
            UIKit.Place(ring.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(70f, -62f), new Vector2(122f, 122f));
            var avatar = UIKit.Image(page, "Avatar", art.Circle, Color.white);
            UIKit.Place(avatar.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(70f, -62f), new Vector2(110f, 110f));
            var g = avatar.gameObject.AddComponent<UIGradient>();
            g.Top = new Color(1f, 0.78f, 0.35f);
            g.Bottom = new Color(0.95f, 0.35f, 0.25f);
            var initial = UIKit.Title(avatar.transform, "M", "M", 70, Color.white, UIKit.Cream);
            UIKit.Stretch(initial.rectTransform);
            initial.rectTransform.anchoredPosition = new Vector2(0f, 3f);

            var name = UIKit.Title(page, "Name", Name, 64, UIKit.Cream, UIKit.Gold, TextAnchor.MiddleLeft);
            UIKit.Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(156f, -40f), new Vector2(560f, 76f));
            var role = UIKit.Label(page, "Role", Role + "   ·   BORN " + Born, 26, UIKit.Sky, TextAnchor.MiddleLeft);
            UIKit.Place(role.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(158f, -94f), new Vector2(700f, 36f));
            var who = UIKit.Label(page, "Whoami", "> whoami", 24, new Color(0.45f, 1f, 0.55f), TextAnchor.MiddleRight);
            UIKit.Place(who.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(-12f, -30f), new Vector2(260f, 34f));

            // bio
            var bioBg = UIKit.Image(page, "BioBg", art.RoundRect, new Color(1f, 1f, 1f, 0.05f), true);
            UIKit.Place(bioBg.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -132f), new Vector2(1040f, 118f));
            var bio = UIKit.Label(bioBg.transform, "Bio", Bio, 23, new Color(0.86f, 0.9f, 0.98f), TextAnchor.MiddleLeft, false);
            UIKit.Stretch(bio.rectTransform, 18f);
            bio.horizontalOverflow = HorizontalWrapMode.Wrap;
            bio.lineSpacing = 1.1f;

            // skills: two aligned columns
            var skillsBg = UIKit.Image(page, "SkillsBg", art.RoundRect, new Color(0f, 0f, 0f, 0.28f), true);
            UIKit.Place(skillsBg.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -262f), new Vector2(1040f, 262f));
            int n = Skills.GetLength(0);
            for (int i = 0; i < n; i++)
            {
                float y = -18f - i * 34f;
                var dot = UIKit.Image(skillsBg.transform, "Dot" + i, art.Circle, accent);
                UIKit.Place(dot.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(30f, y - 15f), new Vector2(10f, 10f));
                var k = UIKit.Label(skillsBg.transform, "Skill" + i, Skills[i, 0], 22, UIKit.Gold, TextAnchor.MiddleLeft);
                UIKit.Place(k.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(48f, y), new Vector2(250f, 32f));
                var v = UIKit.Label(skillsBg.transform, "Detail" + i, Skills[i, 1], 22, new Color(0.88f, 0.92f, 1f), TextAnchor.MiddleLeft, false);
                UIKit.Place(v.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(300f, y), new Vector2(720f, 32f));
            }

            // game + privacy
            var ver = UIKit.Label(page, "Version", "NEON BRAWL  v" + Application.version + "   ·   MADE WITH UNITY", 22, new Color(0.62f, 0.68f, 0.8f), TextAnchor.MiddleLeft);
            UIKit.Place(ver.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0.5f), new Vector2(6f, 30f), new Vector2(680f, 34f));
            var privacy = UIKit.ArtButton(page, "Privacy", "PRIVACY POLICY", ArtIds.PanelBlue, new Vector2(300f, 64f), 26, () => Application.OpenURL(AdIds.PrivacyPolicyUrl));
            UIKit.Place((RectTransform)privacy.transform, new Vector2(1f, 0f), new Vector2(1f, 0.5f), new Vector2(0f, 30f), new Vector2(300f, 64f));
        }
    }
}
