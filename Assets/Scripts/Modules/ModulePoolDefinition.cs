using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Modules/Module Pool", fileName = "New Module Pool")]
public class ModulePool : ScriptableObject
{
    public List<ArtifactDefinition> modules = new List<ArtifactDefinition>();
    
    public List<ArtifactDefinition> GetThreeRandomModules(System.Random rng = null)
    {
        if (modules == null)
        {
            return null;
        }

        if (rng == null)
        {
            rng = new System.Random();
        }

        // Valid (non-null) modules, then the testing subset: if ANY module is flagged
        // IsolateForTesting, the pick draws only from those.
        var validModules = new List<ArtifactDefinition>();
        var isolated = new List<ArtifactDefinition>();
        foreach (var module in modules)
        {
            if (module == null) continue;
            validModules.Add(module);
            if (module.IsolateForTesting) isolated.Add(module);
        }

        var pool = isolated.Count > 0 ? isolated : validModules;
        if (pool.Count == 0)
        {
            return null;
        }

        var selectedModules = new List<ArtifactDefinition>();

        if (pool.Count >= 3)
        {
            // Distinct picks when the pool is big enough.
            var remaining = new List<ArtifactDefinition>(pool);
            for (int i = 0; i < 3; i++)
            {
                int index = rng.Next(remaining.Count);
                selectedModules.Add(remaining[index]);
                remaining.RemoveAt(index);
            }
        }
        else
        {
            // A thinned pool (< 3) still fills all three cards, repeating as needed so
            // an isolated module can be tested.
            for (int i = 0; i < 3; i++)
            {
                selectedModules.Add(pool[rng.Next(pool.Count)]);
            }
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
