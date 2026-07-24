// Updated by Claude Code (claude-opus-5) for JJ on 2026-07-24:
// Populate now routes through a string overload and null-guards its refs so an
// unresolved keyword can no longer NRE or leave the previous item's text.
using TMPro;
using UnityEngine;

public class DefinitionPanel : MonoBehaviour
{
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text definitionText;

    public void Populate(BallDefinition def)
    {
        if (def == null)
        {
            return;
        }

        Populate(def.GetSafeDisplayName(), def.Description);
    }

    public void Populate(TermDefinition def)
    {
        if (def == null)
        {
            return;
        }

        Populate(def.GetSafeDisplayName(), def.Description);
    }

    public void Populate(string displayName, string description)
    {
        if (nameText != null)
        {
            nameText.text = displayName ?? "";
        }

        if (definitionText != null)
        {
            definitionText.text = description ?? "";
        }
    }
}
