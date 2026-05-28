using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Modules/Module Pool", fileName = "New Module Pool")]
public class ModulePool : ScriptableObject
{
    public List<ModuleDefinition> modules = new List<ModuleDefinition>();
    
    public List<ModuleDefinition> GetThreeRandomModules(System.Random rng = null)
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
        var validModules = new List<ModuleDefinition>();
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

        List<ModuleDefinition> selectedModules = new List<ModuleDefinition>();

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
