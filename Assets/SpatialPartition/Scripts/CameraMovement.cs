using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

public class CameraMovement : MonoBehaviour
{
    [Header("Camera Defaults")]
    public Vector3 cameraPosOffset = new Vector3(0,16,0);
    public Vector3 cameraRotationOffset = new Vector3(50,0,0);
    public bool controlToggle = true;

    [Header("Edge Scroll Setting")]
    public float moveSpeed = 20f;
    public float edgeThreshold = 400f;

    [Header("Edge Scroll Setting")]
    public float zoomSpeed = 100f;
    public float minHeight = 5f;
    public float maxHeight = 30f;

    public void CameraInitialized()
    {
        transform.position = cameraPosOffset;
        transform.rotation = Quaternion.Euler(cameraRotationOffset);
    }
    public void CameraInitialized(Vector3 pos)
    {
        transform.position = pos;
        transform.rotation = Quaternion.Euler(cameraRotationOffset);
    }
    private void Update()
    {

        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            controlToggle = !controlToggle;
        }
        if (controlToggle)
        {
            HandleEdgeScroll();
            HandleZoom();
        }
    }
    private void HandleEdgeScroll()
    {
        Vector3 moveDir = Vector3.zero;
        Vector3 mousePos = Mouse.current.position.ReadValue();

        if (mousePos.x >= 0 && mousePos.x <= edgeThreshold)
            moveDir.x -= 1f;
        else if (mousePos.x >= Screen.width - edgeThreshold && mousePos.x <= Screen.width)
            moveDir.x += 1f;

        if (mousePos.y >= 0 && mousePos.y <= edgeThreshold)
            moveDir.z -= 1f;
        else if (mousePos.y >= Screen.height - edgeThreshold && mousePos.y <= Screen.height)
            moveDir.z += 1f;

        if (moveDir != Vector3.zero)
        {
            moveDir.Normalize();
            transform.Translate(moveDir * moveSpeed * Time.deltaTime, Space.World);
        }
    }

    private void HandleZoom()
    {
        float scrollInput = Mouse.current.scroll.ReadValue().y;

        if (Mathf.Abs(scrollInput) > 0.01f)
        {
            Vector3 newPos = transform.position + transform.forward * scrollInput * zoomSpeed * Time.deltaTime;

            if(newPos.y > maxHeight || newPos.y < minHeight)
                return;
            transform.position = newPos;
        }
    }
}
