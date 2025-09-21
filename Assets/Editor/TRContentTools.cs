#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;
using TR.Data;

public static class TRContentTools
{
    private const string ResourcesRoot = "Assets/Resources";
    private const string RaritiesPath = ResourcesRoot + "/Rarities";
    private const string CardsPath = ResourcesRoot + "/Cards";
    private const string PacksPath = ResourcesRoot + "/Packs";

    [MenuItem("TR/Content/Create Default Rarities")]
    public static void CreateDefaultRarities()
    {
        EnsureDirs();
        CreateRarity("common", "Common", new Color(1f,1f,1f), 10,
            AnimationCurve.Linear(1, 0, 10, 45),
            AnimationCurve.Linear(1, 1, 10, 1));
        CreateRarity("rare", "Rare", new Color(0.3f,0.6f,1f), 8,
            AnimationCurve.Linear(1, 0, 8, 60),
            AnimationCurve.Linear(1, 1, 8, 1));
        CreateRarity("epic", "Epic", new Color(0.7f,0.3f,1f), 6,
            AnimationCurve.Linear(1, 0, 6, 80),
            AnimationCurve.Linear(1, 1, 6, 1));
        CreateRarity("legendary", "Legendary", new Color(1f,0.8f,0.2f), 5,
            AnimationCurve.Linear(1, 0, 5, 120),
            AnimationCurve.Linear(1, 1, 5, 1));
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("TR: Default rarities created under Resources/Rarities.");
    }

    [MenuItem("TR/Content/Create Sample Cards (no prefabs)")]
    public static void CreateSampleCards()
    {
        EnsureDirs();
        var common = Load<RarityDefinition>(RaritiesPath + "/common.asset");
        var rare = Load<RarityDefinition>(RaritiesPath + "/rare.asset");
        if (common == null || rare == null)
        {
            Debug.LogWarning("TR: Create rarities first via TR/Content/Create Default Rarities.");
            return;
        }

        CreateCard("arrow_tower", "Arrow Tower", common,
            AnimationCurve.Linear(1, 5, common.MaxLevel, 35),
            AnimationCurve.Linear(1, 1, common.MaxLevel, 2),
            AnimationCurve.Linear(1, 3, common.MaxLevel, 5),
            AnimationCurve.Linear(1, 0, common.MaxLevel, 0),
            AnimationCurve.Linear(1, 50, common.MaxLevel, 100));

        CreateCard("cannon_tower", "Cannon Tower", rare,
            AnimationCurve.Linear(1, 12, rare.MaxLevel, 60),
            AnimationCurve.Linear(1, 0.6f, rare.MaxLevel, 1.2f),
            AnimationCurve.Linear(1, 2.5f, rare.MaxLevel, 4.5f),
            AnimationCurve.Linear(1, 0.6f, rare.MaxLevel, 1.2f),
            AnimationCurve.Linear(1, 70, rare.MaxLevel, 130));

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("TR: Sample cards created under Resources/Cards.");
    }

    [MenuItem("TR/Content/Create Sample Packs")]
    public static void CreateSamplePacks()
    {
        EnsureDirs();
        var normal = ScriptableObject.CreateInstance<PackDefinition>();
        SetSerialized(normal, (so) =>
        {
            so.FindProperty("packId").stringValue = "normal_pack";
            so.FindProperty("displayName").stringValue = "Normal Pack";
            so.FindProperty("cardsPerPack").intValue = 5;
            var entries = new SerializedObject(normal).FindProperty("rarityWeights");
        });

        // Build rarity weights array via serialization
        var rarities = new[] { "common", "rare", "epic", "legendary" };
        var weights = new[] { 700, 250, 45, 5 };
        AssignRarityWeights(normal, rarities, weights);
        CreateAssetIfNotExists(normal, PacksPath + "/normal_pack.asset");

        var pro = ScriptableObject.CreateInstance<PackDefinition>();
        SetSerialized(pro, (so) =>
        {
            so.FindProperty("packId").stringValue = "pro_pack";
            so.FindProperty("displayName").stringValue = "Pro Pack";
            so.FindProperty("cardsPerPack").intValue = 5;
        });
        AssignRarityWeights(pro, rarities, new[] { 550, 330, 100, 20 });
        CreateAssetIfNotExists(pro, PacksPath + "/pro_pack.asset");

        var elite = ScriptableObject.CreateInstance<PackDefinition>();
        SetSerialized(elite, (so) =>
        {
            so.FindProperty("packId").stringValue = "elite_pack";
            so.FindProperty("displayName").stringValue = "Elite Pack";
            so.FindProperty("cardsPerPack").intValue = 5;
        });
        AssignRarityWeights(elite, rarities, new[] { 400, 380, 180, 40 });
        CreateAssetIfNotExists(elite, PacksPath + "/elite_pack.asset");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("TR: Sample packs created under Resources/Packs.");
    }

    private static void AssignRarityWeights(PackDefinition pack, string[] rarityIds, int[] weights)
    {
        var so = new SerializedObject(pack);
        var arr = so.FindProperty("rarityWeights");
        arr.arraySize = rarityIds.Length;
        for (int i = 0; i < rarityIds.Length; i++)
        {
            var elem = arr.GetArrayElementAtIndex(i);
            var rarityProp = elem.FindPropertyRelative("rarity");
            var weightProp = elem.FindPropertyRelative("weight");
            weightProp.intValue = weights[i];
            var rarity = Load<RarityDefinition>($"{RaritiesPath}/{rarityIds[i]}.asset");
            rarityProp.objectReferenceValue = rarity;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void CreateRarity(string id, string name, Color color, int maxLevel, AnimationCurve pointsCurve, AnimationCurve dupCurve)
    {
        var asset = ScriptableObject.CreateInstance<RarityDefinition>();
        var so = new SerializedObject(asset);
        so.FindProperty("rarityId").stringValue = id;
        so.FindProperty("displayName").stringValue = name;
        so.FindProperty("color").colorValue = color;
        so.FindProperty("maxLevel").intValue = maxLevel;
        so.FindProperty("pointsToReachLevelCurve").animationCurveValue = pointsCurve;
        so.FindProperty("duplicateToPointsCurve").animationCurveValue = dupCurve;
        so.ApplyModifiedPropertiesWithoutUndo();

        CreateAssetIfNotExists(asset, $"{RaritiesPath}/{id}.asset");
    }

    private static void CreateCard(string id, string name, RarityDefinition rarity,
        AnimationCurve dps, AnimationCurve fireRate, AnimationCurve range, AnimationCurve splash, AnimationCurve cost)
    {
        var asset = ScriptableObject.CreateInstance<CardDefinition>();
        var so = new SerializedObject(asset);
        so.FindProperty("cardId").stringValue = id;
        so.FindProperty("displayName").stringValue = name;
        so.FindProperty("rarity").objectReferenceValue = rarity;
        so.FindProperty("dpsCurve").animationCurveValue = dps;
        so.FindProperty("fireRateCurve").animationCurveValue = fireRate;
        so.FindProperty("rangeCurve").animationCurveValue = range;
        so.FindProperty("splashRadiusCurve").animationCurveValue = splash;
        so.FindProperty("costCurve").animationCurveValue = cost;
        so.ApplyModifiedPropertiesWithoutUndo();

        CreateAssetIfNotExists(asset, $"{CardsPath}/{id}.asset");
    }

    private static void EnsureDirs()
    {
        if (!AssetDatabase.IsValidFolder(ResourcesRoot)) AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder(RaritiesPath)) AssetDatabase.CreateFolder(ResourcesRoot, "Rarities".Replace("Assets/", string.Empty));
        if (!AssetDatabase.IsValidFolder(CardsPath)) AssetDatabase.CreateFolder(ResourcesRoot, "Cards".Replace("Assets/", string.Empty));
        if (!AssetDatabase.IsValidFolder(PacksPath)) AssetDatabase.CreateFolder(ResourcesRoot, "Packs".Replace("Assets/", string.Empty));
    }

    private static void CreateAssetIfNotExists(Object asset, string path)
    {
        if (File.Exists(path)) return;
        AssetDatabase.CreateAsset(asset, path);
        EditorUtility.SetDirty(asset);
    }

    private static T Load<T>(string path) where T : Object
    {
        return AssetDatabase.LoadAssetAtPath<T>(path);
    }

    private static void SetSerialized(Object obj, System.Action<SerializedObject> setter)
    {
        var so = new SerializedObject(obj);
        setter?.Invoke(so);
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif
