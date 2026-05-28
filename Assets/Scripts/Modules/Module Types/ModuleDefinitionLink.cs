using UnityEngine;


public sealed class ModuleDefinitionLink : MonoBehaviour
{
    [SerializeField] private ModuleDefinition definition;

    public ModuleDefinition Definition => definition;

    public bool TryGetDefinition(out ModuleDefinition outDefinition)
    {
        outDefinition = definition;
        return outDefinition != null;
    }
}

