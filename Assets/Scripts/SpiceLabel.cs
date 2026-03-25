using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Spice jar label via RenderTexture + dedicated Camera.
/// No subtitle — just name + background colour.
/// Render is delayed one frame so the canvas is fully built before capture.
/// </summary>
public class SpiceLabel : MonoBehaviour
{
    [Header("Label Content")]
    public string spiceName   = "PAPRIKA";
    public Color  labelColour = new Color(0.73f, 0.21f, 0.08f);

    [Header("References")]
    public Camera        labelCamera;
    public Image         backgroundImage;
    public TMP_Text      nameText;
    public RenderTexture labelRenderTexture;
    public MeshRenderer  jarRenderer;

    private Material _jarMaterial;

    // ─────────────────────────────────────────────────────────────────────
    // LIFECYCLE
    // ─────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (jarRenderer != null)
            _jarMaterial = jarRenderer.material;
    }

    private void Start()
    {
        // Delay by one frame — canvas layout must complete before we render
        StartCoroutine(RenderNextFrame());
    }

    // ─────────────────────────────────────────────────────────────────────
    // PUBLIC API
    // ─────────────────────────────────────────────────────────────────────

    public void SetLabel(string name, Color colour)
    {
        spiceName   = name;
        labelColour = colour;
        StartCoroutine(RenderNextFrame());
    }

    // ─────────────────────────────────────────────────────────────────────
    // RENDER — waits one frame then captures
    // ─────────────────────────────────────────────────────────────────────

    private IEnumerator RenderNextFrame()
    {
        // Write content to canvas immediately
        UpdateCanvasContent();

        // Wait for end of frame so Unity has fully laid out the canvas
        yield return new WaitForEndOfFrame();

        // Now safe to render
        RenderToTexture();
    }

    private void UpdateCanvasContent()
    {
        if (backgroundImage != null)
            backgroundImage.color = labelColour;

        if (nameText != null)
        {
            nameText.text = spiceName.ToUpper();

            // Pick white or dark text based on background brightness
            float lum = labelColour.r * 0.299f
                      + labelColour.g * 0.587f
                      + labelColour.b * 0.114f;

            nameText.color = lum > 0.45f
                ? new Color(0.08f, 0.08f, 0.08f, 1f)
                : new Color(1.00f, 1.00f, 1.00f, 1f);
        }

        // Ensure background draws behind text
        if (backgroundImage != null)
            backgroundImage.transform.SetAsFirstSibling();
    }

    private void RenderToTexture()
    {
        if (labelCamera == null || labelRenderTexture == null || _jarMaterial == null)
        {
            Debug.LogError("[SpiceLabel] Missing reference — cannot render.");
            return;
        }

        // Force canvas to recalculate layout before capture
        Canvas.ForceUpdateCanvases();

        // Render the label camera into the RenderTexture
        RenderTexture prev        = RenderTexture.active;
        RenderTexture.active      = labelRenderTexture;
        labelCamera.targetTexture = labelRenderTexture;
        labelCamera.Render();
        RenderTexture.active      = prev;

        // Apply to jar material — URP/Lit uses _BaseMap not _MainTex
        _jarMaterial.SetTexture("_BaseMap", labelRenderTexture);
    }

    // ─────────────────────────────────────────────────────────────────────
    // EDITOR — update canvas visuals only, never render camera
    // ─────────────────────────────────────────────────────────────────────

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (backgroundImage != null)
            backgroundImage.color = labelColour;

        if (nameText != null)
        {
            nameText.text = spiceName.ToUpper();

            float lum = labelColour.r * 0.299f
                      + labelColour.g * 0.587f
                      + labelColour.b * 0.114f;

            nameText.color = lum > 0.45f
                ? new Color(0.08f, 0.08f, 0.08f, 1f)
                : new Color(1.00f, 1.00f, 1.00f, 1f);
        }

        if (backgroundImage != null)
            backgroundImage.transform.SetAsFirstSibling();
    }
#endif
}