using UnityEngine;

[RequireComponent(typeof(IARPart))]
public class IARVisualAdjuster : MonoBehaviour
{
    [Header("Mode")]
    public bool useTransparency = true;

    [Header("Smoothing")]
    public float lerpSpeed = 4f;

    [Header("Transparency")]
    [Range(0f, 1f)] public float minAlpha = 0.2f;
    [Range(0f, 1f)] public float maxAlpha = 1f;

    IARPart              _part;
    Renderer             _renderer;
    Material             _material;
    float _currentAlpha = 1f;

    void Awake()
    {
        _part = GetComponent<IARPart>();
    }

    void Update()
    {
        // Lazy initialize: wait for renderer to be assigned on IARPart
        if (_material == null && _part.cachedRenderer != null)
        {
            _renderer = _part.cachedRenderer;
            _material = _renderer.material;
        }

        if (_material == null) return;
        float doi = _part.currentDoI;

        if (useTransparency) ApplyTransparency(doi);
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

        // Debug: Print alpha value for the chopping board
        if (gameObject.name == "Chopping Board")
            Debug.Log($"ChoppingBoard Alpha: {_currentAlpha:F3} (DoI: {doi:F3}, Target: {targetAlpha:F3})");
        
        if (gameObject.name == "Knife")
            Debug.Log($"Knife Alpha: {_currentAlpha:F3} (DoI: {doi:F3}, Target: {targetAlpha:F3})");
        
        if (gameObject.name == "Juice")
            Debug.Log($"Juice Alpha: {_currentAlpha:F3} (DoI: {doi:F3}, Target: {targetAlpha:F3})");
    }
}