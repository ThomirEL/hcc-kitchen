using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// URP-compatible silhouette outline using the inverted-hull technique.
///
/// Why not GL/OnRenderObject:
///   OnRenderObject is not called reliably by URP's render pipeline — it is a
///   legacy Built-in pipeline callback. In URP it fires inconsistently or not at all.
///
/// How inverted-hull works instead:
///   A copy of the mesh is created, its normals are flipped (inverted), and it is
///   scaled slightly outward. It renders with front-face culling so only the "outside"
///   of the inflated mesh is visible — which appears as a clean outline around the
///   original object. This technique works on any mesh shape and is the standard
///   outline method used in URP productions.
///
/// Attach to: kitchen item root GameObject (added automatically by ItemHighlight)
/// </summary>
public class SilhouetteOutline : MonoBehaviour
{
    [Tooltip("Colour of the outline. Defaults to white.")]
    public Color outlineColour = Color.white;

    [Tooltip("How thick the outline is in world units. 0.008–0.02 works well for kitchen items.")]
    [Range(0.002f, 0.05f)]
    public float outlineThickness = 0.012f;

    // One hull GameObject per MeshFilter found on this item
    private readonly List<GameObject> _hullObjects = new List<GameObject>();

    private bool _built = false;

    // ─────────────────────────────────────────────────────────────────────
    // LIFECYCLE
    // ─────────────────────────────────────────────────────────────────────

    private void OnEnable()
    {
        if (!_built) BuildHulls();
        SetHullsVisible(true);
    }

    private void OnDisable()
    {
        SetHullsVisible(false);
    }

    private void OnDestroy()
    {
        foreach (var h in _hullObjects)
            if (h != null) Destroy(h);
        _hullObjects.Clear();
    }

    // ─────────────────────────────────────────────────────────────────────
    // HULL BUILDING — one hull mesh per MeshFilter on this item
    // ─────────────────────────────────────────────────────────────────────

    private void BuildHulls()
    {
        MeshFilter[] filters = GetComponentsInChildren<MeshFilter>();

        foreach (var mf in filters)
        {
            if (mf.sharedMesh == null) continue;

            // Create a child GameObject for this hull
            GameObject hull = new GameObject($"_Outline_Hull_{mf.gameObject.name}");
            hull.transform.SetParent(mf.transform, false); // match the mesh transform exactly
            hull.transform.localPosition = Vector3.zero;
            hull.transform.localRotation = Quaternion.identity;
            hull.transform.localScale    = Vector3.one;

            // Add mesh components
            MeshFilter   hullMF = hull.AddComponent<MeshFilter>();
            MeshRenderer hullMR = hull.AddComponent<MeshRenderer>();

            // Build the inverted-normals hull mesh
            hullMF.mesh = BuildInvertedHullMesh(mf.sharedMesh);

            // Assign the outline material
            hullMR.material          = BuildOutlineMaterial();
            hullMR.shadowCastingMode = ShadowCastingMode.Off;
            hullMR.receiveShadows    = false;

            // Layer: render after the original mesh to avoid z-fighting
            hullMR.rendererPriority  = 1;

            hull.SetActive(false); // start hidden
            _hullObjects.Add(hull);
        }

        _built = true;
    }

    /// <summary>
    /// Creates a copy of the mesh with flipped normals and vertices pushed
    /// outward along those normals by outlineThickness.
    /// Front-face culling on the material means only the back (outer) side shows,
    /// creating a clean silhouette border around the original mesh.
    /// </summary>
    private Mesh BuildInvertedHullMesh(Mesh source)
    {
        Mesh hull     = new Mesh();
        hull.name     = source.name + "_Hull";

        Vector3[] srcVerts   = source.vertices;
        Vector3[] srcNormals = source.normals;
        int[]     srcTris    = source.triangles;

        // If mesh has no normals (some OBJ imports), calculate them
        if (srcNormals == null || srcNormals.Length != srcVerts.Length)
        {
            Mesh temp = UnityEngine.Object.Instantiate(source);
            temp.RecalculateNormals();
            srcNormals = temp.normals;
            DestroyImmediate(temp);
        }

        Vector3[] hullVerts   = new Vector3[srcVerts.Length];
        Vector3[] hullNormals = new Vector3[srcNormals.Length];

        for (int i = 0; i < srcVerts.Length; i++)
        {
            // Push each vertex outward along its normal
            hullVerts[i]   = srcVerts[i] + srcNormals[i] * outlineThickness;
            hullNormals[i] = -srcNormals[i]; // flip normals inward
        }

        // Reverse triangle winding so front face is now the inner face
        // Combined with CullMode.Front on the material, only the outer surface renders
        int[] hullTris = new int[srcTris.Length];
        for (int i = 0; i < srcTris.Length; i += 3)
        {
            hullTris[i]     = srcTris[i];
            hullTris[i + 1] = srcTris[i + 2]; // swap last two vertices = flip winding
            hullTris[i + 2] = srcTris[i + 1];
        }

        hull.vertices  = hullVerts;
        hull.normals   = hullNormals;
        hull.triangles = hullTris;

        // Copy UVs if present (not needed for solid colour but avoids warnings)
        if (source.uv != null && source.uv.Length == srcVerts.Length)
            hull.uv = source.uv;

        return hull;
    }

    // ─────────────────────────────────────────────────────────────────────
    // OUTLINE MATERIAL
    // Single-colour unlit material with front-face culling.
    // Front-face culled + inverted normals = only the outer rim is visible.
    // ─────────────────────────────────────────────────────────────────────

    private Material BuildOutlineMaterial()
    {
        // Use URP/Unlit for a clean solid colour unaffected by scene lighting
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            Debug.LogError("[SilhouetteOutline] Could not find URP/Unlit shader. " +
                           "Make sure URP is installed.");
            shader = Shader.Find("Unlit/Color");
        }

        Material mat = new Material(shader);
        mat.color    = outlineColour;

        // CullMode.Front = cull front faces, show back faces only
        // This is what makes the hull appear only at the silhouette edges
        mat.SetFloat("_Cull", (float)CullMode.Front);

        // Ensure it renders as opaque
        mat.SetFloat("_Surface", 0); // 0 = Opaque in URP
        mat.renderQueue = (int)RenderQueue.Geometry + 1;

        return mat;
    }

    // ─────────────────────────────────────────────────────────────────────
    // HELPERS
    // ─────────────────────────────────────────────────────────────────────

    private void SetHullsVisible(bool visible)
    {
        foreach (var h in _hullObjects)
            if (h != null) h.SetActive(visible);
    }
}