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
    /// (Since 0.2.0, the hair as well, in the same copy: see Recolour.)
    ///
    /// Making a copy is a million points of arithmetic, done on another thread; the game does not wait for it.
    /// </summary>
    [KSPAddon(KSPAddon.Startup.Instantly, true)]
    public sealed class SkinsAtStart : MonoBehaviour
    {
        void Awake() { Skins.Setup(); Destroy(gameObject); }
    }

    [KSPAddon(KSPAddon.Startup.EveryScene, false)]
    public sealed class Skins : MonoBehaviour
    {
        public const string Version = "0.2.0";

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

        // (Since 0.2.0: people's own skin, the ten tones of the Monk Skin Tone Scale (Ellis Monk and Google, 2022, CC BY 4.0), from the
        // lightest to the darkest; each kerbal given one by name, a little lighter or darker and warmer or cooler than the tone itself,
        // as no two people's skin is the same.)
        static readonly Color[] People =
        {
            Rgb(246, 237, 228), Rgb(243, 231, 219), Rgb(247, 234, 208), Rgb(234, 218, 186), Rgb(215, 189, 150),
            Rgb(160, 126, 86), Rgb(130, 92, 67), Rgb(96, 65, 52), Rgb(58, 49, 42), Rgb(41, 36, 32),
        };

        static Color Rgb(int r, int g, int b) => new Color(r / 255f, g / 255f, b / 255f, 1f);

        /// <summary>A tone of the scale made a kerbal's own: up to 6% lighter or darker and a little warmer or cooler, by two numbers from 0 to 1.</summary>
        static Color Own(Color tone, float light, float warm)
        {
            float l = 0.94f + 0.12f * light, w = 0.03f * (warm - 0.5f);
            return new Color(Mathf.Clamp01(tone.r * l * (1f + w)), Mathf.Clamp01(tone.g * l), Mathf.Clamp01(tone.b * l * (1f - w)), 1f);
        }

        // (Since 0.2.0: hair. The colours people's hair comes in, from black to white, and how many in a hundred have each: black and
        // dark brown most people, blond and red a few in a hundred, and the greys of age. Light skin is given the whole range;
        // darker skin, the second row, mostly black and dark brown, as it comes; every kerbal of another colour, the third.)
        static readonly Color[] Hairs =
        {
            Rgb(28, 24, 22), Rgb(52, 36, 28), Rgb(85, 58, 40), Rgb(125, 90, 62), Rgb(150, 118, 80), Rgb(196, 160, 110),
            Rgb(222, 202, 160), Rgb(110, 48, 30), Rgb(165, 75, 40), Rgb(150, 148, 144), Rgb(215, 212, 205),
        };
        static readonly string[] HairNames = { "black", "dark brown", "brown", "light brown", "dark blond", "blond", "fair", "auburn", "red", "grey", "white" };
        static readonly int[] HairsLight = { 12, 24, 20, 12, 9, 8, 3, 3, 4, 3, 2 };
        static readonly int[] HairsDark = { 58, 30, 6, 0, 0, 0, 0, 0, 0, 4, 2 };
        static readonly int[] HairsAny = { 25, 25, 17, 9, 6, 5, 1, 3, 3, 4, 2 };

        // (Since 0.2.0: eyes. A kerbal's eye is a white ball with a big dark pupil and no iris: the colour goes on the pupil, kept
        // dark as an iris is in shade. The colours eyes come in and how many in a hundred have each: brown most people, blue, hazel,
        // green, grey and amber some, nearly all brown among darker skins, as it comes.)
        static readonly Color[] EyeColours =
        {
            Rgb(38, 24, 15), Rgb(64, 40, 22), Rgb(80, 66, 38), Rgb(42, 66, 102), Rgb(48, 74, 48), Rgb(78, 86, 96), Rgb(100, 68, 26),
        };
        static readonly string[] EyeNames = { "dark brown", "brown", "hazel", "blue", "green", "grey", "amber" };
        static readonly int[] EyesLight = { 25, 25, 12, 22, 8, 5, 3 };
        static readonly int[] EyesDark = { 70, 26, 3, 0, 0, 0, 1 };
        static readonly int[] EyesAny = { 35, 25, 10, 17, 7, 3, 3 };
        static readonly Color GameEyes = Rgb(29, 29, 29);

        static Mod mod;
        static Toggle on;
        static Tint everyone;
        static Choice variety;
        static Toggle hairToo, eyesToo;

        // The game's own head pictures, as points that can be read (taken off the graphics card once), and the copies made of them.
        static readonly Dictionary<Texture, Color32[]> sources = new Dictionary<Texture, Color32[]>();
        static readonly Dictionary<string, Made> made = new Dictionary<string, Made>();
        sealed class Made { public Texture2D picture; public float used; public Task<Color32[]> making; public Texture from; }

        readonly List<Who> here = new List<Who>();
        sealed class Who { public string name; public Component kerbal; public bool outside, nobody; }
        MaterialPropertyBlock block;
        float nextLook;
        string open;                                    // whose colour is being changed in the window
        // (Since 0.2.0. Outside flight the scene is looked through in full only every five seconds: each look walked every object in
        // it three times over, 7 to 10 ms in the assembly building with a 230-part craft, twice a second, a hitch each time
        // (2026-10-20); the heads found are kept between, and each kerbal's head pieces and pupils with them, not gathered again at
        // every dressing.)
        readonly List<Who> scanned = new List<Who>();
        readonly Dictionary<Component, Renderer[]> headsOf = new Dictionary<Component, Renderer[]>();
        readonly Dictionary<Component, Renderer[]> eyesOf = new Dictionary<Component, Renderer[]>();
        float nextScan;
        const float ScanEvery = 5f;
        static readonly int MainTex = Shader.PropertyToID("_MainTex");

        /// <summary>Say that the mod is here and what can be set about it: once, when the game starts, so that its page is in the window in every scene.</summary>
        public static void Setup()
        {
            if (mod == null || Kit.Find("Kerbal Skins") != mod)
            {
                mod = Kit.Register("Kerbal Skins", Version, "The colour of the kerbals' skin and hair: one for everyone, a different one for each, or your own choice for any one of them. Eyes and suits stay.");
                on = mod.Toggle("on", "Change skin colours", true, "Off: every kerbal is the game's own green again.");
                everyone = mod.Tint("everyone", "Everyone", Classic, "The colour of every kerbal who has not been given one of their own below.");
                variety = mod.Choice("variety", "Each their own", 0, new[] { "no", "greens", "any colour", "people's" },
                    "Give every kerbal without a colour of their own a different one, always the same one for the same name: shades of green, any colour at all, or people's own skin (the ten tones of the Monk Skin Tone Scale, each kerbal a little lighter, darker, warmer or cooler than the tone).");
                hairToo = mod.Toggle("hair", "Their own hair too", true,
                    "With each their own skin, each their own hair colour as well, always the same for the same name: black and browns for most, blond, red and grey for a few, as among people (darker skin mostly black and dark brown). Off: the game's dark brown for all but those given a colour below.");
                eyesToo = mod.Toggle("eyes", "Their own eye colour too", true,
                    "With each their own skin, each their own eye colour as well, always the same for the same name: brown for most, blue, hazel, green, grey and amber for some, as among people (darker skin nearly all brown). A kerbal's eye has no iris of its own: the colour is its big dark pupil's, kept dark. Off: the game's black.");
            }
        }

        void Awake()
        {
            Setup();
            mod.Panel = Panel;
            block = new MaterialPropertyBlock();
            hired = hired ?? new Hired();
            GameEvents.onKerbalAdded.Add(hired.Added);
        }

        /// <summary>
        /// A kerbal newly hired, while each has a colour of their own: given one of the colours at random, kept by name from then on
        /// (the game's events take a handler only with an object behind it).
        /// </summary>
        sealed class Hired
        {
            readonly System.Random dice = new System.Random();
            public void Added(ProtoCrewMember crew)
            {
                if (crew == null || mod == null || variety == null || variety.Value == 0 || string.IsNullOrEmpty(crew.name) || Own(crew.name, out Color _)) return;
                Color[] from = variety.Value == 1 ? Greens : variety.Value == 3 ? People : Any;
                Color c = from[dice.Next(from.Length)];
                if (variety.Value == 3) c = Own(c, (float)dice.NextDouble(), (float)dice.NextDouble());
                mod.Keep(KeyOf(crew.name), c.r.ToString("F3", CultureInfo.InvariantCulture) + "," + c.g.ToString("F3", CultureInfo.InvariantCulture) + "," + c.b.ToString("F3", CultureInfo.InvariantCulture));
            }
        }
        static Hired hired;

        void OnDestroy()
        {
            if (hired != null) GameEvents.onKerbalAdded.Remove(hired.Added);
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
            Color[] from = variety.Value == 1 ? Greens : variety.Value == 3 ? People : Any;
            Color picked = from[(sum >> 8) % (uint)from.Length];
            return variety.Value == 3 ? Own(picked, ((sum >> 16) & 255) / 255f, ((sum >> 24) & 255) / 255f) : picked;
        }

        static bool IsClassic(Color colour) => Mathf.Abs(colour.r - Classic.r) + Mathf.Abs(colour.g - Classic.g) + Mathf.Abs(colour.b - Classic.b) < 0.02f;

        static string HairKeyOf(string name) => "hair_" + name.Replace(' ', '_');

        /// <summary>A hair colour chosen for a kerbal in the window: true and the colour, or true and none for the game's own ("-" kept).</summary>
        static bool OwnHair(string name, out bool drawn, out Color hair)
        {
            drawn = false; hair = Hairs[1];
            string text = mod.Kept(HairKeyOf(name));
            if (string.IsNullOrEmpty(text)) return false;
            if (text == "-") { drawn = true; return true; }
            string[] parts = text.Split(',');
            if (parts.Length < 3) return false;
            try { hair = new Color(float.Parse(parts[0], CultureInfo.InvariantCulture), float.Parse(parts[1], CultureInfo.InvariantCulture), float.Parse(parts[2], CultureInfo.InvariantCulture), 1f); }
            catch (Exception) { return false; }
            return true;
        }

        /// <summary>
        /// The colour a kerbal's hair is to be: false for the game's own. Each their own by name (with "Their own hair too"): one of
        /// the hair colours drawn by their shares, a little lighter or darker as the skin is, by other bits of the name than the skin's.
        /// </summary>
        public static bool HairOf(string name, out Color hair)
        {
            if (OwnHair(name, out bool drawn, out hair)) return !drawn;
            if (variety.Value == 0 || !hairToo.Value) return false;
            uint sum = 84696351;
            foreach (char letter in name) sum = unchecked((sum ^ letter) * 16777619);
            sum = unchecked(sum * 2654435761u);
            Color skin = ColourOf(name);
            int[] shares = variety.Value != 3 ? HairsAny : 0.299f * skin.r + 0.587f * skin.g + 0.114f * skin.b < 0.6f ? HairsDark : HairsLight;
            int roll = (int)((sum >> 4) % 100u), i = 0;
            while (i < shares.Length - 1 && roll >= shares[i]) roll -= shares[i++];
            hair = Own(Hairs[i], ((sum >> 16) & 255) / 255f, ((sum >> 24) & 255) / 255f);
            return true;
        }

        static string EyeKeyOf(string name) => "eyes_" + name.Replace(' ', '_');

        /// <summary>The colour a kerbal's pupils are to be: false for the game's own. Chosen in the window (kept), or each their own by name.</summary>
        public static bool EyeOf(string name, out Color eye)
        {
            eye = GameEyes;
            string text = mod.Kept(EyeKeyOf(name));
            if (!string.IsNullOrEmpty(text))
            {
                if (text == "-") return false;
                string[] parts = text.Split(',');
                if (parts.Length >= 3)
                    try { eye = new Color(float.Parse(parts[0], CultureInfo.InvariantCulture), float.Parse(parts[1], CultureInfo.InvariantCulture), float.Parse(parts[2], CultureInfo.InvariantCulture), 1f); return true; }
                    catch (Exception) { }
            }
            if (variety.Value == 0 || eyesToo == null || !eyesToo.Value) return false;
            uint sum = 3141592653u;
            foreach (char letter in name) sum = unchecked((sum ^ letter) * 16777619);
            sum = unchecked(sum * 2246822519u);
            Color skin = ColourOf(name);
            int[] shares = variety.Value != 3 ? EyesAny : 0.299f * skin.r + 0.587f * skin.g + 0.114f * skin.b < 0.6f ? EyesDark : EyesLight;
            int roll = (int)((sum >> 5) % 100u), i = 0;
            while (i < shares.Length - 1 && roll >= shares[i]) roll -= shares[i++];
            eye = EyeColours[i];
            return true;
        }

        static string EyeName(Color eye)
        {
            int best = 0; float bestD = float.MaxValue;
            for (int i = 0; i < EyeColours.Length; i++)
            {
                float d = Mathf.Abs(eye.r - EyeColours[i].r) + Mathf.Abs(eye.g - EyeColours[i].g) + Mathf.Abs(eye.b - EyeColours[i].b);
                if (d < bestD) { bestD = d; best = i; }
            }
            return EyeNames[best];
        }

        static readonly int ColorId = Shader.PropertyToID("_Color");

        /// <summary>A kerbal's pupils in their colour (or the game's own again).</summary>
        void DressEyes(Who who, bool has, Color eye)
        {
            foreach (Renderer renderer in EyesOf(who.kerbal))
            {
                if (renderer == null) continue;
                renderer.GetPropertyBlock(block);
                bool set = block.isEmpty == false && block.GetColor(ColorId).a > 0f && block.GetColor(ColorId) != Color.clear;
                if (!has) { if (set) { block.Clear(); renderer.SetPropertyBlock(null); } continue; }
                Color now = block.GetColor(ColorId);
                if (set && Mathf.Abs(now.r - eye.r) + Mathf.Abs(now.g - eye.g) + Mathf.Abs(now.b - eye.b) < 0.004f) continue;
                block.SetColor(ColorId, eye);
                renderer.SetPropertyBlock(block);
            }
        }

        /// <summary>A hair colour's name, the nearest of the list's.</summary>
        static string HairName(Color hair)
        {
            int best = 0; float bestD = float.MaxValue;
            for (int i = 0; i < Hairs.Length; i++)
            {
                float d = Mathf.Abs(hair.r - Hairs[i].r) + Mathf.Abs(hair.g - Hairs[i].g) + Mathf.Abs(hair.b - Hairs[i].b);
                if (d < bestD) { bestD = d; best = i; }
            }
            return HairNames[best];
        }

        // ---- putting the colours on

        void Update()
        {
            if (Time.unscaledTime < nextLook) return;
            nextLook = Time.unscaledTime + (open != null ? 0.1f : 0.5f);
            Look();
            if (!on.Value) { Clear(); return; }
            float now = Time.unscaledTime;
            foreach (Who who in here)
            {
                Dress(who, ColourOf(who.name), HairOf(who.name, out Color hair), hair, now);
                if (who.kerbal != null) DressEyes(who, EyeOf(who.name, out Color eye), eye);
            }
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
            bool scan = Time.unscaledTime >= nextScan;
            if (scan) { nextScan = Time.unscaledTime + ScanEvery; headsOf.Clear(); eyesOf.Clear(); }
            if (HighLogic.LoadedSceneIsFlight)
            {
                // (in flight, the crews of the craft that are loaded, from the craft themselves: a search of every object in the
                // scene, twice a second, cost up to 13 ms in the frame it fell in, a hitch (measured 2026-10-18))
                foreach (Vessel v in FlightGlobals.VesselsLoaded)
                {
                    if (v == null) continue;
                    if (v.isEVA)
                    {
                        KerbalEVA eva = v.evaController;
                        if (eva != null && eva.part != null && eva.part.protoModuleCrew != null && eva.part.protoModuleCrew.Count > 0)
                            here.Add(new Who { name = eva.part.protoModuleCrew[0].name, kerbal = eva, outside = true });
                        continue;
                    }
                    foreach (ProtoCrewMember crew in v.GetVesselCrew())
                        if (crew != null && crew.KerbalRef != null) here.Add(new Who { name = crew.name, kerbal = crew.KerbalRef });
                }
                return;
            }
            if (!scan)
            {
                foreach (Who who in scanned) if (who.kerbal != null) here.Add(who);
                return;
            }
            scanned.Clear();
            foreach (Kerbal kerbal in FindObjectsOfType<Kerbal>())
                if (kerbal != null && kerbal.protoCrewMember != null) here.Add(new Who { name = kerbal.protoCrewMember.name, kerbal = kerbal });
            foreach (KerbalEVA kerbal in FindObjectsOfType<KerbalEVA>())
                if (kerbal != null && kerbal.part != null && kerbal.part.protoModuleCrew != null && kerbal.part.protoModuleCrew.Count > 0)
                    here.Add(new Who { name = kerbal.part.protoModuleCrew[0].name, kerbal = kerbal, outside = true });
            // (and every other kerbal's head that is drawn, with no crew record behind it: the opening screen's, the assembly
            // building's ground crew, the astronaut complex's: each by the name of the thing it is part of, as "each their own"
            // colours a name, and the ground crew, who share one name, each by which of them it is)
            var counted = new HashSet<Transform>();
            foreach (Who who in here) if (who.kerbal != null) counted.Add(who.kerbal.transform);
            foreach (SkinnedMeshRenderer r in FindObjectsOfType<SkinnedMeshRenderer>())
            {
                if (r == null || r.sharedMaterial == null || !r.sharedMaterial.HasProperty(MainTex) || !(IsHead(r.sharedMaterial.mainTexture) || Foreign(r, r.sharedMaterial.mainTexture))) continue;
                // (each kerbal by its own animated body: the opening screen's three hang under one thing of the scene's)
                Animator body = r.GetComponentInParent<Animator>();
                Transform top = body != null ? body.transform : r.transform.root;
                if (!counted.Add(top)) continue;
                here.Add(new Who { name = top.name + " " + top.GetInstanceID(), kerbal = top, nobody = true });
            }
            scanned.AddRange(here);
        }

        /// <summary>A kerbal's head pieces (those drawn with a kerbal's head picture, or another mod's on a kerbal's head mesh), kept until the next full look.</summary>
        Renderer[] HeadsOf(Component kerbal)
        {
            if (headsOf.TryGetValue(kerbal, out Renderer[] found)) return found;
            var list = new List<Renderer>();
            foreach (Renderer r in kerbal.GetComponentsInChildren<Renderer>(true))
            {
                Material m = r.sharedMaterial;
                if (m == null || !m.HasProperty(MainTex)) continue;
                if (IsHead(m.mainTexture) || Foreign(r, m.mainTexture)) list.Add(r);
            }
            found = list.ToArray();
            headsOf[kerbal] = found;
            return found;
        }

        /// <summary>A kerbal's pupils, kept as its head pieces are.</summary>
        Renderer[] EyesOf(Component kerbal)
        {
            if (eyesOf.TryGetValue(kerbal, out Renderer[] found)) return found;
            var list = new List<Renderer>();
            foreach (Renderer r in kerbal.GetComponentsInChildren<Renderer>(true))
                if (r.name.IndexOf("pupil", StringComparison.OrdinalIgnoreCase) >= 0) list.Add(r);
            found = list.ToArray();
            eyesOf[kerbal] = found;
            return found;
        }

        static bool IsHead(Texture picture) => picture != null && (picture.name == "kerbalHead" || picture.name.StartsWith("kerbalGirl", StringComparison.Ordinal));

        /// <summary>
        /// (Since 0.2.0.) A kerbal's head by the mesh it is drawn on: the men's "headMesh01", the women's "..._polySurface51". Another mod
        /// that gives kerbals heads of its own (TextureReplacer's packs, for one) puts its own picture on that same mesh, under a name of
        /// its own: such a head is known by its mesh, and its skin by its own colours (see Band).
        /// </summary>
        static bool IsHeadMesh(Renderer r) => r != null && (r.name == "headMesh01" || r.name.EndsWith("_polySurface51", StringComparison.Ordinal));

        /// <summary>A head picture of another mod's on a kerbal's head mesh.</summary>
        static bool Foreign(Renderer r, Texture picture) => picture != null && !IsHead(picture) && IsHeadMesh(r);

        void Dress(Who who, Color colour, bool hasHair, Color hair, float now)
        {
            bool classic = IsClassic(colour) && !hasHair;
            foreach (Renderer renderer in HeadsOf(who.kerbal))
            {
                if (renderer == null) continue;
                Material material = renderer.sharedMaterial;
                if (material == null || !material.HasProperty(MainTex)) continue;
                Texture own = material.mainTexture;
                bool foreign = Foreign(renderer, own);
                if (!IsHead(own) && !foreign) continue;
                // (another mod's head: its skin by its own colours; its hair, drawn where that mod's picture has it, left as it is)
                Texture2D wanted = classic ? null : Copy(own, colour, hasHair && !foreign, hair, now, foreign);
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
                    if (renderer.name.IndexOf("pupil", StringComparison.OrdinalIgnoreCase) >= 0) { renderer.SetPropertyBlock(null); continue; }
                    Material material = renderer.sharedMaterial;
                    if (material == null || !material.HasProperty(MainTex) || !(IsHead(material.mainTexture) || Foreign(renderer, material.mainTexture))) continue;
                    renderer.GetPropertyBlock(block);
                    if (block.GetTexture(MainTex) != null) renderer.SetPropertyBlock(null);
                }
            }
        }

        /// <summary>The copy of a head picture with the skin (and the hair, if given) in a colour: made if there is none yet (nothing is given back until it is ready).</summary>
        static Texture2D Copy(Texture own, Color colour, bool hasHair, Color hair, float now, bool foreign = false)
        {
            var c = (Color32)colour;
            var h = (Color32)hair;
            string key = own.GetInstanceID() + ":" + c.r.ToString("X2") + c.g.ToString("X2") + c.b.ToString("X2") + (hasHair ? "/" + h.r.ToString("X2") + h.g.ToString("X2") + h.b.ToString("X2") : "");
            if (!made.TryGetValue(key, out Made one))
            {
                // (one at a time: dragging a slider asks for a colour every tenth of a second)
                foreach (Made other in made.Values) if (other.making != null && !other.making.IsCompleted) return null;
                if (!sources.TryGetValue(own, out Color32[] points)) sources[own] = points = Read(own);
                Color tint = colour;
                int width = own.width, height = own.height;
                bool skin = !IsClassic(colour), girl = own.name.StartsWith("kerbalGirl", StringComparison.Ordinal);
                // (the game's own pictures: the kerbals' skin; another mod's: the skin its own picture has, found in it)
                float middle = KerbalHue, plain = KerbalPlain;
                if (foreign) Band(points, out middle, out plain);
                one = new Made { used = now, from = own, making = Task.Run(() => Recolour(points, width, height, girl, skin, tint, hasHair, hair, middle, plain)) };
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
        /// The head picture with its skin, and its hair if asked, in other colours. A point counts as skin by its hue (the kerbals'
        /// yellow-green and a little either side) and by having some colour to it at all; a point that is half skin and half hair,
        /// at the hairline, is changed half way. The new colour is given the lightness the point had, against the lightness of
        /// plain skin. (Runs on another thread: nothing here is the game's.)
        ///
        /// (Since 0.2.0.) The hair is drawn dark brown in the upper part of the picture, a strand at a time. Where it is: what is
        /// dark there over at least half of its neighbourhood (not a speck: a block of the picture's compression, a dot), and
        /// near it the zone it covers, reaching some thirty points past its edge; the shading round the eyes, further off, is not
        /// reached. In that zone each point is taken as hair and skin mixed by how light it is (the hair as dark as the picture's
        /// hair, the skin as light as plain skin) and given the new hair and skin in the same mix: the strands keep their lighter
        /// and darker lines, softer for light hair, as light hair is; the hairline keeps its soft edge, with a little of the
        /// hair's shadow on the skin below it. (Tried first on the game's own picture in a copy in Python: with the hair found
        /// by its hue, as the skin is, the dark hairline was taken for skin in shadow, a dark rim under blond hair, and the
        /// compression's blocks came out as black specks in it.)
        /// </summary>
        static Color32[] Recolour(Color32[] from, int width, int height, bool girl, bool skinToo, Color tint, bool hairToo, Color hair, float middle, float plain)
        {
            var to = new Color32[from.Length];
            float tr = tint.r, tg = tint.g, tb = tint.b, hr = hair.r, hg = hair.g, hb = hair.b;
            // (Since 0.2.0, skin in shade: light goes into skin and comes out again further on, and the red of it furthest, through
            // the blood, so skin in shadow is warmer and redder than its lit colour darkened, most at the shadow's edge (the red rim
            // of a shadow across a face). The picture's own shading, round the eyes and under the hair, is given that: each of red,
            // green and blue darkened by its own power of the shade. For people's colours of skin only, the browns and tans: a
            // green or blue kerbal's flesh is no one's to know.)
            float most0 = Mathf.Max(tr, Mathf.Max(tg, tb)), least0 = Mathf.Min(tr, Mathf.Min(tg, tb));
            bool flesh = skinToo && most0 == tr && tg >= tb && most0 - least0 > 0.03f && (tg - tb) / Mathf.Max(0.001f, most0 - least0) < 0.85f;
            float typical = 0.1f;
            float[] zone = hairToo && from.Length == width * height ? HairZone(from, width, height, girl, out typical) : null;
            // (how far the strands' lines are kept: as drawn for dark hair, a little under half of it for blond and grey; and never
            // so far above the hair's colour that it is lost in white)
            float contrast = 1f - 0.6f * Smooth((0.299f * hr + 0.587f * hg + 0.114f * hb - 0.12f) / 0.45f);
            float most = Mathf.Min(1.8f, 0.95f / Mathf.Max(0.05f, Mathf.Max(hr, Mathf.Max(hg, hb))));
            for (int n = 0; n < from.Length; n++)
            {
                Color32 p = from[n];
                float r = p.r * 0.003921569f, g = p.g * 0.003921569f, b = p.b * 0.003921569f;
                float skin = skinToo ? SkinOf(r, g, b, middle) : 0f, near = zone != null ? zone[n] : 0f;
                if (skin <= 0f && near <= 0f) { to[n] = p; continue; }
                float light = 0.299f * r + 0.587f * g + 0.114f * b, shade = light / plain;
                float redShade = shade, blueShade = shade;
                if (flesh && shade < 1f) { redShade = Mathf.Pow(shade, 0.88f); blueShade = Mathf.Pow(shade, 1.12f); }
                float R = r + (tr * redShade - r) * skin, G = g + (tg * shade - g) * skin, B = b + (tb * blueShade - b) * skin;
                if (near > 0f)
                {
                    float part = Mathf.Clamp01((light - 0.10f) / (plain - 0.10f));          // (how much of the point is skin)
                    float strand = Mathf.Clamp(1f + contrast * (light / typical - 1f), 0.15f, most);
                    strand += (1f - strand) * Mathf.Min(1f, 2f * part);           // (at the hairline, the strands' own lightness is not known: as the hair's)
                    float under = 1f - 0.3f * (1f - part);                                   // (the hair's shadow on the skin)
                    float sr = skinToo ? tr : Classic.r, sg = skinToo ? tg : Classic.g, sb = skinToo ? tb : Classic.b;
                    float zr = hr * strand + (sr * under - hr * strand) * part, zg = hg * strand + (sg * under - hg * strand) * part, zb = hb * strand + (sb * under - hb * strand) * part;
                    R += (zr - R) * near; G += (zg - G) * near; B += (zb - B) * near;
                }
                to[n] = new Color32((byte)(R <= 0f ? 0f : R >= 1f ? 255f : R * 255f + 0.5f), (byte)(G <= 0f ? 0f : G >= 1f ? 255f : G * 255f + 0.5f), (byte)(B <= 0f ? 0f : B >= 1f ? 255f : B * 255f + 0.5f), p.a);
            }
            return to;
        }

        // (the kerbals' skin in the game's own pictures: its hue, the middle of 0.175 to 0.205, and its plain lightness, of its typical
        // colour (198, 212, 127))
        const float KerbalHue = 0.19f, KerbalPlain = 0.299f * 198f / 255f + 0.587f * 212f / 255f + 0.114f * 127f / 255f;

        /// <summary>
        /// How much a point is skin, by its hue and how much colour it has (0 to 1): skin is a band of hues round its middle (for the
        /// kerbals' yellow-green, 0.11 to 0.30 of the way round the colour wheel, all of it from 0.165 to 0.235; for another mod's head
        /// the same band round the middle of its own skin, wrapping round past red where it must).
        /// </summary>
        static float SkinOf(float r, float g, float b, float middle)
        {
            float most = r > g ? (r > b ? r : b) : (g > b ? g : b), least = r < g ? (r < b ? r : b) : (g < b ? g : b), spread = most - least;
            if (most <= 0.02f || spread <= 0.1f * most) return 0f;
            float d = Hue(r, g, b, most, spread) - middle;
            d -= Mathf.Floor(d + 0.5f);                                       // (the nearest way round)
            float byHue = d <= -0.08f || d >= 0.11f ? 0f : d < -0.025f ? Smooth((d + 0.08f) / 0.055f) : d > 0.045f ? 1f - Smooth((d - 0.045f) / 0.065f) : 1f;
            float strength = spread / most, byStrength = strength >= 0.25f ? 1f : Smooth((strength - 0.10f) / 0.15f);
            return byHue * byStrength;
        }

        /// <summary>The hue as a part of the way round the colour wheel: red 0, yellow a sixth, green a third.</summary>
        static float Hue(float r, float g, float b, float most, float spread)
        {
            float hue = most == g ? (2f + (b - r) / spread) * 0.16666667f : most == r ? (g - b) / spread * 0.16666667f : (4f + (r - g) / spread) * 0.16666667f;
            return hue < 0f ? hue + 1f : hue;
        }

        /// <summary>
        /// (Since 0.2.0.) The skin of another mod's head picture: the hue most of its coloured points have (skin is most of a head's
        /// picture: the face, the ears, the neck), and its plain lightness, the middle of those points'. Found from a count of the hues
        /// of the points with some colour to them and neither near black nor near white, each counted by how much colour it has.
        /// </summary>
        static void Band(Color32[] points, out float middle, out float plain)
        {
            var count = new float[100];
            for (int n = 0; n < points.Length; n += 3)
            {
                Color32 p = points[n];
                float r = p.r * 0.003921569f, g = p.g * 0.003921569f, b = p.b * 0.003921569f;
                float most = Mathf.Max(r, Mathf.Max(g, b)), spread = most - Mathf.Min(r, Mathf.Min(g, b));
                float light = 0.299f * r + 0.587f * g + 0.114f * b;
                if (most <= 0.02f || spread < 0.2f * most || light < 0.2f || light > 0.95f) continue;
                count[(int)(Hue(r, g, b, most, spread) * 99.99f)] += spread / most;
            }
            int best = 0; float bestSum = -1f;
            for (int k = 0; k < 100; k++)
            {
                float sum = 0f;
                for (int j = -2; j <= 2; j++) sum += count[(k + j + 100) % 100];
                if (sum > bestSum) { bestSum = sum; best = k; }
            }
            middle = (best + 0.5f) / 100f;
            var lights = new List<float>();
            for (int n = 0; n < points.Length; n += 7)
            {
                Color32 p = points[n];
                float r = p.r * 0.003921569f, g = p.g * 0.003921569f, b = p.b * 0.003921569f;
                if (SkinOf(r, g, b, middle) > 0.9f) lights.Add(0.299f * r + 0.587f * g + 0.114f * b);
            }
            lights.Sort();
            plain = lights.Count > 20 ? lights[lights.Count / 2] : KerbalPlain;
        }

        /// <summary>
        /// How near each point is to the hair (1 on it and up to some twenty points past its edge, falling to nothing by thirty;
        /// distances for a picture 1024 wide, in proportion for others), and the hair's typical lightness (the middle of its dark
        /// points'). On the men's picture the hair is all that is dark above the lower third (the face's and the mouth's, whose
        /// black band and lips are not hair). On the women's it is in three pieces, the head's and two at the foot (the bun and a
        /// strip), in a ground of unused picture of a flat dark grey-green (16, 16, 12): there hair is what is dark and warm (redder
        /// than green, greener than blue, but not as red as the lips), or nearly black without the ground's green.
        /// </summary>
        static float[] HairZone(Color32[] from, int width, int height, bool girl, out float typical)
        {
            int count = width * height;
            float scale = width / 1024f;
            var dark = new float[count];
            var lights = new int[64];
            int counted = 0;
            for (int y = 0; y < height; y++)
            {
                float above = girl ? 1f : Smooth((y / (float)height - 0.36f) / 0.04f);  // (rows from the bottom, as the picture's points come)
                if (above <= 0f) continue;
                for (int x = 0, n = y * width; x < width; x++, n++)
                {
                    Color32 p = from[n];
                    float light = (0.299f * p.r + 0.587f * p.g + 0.114f * p.b) * 0.003921569f;
                    if (light >= 0.30f) continue;
                    if (girl)
                    {
                        int rg = p.r - p.g, rb = p.r - p.b;
                        bool warm = rg >= 2 && rb >= 3 && rg <= 15, black = light < 0.035f && p.r >= p.g && p.g >= p.b;
                        if (!warm && !black) continue;
                    }
                    dark[n] = above;
                    if (above > 0.5f) { lights[Mathf.Min(63, (int)(light / 0.30f * 64f))]++; counted++; }
                }
            }
            typical = 0.1f;
            for (int i = 0, run = 0; i < 64 && counted > 0; i++)
                if ((run += lights[i]) >= counted / 2) { typical = Mathf.Max(0.03f, (i + 0.5f) * 0.30f / 64f); break; }
            float[] around = Blur(dark, width, height, Mathf.Max(1, Mathf.RoundToInt(6f * scale)));
            for (int n = 0; n < count; n++) around[n] = around[n] > 0.5f ? 1f : 0f;
            int reach = Mathf.Max(1, Mathf.RoundToInt(16f * scale));
            float[] zone = Blur(Blur(around, width, height, reach), width, height, reach);
            for (int n = 0; n < count; n++) zone[n] = Mathf.Min(1f, 2.5f * zone[n]);
            return zone;
        }

        /// <summary>A picture's numbers each made the mean of those within so many points across and up and down (a box, its edges held).</summary>
        static float[] Blur(float[] a, int width, int height, int r)
        {
            var across = new float[a.Length];
            var both = new float[a.Length];
            float each = 1f / (2 * r + 1);
            for (int y = 0; y < height; y++)
            {
                int row = y * width;
                float sum = 0f;
                for (int x = -r; x <= r; x++) sum += a[row + Mathf.Clamp(x, 0, width - 1)];
                for (int x = 0; x < width; x++)
                {
                    across[row + x] = sum * each;
                    sum += a[row + Mathf.Min(x + r + 1, width - 1)] - a[row + Mathf.Max(x - r, 0)];
                }
            }
            for (int x = 0; x < width; x++)
            {
                float sum = 0f;
                for (int y = -r; y <= r; y++) sum += across[Mathf.Clamp(y, 0, height - 1) * width + x];
                for (int y = 0; y < height; y++)
                {
                    both[y * width + x] = sum * each;
                    sum += across[Mathf.Min(y + r + 1, height - 1) * width + x] - across[Mathf.Max(y - r, 0) * width + x];
                }
            }
            return both;
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
                if (who.nobody || !listed.Add(who.name)) continue;          // (the kerbals with no crew record are coloured, not listed)
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
                // (since 0.2.0) the hair: one of people's hair colours, the game's own, or back to drawn by name
                bool ownHair = OwnHair(who.name, out bool _, out Color _);
                bool hasHair = HairOf(who.name, out Color hair);
                GUILayout.BeginHorizontal();
                GUILayout.Space(18f);
                GUILayout.Label("<size=11>Hair: " + (hasHair ? HairName(hair) : "the game's") + (ownHair ? "" : " <color=#a0a0a0>(by name)</color>") + "</size>", Host.Rich, GUILayout.Width(150f));
                GUILayout.FlexibleSpace();
                if (ownHair && GUILayout.Button("by name", GUILayout.Width(70f))) { mod.Keep(HairKeyOf(who.name), null); nextLook = 0f; }
                if (GUILayout.Button("the game's", GUILayout.Width(80f))) { mod.Keep(HairKeyOf(who.name), "-"); nextLook = 0f; }
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                GUILayout.Space(18f);
                foreach (Color one in Hairs)
                {
                    if (Host.Pick(one, 20f, 18f))
                    {
                        mod.Keep(HairKeyOf(who.name), one.r.ToString("F3", CultureInfo.InvariantCulture) + "," + one.g.ToString("F3", CultureInfo.InvariantCulture) + "," + one.b.ToString("F3", CultureInfo.InvariantCulture));
                        nextLook = 0f;
                    }
                    GUILayout.Space(2f);
                }
                GUILayout.EndHorizontal();
                // (since 0.2.0) the eyes: one of people's eye colours (on the pupil), the game's own, or back to drawn by name
                bool ownEyes = !string.IsNullOrEmpty(mod.Kept(EyeKeyOf(who.name)));
                bool hasEyes = EyeOf(who.name, out Color eyes);
                GUILayout.BeginHorizontal();
                GUILayout.Space(18f);
                GUILayout.Label("<size=11>Eyes: " + (hasEyes ? EyeName(eyes) : "the game's") + (ownEyes ? "" : " <color=#a0a0a0>(by name)</color>") + "</size>", Host.Rich, GUILayout.Width(150f));
                foreach (Color one in EyeColours)
                {
                    if (Host.Pick(one, 20f, 18f))
                    {
                        mod.Keep(EyeKeyOf(who.name), one.r.ToString("F3", CultureInfo.InvariantCulture) + "," + one.g.ToString("F3", CultureInfo.InvariantCulture) + "," + one.b.ToString("F3", CultureInfo.InvariantCulture));
                        nextLook = 0f;
                    }
                    GUILayout.Space(2f);
                }
                GUILayout.FlexibleSpace();
                if (ownEyes && GUILayout.Button("by name", GUILayout.Width(70f))) { mod.Keep(EyeKeyOf(who.name), null); nextLook = 0f; }
                if (GUILayout.Button("the game's", GUILayout.Width(80f))) { mod.Keep(EyeKeyOf(who.name), "-"); nextLook = 0f; }
                GUILayout.EndHorizontal();
            }
        }

#if DEV
        /// <summary>(For the development build: every head picture the game draws kerbals with, written as raw RGBA bytes into a folder,
        /// its name and size in each file's name: to find the hair's and the eyes' colours.)</summary>
        public static string Dump(string folder)
        {
            var said = new System.Text.StringBuilder();
            var seen = new HashSet<Texture>();
            foreach (SkinnedMeshRenderer r in FindObjectsOfType<SkinnedMeshRenderer>())
            {
                if (r == null || r.sharedMaterial == null || !r.sharedMaterial.HasProperty(MainTex)) continue;
                Texture t = r.sharedMaterial.mainTexture;
                if (!IsHead(t) || !seen.Add(t)) continue;
                Color32[] points = Read(t);
                var bytes = new byte[points.Length * 4];
                for (int n = 0; n < points.Length; n++) { bytes[4 * n] = points[n].r; bytes[4 * n + 1] = points[n].g; bytes[4 * n + 2] = points[n].b; bytes[4 * n + 3] = points[n].a; }
                string path = System.IO.Path.Combine(folder, t.name + "_" + t.width + "x" + t.height + ".rgba");
                System.IO.File.WriteAllBytes(path, bytes);
                said.Append(path).Append("; ");
            }
            return said.Length > 0 ? said.ToString() : "no head pictures in the scene";
        }

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
                foreach (Who who in one.here) text.Append(who.name + (who.outside ? " (outside)" : "") + " " + (Color32)ColourOf(who.name) + (IsClassic(ColourOf(who.name)) ? " (the game's own)" : "") + (HairOf(who.name, out Color hair) ? ", hair " + HairName(hair) + " " + (Color32)hair : ", the game's hair") + (EyeOf(who.name, out Color eye) ? ", eyes " + EyeName(eye) : ", the game's eyes") + "; ");
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

        /// <summary>For the development build: each kerbal's eyeballs and pupils: their meshes' points, the spread of their pictures' coordinates, their materials' pictures and colours (for eye colour).</summary>
        public static string Eyes()
        {
            var said = new System.Text.StringBuilder();
            var seen = new HashSet<Mesh>();
            foreach (SkinnedMeshRenderer r in FindObjectsOfType<SkinnedMeshRenderer>())
            {
                if (r == null || r.sharedMesh == null) continue;
                string n = r.name.ToLowerInvariant();
                if (!n.Contains("pupil") && !n.Contains("eyeball")) continue;
                if (!seen.Add(r.sharedMesh)) continue;
                Mesh m = r.sharedMesh;
                Vector2[] uv = m.uv;
                Vector2 lo = new Vector2(float.MaxValue, float.MaxValue), hi = new Vector2(float.MinValue, float.MinValue);
                foreach (Vector2 q in uv) { lo = Vector2.Min(lo, q); hi = Vector2.Max(hi, q); }
                Material mat = r.sharedMaterial;
                Texture t = mat != null && mat.HasProperty(MainTex) ? mat.mainTexture : null;
                said.Append(r.name).Append(": ").Append(m.vertexCount).Append(" points, uv ").Append(uv.Length > 0 ? lo.ToString("F3") + " to " + hi.ToString("F3") : "none")
                    .Append(", bounds ").Append(m.bounds.size.ToString("F4")).Append(", ").Append(mat != null ? mat.name + " / " + mat.shader.name : "no material")
                    .Append(", picture ").Append(t != null ? t.name + " " + t.width + "x" + t.height : "none")
                    .Append(", colour ").Append(mat != null && mat.HasProperty("_Color") ? ((Color32)mat.color).ToString() : "-").Append("\n");
            }
            return said.Length > 0 ? said.ToString() : "no eyes in the scene";
        }

        /// <summary>For the development build: each kerbal's portrait, as the game draws it for the portraits at the screen's corner, written into a folder as a PNG at its own size.</summary>
        public static string Portraits(string folder)
        {
            var said = new System.Text.StringBuilder();
            foreach (Kerbal kerbal in FindObjectsOfType<Kerbal>())
            {
                if (kerbal == null || kerbal.protoCrewMember == null) continue;
                RenderTexture rt = kerbal.avatarTexture;
                if (rt == null) { said.Append(kerbal.protoCrewMember.name).Append(": no portrait; "); continue; }
                RenderTexture before = RenderTexture.active;
                RenderTexture.active = rt;
                var copy = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                copy.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                copy.Apply(false);
                RenderTexture.active = before;
                string path = System.IO.Path.Combine(folder, "portrait " + kerbal.protoCrewMember.name + ".png");
                System.IO.File.WriteAllBytes(path, copy.EncodeToPNG());
                Destroy(copy);
                said.Append(path).Append(" (").Append(rt.width).Append("x").Append(rt.height).Append("); ");
            }
            return said.Length > 0 ? said.ToString() : "no kerbals with portraits";
        }

        /// <summary>For the development build: a kept value set ("key=value") or taken away ("key="): "hair_Bill_Kerman=0.77,0.63,0.43", "skin_Bill_Kerman=".</summary>
        public static string Keep(string arg)
        {
            int at = (arg ?? "").IndexOf('=');
            if (at <= 0 || mod == null) return "key=value";
            string key = arg.Substring(0, at).Trim(), value = arg.Substring(at + 1).Trim();
            mod.Keep(key, value.Length > 0 ? value : null);
            return key + " " + (value.Length > 0 ? "kept as " + value : "taken away");
        }

        static readonly Dictionary<Renderer, Material> ownMaterials = new Dictionary<Renderer, Material>();

        /// <summary>
        /// (For the development build, item 15.3.) A kerbal's head given another mod's picture, as TextureReplacer's head packs give
        /// one: the game's own picture, its skin turned a person's light tan, under a name of its own, on a copy of its material.
        /// "who" by name (or the first kerbal found); "-" gives every head so changed its own material back. Says where its skin was
        /// found in the new picture (see Band).
        /// </summary>
        public static string ForeignHead(string who)
        {
            string w = (who ?? "").Trim();
            if (w == "-")
            {
                int back = 0;
                foreach (var kv in ownMaterials) if (kv.Key != null) { kv.Key.sharedMaterial = kv.Value; back++; }
                ownMaterials.Clear();
                return back + " heads given their own back";
            }
            var said = new System.Text.StringBuilder();
            var kerbals = new List<Component>();
            foreach (Kerbal k in FindObjectsOfType<Kerbal>()) if (k != null && k.protoCrewMember != null && (w.Length == 0 || k.protoCrewMember.name == w)) kerbals.Add(k);
            foreach (KerbalEVA k in FindObjectsOfType<KerbalEVA>()) if (k != null && k.part != null && k.part.protoModuleCrew.Count > 0 && (w.Length == 0 || k.part.protoModuleCrew[0].name == w)) kerbals.Add(k);
            if (kerbals.Count == 0) return "no such kerbal here";
            if (w.Length == 0) kerbals.RemoveRange(1, kerbals.Count - 1);
            int given = 0;
            foreach (Component k in kerbals)
                foreach (Renderer r in k.GetComponentsInChildren<Renderer>(true))
                {
                    if (!IsHeadMesh(r) || r.sharedMaterial == null || !IsHead(r.sharedMaterial.mainTexture)) continue;
                    Texture own = r.sharedMaterial.mainTexture;
                    Color32[] points = Read(own);
                    var tan = new Color(0.87f, 0.66f, 0.53f);
                    for (int n = 0; n < points.Length; n++)
                    {
                        Color32 q = points[n];
                        float rr = q.r * 0.003921569f, gg = q.g * 0.003921569f, bb = q.b * 0.003921569f;
                        float s = SkinOf(rr, gg, bb, KerbalHue);
                        if (s <= 0f) continue;
                        float shade = (0.299f * rr + 0.587f * gg + 0.114f * bb) / KerbalPlain;
                        points[n] = new Color32((byte)Mathf.Clamp(255f * (rr + (tan.r * shade - rr) * s), 0f, 255f), (byte)Mathf.Clamp(255f * (gg + (tan.g * shade - gg) * s), 0f, 255f),
                                                (byte)Mathf.Clamp(255f * (bb + (tan.b * shade - bb) * s), 0f, 255f), q.a);
                    }
                    var picture = new Texture2D(own.width, own.height, TextureFormat.RGBA32, true) { name = "testForeignHead", wrapMode = own.wrapMode, filterMode = own.filterMode };
                    picture.SetPixels32(points);
                    picture.Apply(true, false);
                    if (!ownMaterials.ContainsKey(r)) ownMaterials[r] = r.sharedMaterial;
                    r.sharedMaterial = new Material(r.sharedMaterial) { name = "test foreign head", mainTexture = picture };
                    Band(points, out float middle, out float plain);
                    said.Append(r.name).Append(": its skin found at hue ").Append(middle.ToString("F3")).Append(", plain ").Append(plain.ToString("F2")).Append("; ");
                    given++;
                }
            return given + " heads given another mod's picture; " + said;
        }

        /// <summary>For the development build (the test save only): a woman free to fly, the first on the books, or one hired if none is (for a test of the women's head picture).</summary>
        public static string Woman()
        {
            KerbalRoster roster = HighLogic.CurrentGame.CrewRoster;
            foreach (ProtoCrewMember member in roster.Crew)
                if (member.gender == ProtoCrewMember.Gender.Female && member.rosterStatus == ProtoCrewMember.RosterStatus.Available) return member.name;
            ProtoCrewMember hired = roster.GetNewKerbal(ProtoCrewMember.KerbalType.Crew);
            if (hired.gender != ProtoCrewMember.Gender.Female)
            {
                hired.gender = ProtoCrewMember.Gender.Female;
                hired.ChangeName(CrewGenerator.GetRandomName(ProtoCrewMember.Gender.Female));
            }
            hired.rosterStatus = ProtoCrewMember.RosterStatus.Available;
            return hired.name;
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
