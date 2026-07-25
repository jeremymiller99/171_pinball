using UnityEngine;
using UnityEditor;
using System.IO;

public class TermCSVPopulator
{
    private static string termCSVPath = "/Editor/SOPopulation/Term-Descriptions.csv";
    private static string termSOPath = "/Resources/TermDefinitions/";

    [MenuItem("Tools/Terms/Populate Term Descriptions")]
    public static void PopulateTermDescriptions()
    {
        string[] allLines = File.ReadAllLines(Application.dataPath + termCSVPath);

        foreach (string line in allLines)
        {
            string[] splitData = line.Split(';');
            if (AssetDatabase.AssetPathExists("Assets" + termSOPath + splitData[0] + ".asset"))
            {
                TermDefinition termDef = AssetDatabase.LoadAssetAtPath<TermDefinition>("Assets" + termSOPath + splitData[0] + ".asset");
                termDef.UpdateDesc(splitData);
                EditorUtility.SetDirty(termDef);
                AssetDatabase.SaveAssetIfDirty(termDef);
            }
        }
    }
}
