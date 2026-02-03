#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor helper to create sample round modifier assets.
/// Access via menu: Tools > Round Modifiers > Create Sample Assets
/// </summary>
public static class RoundModifierSetupHelper
{
    private const string ModifierDefinitionsPath = "Assets/Scripts/ModifierDefinitions/";
    private const string ModifierPoolsPath = "Assets/Scripts/ModifierPools/";

    [MenuItem("Tools/Round Modifiers/Create Sample Assets")]
    public static void CreateSampleAssets()
    {
        // Create angel modifiers
        CreateModifier("Blessed Scoring", "Points earned are increased by 50%.",
            RoundModifierDefinition.ModifierType.Angel, scoreMultiplier: 1.5f);

        CreateModifier("Lower Bar", "Goal requirement reduced by 300.",
            RoundModifierDefinition.ModifierType.Angel, goalModifier: -300f);

        CreateModifier("Golden Touch", "Coins earned are doubled.",
            RoundModifierDefinition.ModifierType.Angel, coinMultiplier: 2f);

        CreateModifier("Extra Life", "Start with one extra ball.",
            RoundModifierDefinition.ModifierType.Angel, ballModifier: 1);

        // Create devil modifiers
        CreateModifier("Cursed Multiplier", "Multiplier cannot increase this round.",
            RoundModifierDefinition.ModifierType.Devil, disableMultiplier: true);

        CreateModifier("Higher Stakes", "Goal requirement increased by 500.",
            RoundModifierDefinition.ModifierType.Devil, goalModifier: 500f);

        CreateModifier("Poverty", "Coins earned reduced by 50%.",
            RoundModifierDefinition.ModifierType.Devil, coinMultiplier: 0.5f);

        CreateModifier("Fragile", "Start with one fewer ball.",
            RoundModifierDefinition.ModifierType.Devil, ballModifier: -1);

        CreateModifier("Weakened", "Points earned reduced by 25%.",
            RoundModifierDefinition.ModifierType.Devil, scoreMultiplier: 0.75f);

        // Create pools
        CreateAngelPool();
        CreateDevilPool();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("Sample round modifier assets created successfully!");
        Debug.Log($"Angel modifiers: {ModifierDefinitionsPath}");
        Debug.Log($"Devil modifiers: {ModifierDefinitionsPath}");
        Debug.Log($"Pools: {ModifierPoolsPath}");
    }

    private static RoundModifierDefinition CreateModifier(
        string displayName,
        string description,
        RoundModifierDefinition.ModifierType type,
        float scoreMultiplier = 1f,
        float goalModifier = 0f,
        float coinMultiplier = 1f,
        int ballModifier = 0,
        bool disableMultiplier = false)
    {
        string sanitizedName = displayName.Replace(" ", "_");
        string path = $"{ModifierDefinitionsPath}{sanitizedName}.asset";

        // Check if asset already exists
        var existing = AssetDatabase.LoadAssetAtPath<RoundModifierDefinition>(path);
        if (existing != null)
        {
            Debug.Log($"Modifier already exists: {path}");
            return existing;
        }

        // Ensure directory exists
        EnsureDirectoryExists(ModifierDefinitionsPath);

        // Create the modifier
        var modifier = ScriptableObject.CreateInstance<RoundModifierDefinition>();
        modifier.displayName = displayName;
        modifier.description = description;
        modifier.type = type;
        modifier.scoreMultiplier = scoreMultiplier;
        modifier.goalModifier = goalModifier;
        modifier.coinMultiplier = coinMultiplier;
        modifier.ballModifier = ballModifier;
        modifier.disableMultiplier = disableMultiplier;

        AssetDatabase.CreateAsset(modifier, path);
        Debug.Log($"Created modifier: {path}");

        return modifier;
    }

    private static void CreateAngelPool()
    {
        string path = $"{ModifierPoolsPath}AngelPool.asset";

        var existing = AssetDatabase.LoadAssetAtPath<RoundModifierPool>(path);
        if (existing != null)
        {
            Debug.Log($"Pool already exists: {path}");
            return;
        }

        EnsureDirectoryExists(ModifierPoolsPath);

        var pool = ScriptableObject.CreateInstance<RoundModifierPool>();

        // Find all angel modifiers
        string[] guids = AssetDatabase.FindAssets("t:RoundModifierDefinition", new[] { ModifierDefinitionsPath });
        foreach (string guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            var modifier = AssetDatabase.LoadAssetAtPath<RoundModifierDefinition>(assetPath);
            if (modifier != null && modifier.type == RoundModifierDefinition.ModifierType.Angel)
            {
                pool.modifiers.Add(modifier);
            }
        }

        AssetDatabase.CreateAsset(pool, path);
        Debug.Log($"Created angel pool with {pool.modifiers.Count} modifiers: {path}");
    }

    private static void CreateDevilPool()
    {
        string path = $"{ModifierPoolsPath}DevilPool.asset";

        var existing = AssetDatabase.LoadAssetAtPath<RoundModifierPool>(path);
        if (existing != null)
        {
            Debug.Log($"Pool already exists: {path}");
            return;
        }

        EnsureDirectoryExists(ModifierPoolsPath);

        var pool = ScriptableObject.CreateInstance<RoundModifierPool>();

        // Find all devil modifiers
        string[] guids = AssetDatabase.FindAssets("t:RoundModifierDefinition", new[] { ModifierDefinitionsPath });
        foreach (string guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            var modifier = AssetDatabase.LoadAssetAtPath<RoundModifierDefinition>(assetPath);
            if (modifier != null && modifier.type == RoundModifierDefinition.ModifierType.Devil)
            {
                pool.modifiers.Add(modifier);
            }
        }

        AssetDatabase.CreateAsset(pool, path);
        Debug.Log($"Created devil pool with {pool.modifiers.Count} modifiers: {path}");
    }

    private static void EnsureDirectoryExists(string path)
    {
        if (!AssetDatabase.IsValidFolder(path.TrimEnd('/')))
        {
            string parent = System.IO.Path.GetDirectoryName(path.TrimEnd('/'));
            string folderName = System.IO.Path.GetFileName(path.TrimEnd('/'));

            if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(folderName))
            {
                if (!AssetDatabase.IsValidFolder(parent))
                {
                    EnsureDirectoryExists(parent + "/");
                }
                AssetDatabase.CreateFolder(parent, folderName);
            }
        }
    }

    [MenuItem("Tools/Round Modifiers/Assign Pools to Challenge Mode")]
    public static void AssignPoolsToChallengeMode()
    {
        // Find the first ChallengeModeDefinition that doesn't have pools assigned
        string[] guids = AssetDatabase.FindAssets("t:ChallengeModeDefinition");

        if (guids.Length == 0)
        {
            Debug.LogWarning("No ChallengeModeDefinition assets found.");
            return;
        }

        var angelPool = AssetDatabase.LoadAssetAtPath<RoundModifierPool>($"{ModifierPoolsPath}AngelPool.asset");
        var devilPool = AssetDatabase.LoadAssetAtPath<RoundModifierPool>($"{ModifierPoolsPath}DevilPool.asset");

        if (angelPool == null || devilPool == null)
        {
            Debug.LogError("Pools not found. Run 'Create Sample Assets' first.");
            return;
        }

        int assigned = 0;
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var challenge = AssetDatabase.LoadAssetAtPath<ChallengeModeDefinition>(path);

            if (challenge != null && challenge.angelPool == null && challenge.devilPool == null)
            {
                challenge.angelPool = angelPool;
                challenge.devilPool = devilPool;
                challenge.totalRounds = 7;
                challenge.distributionMode = RoundDistributionMode.Guaranteed;
                challenge.guaranteedAngels = 2;
                challenge.guaranteedDevils = 2;

                EditorUtility.SetDirty(challenge);
                assigned++;
                Debug.Log($"Assigned pools to: {path}");
            }
        }

        AssetDatabase.SaveAssets();

        if (assigned > 0)
        {
            Debug.Log($"Assigned pools to {assigned} challenge mode(s).");
        }
        else
        {
            Debug.Log("All challenge modes already have pools assigned, or none were found without pools.");
        }
    }
}
#endif
