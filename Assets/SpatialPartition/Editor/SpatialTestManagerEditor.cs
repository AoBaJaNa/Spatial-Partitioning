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

        GUILayout.Space(8f);

        if (GUILayout.Button("Reset Grid Update Metrics", GUILayout.Height(26f)))
            manager.ResetGridUpdateMetrics();

        if (GUILayout.Button("Validate Grid Integrity", GUILayout.Height(26f)))
            manager.ValidateGridIntegrity();

        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            if (GUILayout.Button("Run Search Benchmark", GUILayout.Height(26f)))
                manager.RunSearchBenchmark();

            if (GUILayout.Button("Validate Uniform Grid Search", GUILayout.Height(26f)))
                manager.ValidateUniformGridSearch();
        }

        GUILayout.Space(8f);
        using (new EditorGUI.DisabledScope(!Application.isPlaying ||
                                          manager.IsBatchBenchmarkRunning))
        {
            if (GUILayout.Button(
                    "Run Grid Update Benchmark (CSV)",
                    GUILayout.Height(30f)))
            {
                manager.RunBatchBenchmark();
            }
        }
    }
}
