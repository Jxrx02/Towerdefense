using UnityEngine;

public class YSort : MonoBehaviour
{
    [SerializeField] private int offset;
    [SerializeField] private int multiplier = 100;

    private SpriteRenderer spriteRenderer;
    private float lastY;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        UpdateSorting();
    }

    public void UpdateSorting()
    {
        float y = transform.position.y;

        if (Mathf.Approximately(y, lastY))
            return;

        lastY = y;

        spriteRenderer.sortingOrder = Mathf.RoundToInt(-y * multiplier) + offset;
    }
        
    public void UpdateSorting(float y)
    {
        if (Mathf.Approximately(y, lastY))
            return;

        lastY = y;

        spriteRenderer.sortingOrder = Mathf.RoundToInt(-y * multiplier) + offset;
    }
}