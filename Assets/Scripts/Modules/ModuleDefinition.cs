using UnityEngine;

[CreateAssetMenu(menuName = "Modules/Module Definition", fileName = "ModuleDefinition_")]
public sealed class ModuleDefinition : ScriptableObject
{
    [Header("Presentation")]
    [SerializeField] private string displayName = "Module";
    [TextArea]
    [SerializeField] private string description = "";
    [SerializeField] private Sprite icon;

    [Header("Gameplay")]
    [Tooltip("Prefab that will be spawned/used for this module.")]
    [SerializeField] private GameObject prefab;

    public string DisplayName => displayName;
    public string Description => description;
    public Sprite Icon => icon;
    public GameObject Prefab => prefab;

    public static ModuleDefinition CreateRuntime(
        string runtimeId,
        string runtimeDisplayName,
        string runtimeDescription,
        BallRarity runtimeRarity,
        ElementType runtimeElementType,
        Sprite runtimeIcon,
        GameObject runtimePrefab,
        int runtimePrice)
    {
        var def = CreateInstance<ModuleDefinition>();
        def.displayName =
            string.IsNullOrWhiteSpace(runtimeDisplayName)
                ? "Module"
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
        return string.IsNullOrWhiteSpace(displayName) ? "Module" : displayName;
    }

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = "Module";
        }
    }
}

