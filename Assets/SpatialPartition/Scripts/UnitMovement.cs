using UnityEngine;

public class UnitMovement : MonoBehaviour
{
    public int Index { get; set; }
    MaterialPropertyBlock MaterialPropertyBlock;
    Renderer renderer;
    public bool IsSelected { get; private set; }

    private void Awake()
    {
        renderer = GetComponent<Renderer>();
        MaterialPropertyBlock = new MaterialPropertyBlock();
    }

    public void Selected()
    {
        SetColor(true);
        IsSelected = true;
    }
    public void DeSelected()
    {
        SetColor(false);
        IsSelected = false;
    }
    void SetColor(bool isSelected)
    {
        renderer.GetPropertyBlock(MaterialPropertyBlock);
        Color color = isSelected ? Color.red : Color.white;
        MaterialPropertyBlock.SetColor("_BaseColor", color);
        renderer.SetPropertyBlock(MaterialPropertyBlock);
    }
}
