using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(TheOtherFactor))]
public class TheOtherFactorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        TheOtherFactor script = (TheOtherFactor)target;

        if (GUILayout.Button(script.RunJobs ? "Stop" : "Start"))
        {
            if (script.RunJobs)
            {
                script.StopTheOtherFactor();
            }
            else
            {
                script.DelayedStart();
            }
        }
    }
}
