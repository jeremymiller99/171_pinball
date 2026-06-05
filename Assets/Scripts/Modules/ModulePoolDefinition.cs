using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Modules/Module Pool", fileName = "New Module Pool")]
public class ModulePool : ScriptableObject
{
    public List<ArtifactDefinition> modules = new List<ArtifactDefinition>();
    
    public List<ArtifactDefinition> GetThreeRandomModules(System.Random rng = null)
    {
        if (modules == null || modules.Count < 3)
        {
            return null;
        }
        
        if (rng == null)
        {
            rng = new System.Random();
        }

        // Filter out nulls
        var validModules = new List<ArtifactDefinition>();
        foreach (var module in modules)
        {
            if (module != null)
            {
                validModules.Add(module);
            }
        }

        if (validModules.Count < 3)
        {
            return null;
        }

        List<ArtifactDefinition> selectedModules = new List<ArtifactDefinition>();

        for (int i = 0; i < 3; i++)
        {
            int index = rng.Next(validModules.Count);
            selectedModules.Add(validModules[index]);
            validModules.RemoveAt(index);
        }

        return selectedModules;
    }

    /// <summary>
    /// Returns the count of valid (non-null) modules in the pool.
    /// </summary>
    public int ValidCount
    {
        get
        {
            if (modules == null) return 0;
            int count = 0;
            foreach (var module in modules)
            {
                if (module != null) count++;
            }
            return count;
        }
    }
}
