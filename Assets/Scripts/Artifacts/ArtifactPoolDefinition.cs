using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Artifacts/Artifact Pool", fileName = "New Artifact Pool")]
public class ArtifactPool : ScriptableObject
{
    public List<ArtifactDefinition> artifacts = new List<ArtifactDefinition>();
    
    public List<ArtifactDefinition> GetThreeRandomArtifacts(System.Random rng = null)
    {
        if (artifacts == null || artifacts.Count < 3)
        {
            return null;
        }
        
        if (rng == null)
        {
            rng = new System.Random();
        }

        // Filter out nulls
        var validArtifacts = new List<ArtifactDefinition>();
        foreach (var artifact in artifacts)
        {
            if (artifact != null)
            {
                validArtifacts.Add(artifact);
            }
        }

        if (validArtifacts.Count < 3)
        {
            return null;
        }

        List<ArtifactDefinition> selectedArtifacts = new List<ArtifactDefinition>();

        for (int i = 0; i < 3; i++)
        {
            int index = rng.Next(validArtifacts.Count);
            selectedArtifacts.Add(validArtifacts[index]);
            validArtifacts.RemoveAt(index);
        }

        return selectedArtifacts;
    }

    /// <summary>
    /// Returns the count of valid (non-null) artifacts in the pool.
    /// </summary>
    public int ValidCount
    {
        get
        {
            if (artifacts == null) return 0;
            int count = 0;
            foreach (var artifact in artifacts)
            {
                if (artifact != null) count++;
            }
            return count;
        }
    }
}
