using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class SelectionBoxUI : MonoBehaviour
{
    private RectTransform selectionBoxRect;
    private Image selectionBoxImage;

    private Vector2 startMousePos;
    private Vector2 currentMousePos;

    private void Awake()
    {
        selectionBoxRect = GetComponent<RectTransform>();
        selectionBoxImage = GetComponent<Image>();
        selectionBoxImage.enabled = false;

        selectionBoxRect.anchorMin = Vector2.zero;
        selectionBoxRect.anchorMax = Vector2.zero;
        selectionBoxRect.pivot = new Vector2(0.5f, 0.5f);
    }

    private void Update()
    {
        if (Mouse.current == null) return;

        // 마우스 좌클릭 시작
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            startMousePos = Mouse.current.position.ReadValue();
            selectionBoxImage.enabled = true;
        }

        // 마우스 좌클릭 유지 (드래그 중)
        if (Mouse.current.leftButton.isPressed)
        {
            currentMousePos = Mouse.current.position.ReadValue();
            UpdateSelectionBox();
        }

        // 마우스 좌클릭 뗌
        if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            selectionBoxImage.enabled = false;
        }
    }

    private void UpdateSelectionBox()
    {
        float width = currentMousePos.x - startMousePos.x;
        float height = currentMousePos.y - startMousePos.y;

        selectionBoxRect.sizeDelta = new Vector2(Mathf.Abs(width), Mathf.Abs(height));
        selectionBoxRect.anchoredPosition = startMousePos + new Vector2(width / 2, height / 2);
    }

    // 외부(UnitManager)에서 드래그 박스 영역(Screen Space)을 읽을 때 사용
    public Rect GetScreenRect()
    {
        Vector2 min = Vector2.Min(startMousePos, currentMousePos);
        Vector2 max = Vector2.Max(startMousePos, currentMousePos);
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }
}