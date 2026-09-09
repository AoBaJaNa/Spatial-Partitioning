using System.Collections.Generic;
using UnityEngine;

public sealed class SpatialUnitMovementSimulation : MonoBehaviour
{
    [SerializeField, Range(0, 100)] private int movePercent;
    [SerializeField, Min(0f)] private float moveSpeed = 30f;
    [SerializeField, Min(0f)] private float moveRange = 50f;
    [SerializeField] private int randomSeed = 12345;

    private Vector3[] initialPositions;
    private Vector3[] origins;
    private Vector3[] directions;
    private int[] movingUnitIndices;
    private int selectedMovePercent = -1;

    public int MovePercent => movePercent;
    public int LastMovedCount { get; private set; }
    public IReadOnlyList<int> MovingUnitIndices => movingUnitIndices;

    public void SetMovePercent(int value)
    {
        movePercent = Mathf.Clamp(value, 0, 100);
    }

    public void Initialize(IReadOnlyList<GameObject> units)
    {
        int count = units.Count;
        initialPositions = new Vector3[count];
        origins = new Vector3[count];
        directions = new Vector3[count];
        var random = new System.Random(randomSeed);

        for (int i = 0; i < count; i++)
        {
            GameObject unit = units[i];

            if (unit == null)
                continue;

            Vector3 position = unit.transform.position;
            initialPositions[i] = position;
            origins[i] = position;

            float angle = (float)(random.NextDouble() * 360d) * Mathf.Deg2Rad;
            directions[i] = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
        }

        SelectMovingUnits(count, random);
    }

    public void RestoreInitialPositions(IReadOnlyList<GameObject> units)
    {
        if (initialPositions == null || initialPositions.Length != units.Count)
            return;

        for (int i = 0; i < units.Count; i++)
        {
            GameObject unit = units[i];

            if (unit != null)
                unit.transform.position = initialPositions[i];
        }
    }

    public void MoveUnits(
        IReadOnlyList<GameObject> units,
        float deltaTime)
    {
        LastMovedCount = 0;

        if (movePercent <= 0 || units.Count == 0)
            return;

        if (origins == null || directions == null || origins.Length != units.Count)
            Initialize(units);

        if (movingUnitIndices == null ||
            selectedMovePercent != Mathf.Clamp(movePercent, 0, 100))
        {
            SelectMovingUnits(units.Count, CreateSelectionRandom(units.Count));
        }

        float maxDistanceSqr = moveRange * moveRange;

        for (int i = 0; i < movingUnitIndices.Length; i++)
        {
            int unitIndex = movingUnitIndices[i];
            GameObject unit = units[unitIndex];

            if (unit == null)
                continue;

            LastMovedCount++;
            Transform unitTransform = unit.transform;
            unitTransform.Translate(
                directions[unitIndex] * moveSpeed * deltaTime,
                Space.World);

            if ((unitTransform.position - origins[unitIndex]).sqrMagnitude >
                maxDistanceSqr)
            {
                directions[unitIndex] = -directions[unitIndex];
            }
        }
    }

    private System.Random CreateSelectionRandom(int unitCount)
    {
        var random = new System.Random(randomSeed);

        for (int i = 0; i < unitCount; i++)
            random.NextDouble();

        return random;
    }

    private void SelectMovingUnits(int unitCount, System.Random random)
    {
        selectedMovePercent = Mathf.Clamp(movePercent, 0, 100);
        int movingCount = unitCount * selectedMovePercent / 100;
        movingUnitIndices = new int[movingCount];

        int[] indexPool = new int[unitCount];

        for (int i = 0; i < unitCount; i++)
            indexPool[i] = i;

        for (int i = 0; i < movingCount; i++)
        {
            int selectedIndex = random.Next(i, unitCount);
            (indexPool[i], indexPool[selectedIndex]) =
                (indexPool[selectedIndex], indexPool[i]);
            movingUnitIndices[i] = indexPool[i];
        }
    }

    public void ResetSimulation()
    {
        initialPositions = null;
        origins = null;
        directions = null;
        movingUnitIndices = null;
        selectedMovePercent = -1;
        LastMovedCount = 0;
    }

    private void OnValidate()
    {
        movePercent = Mathf.Clamp(movePercent, 0, 100);
        moveSpeed = Mathf.Max(0f, moveSpeed);
        moveRange = Mathf.Max(0f, moveRange);
    }
}
