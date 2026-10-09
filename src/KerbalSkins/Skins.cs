using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Keystone;
using UnityEngine;

#if !DEV
[assembly: KSPAssembly("KerbalSkins", 0, 1)]
[assembly: KSPAssemblyDependency("Keystone", 0, 4)]
#endif

namespace KerbalSkins
{
    /// <summary>
    /// The colour of the kerbals' skin: one colour for everyone, a different one for each by name, or a
    /// colour of your own choosing for any one of them.
    ///
    /// A kerbal's head is drawn with one picture (one for the men, one for the women) that has the skin,
    /// the hair and the inside of the mouth all on it. The mod makes a copy of that picture for each skin
    /// colour in use, in which only the skin has been changed: every point that is the kerbals' own
    /// yellow-green is given the new colour, as light or as dark as it was (so the shading round the eyes
    /// and under the hair stays), and hair, lips, teeth and tongue are left alone. The copy is handed to
    /// that kerbal's head alone. Everything that shows the kerbal shows the new skin: the cockpit view, the
    /// portraits, and the kerbal outside the ship.
    ///
    /// Making a copy is a million points of arithmetic, done on another thread; the game does not wait for it.
    /// </summary>
    [KSPAddon(KSPAddon.Startup.Instantly, true)]
    public sealed class SkinsAtStart : MonoBehaviour
    {
        void Awake() { Skins.Setup(); Destroy(gameObject); }
    }

    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public sealed class Skins : MonoBehaviour
    {
        public const string Version = "0.1.2";

        /// <summary>The kerbals' own colour, as it is in the game's pictures (the middle one of all their skin's points).</summary>
        public static readonly Color Classic = new Color(198f / 255f, 212f / 255f, 127f / 255f, 1f);

        // Colours to pick from with one click, and what "a different one for each" draws on.
        static readonly Color[] Greens =
        {
            Classic, Rgb(176, 204, 110), Rgb(150, 190, 96), Rgb(205, 222, 150), Rgb(170, 180, 92), Rgb(128, 170, 104), Rgb(214, 214, 120), Rgb(140, 196, 140),
        };
        static readonly Color[] Any =
        {
            Classic, Rgb(150, 190, 96), Rgb(128, 170, 104), Rgb(110, 190, 170), Rgb(120, 170, 215), Rgb(160, 150, 215), Rgb(215, 160, 200), Rgb(240, 200, 175),
            Rgb(225, 180, 140), Rgb(190, 140, 100), Rgb(140, 95, 65), Rgb(95, 65, 50), Rgb(235, 225, 205), Rgb(170, 175, 180), Rgb(235, 215, 110), Rgb(225, 140, 90),
        };

        static Color Rgb(int r, int g, int b) => new Color(r / 255f, g / 255f, b / 255f, 1f);

        static Mod mod;
        static Toggle on;
        static Tint everyone;
        static Choice variety;

        // The game's own head pictures, as points that can be read (taken off the graphics card once), and the copies made of them.
        static readonly Dictionary<Texture, Color32[]> sources = new Dictionary<Texture, Color32[]>();
        static readonly Dictionary<string, Made> made = new Dictionary<string, Made>();
        sealed class Made { public Texture2D picture; public float used; public Task<Color32[]> making; public Texture from; }

        readonly List<Who> here = new List<Who>();
        sealed class Who { public string name; public Component kerbal; public bool outside; }
        MaterialPropertyBlock block;
        float nextLook;
        string open;                                    // whose colour is being changed in the window
        static readonly int MainTex = Shader.PropertyToID("_MainTex");

        /// <summary>Say that the mod is here and what can be set about it: once, when the game starts, so that its page is in the window in every scene.</summary>
        public static void Setup()
        {
            if (mod == null || Kit.Find("Kerbal Skins") != mod)
            {
                mod = Kit.Register("Kerbal Skins", Version, "The colour of the kerbals' skin: one for everyone, a different one for each, or your own choice for any one of them. Only the skin changes; hair, eyes and suits stay.");
                on = mod.Toggle("on", "Change skin colours", true, "Off: every kerbal is the game's own green again.");
                everyone = mod.Tint("everyone", "Everyone", Classic, "The colour of every kerbal who has not been given one of their own below.");
                variety = mod.Choice("variety", "Each their own", 0, new[] { "no", "greens", "any colour" },
                    "Give every kerbal without a colour of their own a different one, always the same one for the same name: shades of green, or any colour at all.");
            }
        }

        void Awake()
        {
            Setup();
            mod.Panel = Panel;
            block = new MaterialPropertyBlock();
        }

        void OnDestroy()
        {
            if (mod != null && mod.Panel == Panel) mod.Panel = null;
            Clear();
            // (the copies are for this flight's kerbals: the next flight makes its own)
            foreach (Made one in made.Values) if (one.picture != null) Destroy(one.picture);
            made.Clear();
            sources.Clear();
        }

        // ---- who gets which colour

        static string KeyOf(string name) => "skin_" + name.Replace(' ', '_');

        static bool Own(string name, out Color colour)
        {
            colour = Classic;
            string text = mod.Kept(KeyOf(name));
            if (string.IsNullOrEmpty(text)) return false;
            string[] parts = text.Split(',');
            if (parts.Length < 3) return false;
            try { colour = new Color(float.Parse(parts[0], CultureInfo.InvariantCulture), float.Parse(parts[1], CultureInfo.InvariantCulture), float.Parse(parts[2], CultureInfo.InvariantCulture), 1f); }
            catch (Exception) { return false; }
            return true;
        }

        /// <summary>The colour a kerbal's skin is to be.</summary>
        public static Color ColourOf(string name)
        {
            if (Own(name, out Color own)) return own;
            if (variety.Value == 0) return everyone.Value;
            // (the same name always draws the same colour)
            uint sum = 2166136261;
            foreach (char letter in name) sum = unchecked((sum ^ letter) * 16777619);
            Color[] from = variety.Value == 1 ? Greens : Any;
            return from[(sum >> 8) % (uint)from.Length];
        }

        static bool IsClassic(Color colour) => Mathf.Abs(colour.r - Classic.r) + Mathf.Abs(colour.g - Classic.g) + Mathf.Abs(colour.b - Classic.b) < 0.02f;

        // ---- putting the colours on

        void Update()
        {
            if (Time.unscaledTime < nextLook) return;
            nextLook = Time.unscaledTime + (open != null ? 0.1f : 0.5f);
            Look();
            if (!on.Value) { Clear(); return; }
            float now = Time.unscaledTime;
            foreach (Who who in here) Dress(who, ColourOf(who.name), now);
            // Copies nobody has worn for a while are thrown away (one is made for every colour passed through while a slider is dragged).
            List<string> stale = null;
            foreach (KeyValuePair<string, Made> one in made)
                if (one.Value.making == null && now - one.Value.used > 4f) (stale ?? (stale = new List<string>())).Add(one.Key);
            if (stale != null)
                foreach (string key in stale) { Destroy(made[key].picture); made.Remove(key); }
        }

        /// <summary>Find the kerbals there are: those sitting in the ship being flown and those standing or floating outside.</summary>
        void Look()
        {
            here.Clear();
            foreach (Kerbal kerbal in FindObjectsOfType<Kerbal>())
                if (kerbal != null && kerbal.protoCrewMember != null) here.Add(new Who { name = kerbal.protoCrewMember.name, kerbal = kerbal });
            foreach (KerbalEVA kerbal in FindObjectsOfType<KerbalEVA>())
                if (kerbal != null && kerbal.part != null && kerbal.part.protoModuleCrew != null && kerbal.part.protoModuleCrew.Count > 0)
                    here.Add(new Who { name = kerbal.part.protoModuleCrew[0].name, kerbal = kerbal, outside = true });
        }

        static bool IsHead(Texture picture) => picture != null && (picture.name == "kerbalHead" || picture.name.StartsWith("kerbalGirl", StringComparison.Ordinal));

        void Dress(Who who, Color colour, float now)
        {
            bool classic = IsClassic(colour);
            foreach (Renderer renderer in who.kerbal.GetComponentsInChildren<Renderer>(true))
            {
                Material material = renderer.sharedMaterial;
                if (material == null || !material.HasProperty(MainTex)) continue;
                Texture own = material.mainTexture;
                if (!IsHead(own)) continue;
                Texture2D wanted = classic ? null : Copy(own, colour, now);
                if (!classic && wanted == null) continue;            // (still being made: it keeps what it has meanwhile)
                renderer.GetPropertyBlock(block);
                Texture has = block.GetTexture(MainTex);
                if (classic)
                {
                    if (has == null) continue;
                    block.Clear();                                   // (nothing else of ours or the game's is set this way on a head)
                    renderer.SetPropertyBlock(null);
                    continue;
                }
                if (has == wanted) continue;
                block.SetTexture(MainTex, wanted);
                renderer.SetPropertyBlock(block);
            }
        }

        /// <summary>Every head back to the game's own picture.</summary>
        void Clear()
        {
            foreach (Who who in here)
            {
                if (who.kerbal == null) continue;
                foreach (Renderer renderer in who.kerbal.GetComponentsInChildren<Renderer>(true))
                {
                    Material material = renderer.sharedMaterial;
                    if (material == null || !material.HasProperty(MainTex) || !IsHead(material.mainTexture)) continue;
                    renderer.GetPropertyBlock(block);
                    if (block.GetTexture(MainTex) != null) renderer.SetPropertyBlock(null);
                }
            }
        }

        /// <summary>The copy of a head picture with the skin in a colour: made if there is none yet (nothing is given back until it is ready).</summary>
        static Texture2D Copy(Texture own, Color colour, float now)
        {
            var c = (Color32)colour;
            string key = own.GetInstanceID() + ":" + c.r.ToString("X2") + c.g.ToString("X2") + c.b.ToString("X2");
            if (!made.TryGetValue(key, out Made one))
            {
                // (one at a time: dragging a slider asks for a colour every tenth of a second)
                foreach (Made other in made.Values) if (other.making != null && !other.making.IsCompleted) return null;
                if (!sources.TryGetValue(own, out Color32[] points)) sources[own] = points = Read(own);
                Color tint = colour;
                one = new Made { used = now, from = own, making = Task.Run(() => Recolour(points, tint)) };
                made[key] = one;
                return null;
            }
            one.used = now;
            if (one.making != null)
            {
                if (!one.making.IsCompleted) return null;
                Task<Color32[]> done = one.making;
                one.making = null;
                if (done.IsFaulted) { Kit.Log("Kerbal Skins", "a skin could not be made: " + done.Exception.InnerException.Message); made.Remove(key); return null; }
                var picture = new Texture2D(own.width, own.height, TextureFormat.RGBA32, true) { name = own.name + " " + key, wrapMode = own.wrapMode, filterMode = own.filterMode, anisoLevel = own.anisoLevel };
                picture.SetPixels32(done.Result);
                picture.Apply(true, true);
                one.picture = picture;
            }
            return one.picture;
        }

        /// <summary>A picture of the game's as points that can be read (its own copy is on the graphics card only).</summary>
        static Color32[] Read(Texture picture)
        {
            RenderTexture onto = RenderTexture.GetTemporary(picture.width, picture.height, 0, RenderTextureFormat.ARGB32);
            RenderTexture before = RenderTexture.active;
            Graphics.Blit(picture, onto);
            RenderTexture.active = onto;
            var copy = new Texture2D(picture.width, picture.height, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(0, 0, picture.width, picture.height), 0, 0);
            RenderTexture.active = before;
            RenderTexture.ReleaseTemporary(onto);
            Color32[] points = copy.GetPixels32();
            Destroy(copy);
            return points;
        }

        /// <summary>
        /// The head picture with its skin in another colour. A point counts as skin by its hue (the kerbals'
        /// yellow-green and a little either side) and by having some colour to it at all; a point that is half
        /// skin and half hair, at the hairline, is changed half way. The new colour is given the lightness the
        /// point had, against the lightness of plain skin. (Runs on another thread: nothing here is the game's.)
        /// </summary>
        static Color32[] Recolour(Color32[] from, Color tint)
        {
            var to = new Color32[from.Length];
            float tr = tint.r, tg = tint.g, tb = tint.b;
            const float plain = 0.299f * 198f / 255f + 0.587f * 212f / 255f + 0.114f * 127f / 255f;
            for (int n = 0; n < from.Length; n++)
            {
                Color32 p = from[n];
                float r = p.r * 0.003921569f, g = p.g * 0.003921569f, b = p.b * 0.003921569f;
                float most = r > g ? (r > b ? r : b) : (g > b ? g : b), least = r < g ? (r < b ? r : b) : (g < b ? g : b), spread = most - least;
                float skin = 0f;
                if (most > 0.02f && spread > 0.1f * most)
                {
                    // (the hue as a part of the way round the colour wheel: red 0, yellow a sixth, green a third)
                    float hue = most == g ? (2f + (b - r) / spread) * 0.16666667f : most == r ? (g - b) / spread * 0.16666667f : (4f + (r - g) / spread) * 0.16666667f;
                    float byHue = hue <= 0.11f || hue >= 0.30f ? 0f : hue < 0.165f ? Smooth((hue - 0.11f) / 0.055f) : hue > 0.235f ? 1f - Smooth((hue - 0.235f) / 0.065f) : 1f;
                    float strength = spread / most, byStrength = strength >= 0.25f ? 1f : Smooth((strength - 0.10f) / 0.15f);
                    skin = byHue * byStrength;
                }
                if (skin <= 0f) { to[n] = p; continue; }
                float shade = (0.299f * r + 0.587f * g + 0.114f * b) / plain;
                r += (tr * shade - r) * skin; g += (tg * shade - g) * skin; b += (tb * shade - b) * skin;
                to[n] = new Color32((byte)(r <= 0f ? 0f : r >= 1f ? 255f : r * 255f + 0.5f), (byte)(g <= 0f ? 0f : g >= 1f ? 255f : g * 255f + 0.5f), (byte)(b <= 0f ? 0f : b >= 1f ? 255f : b * 255f + 0.5f), p.a);
            }
            return to;
        }

        static float Smooth(float x) => x <= 0f ? 0f : x >= 1f ? 1f : x * x * (3f - 2f * x);

        // ---- the mod's page in the window

        void Panel()
        {
            GUILayout.Space(6f);
            GUILayout.Label("<b>The kerbals here</b>", Host.Rich);
            if (here.Count == 0) { GUILayout.Label("<size=11>None in sight. (Those aboard the ship being flown and those outside it are listed.)</size>", Host.Rich); return; }
            var listed = new HashSet<string>();
            foreach (Who who in here)
            {
                if (!listed.Add(who.name)) continue;
                bool own = Own(who.name, out Color chosen);
                Color colour = own ? chosen : ColourOf(who.name);
                GUILayout.BeginHorizontal();
                // (the patch of colour opens the field to choose from, as "change" does)
                if (Host.Pick(colour, 28f, 18f)) open = open == who.name ? null : who.name;
                GUILayout.Label(who.name + (own ? "" : "  <size=10><color=#a0a0a0>" + (variety.Value == 0 ? "as everyone" : "drawn by name") + "</color></size>"), Host.Rich, GUILayout.Width(210f));
                GUILayout.FlexibleSpace();
                if (own && GUILayout.Button("as the rest", GUILayout.Width(84f))) { mod.Keep(KeyOf(who.name), null); nextLook = 0f; }
                if (GUILayout.Button(open == who.name ? "done" : "change", GUILayout.Width(64f))) open = open == who.name ? null : who.name;
                GUILayout.EndHorizontal();
                if (open != who.name) continue;
                // one click: a row of colours
                Color picked = colour;
                GUILayout.BeginHorizontal();
                GUILayout.Space(18f);
                foreach (Color one in Any)
                {
                    if (Host.Pick(one, 20f, 18f)) picked = one;
                    GUILayout.Space(2f);
                }
                GUILayout.EndHorizontal();
                // or any colour at all
                picked = picked != colour ? picked : Host.Mixer(colour, Host.TypedName + "Kerbal Skins:" + who.name);
                if (picked != colour)
                {
                    mod.Keep(KeyOf(who.name), picked.r.ToString("F3", CultureInfo.InvariantCulture) + "," + picked.g.ToString("F3", CultureInfo.InvariantCulture) + "," + picked.b.ToString("F3", CultureInfo.InvariantCulture));
                    nextLook = 0f;
                }
            }
        }

#if DEV
        /// <summary>For the development build: open the first listed kerbal's colour mixer in the window, or close it.</summary>
        public static string Mix()
        {
            foreach (Skins one in FindObjectsOfType<Skins>())
            {
                one.open = one.open == null && one.here.Count > 0 ? one.here[0].name : null;
                return one.open ?? "closed";
            }
            return "not running";
        }

        /// <summary>For the development build: who is here, the colour each is to be, and the copies there are.</summary>
        public static string Stats()
        {
            foreach (Skins one in FindObjectsOfType<Skins>())
            {
                var text = new System.Text.StringBuilder("on " + on.Value + ", everyone " + (Color32)everyone.Value + ", each their own: " + variety.Options[variety.Value] + " | ");
                foreach (Who who in one.here) text.Append(who.name + (who.outside ? " (outside)" : "") + " " + (Color32)ColourOf(who.name) + (IsClassic(ColourOf(who.name)) ? " (the game's own)" : "") + "; ");
                text.Append("| " + made.Count + " copies, " + sources.Count + " pictures read");
                return text.ToString();
            }
            return "not running (the flight scene only)";
        }

        /// <summary>For the development build: every kerbal in the scene, and what each of their parts is drawn with.</summary>
        public static string Survey()
        {
            var text = new System.Text.StringBuilder();
            foreach (Kerbal kerbal in FindObjectsOfType<Kerbal>()) Describe(text, "inside " + kerbal.crewMemberName, kerbal.gameObject);
            foreach (KerbalEVA kerbal in FindObjectsOfType<KerbalEVA>()) Describe(text, "outside " + kerbal.name, kerbal.gameObject);
            return text.Length > 0 ? text.ToString() : "no kerbals";
        }

        static void Describe(System.Text.StringBuilder text, string who, GameObject root)
        {
            text.Append("== " + who + ": ");
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                Material material = renderer.sharedMaterial;
                Texture picture = material != null && material.HasProperty("_MainTex") ? material.mainTexture : null;
                text.Append(renderer.name + " [" + renderer.GetType().Name.Replace("Renderer", "") + (renderer.enabled && renderer.gameObject.activeInHierarchy ? "" : ", off") + ", " +
                            (material != null ? material.name + " / " + material.shader.name : "no material") +
                            (picture != null ? ", " + picture.name + " " + picture.width + "x" + picture.height + (picture is Texture2D flat ? " " + flat.format + (flat.isReadable ? " readable" : "") : "") : "") + "] ");
            }
        }

        /// <summary>For the development build: who is on the books, and whether they are free to fly.</summary>
        public static string Roster()
        {
            var text = new System.Text.StringBuilder();
            foreach (ProtoCrewMember member in HighLogic.CurrentGame.CrewRoster.Crew)
                text.Append(member.name + " (" + member.gender + ", " + member.rosterStatus + ") ");
            return text.ToString();
        }
#endif
    }
}
