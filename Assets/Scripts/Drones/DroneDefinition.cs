using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Central source of truth for a drone's UI metadata.
/// </summary>
[CreateAssetMenu(menuName = "Drones/Drone Definition", fileName = "DroneDefinition_")]
public class DroneDefinition : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Stable unique id for this drone (used for references and future persistence).")]
    [SerializeField] private string id = "";

    [Header("Presentation")]
    [SerializeField] private string displayName = "Drone";
    [TextArea]
    [SerializeField] private string description = "";
    [SerializeField] private List<string> tags = new List<string>();
    [SerializeField] private Sprite icon;

    [Header("Gameplay")]
    [Tooltip("Prefab that will be spawned/used for this drone.")]
    [SerializeField] private GameObject prefab;
    [Tooltip("Prefab for the upgraded version of this drone.")]
    [SerializeField] private GameObject upgrade;
#if UNITY_EDITOR
    private static string tooltipPrefabPath = "/Prefabs/UI/Tooltips/Tooltip Panel";
    private static string ballSOPath = "/Resources/BallDefinitions/";
    private static string termSOPath = "/Resources/TermDefinitions/";
#endif

    public string Id => id;
    public string DisplayName => LocalizedContent.Get("drone", name, "name", displayName);
    public string Description => LocalizedContent.Get("drone", name, "desc", description);
    public Sprite Icon => icon;
    public GameObject Prefab => prefab;
    public GameObject Upgrade => upgrade;
    public List<string> Tags => tags;

    public static DroneDefinition CreateRuntime(
        string runtimeId,
        string runtimeDisplayName,
        string runtimeDescription,
        Sprite runtimeIcon,
        GameObject runtimePrefab,
        GameObject runtimeUpgrade)
    {
        var def = CreateInstance<DroneDefinition>();
        def.id = runtimeId ?? "";
        def.displayName =
            string.IsNullOrWhiteSpace(runtimeDisplayName)
                ? "Drone"
                : runtimeDisplayName;
        def.description = runtimeDescription ?? "";
        def.icon = runtimeIcon;
        def.prefab = runtimePrefab;
        def.upgrade = runtimeUpgrade;
        return def;
    }

    public bool IsValid()
    {
        return prefab != null;
    }

    public string GetSafeDisplayName()
    {
        var localized = DisplayName;
        return string.IsNullOrWhiteSpace(localized) ? "Drone" : localized;
    }

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = "Drone";
        }
    }

    public void UpdateDesc(string[] data)
    {
        displayName = data[0];
        description = data[1];
        string[] splitTags = data[2].Split(' ');

        for (int i = 0; i < splitTags.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(splitTags[i]) && !tags.Contains(splitTags[i]))
            {
                tags.Add(splitTags[i]);
            }
        }

#if UNITY_EDITOR
        TooltipUI tooltipPrefab = AssetDatabase.LoadAssetAtPath<TooltipUI>("Assets" + tooltipPrefabPath + ".prefab");
        for (int i = 0; i < tags.Count; i++)
        {
            if (AssetDatabase.AssetPathExists("Assets" + ballSOPath + tags[i] + ".asset"))
            {
                BallDefinition ballDef = AssetDatabase.LoadAssetAtPath<BallDefinition>("Assets" + ballSOPath + tags[i] + ".asset");
                if (tooltipPrefab.necessaryBallDefinitions.Contains(ballDef))
                {
                    continue;
                }
                tooltipPrefab.necessaryBallDefinitions.Add(ballDef);
            }
            else if (AssetDatabase.AssetPathExists("Assets" + termSOPath + tags[i] + ".asset"))
            {
                TermDefinition termDef = AssetDatabase.LoadAssetAtPath<TermDefinition>("Assets" + termSOPath + tags[i] + ".asset");
                if (tooltipPrefab.necessaryTermDefinitions.Contains(termDef))
                {
                    continue;
                }
                tooltipPrefab.necessaryTermDefinitions.Add(termDef);
            }
        }
        EditorUtility.SetDirty(tooltipPrefab);
        AssetDatabase.SaveAssetIfDirty(tooltipPrefab);
#endif
    }
}
