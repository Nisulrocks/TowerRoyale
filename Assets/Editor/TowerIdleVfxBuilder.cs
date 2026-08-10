using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TR.Data;
using TR.VFX;

// Generates one unique idle aura per tower, registers it with the ParticleManager, and assigns the
// key to the matching card.
//
// The auras are built as tinted, rescaled variants of the project's existing Cartoon FX Remaster
// prefabs rather than hand-rolled particle systems: CFXR is the established look here, and
// procedurally authored particles would read as foreign next to every other effect in the game.
// Every tower still gets its own prefab asset, so tuning one never affects another.
//
// Run: TR/VFX/Build Tower Idle Auras
public static class TowerIdleVfxBuilder
{
    private const string OutDir = "Assets/Prefabs/VFX/TowerIdle";
    private const string CfxrDir = "Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs";
    // The registry lives on LobbyBootstrap in the Lobby scene. Searched rather than hardcoded so
    // moving it does not silently skip registration.
    private static readonly string[] CandidateScenes =
    {
        "Assets/Scenes/Lobby.unity",
        "Assets/Scenes/Boot.unity",
    };

    private struct Aura
    {
        public string cardAsset;   // card asset name (unique; two cards share the id "cannon_tower")
        public string key;         // ParticleManager key + generated prefab name
        public string basePrefab;  // CFXR source, relative to CfxrDir
        public Color tint;
        public float scale;
        public string note;        // the fantasy being aimed at

        public Aura(string cardAsset, string key, string basePrefab, Color tint, float scale, string note)
        {
            this.cardAsset = cardAsset; this.key = key; this.basePrefab = basePrefab;
            this.tint = tint; this.scale = scale; this.note = note;
        }
    }

    private static Color C(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out var c);
        return c;
    }

    // Towers are tiny: measured at 1.00-1.28 world units across, averaging 1.06. CFXR prefabs are
    // authored at character/explosion scale (Ambient Glows alone is a 30-unit box), so scales here
    // are deliberately small - roughly 0.2-0.45 - to keep an aura about the size of its tower.
    //
    // Only bases that read flat from a top-down camera are used. Cone, ConeVolume and Box emitters
    // (Fire, Firewall, Shield Leaves, Ambient Glows) look like 3D volumes or a square of dots from
    // above, so they are avoided entirely.
    private static Aura[] BuildTable() => new[]
    {
        // --- Common: restrained, barely larger than the tower ---
        new Aura("Archer Tower",           "idle_archer",     "Light/CFXR3 LightGlow A (Loop)",        C("#C8E6A0"), 0.22f, "faint fletching-green focus"),
        new Aura("arrow_tower",            "idle_arrow",      "Light/CFXR3 LightGlow A (Loop)",        C("#A8E4F0"), 0.20f, "cool draw-string shimmer"),
        new Aura("Crossbow Tower",         "idle_crossbow",   "Light/CFXR3 LightGlow A (Loop)",        C("#8FA8C8"), 0.21f, "cold steel glint"),
        new Aura("Guard Tower",            "idle_guard",      "Liquids/CFXR Water Ripples",            C("#7FA6D8"), 0.26f, "flat protective ward rings"),
        new Aura("Catapult Tower",         "idle_catapult",   "Misc/CFXR Smoke Source 3D",             C("#C2A87C"), 0.16f, "kicked-up siege dust"),
        new Aura("Horn Tower",             "idle_horn",       "Liquids/CFXR Water Ripples",            C("#E8B45A"), 0.24f, "warm amber sound rings"),
        new Aura("Mage Tower",             "idle_mage",       "Magic Misc/CFXR3 Magic Aura A (Runic)", C("#6EA8FF"), 0.22f, "small arcane rune circle"),
        new Aura("BuffCardDefinition",     "idle_buff",       "Magic Misc/CFXR3 Magic Aura A (Runic)", C("#5BE08A"), 0.26f, "uplifting emerald runes"),
        new Aura("EconomyCardDefinition",  "idle_money",      "Misc/CFXR2 Shiny Item (Loop)",          C("#FFD24A"), 0.35f, "coin-glint sparkle"),
        new Aura("PulseCardDefinition",    "idle_pulse",      "Liquids/CFXR Water Ripples",            C("#5FE7E0"), 0.28f, "concentric shock rings"),
        new Aura("InfernoCardDefinition",  "idle_inferno",    "Electric/CFXR Inferno",                 C("#FF8A3D"), 0.26f, "churning heat haze"),

        // --- Rare ---
        new Aura("Cannon Tower",           "idle_cannon",     "Misc/CFXR Smoke Source 3D",             C("#9AA0A6"), 0.18f, "cold gunsmoke"),
        new Aura("cannon_tower",           "idle_cannon_b",   "Misc/CFXR Smoke Source 3D",             C("#B98A5E"), 0.20f, "brass-warm powder smoke"),
        new Aura("Fireball Tower",         "idle_fireball",   "Electric/CFXR Inferno",                 C("#FF7A2A"), 0.24f, "smouldering ember heat"),
        new Aura("Poison Tower",           "idle_poison",     "Misc/CFXR2 Poison Cloud",               C("#7CD64A"), 0.18f, "acrid green seep"),
        new Aura("DOT Tower",              "idle_dot",        "Misc/CFXR2 Poison Cloud",               C("#A8C24A"), 0.15f, "sickly lingering rot"),
        new Aura("Cobweb Tower",           "idle_cobweb",     "Liquids/CFXR Water Ripples",            C("#D8D8D0"), 0.22f, "pale drifting silk rings"),

        // --- Epic ---
        new Aura("Lightning Coil",         "idle_coil",       "Electric/CFXR tesla",                   C("#6FD8FF"), 0.26f, "crackling arc field"),
        new Aura("Enchanting Shrine",      "idle_enchant",    "Magic Misc/CFXR3 Magic Aura A (Runic)", C("#E07AE0"), 0.30f, "violet enchantment sigils"),
        new Aura("Infernal Pillar",        "idle_infernal",   "Electric/CFXR Inferno",                 C("#E03A2A"), 0.30f, "crimson infernal heat"),
        new Aura("Toxic Fortress",         "idle_toxic",      "Misc/CFXR2 Poison Cloud",               C("#3E8F46"), 0.24f, "heavy toxic miasma"),
        new Aura("Royal Treasury",         "idle_treasury",   "Misc/CFXR2 Shiny Item (Loop)",          C("#FFC02E"), 0.45f, "opulent gold shimmer"),
        new Aura("War Gong",               "idle_wargong",    "Liquids/CFXR Water Ripples",            C("#C98A3C"), 0.32f, "bronze resonance rings"),

        // --- Legendary / Mythical: still small, but bright and busy ---
        new Aura("Astral Obelisk",         "idle_astral",     "Magic Misc/CFXR4 Falling Stars",        C("#9A7CFF"), 0.20f, "falling starlight"),
        new Aura("Celestial Beacon",       "idle_celestial",  "Light/CFXR3 LightGlow A (Loop)",        C("#FFF0B0"), 0.34f, "radiant golden beacon"),
        new Aura("Hellfire Monolith",      "idle_hellfire",   "Magic Misc/CFXR3 Magic Aura A (Runic)", C("#B31A1A"), 0.34f, "black-red summoning circle"),
        new Aura("Hyperion",               "idle_hyperion",   "Fire/CFXR4 Sun",                        C("#FFF6D0"), 0.14f, "white-hot solar core"),
        new Aura("Chrono Nexus",           "idle_chrono",     "Magic Misc/CFXR3 Magic Aura A (Runic)", C("#7FF3FF"), 0.32f, "time-fractured cyan rings"),
    };

    [MenuItem("TR/VFX/Build Tower Idle Auras")]
    public static void BuildAll()
    {
        var table = BuildTable();
        System.IO.Directory.CreateDirectory(OutDir);

        var cards = Resources.LoadAll<CardDefinition>("Cards");
        var byAsset = new Dictionary<string, CardDefinition>();
        foreach (var c in cards) if (c != null) byAsset[c.name] = c;

        var built = new Dictionary<string, GameObject>();
        int made = 0, missingBase = 0, missingCard = 0;

        foreach (var aura in table)
        {
            string basePath = $"{CfxrDir}/{aura.basePrefab}.prefab";
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(basePath);
            if (src == null)
            {
                Debug.LogError($"[IdleVfx] Missing CFXR base for '{aura.key}': {basePath}");
                missingBase++;
                continue;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(src);
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            instance.name = aura.key;
            instance.transform.localScale = Vector3.one * Mathf.Max(0.05f, aura.scale);

            ApplyTint(instance, aura.tint);
            MakeLoopingAndPersistent(instance);

            string path = $"{OutDir}/{aura.key}.prefab";
            var saved = PrefabUtility.SaveAsPrefabAsset(instance, path);
            Object.DestroyImmediate(instance);

            if (saved != null) { built[aura.key] = saved; made++; }

            if (byAsset.TryGetValue(aura.cardAsset, out var card))
            {
                var so = new SerializedObject(card);
                var p = so.FindProperty("idleVfxKey");
                if (p != null) { p.stringValue = aura.key; so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(card); }
            }
            else
            {
                Debug.LogWarning($"[IdleVfx] No card asset named '{aura.cardAsset}' for key '{aura.key}'.");
                missingCard++;
            }
        }

        AssetDatabase.SaveAssets();
        RegisterWithParticleManager(built);

        Debug.Log($"[IdleVfx] Built {made} auras. Missing bases: {missingBase}. Unmatched cards: {missingCard}.");
    }

    // Multiply every particle system's colours by the aura tint so each tower reads distinctly
    // while keeping the source effect's shading and alpha animation.
    private static void ApplyTint(GameObject root, Color tint)
    {
        foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = ps.main;
            main.startColor = Tinted(main.startColor, tint);

            var col = ps.colorOverLifetime;
            if (col.enabled) col.color = Tinted(col.color, tint);

            var bySpeed = ps.colorBySpeed;
            if (bySpeed.enabled) bySpeed.color = Tinted(bySpeed.color, tint);
        }

        // CFXR effects often drive a Light for bloom; tint it too or the glow fights the particles.
        foreach (var light in root.GetComponentsInChildren<Light>(true))
            light.color = tint;
    }

    private static ParticleSystem.MinMaxGradient Tinted(ParticleSystem.MinMaxGradient g, Color tint)
    {
        switch (g.mode)
        {
            case ParticleSystemGradientMode.Color:
                return new ParticleSystem.MinMaxGradient(g.color * tint);
            case ParticleSystemGradientMode.TwoColors:
                return new ParticleSystem.MinMaxGradient(g.colorMin * tint, g.colorMax * tint);
            case ParticleSystemGradientMode.Gradient:
                return new ParticleSystem.MinMaxGradient(TintGradient(g.gradient, tint));
            case ParticleSystemGradientMode.TwoGradients:
                return new ParticleSystem.MinMaxGradient(TintGradient(g.gradientMin, tint), TintGradient(g.gradientMax, tint));
            default:
                return g;
        }
    }

    private static Gradient TintGradient(Gradient src, Color tint)
    {
        if (src == null) return null;
        var keys = src.colorKeys;
        for (int i = 0; i < keys.Length; i++) keys[i].color = keys[i].color * tint;

        var g = new Gradient();
        g.SetKeys(keys, src.alphaKeys);
        g.mode = src.mode;
        return g;
    }

    // Idle auras are pooled and must run forever. CFXR prefabs default to destroying themselves
    // when their effect finishes, which would tear the pooled instance out from under the pool.
    private static void MakeLoopingAndPersistent(GameObject root)
    {
        foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.stopAction = ParticleSystemStopAction.None;

            // Volume emitters (a cone or box of particles) betray depth the game does not have.
            // Collapsing them to a flat disc keeps the aura reading as a ring around the tower.
            var shape = ps.shape;
            if (shape.enabled &&
                (shape.shapeType == ParticleSystemShapeType.ConeVolume ||
                 shape.shapeType == ParticleSystemShapeType.Box ||
                 shape.shapeType == ParticleSystemShapeType.BoxShell))
            {
                shape.shapeType = ParticleSystemShapeType.Circle;
                shape.radius = Mathf.Clamp(shape.radius, 0.05f, 1.0f);
                shape.scale = Vector3.one;
            }
        }

        foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (mb == null || mb.GetType().Name != "CFXR_Effect") continue;
            var so = new SerializedObject(mb);
            var clear = so.FindProperty("clearBehavior");
            if (clear != null) clear.enumValueIndex = 0;   // ClearBehavior.None
            var loop = so.FindProperty("looping");
            if (loop != null && loop.propertyType == SerializedPropertyType.Boolean) loop.boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    // The registry lives on a scene object in Boot, so it has to be edited and saved there.
    private static void RegisterWithParticleManager(Dictionary<string, GameObject> built)
    {
        if (built.Count == 0) return;

        // Scenes are loaded additively, never Single: Single would close whatever the user has open
        // and discard unsaved work without asking.
        ParticleManager pm = null;
        UnityEngine.SceneManagement.Scene hostScene = default;
        bool openedHere = false;

        foreach (var path in CandidateScenes)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(path);
            bool weOpenedIt = false;
            if (!scene.isLoaded)
            {
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                weOpenedIt = true;
            }

            foreach (var root in scene.GetRootGameObjects())
            {
                pm = root.GetComponentInChildren<ParticleManager>(true);
                if (pm != null) break;
            }

            if (pm != null) { hostScene = scene; openedHere = weOpenedIt; break; }
            if (weOpenedIt) EditorSceneManager.CloseScene(scene, true);
        }

        if (pm == null)
        {
            Debug.LogError("[IdleVfx] No ParticleManager found in any candidate scene; keys were not registered.");
            return;
        }
        Debug.Log($"[IdleVfx] Registry found on '{pm.gameObject.name}' in {hostScene.path}.");

        int added = 0, updated = 0;
        foreach (var kv in built)
        {
            var ps = kv.Value.GetComponentInChildren<ParticleSystem>(true);
            if (ps == null)
            {
                Debug.LogWarning($"[IdleVfx] '{kv.Key}' has no ParticleSystem; skipped.");
                continue;
            }

            var existing = pm.particles.Find(e => e != null && e.key == kv.Key);
            if (existing != null) { existing.prefab = ps; updated++; }
            else
            {
                pm.particles.Add(new ParticleManager.ParticleEntry
                {
                    key = kv.Key,
                    prefab = ps,
                    preloadCount = 0,
                    maxPoolSize = 8
                });
                added++;
            }
        }

        EditorUtility.SetDirty(pm);
        EditorSceneManager.MarkSceneDirty(hostScene);
        EditorSceneManager.SaveScene(hostScene);
        if (openedHere) EditorSceneManager.CloseScene(hostScene, true);

        Debug.Log($"[IdleVfx] ParticleManager: {added} keys added, {updated} updated.");
    }
}
