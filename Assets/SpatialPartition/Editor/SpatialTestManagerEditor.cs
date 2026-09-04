using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SpatialTestManager))]
public sealed class SpatialTestManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        var manager = (SpatialTestManager)target;

        GUILayout.Space(12f);
        if (GUILayout.Button("Spawn Units", GUILayout.Height(30f)))
            manager.SpawnUnits();

        if (GUILayout.Button("Clear Units", GUILayout.Height(30f)))
            manager.ClearUnits();
    }
}
