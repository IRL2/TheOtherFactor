using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

[CustomEditor(typeof(ScheduleTheOtherFactorStates))]
public class ScheduleTheOtherFactorStatesEditor : Editor
{
    private string presetName = "New Preset";
    private string applyPresetName = "Default";
    private int applyPresetIndex = 0;

    public override void OnInspectorGUI()
    {
        // Custom style for a larger header
        GUIStyle headerStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14, // Increase the font size
            fontStyle = FontStyle.Bold, // Make the font bold
            alignment = TextAnchor.UpperCenter // Center the text
            // You can also set the normal.textColor to change the color
        };

        ScheduleTheOtherFactorStates script = (ScheduleTheOtherFactorStates)target;

        // Applying a preset
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Apply Preset by Name or Index (Name will get Priority)", headerStyle);
        applyPresetName = EditorGUILayout.TextField("Preset Name", applyPresetName);
        applyPresetIndex = EditorGUILayout.IntField("Preset Index", applyPresetIndex);
        if (GUILayout.Button("Apply Preset"))
        {
            if (!string.IsNullOrEmpty(applyPresetName))
            {
                script.ApplyPresetByNameOrIndex(name: applyPresetName);
            }
            else
            {
                script.ApplyPresetByNameOrIndex(index: applyPresetIndex);
            }
        }
        if (GUILayout.Button("Next Preset"))
        {
            script.NextPreset();
        }

        if (GUILayout.Button("Previous Preset"))
        {
            script.PreviousPreset();
        }

        if (Application.isPlaying && GUILayout.Button(script.tofIsRunningJobs ? "Stop" : "Start"))
        {
            if (script.tofIsRunningJobs)
            {
                script.StopTOF();
            }
            else
            {
                script.StartTOF();
            }
        }

        DrawDefaultInspector(); // Draws the default inspector

        // Saving new preset
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Save New Preset", EditorStyles.boldLabel);
        presetName = EditorGUILayout.TextField("Preset Name", presetName);
        if (GUILayout.Button("Save Current State as Preset"))
        {
            script.SavePreset(presetName);
            Debug.Log("Preset saved: " + presetName);
        }
    }
}
