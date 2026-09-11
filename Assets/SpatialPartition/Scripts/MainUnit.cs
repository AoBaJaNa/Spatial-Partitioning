using UnityEngine;

public class MainUnit : MonoBehaviour
{
    [Header("Common Setting")]
    public float searchRadius = 10f;
    public Vector3 targetScale = new(2f, 2f, 2f);
    public Color mainUnitColor = Color.red;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private MaterialPropertyBlock materialProperties;

    private void Awake()
    {
        materialProperties = new MaterialPropertyBlock();
    }

    private void OnValidate()
    {
        transform.localScale = targetScale;
        gameObject.name = "MainUnit";
    }

    private void Start()
    {
        ApplyVisuals();
    }

    private void ApplyVisuals()
    {
        transform.localScale = targetScale;

        if (materialProperties == null ||
            !TryGetComponent<MeshRenderer>(out var renderer))
            return;

        renderer.GetPropertyBlock(materialProperties);
        materialProperties.SetColor(BaseColorId, mainUnitColor);
        materialProperties.SetColor(ColorId, mainUnitColor);
        renderer.SetPropertyBlock(materialProperties);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, searchRadius);
    }
}
