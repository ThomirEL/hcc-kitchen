using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class DOIGlowController : MonoBehaviour
{
    [Header("Glow Settings")]
    public float minEmission = 0f;
    public float maxEmission = 5f;
    public Color glowColor = Color.white;

    private Material _mat;
    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

    void Awake()
    {
        // Instance the material so we don't affect shared assets
        _mat = GetComponent<Renderer>().material;
        _mat.EnableKeyword("_EMISSION");
    }

    void Update()
    {
        var part = GetComponent<IARPart>();
        if (part != null)
        {
            SetDOI(part.currentDoI);
        }
    }

    public void SetDOI(float doi) // doi in [0, 1]
    {
        float intensity = DOIToEmission(doi);
        _mat.SetColor(EmissionColor, glowColor * intensity);
    }

    float DOIToEmission(float doi)
    {
        // swap this function out freely
        return Mathf.Lerp(minEmission, maxEmission, doi);
    }
}