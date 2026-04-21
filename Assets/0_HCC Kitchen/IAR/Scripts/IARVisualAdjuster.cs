using UnityEngine;

[RequireComponent(typeof(IARPart))]
public class IARVisualAdjuster : MonoBehaviour
{
    [Header("Mode")]
    public bool useTransparency = true;
    public bool useBlur = false;
    public bool useOutline = false;

    [Header("Smoothing")]
    public float lerpSpeed = 4f;

    [Header("Transparency")]
    [Range(0f, 1f)] public float minAlpha = 0.2f;
    [Range(0f, 1f)] public float maxAlpha = 1f;

    [Header("Blur (DoI inverted: high DoI = no blur, low DoI = max blur)")]
    [Range(0f, 1f)] public float maxBlur = 1f;

    [Header("Outline Highlight (DoI direct: high DoI = max outline, low DoI = no outline)")]
    [Range(0f, 1f)] public float maxOutlineWidth = 0.05f;
    public Color outlineColor = Color.yellow;

    IARPart              _part;
    Renderer             _renderer;
    Material             _material;
    float _currentAlpha = 1f;
    float _currentBlur = 0f;
    float _currentOutlineWidth = 0f;

    static readonly int BlurID = Shader.PropertyToID("_BlurAmount");
    static readonly int OutlineWidthID = Shader.PropertyToID("_OutlineWidth");
    static readonly int OutlineColorID = Shader.PropertyToID("_OutlineColor");

    void Awake()
    {
        _part = GetComponent<IARPart>();
    }

    void Update()
    {
        // // Lazy initialize: wait for renderer to be assigned on IARPart
        // if (_material == null && _part.cachedRenderer != null)
        // {
        //     _renderer = _part.cachedRenderer;
        //     _material = _renderer.material;
        // }

        // if (_material == null) return;
        // float doi = _part.currentDoI;

        // if (useTransparency) ApplyTransparency(doi);
    }

    void ApplyTransparency(float doi)
    {
        // Map DoI to alpha: high DoI = opaque (maxAlpha), low DoI = transparent (minAlpha)
        float targetAlpha = Mathf.Lerp(minAlpha, maxAlpha, doi);
        _currentAlpha = Mathf.Lerp(_currentAlpha, targetAlpha, Time.deltaTime * lerpSpeed);

        // Apply the alpha to the material
        Color color = _material.color;
        color.a = _currentAlpha;
        _material.color = color;
    }

}