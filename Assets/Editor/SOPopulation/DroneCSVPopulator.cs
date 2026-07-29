using UnityEngine;
using UnityEditor;
using System.IO;

public class DroneCSVPopulator
{
    private static string droneCSVPath = "/Editor/SOPopulation/Drone-Descriptions.csv";
    private static string droneSOPath = "/Resources/DroneDefinitions/";

    [MenuItem("Tools/Drones/Populate Drone Descriptions")]
    public static void PopulateDroneDescriptions()
    {
        string[] allLines = File.ReadAllLines(Application.dataPath + droneCSVPath);

        foreach (string line in allLines)
        {
            string[] splitData = line.Split(';');
            if (AssetDatabase.AssetPathExists("Assets" + droneSOPath + splitData[0] + ".asset"))
            {
                DroneDefinition droneDef = AssetDatabase.LoadAssetAtPath<DroneDefinition>("Assets" + droneSOPath + splitData[0] + ".asset");
                droneDef.UpdateDesc(splitData);
                EditorUtility.SetDirty(droneDef);
                AssetDatabase.SaveAssetIfDirty(droneDef);
            }
        }
    }
}
