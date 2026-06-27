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
                if (splitData.Length == 4)
                {
                    ballDef.UpdateDesc(splitData[0], splitData[1], splitData[2], splitData[3]);
                } else
                {
                    ballDef.UpdateDesc(splitData[0], splitData[1], splitData[2], splitData[3], splitData[4]);
                }
                
                EditorUtility.SetDirty(ballDef);
                AssetDatabase.SaveAssetIfDirty(ballDef);
            }
        }
    }
}
