using UnityEditor;
using UnityEngine;

/// <summary>
/// Adds a debug "Toggle Transition" button to the CameraLerpBetweenPoints
/// inspector. Editor-only, for testing the camera move without wiring up UI.
/// </summary>
[CustomEditor(typeof(CameraLerpBetweenPoints))]
public sealed class CameraLerpBetweenPointsEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Debug", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            if (GUILayout.Button("Toggle Transition"))
                ((CameraLerpBetweenPoints)target).ToggleTarget();
        }

        if (!Application.isPlaying)
            EditorGUILayout.HelpBox("Enter Play mode to use the toggle.", MessageType.Info);
    }
}
