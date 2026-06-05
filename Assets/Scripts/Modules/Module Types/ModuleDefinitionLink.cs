using UnityEngine;


public sealed class ModuleDefinitionLink : MonoBehaviour
{
    [SerializeField] private ArtifactDefinition definition;

    public ArtifactDefinition Definition => definition;

    public bool TryGetDefinition(out ArtifactDefinition outDefinition)
    {
        outDefinition = definition;
        return outDefinition != null;
    }
}

