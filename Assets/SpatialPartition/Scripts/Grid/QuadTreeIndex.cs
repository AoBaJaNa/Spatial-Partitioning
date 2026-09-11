using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Rendering.Universal;
public class QuadtreeNode
{
    private const int MaxObjectCount = 8;
    private const int MaxDepth = 5;

    public Rect Bounds { get; private set; }
    public int Depth { get; private set; }

    public List<GameObject> Objects { get; private set; } = new();
    public QuadtreeNode[] Children { get; private set; }

    public QuadtreeNode(Rect bounds, int depth)
    {
        this.Bounds = bounds;
        this.Depth = depth;
    }
    public bool IsLeaf => Children == null;

    public void Insert(GameObject unit)
    {
        if (!IsLeaf)
        {
            int index = GetChildIndex(unit.transform.position);
            Children[index].Insert(unit);
            return;
        }

        Objects.Add(unit);

        if (Objects.Count > MaxObjectCount && Depth < MaxDepth)
        {
            Subdivide();

            for (int i = Objects.Count - 1; i >= 0; i--)
            {
                int index = GetChildIndex(Objects[i].transform.position);
                Children[index].Insert(Objects[i]);
                Objects.RemoveAt(i);
            }
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
        Children[0] = new QuadtreeNode(new Rect(Bounds.x, Bounds.y + YSize, XSize, YSize), Depth + 1);
        Children[1] = new QuadtreeNode(new Rect(midX, midY, XSize, YSize), Depth + 1);
        Children[2] = new QuadtreeNode(new Rect(Bounds.x, Bounds.y, XSize, YSize), Depth + 1);
        Children[3] = new QuadtreeNode(new Rect(midX, Bounds.y, XSize, YSize), Depth + 1);
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
    public QuadtreeNode QuadtreeNode { get; private set;}
   public void QuadTreeBuild(IReadOnlyList<GameObject> allUnits)
    {
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

        float XSize = maxX - minX + 20;
        float YSize = maxY - minY + 20;

        QuadtreeNode = new QuadtreeNode(new Rect(minX - 10, minY - 10, XSize, YSize), 0);

        for (int i = 0; i < allUnits.Count; i++)
        {
            QuadtreeNode.Insert(allUnits[i]);
        }
    }
}
