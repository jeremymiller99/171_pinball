using TMPro;
using UnityEngine;

public class DefinitionPanel : MonoBehaviour
{
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text definitionText;

    public void Populate(BallDefinition def)
    {
        nameText.text = def.DisplayName;
        definitionText.text = def.Description;
    }

    public void Populate(TermDefinition def)
    {
        nameText.text = def.DisplayName;
        definitionText.text = def.Description;
    }
}
