using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif


[CreateAssetMenu(menuName = "Pinball/Term Definition", fileName = "TermDefinition_")]
public class TermDefinition : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Stable unique id for this term (used for references and future persistence).")]
    [SerializeField] private string id = "";

    [Header("Presentation")]
    [SerializeField] private string displayName = "";
    [TextArea]
    [SerializeField] private string description = "";

    public string Id => id;
    public string DisplayName => displayName;
    public string Description => description;

    public static TermDefinition CreateRuntime(
        string runtimeId,
        string runtimeDisplayName,
        string runtimeDescription)
    {
        var def = CreateInstance<TermDefinition>();
        def.id = runtimeId ?? "";
        def.displayName =
            string.IsNullOrWhiteSpace(runtimeDisplayName)
                ? "Term"
                : runtimeDisplayName;
        def.description = runtimeDescription ?? "";
        return def;
    }

    public string GetSafeDisplayName()
    {
        var localized = DisplayName;
        return string.IsNullOrWhiteSpace(localized) ? "Term" : localized;
    }

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = "Term";
        }

    }

    public void UpdateDesc(string[] data)
    {
        displayName = data[0];
        description = data[1];
    }
}

