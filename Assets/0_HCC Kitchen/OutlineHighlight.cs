using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Drop this on any GameObject to get an instant toggle-able outline.
/// Works with multi-mesh, multi-child hierarchies.
/// Call Enable() / Disable() / Toggle() from any other script.
/// </summary>
[DisallowMultipleComponent]
public class OutlineHighlight : MonoBehaviour
{
    // ─── Inspector ────────────────────────────────────────────────────────────

    [Header("Appearance")]
    [SerializeField] private Color  outlineColor = new Color(1f, 0.85f, 0f, 1f);
    [SerializeField, Range(0f, 0.05f)] private float outlineWidth = 0.006f;

    [Header("Setup")]
    [Tooltip("The material using Custom/URP_InvertedHullOutline shader.")]
    [SerializeField] private Material outlineMaterialSource;

    [Tooltip("Should the outline be visible when the scene starts?")]
    [SerializeField] private bool enableOnStart = false;

    // ─── Private state ────────────────────────────────────────────────────────

    // One outline child GO per MeshFilter found in the hierarchy
    private readonly List<GameObject> _outlineGOs    = new();
    private readonly List<Material>   _outlineMats   = new();  // For cleanup
    private bool _isEnabled;

    // ─── Lifecycle ────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (outlineMaterialSource == null)
        {
            Debug.LogError($"[OutlineHighlight] No outline material assigned on {name}!", this);
            return;
        }

        BuildOutlineRenderers();
    }

    private void Start()
    {
        // Apply initial state without triggering unnecessary work in Awake ordering
        SetOutline(enableOnStart);
    }

    private void OnDestroy()
    {
        // Destroy the instanced materials we created to avoid memory leaks
        foreach (var mat in _outlineMats)
            if (mat) Destroy(mat);

        foreach (var go in _outlineGOs)
            if (go) Destroy(go);
    }

    // ─── Public API ───────────────────────────────────────────────────────────

    public bool IsEnabled => _isEnabled;

    /// <summary>Show the outline.</summary>
    public void Enable()  => SetOutline(true);

    /// <summary>Hide the outline.</summary>
    public void Disable() => SetOutline(false);

    /// <summary>Flip the current state.</summary>
    public void Toggle()  => SetOutline(!_isEnabled);

    /// <summary>Show or hide with an explicit bool — useful for DOI/IAR integration.</summary>
    public void SetOutline(bool active)
    {
        _isEnabled = active;
        foreach (var go in _outlineGOs)
            if (go) go.SetActive(active);
    }

    /// <summary>Change colour at runtime (e.g. danger = red, task = yellow).</summary>
    public void SetColor(Color color)
    {
        outlineColor = color;
        foreach (var mat in _outlineMats)
            mat.SetColor("_OutlineColor", color);
    }

    /// <summary>Change width at runtime.</summary>
    public void SetWidth(float width)
    {
        outlineWidth = width;
        foreach (var mat in _outlineMats)
            mat.SetFloat("_OutlineWidth", width);
    }

    // ─── Internal build ───────────────────────────────────────────────────────

    /// <summary>
    /// For every MeshFilter found in this object's hierarchy, create a sibling
    /// outline GameObject that carries the inverted-hull renderer.
    /// Parented to the same transform so it moves with the mesh automatically.
    /// </summary>
    private void BuildOutlineRenderers()
    {
        MeshFilter[] meshFilters = GetComponentsInChildren<MeshFilter>(true);

        if (meshFilters.Length == 0)
        {
            Debug.LogWarning($"[OutlineHighlight] No MeshFilters found under {name}. " +
                             "Outline will have nothing to render.", this);
            return;
        }

        foreach (MeshFilter mf in meshFilters)
        {
            if (mf.sharedMesh == null) continue;

            // ── Create the outline sibling ────────────────────────────────
            var outlineGO = new GameObject($"[Outline]{mf.gameObject.name}");
            outlineGO.transform.SetParent(mf.transform, worldPositionStays: false);
            outlineGO.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            outlineGO.transform.localScale = Vector3.one;

            // ── Assign the same mesh ──────────────────────────────────────
            var outlineMF = outlineGO.AddComponent<MeshFilter>();
            outlineMF.sharedMesh = mf.sharedMesh;

            // ── Create a per-instance material so colours can differ ──────
            var matInstance = new Material(outlineMaterialSource);
            matInstance.SetColor("_OutlineColor", outlineColor);
            matInstance.SetFloat("_OutlineWidth",  outlineWidth);
            _outlineMats.Add(matInstance);

            // ── Apply to renderer ─────────────────────────────────────────
            var outlineMR = outlineGO.AddComponent<MeshRenderer>();
            outlineMR.sharedMaterial     = matInstance;
            outlineMR.shadowCastingMode  = UnityEngine.Rendering.ShadowCastingMode.Off;
            outlineMR.receiveShadows     = false;
            outlineMR.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;

            // ── Start hidden ──────────────────────────────────────────────
            outlineGO.SetActive(false);
            _outlineGOs.Add(outlineGO);

            Debug.Log($"[OutlineHighlight] Built outline for mesh '{mf.sharedMesh.name}' on {name}");
        }
    }
}