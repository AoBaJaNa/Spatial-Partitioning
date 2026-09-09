using System.Collections.Generic;
using UnityEngine;

public sealed class SpatialUnitMovementSimulation : MonoBehaviour
{
    [SerializeField, Range(0, 100)] private int movePercent;
    [SerializeField, Min(0f)] private float moveSpeed = 30f;
    [SerializeField, Min(0f)] private float moveRange = 50f;

    private Vector3[] origins;
    private Vector3[] directions;

    UniformGridIndex uniformGridIndex;

    private void Start()
    {
        uniformGridIndex = GetComponent<UniformGridIndex>();
    }
    public void Initialize(IReadOnlyList<GameObject> units)
    {
        int count = units.Count;
        origins = new Vector3[count];
        directions = new Vector3[count];

        for (int i = 0; i < count; i++)
        {
            GameObject unit = units[i];

            if (unit == null)
                continue;

            origins[i] = unit.transform.position;
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            directions[i] = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
        }
    }

    public void Tick(IReadOnlyList<GameObject> units, float deltaTime)
    {
        if (movePercent <= 0 || units.Count == 0)
            return;

        if (origins == null || directions == null || origins.Length != units.Count)
            Initialize(units);

        int movingCount = units.Count * Mathf.Clamp(movePercent, 0, 100) / 100;
        float maxDistanceSqr = moveRange * moveRange;

        for (int i = 0; i < movingCount; i++)
        {
            GameObject unit = units[i];

            if (unit == null)
                continue;

            Vector2Int originalCell = uniformGridIndex.WorldToCell(unit.transform.position);

            Transform unitTransform = unit.transform;
            unitTransform.Translate(directions[i] * moveSpeed * deltaTime, Space.World);

            if ((unitTransform.position - origins[i]).sqrMagnitude > maxDistanceSqr)
                directions[i] = -directions[i];

            Vector2Int currentCell = uniformGridIndex.WorldToCell(unit.transform.position);

            uniformGridIndex.UpdateUnitCell(unit, originalCell, currentCell);
        }
    }

    public void ResetSimulation()
    {
        origins = null;
        directions = null;
    }

    private void OnValidate()
    {
        moveSpeed = Mathf.Max(0f, moveSpeed);
        moveRange = Mathf.Max(0f, moveRange);
    }
}
