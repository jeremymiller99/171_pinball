using UnityEngine;


public sealed class ArtifactDefinitionLink : MonoBehaviour
{
    [SerializeField] private ArtifactDefinition definition;

    public ArtifactDefinition Definition => definition;

    public bool TryGetDefinition(out ArtifactDefinition outDefinition)
    {
        outDefinition = definition;
        return outDefinition != null;
    }
}

