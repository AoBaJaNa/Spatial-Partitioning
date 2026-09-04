using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class UnitSelectionManager : MonoBehaviour
{
    private Camera mainCamera;
    [SerializeField] private SelectionBoxUI selectionBoxUI;
    [SerializeField] private TMP_Text selectedCountTxt;
    public float unitMinimumSpace = 1f;
    private LayerMask ground;
    private SpatialTestManager testManager;
    private List<UnitMovement> selectedUnit = new();
    Vector3 targetPos;

    UnitMovementManager UnitMovementManager;
    private void Awake()
    {
        UnitMovementManager = GetComponent<UnitMovementManager>();
        mainCamera = Camera.main;
        testManager = GetComponent<SpatialTestManager>();
    }
    private void Start()
    {
        ground = LayerMask.GetMask("Ground");
    }
    private void Update()
    {
        if (Mouse.current == null) return;

        // 드래그 마치는 순간 일괄 선택 판별
        if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            SelectUnitsInBox(selectionBoxUI.GetScreenRect());
            selectedCountTxt.text = "Selected: " + selectedUnit.Count;
        }

        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            Vector2 mousePos = Mouse.current.position.ReadValue();

            // 1. 카메라에서 마우스 위치로 향하는 Ray 생성
            Ray ray = mainCamera.ScreenPointToRay(mousePos);

            // 2-A. 바닥 오브젝트에 Collider가 있는 경우 (Raycast 활용)
            if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, ground))
            {
                CalcTargetPos(hit.point);
            }
        }
    }
    public void CalcTargetPos(Vector3 pos)
    {
        targetPos = pos;
        targetPos = new Vector3(targetPos.x, 1, targetPos.z);

        int columns = Mathf.CeilToInt(Mathf.Sqrt(selectedUnit.Count));
        int rows = Mathf.CeilToInt((float)selectedUnit.Count / columns);

        float halfWidth = (columns - 1) * unitMinimumSpace * 0.5f;
        float halfHeight = (rows - 1) * unitMinimumSpace * 0.5f;

        for (int i = 0; i < selectedUnit.Count; i++)
        {
            int row = i / columns;
            int col = i % columns;

            Vector3 offset = new Vector3((
                col * unitMinimumSpace) - halfWidth,
                0,
                row * unitMinimumSpace - halfHeight);

            UnitMovementManager.SetTargetPos(selectedUnit[i].Index, targetPos + offset);
        }
    }

    private void SelectUnitsInBox(Rect dragRect)
    {
        // SpatialTestManager에서 스폰된 유닛 리스트를 가져와 순회
        selectedUnit.Clear();
        foreach (var unit in testManager.SpawnPosList)
        {
            if (unit == null) continue;

            Vector3 screenPos = mainCamera.WorldToScreenPoint(unit.transform.position);
            UnitMovement um = unit.GetComponent<UnitMovement>();

            if (screenPos.z > 0 && dragRect.Contains(screenPos))
            {
                selectedUnit.Add(um);
                um.Selected();
            }
            else
            {
                um.DeSelected();
            }
        }
        
    }
}