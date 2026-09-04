using System.Collections.Generic;
using System.Linq;
using UnityEngine.InputSystem;
using UnityEngine;

public class SpatialTestManager : MonoBehaviour
{
    [Header("Spawn Setting")]
    public GameObject unitPrefab;
    public int spawnCount = 10000;
    public float moveSpeed = 5f;
    public Transform spawnPoint;
    [Tooltip("유닛 간의 최소 격자 간격")]
    public float unitSpacing = 1.5f;

    [Tooltip("자연스러운 배치를 위한 무작위 오프셋 범위")]
    public float randomJitter = 0.3f;

    public List<GameObject> SpawnPosList { get; private set; } = new();

    UnitSelectionManager unitSelectionManager;
    CameraMovement cameraMovement;
    private void Awake()
    {
        SpawnPosList = GetComponentsInChildren<UnitMovement>(true)
            .Select(unit => unit.gameObject)
            .ToList();
    }
    private void Start()
    {
        unitSelectionManager = FindFirstObjectByType<UnitSelectionManager>();
        cameraMovement = FindFirstObjectByType<CameraMovement>();
    }
    private void Update()
    {
   
    }
    public void SpawnUnit()
    {
        if (unitPrefab == null) return;

        float startTime = Time.realtimeSinceStartup;
        ClearUnits();

        if (SpawnPosList.Capacity < spawnCount)
        {
            SpawnPosList.Capacity = spawnCount;
        }

        Vector3 basePos = spawnPoint != null ? spawnPoint.position : transform.position;

        // 황금각 (Golden Angle) : 약 137.508도 (라디안 변환)
        float goldenAngle = 137.507764f * Mathf.Deg2Rad;

        for (int i = 0; i < spawnCount; i++)
        {
            // 1. 반경 계산 : index의 제곱근에 비례하여 늘어남 (중심 밀집 방지 핵심)
            float radius = unitSpacing * Mathf.Sqrt(i);

            // 2. 각도 계산 : 황금각을 누적하여 균일하게 회전
            float theta = i * goldenAngle;

            // 3. 극좌표계(r, theta)를 직교좌표계(x, z)로 변환
            float x = radius * Mathf.Cos(theta);
            float z = radius * Mathf.Sin(theta);

            // 4. 약간의 무작위 오프셋(Jitter) 추가
            float offsetX = Random.Range(-randomJitter, randomJitter);
            float offsetZ = Random.Range(-randomJitter, randomJitter);

            Vector3 spawnPos = basePos + new Vector3(x + offsetX, 1f, z + offsetZ);

            GameObject instance = Instantiate(unitPrefab, spawnPos, Quaternion.identity, transform);
            SpawnPosList.Add(instance);
        }

        Debug.Log($"[TestManager] 원형 배치 {spawnCount:N0}개 완료! (소요 시간: {Time.realtimeSinceStartup - startTime:F2}초)");
    }

    public void ClearUnits()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            // 에디터/런타임 겸용 안전한 삭제
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                DestroyImmediate(transform.GetChild(i).gameObject);
                continue;
            }
#endif
            Destroy(transform.GetChild(i).gameObject);
        }

        SpawnPosList.Clear();
        Debug.Log("[TestManager] 생성된 모든 유닛이 삭제되었습니다.");
    }
}