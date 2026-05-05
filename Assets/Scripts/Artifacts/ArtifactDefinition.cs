using UnityEngine;

[CreateAssetMenu(menuName = "Artifacts/Artifact Definition", fileName = "ArtifactDefinition_")]
public sealed class ArtifactDefinition : ScriptableObject
{
    [Header("Presentation")]
    [SerializeField] private string displayName = "Artifact";
    [TextArea]
    [SerializeField] private string description = "";
    [SerializeField] private Sprite icon;

    [Header("Gameplay")]
    [Tooltip("Prefab that will be spawned/used for this artifact.")]
    [SerializeField] private GameObject prefab;

    public string DisplayName => displayName;
    public string Description => description;
    public Sprite Icon => icon;
    public GameObject Prefab => prefab;

    public static ArtifactDefinition CreateRuntime(
        string runtimeId,
        string runtimeDisplayName,
        string runtimeDescription,
        BallRarity runtimeRarity,
        ElementType runtimeElementType,
        Sprite runtimeIcon,
        GameObject runtimePrefab,
        int runtimePrice)
    {
        var def = CreateInstance<ArtifactDefinition>();
        def.displayName =
            string.IsNullOrWhiteSpace(runtimeDisplayName)
                ? "Ball"
                : runtimeDisplayName;
        def.description = runtimeDescription ?? "";
        def.icon = runtimeIcon;
        def.prefab = runtimePrefab;
        return def;
    }

    public bool IsValid()
    {
        return prefab != null;
    }

    public string GetSafeDisplayName()
    {
        return string.IsNullOrWhiteSpace(displayName) ? "Artifact" : displayName;
    }

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = "Ball";
        }
    }
}

