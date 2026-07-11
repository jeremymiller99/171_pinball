using UnityEngine;
using UnityEditor;
using System.IO;

public class BallCSVPopulator
{
    private static string ballCSVPath = "/Editor/SOPopulation/Ball-Descriptions.csv";
    private static string ballSOPath = "/Resources/BallDefinitions/";

    [MenuItem("Tools/Balls/Populate Ball Descriptions")]
    public static void PopulateBallDescriptions()
    {
        string[] allLines = File.ReadAllLines(Application.dataPath + ballCSVPath);

        foreach (string line in allLines)
        {
            string[] splitData = line.Split(';');
            if (AssetDatabase.AssetPathExists("Assets" + ballSOPath + splitData[0] + ".asset"))
            {
                BallDefinition ballDef = AssetDatabase.LoadAssetAtPath<BallDefinition>("Assets" + ballSOPath + splitData[0] + ".asset");
                ballDef.UpdateDesc(splitData);
                EditorUtility.SetDirty(ballDef);
                AssetDatabase.SaveAssetIfDirty(ballDef);
            }
        }
    }
}
