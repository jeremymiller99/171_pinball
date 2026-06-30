using UnityEngine;

/// <summary>
/// Source of truth for a buyable component group: an assembled arrangement of
/// several board components (e.g. a bumper cluster or a flipper lane) that gets
/// installed into a matching <see cref="BoardSection"/> as a single unit.
/// Mirrors the <see cref="BoardComponentDefinition"/> pattern, but keys off a
/// <see cref="BoardSectionCategory"/> and points at a group prefab instead of a
/// single-component prefab.
/// </summary>
[CreateAssetMenu(menuName = "Pinball/Component Group Definition", fileName = "ComponentGroupDefinition_")]
public sealed class ComponentGroupDefinition : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Stable unique id for this group (used for references and future persistence).")]
    [SerializeField] private string id = "";

    [Header("Presentation")]
    [SerializeField] private string displayName = "Component Group";
    [TextArea]
    [SerializeField] private string description = "";
    [Tooltip("Only sections with a matching category can install this group.")]
    [SerializeField] private BoardSectionCategory category = BoardSectionCategory.BumperCluster;
    [SerializeField] private BallRarity rarity = BallRarity.Common;
    [SerializeField] private ElementType elementType = ElementType.None;
    [SerializeField] private Sprite icon;

    [Header("Gameplay")]
    [Tooltip("Prefab containing the arranged board components for this group.")]
    [SerializeField] private GameObject groupPrefab;
    [Tooltip("Amount of coins this group will cost")]
    [Min(0)]
    [SerializeField] private int price = 25;

    public string Id => id;
    public string DisplayName => LocalizedContent.Get("group", name, "name", displayName);
    public string Description => LocalizedContent.Get("group", name, "desc", description);
    public BoardSectionCategory Category => category;
    public BallRarity Rarity => rarity;
    public ElementType ElementType => elementType;
    public Sprite Icon => icon;
    public GameObject GroupPrefab => groupPrefab;
    public float Price => price;

    public bool IsValid()
    {
        return groupPrefab != null;
    }

    public string GetSafeDisplayName()
    {
        var localized = DisplayName;
        return string.IsNullOrWhiteSpace(localized) ? "Component Group" : localized;
    }

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = "Component Group";
        }
    }
}
