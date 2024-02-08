using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TheOtherFactor))]
public class TheOtherFactorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // Do not call base.OnInspectorGUI() to ensure the inspector is blank
    }
}
