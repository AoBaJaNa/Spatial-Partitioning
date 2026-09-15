using UnityEngine;
using System.Collections.Generic;
public class QuadtreeNode
{
    private const int MergeCountThreshold = 3;
    private readonly int maxObjectCount;
    private readonly int maxDepth;

    public Rect Bounds { get; private set; }
    public int Depth { get; private set; }

    public List<GameObject> Objects { get; private set; } = new();
    public QuadtreeNode[] Children { get; private set; }

    public QuadtreeNode(
        Rect bounds,
        int depth,
        int maxObjectCount,
        int maxDepth,
        QuadtreeNode parent = null)
    {
        this.Bounds = bounds;
        this.Depth = depth;
        this.maxObjectCount = maxObjectCount;
        this.maxDepth = maxDepth;
        this.ParentNode = parent;
    }
    public bool IsLeaf => Children == null;
    public QuadtreeNode ParentNode { get; private set; }

    public void Insert(
        GameObject unit,
        Dictionary<GameObject, QuadtreeNode> unitNodeMap,
        ref int splitCount)
    {
        if (!IsLeaf)
        {
            int index = GetChildIndex(unit.transform.position);
            Children[index].Insert(unit, unitNodeMap, ref splitCount);
            return;
        }

        unitNodeMap[unit] = this;
        Objects.Add(unit);

        if (Objects.Count > maxObjectCount && Depth < maxDepth)
        {
            Subdivide();
            splitCount++;

            for (int i = Objects.Count - 1; i >= 0; i--)
            {
                int index = GetChildIndex(Objects[i].transform.position);
                Children[index].Insert(Objects[i], unitNodeMap, ref splitCount);
                Objects.RemoveAt(i);
            }

            return;
        }
    }
    public int GetChildIndex(Vector3 pos)
    {
        float midX = Bounds.x + (Bounds.width / 2f);
        float midZ = Bounds.y + (Bounds.height / 2f);

        bool isRight = pos.x >= midX;
        bool isTop = pos.z >= midZ;

        if (isTop && !isRight) return 0;
        if (isTop && isRight) return 1;
        if (!isTop && !isRight) return 2;
        return 3;

    }

    public void Subdivide()
    {
        float midX = Bounds.x + (Bounds.width / 2f);
        float midY = Bounds.y + (Bounds.height / 2f);

        float XSize = Bounds.width / 2f;
        float YSize = Bounds.height / 2f;

        Children = new QuadtreeNode[4];
        Children[0] = new QuadtreeNode(new Rect(Bounds.x, Bounds.y + YSize, XSize, YSize), Depth + 1, maxObjectCount, maxDepth, this);
        Children[1] = new QuadtreeNode(new Rect(midX, midY, XSize, YSize), Depth + 1, maxObjectCount, maxDepth, this);
        Children[2] = new QuadtreeNode(new Rect(Bounds.x, Bounds.y, XSize, YSize), Depth + 1, maxObjectCount, maxDepth, this);
        Children[3] = new QuadtreeNode(new Rect(midX, Bounds.y, XSize, YSize), Depth + 1, maxObjectCount, maxDepth, this);
    }
    public bool TryMerge(Dictionary<GameObject, QuadtreeNode> unitNodeMap)
    {
        if(IsLeaf)
        {
            return false;
        }

        int totalCount = 0;

        for( int i = 0; i < 4; i++ )
        {
            if (Children[i].IsLeaf)
                totalCount += Children[i].Objects.Count;
            else
            {
                return false;
            }
        }

        if(totalCount > MergeCountThreshold)
            return false;

        List<GameObject> mergeUnits = new();

        foreach(var unit in Children)
        {
            mergeUnits.AddRange(unit.Objects);
        }

        Children = null;

        foreach (var unit in mergeUnits)
        {
            Objects.Add(unit);
            unitNodeMap[unit] = this;
        }

        return true;
    }

    public void Query(Vector3 center, float radius, List<Transform> result, ref int checkCount)
    {
        if (!OverlapBounds(center, radius))
            return;

        if(IsLeaf)
        {
            float radiusSqr = radius * radius;

            for(int i = 0; i < Objects.Count; i++)
            {
                if(Objects[i] == null)
                    continue;

                Vector3 deltaDist = Objects[i].transform.position - center;

                checkCount++;

                if (deltaDist.sqrMagnitude <= radiusSqr)
                    result.Add(Objects[i].transform);
            }
            return;
        }

        for(int i = 0; i < Children.Length; i++)
        {
            Children[i].Query(center, radius, result, ref checkCount);
        }
    }

    public bool OverlapBounds(Vector3 center, float radius)
    {
        float checkX = Mathf.Clamp(center.x, Bounds.xMin, Bounds.xMax);
        float checkZ = Mathf.Clamp(center.z, Bounds.yMin, Bounds.yMax);

        Vector2 checkVector2 = new Vector2(checkX - center.x, checkZ - center.z);

        float deltaSqrDist = checkVector2.sqrMagnitude;

        return deltaSqrDist <= radius * radius;
    }

    
}
public class QuadTreeIndex : MonoBehaviour
{
    [Header("Subdivision Settings")]
    [SerializeField, Min(1)] private int maxObjectsPerLeaf = 10;
    [SerializeField, Min(0)] private int maxDepth = 10;

    [Header("Scene View Gizmos")]
    [SerializeField] private bool drawQuadtreeGizmos = true;
    [SerializeField] private bool onlyDrawWhenQuadtreeModeIsActive = true;
    [SerializeField] private bool drawLeafNodes = true;
    [SerializeField, Min(1)] private int maxDrawnNodes = 5000;
    [SerializeField, Min(0.01f)] private float gizmoHeight = 0.1f;
    [SerializeField] private Color branchNodeColor =
        new(1f, 0.72f, 0.2f, 0.85f);
    [SerializeField] private Color leafNodeColor =
        new(0.35f, 1f, 0.4f, 0.65f);

    public QuadtreeNode QuadtreeNode { get; private set;}
    public readonly Dictionary<GameObject, QuadtreeNode> QuadTreeTable = new();
    private IReadOnlyList<GameObject> unitCache;
    private int lastSplitCount;
    private SpatialTestManager spatialTestManager;

    public int LastNodeReinsertedCount { get; private set; }
    public int LastFullRebuildCount { get; private set; }
    public int LastSplitCount => lastSplitCount;
    public int LastMergeCount { get; private set; }
    public int MaxObjectsPerLeaf => maxObjectsPerLeaf;
    public int MaxDepth => maxDepth;

    public void SetSubdivisionParameters(
        int newMaxDepth,
        int newMaxObjectsPerLeaf)
    {
        maxDepth = Mathf.Max(0, newMaxDepth);
        maxObjectsPerLeaf = Mathf.Max(1, newMaxObjectsPerLeaf);
    }

    public void Clear()
    {
        QuadtreeNode = null;
        QuadTreeTable.Clear();
        ResetUpdateStats();
    }

   public void QuadTreeBuild(IReadOnlyList<GameObject> allUnits)
    {
        Clear();
        unitCache = allUnits;

        if (allUnits == null || allUnits.Count == 0)
        {
            return;
        }

        float maxX = float.MinValue;
        float minX = float.MaxValue;
        float maxY = float.MinValue;
        float minY = float.MaxValue;

        for (int i = 0; i < allUnits.Count; i++)
        {
            Transform unitT = allUnits[i].transform;
            if (unitT.position.x > maxX) maxX = unitT.position.x;
            if (unitT.position.x < minX) minX = unitT.position.x;
            if (unitT.position.z > maxY) maxY = unitT.position.z;
            if (unitT.position.z < minY) minY = unitT.position.z;
        }

        float XSize = maxX - minX + 200;
        float YSize = maxY - minY + 200;

        QuadtreeNode = new QuadtreeNode(
            new Rect(minX - 100, minY - 100, XSize, YSize),
            0,
            maxObjectsPerLeaf,
            maxDepth);

        for (int i = 0; i < allUnits.Count; i++)
        {
            QuadtreeNode.Insert(allUnits[i], QuadTreeTable, ref lastSplitCount);
        }
    }

    public void NodeUpdate(
        IReadOnlyList<GameObject> unitList,
        IReadOnlyList<int> movedUnitIndices,
        out int count)
    {
        count = 0;
        ResetUpdateStats();

        if (QuadtreeNode == null || movedUnitIndices == null)
            return;

        for (int i = 0; i < movedUnitIndices.Count; i++)
        {
            int unitIndex = movedUnitIndices[i];

            if (unitIndex < 0 || unitIndex >= unitList.Count)
                continue;

            GameObject unit = unitList[unitIndex];

            if (!QuadTreeTable.TryGetValue(unit, out QuadtreeNode oldNode))
                continue;

            Vector2 unitPos = new Vector2(unit.transform.position.x, unit.transform.position.z);

            if (oldNode.Bounds.Contains(unitPos))
            {
                continue;
            }

            if(!QuadtreeNode.Bounds.Contains(unitPos))
            {
                RebuildForUpdate(unitCache);
                count = LastNodeReinsertedCount;
                return;
            }

            oldNode.Objects.Remove(unit);
            QuadTreeTable.Remove(unit);

            QuadtreeNode searchNode = oldNode.ParentNode;

            while (searchNode != null)
            {
                if (searchNode.Bounds.Contains(unitPos))
                {
                    searchNode.Insert(unit, QuadTreeTable, ref lastSplitCount);
                    TryMergeUpward(oldNode.ParentNode);
                    count++;
                    LastNodeReinsertedCount++;
                    break;
                }
                searchNode = searchNode.ParentNode;
            }
        }
    }
    public void TryMergeUpward(QuadtreeNode node)
    {
        while (node != null)
        {
            if (!node.TryMerge(QuadTreeTable))
                break;

            LastMergeCount++;
            node = node.ParentNode;
        }
    }

    public void RebuildForUpdate(IReadOnlyList<GameObject> units)
    {
        QuadTreeBuild(units);
        LastFullRebuildCount = 1;
        LastNodeReinsertedCount = units != null ? units.Count : 0;
    }

    public bool Validate(
        IReadOnlyList<GameObject> units,
        out string validationMessage)
    {
        if (units == null || units.Count == 0)
        {
            validationMessage = QuadtreeNode == null && QuadTreeTable.Count == 0
                ? "Tree is empty."
                : "Tree contains data while the unit list is empty.";
            return QuadtreeNode == null && QuadTreeTable.Count == 0;
        }

        if (QuadtreeNode == null)
        {
            validationMessage = "Tree root is missing.";
            return false;
        }

        var indexedUnits = new HashSet<GameObject>();

        if (!ValidateNode(QuadtreeNode, indexedUnits, out validationMessage))
            return false;

        int expectedCount = 0;

        for (int i = 0; i < units.Count; i++)
        {
            GameObject unit = units[i];

            if (unit == null)
                continue;

            expectedCount++;

            if (!indexedUnits.Contains(unit))
            {
                validationMessage = $"{unit.name} is missing from the tree.";
                return false;
            }
        }

        if (indexedUnits.Count != expectedCount ||
            QuadTreeTable.Count != expectedCount)
        {
            validationMessage =
                $"Tree count mismatch. Nodes: {indexedUnits.Count}, " +
                $"Map: {QuadTreeTable.Count}, Expected: {expectedCount}.";
            return false;
        }

        validationMessage = $"{expectedCount:N0} units are registered exactly once.";
        return true;
    }

    private bool ValidateNode(
        QuadtreeNode node,
        HashSet<GameObject> indexedUnits,
        out string validationMessage)
    {
        if (!node.IsLeaf)
        {
            if (node.Objects.Count != 0)
            {
                validationMessage = "A divided node still contains units.";
                return false;
            }

            for (int i = 0; i < node.Children.Length; i++)
            {
                if (node.Children[i].ParentNode != node)
                {
                    validationMessage = "A child has an invalid parent reference.";
                    return false;
                }

                if (!ValidateNode(node.Children[i], indexedUnits, out validationMessage))
                    return false;
            }

            validationMessage = string.Empty;
            return true;
        }

        for (int i = 0; i < node.Objects.Count; i++)
        {
            GameObject unit = node.Objects[i];

            if (unit == null ||
                !node.Bounds.Contains(new Vector2(
                    unit.transform.position.x,
                    unit.transform.position.z)))
            {
                validationMessage = "A leaf contains an invalid unit position.";
                return false;
            }

            if (!indexedUnits.Add(unit))
            {
                validationMessage = $"{unit.name} is registered more than once.";
                return false;
            }

            if (!QuadTreeTable.TryGetValue(unit, out QuadtreeNode mappedNode) ||
                mappedNode != node)
            {
                validationMessage = $"{unit.name} has an invalid node map entry.";
                return false;
            }
        }

        validationMessage = string.Empty;
        return true;
    }

    private void ResetUpdateStats()
    {
        LastNodeReinsertedCount = 0;
        LastFullRebuildCount = 0;
        lastSplitCount = 0;
        LastMergeCount = 0;
    }

    private void OnValidate()
    {
        maxObjectsPerLeaf = Mathf.Max(1, maxObjectsPerLeaf);
        maxDepth = Mathf.Max(0, maxDepth);
        maxDrawnNodes = Mathf.Max(1, maxDrawnNodes);
        gizmoHeight = Mathf.Max(0.01f, gizmoHeight);
    }

    private void OnDrawGizmos()
    {
        if (!drawQuadtreeGizmos ||
            !IsActiveQuadtreeMode() ||
            QuadtreeNode == null)
        return;

        int drawnNodeCount = 0;
        DrawNodeGizmos(QuadtreeNode, ref drawnNodeCount);
    }

    private void DrawNodeGizmos(QuadtreeNode node, ref int drawnNodeCount)
    {
        if (node == null || drawnNodeCount >= maxDrawnNodes)
            return;

        if (!node.IsLeaf || drawLeafNodes)
        {
            Rect bounds = node.Bounds;
            Vector3 center = new(
                bounds.center.x,
                gizmoHeight * 0.5f,
                bounds.center.y);
            Vector3 size = new(bounds.width, gizmoHeight, bounds.height);

            Gizmos.color = node.IsLeaf ? leafNodeColor : branchNodeColor;
            Gizmos.DrawWireCube(center, size);
            drawnNodeCount++;
        }

        if (node.IsLeaf)
            return;

        QuadtreeNode[] children = node.Children;

        for (int i = 0;
             i < children.Length && drawnNodeCount < maxDrawnNodes;
             i++)
        {
            DrawNodeGizmos(children[i], ref drawnNodeCount);
        }
    }

    private bool IsActiveQuadtreeMode()
    {
        if (!onlyDrawWhenQuadtreeModeIsActive)
            return true;

        if (spatialTestManager == null)
            spatialTestManager = GetComponent<SpatialTestManager>();

        return spatialTestManager == null ||
               spatialTestManager.SearchType == SpatialSearchType.QuadTree;
    }
}
