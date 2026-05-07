using UnityEngine;

/// <summary>
/// After content is built, uniformly scales the ItemList down
/// until it fits within the parent page RectTransform.
/// Attach to: the ItemList GameObject (child of the page Canvas)
/// </summary>
public class PageAutoScaler : MonoBehaviour
{
    [Tooltip("The RectTransform of the page canvas — content must fit inside this")]
    [SerializeField] private RectTransform _pageRect;

    [Tooltip("Padding in canvas units on each side")]
    [SerializeField] private float _padding = 20f;

    private RectTransform _contentRect;

    private void Awake()
    {
        _contentRect = GetComponent<RectTransform>();
    }

    /// <summary>
    /// Call this after BuildList finishes — pass via coroutine so layout has settled.
    /// </summary>
    public void FitToPage()
    {
        // Reset scale first so we measure at 1:1
        _contentRect.localScale = Vector3.one;

        // Force the layout system to recalculate immediately
        UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(_contentRect);

        float pageHeight   = _pageRect.rect.height - (_padding * 2f);
        float pageWidth    = _pageRect.rect.width  - (_padding * 2f);
        float contentHeight = _contentRect.rect.height;
        float contentWidth  = _contentRect.rect.width;

        // Compute how much we need to scale down on each axis
        float scaleX = contentWidth  > pageWidth  ? pageWidth  / contentWidth  : 1f;
        float scaleY = contentHeight > pageHeight ? pageHeight / contentHeight : 1f;

        // Use the smaller of the two to maintain aspect ratio
        float uniformScale = Mathf.Min(scaleX, scaleY);

        _contentRect.localScale = Vector3.one * uniformScale;

        Debug.Log($"[PageAutoScaler] Content: {contentWidth:F0}x{contentHeight:F0} " +
                  $"| Page: {pageWidth:F0}x{pageHeight:F0} | Scale applied: {uniformScale:F3}");
    }
}