using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SpatialTestManager))]
public class SpatialTestManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        SpatialTestManager manager = (SpatialTestManager)target;

        GUILayout.Space(15);

        if(GUILayout.Button("À¯´Ö ½ºÆù(Spawn)", GUILayout.Height(30)))
        {
            manager.SpawnUnit();
        }

        GUILayout.Space(5);
        
        if(GUILayout.Button("À¯´Ö »èÁ¦(Clear)", GUILayout.Height(30)))
        {
            manager.ClearUnits();
        }
    }
}
