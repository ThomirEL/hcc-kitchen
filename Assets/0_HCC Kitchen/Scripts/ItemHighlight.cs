using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// Per-item highlight component.
/// Manages billboard ring, silhouette outline, and arrow highlights.
/// Hooks into XRSimpleInteractable so grabbing the item collects it.
///
/// SETUP: Add this component to each kitchen item. It will automatically
/// find the existing XRSimpleInteractable — do NOT add a second one manually.
///
/// Outline material: place your "ItemHighlight_Outline.mat" inside Assets/Resources/.
/// The script loads it automatically — no per-item assignment needed.
///
/// Attach to: each kitchen item GameObject
/// </summary>

[RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable))]
[RequireComponent(typeof(Rigidbody))]
public class ItemHighlight : MonoBehaviour
{
    [Header("Does collecting it make it disappear?")]
    public bool disappearOnCollect = true;

    [Header("Appearance")]
    [Tooltip("Colour used for the ring and arrow highlights.")]
    public Color highlightColour = Color.white;

    [Tooltip("Extra padding around bounds for ring radius.")]
    public float boundsPadding = 0.05f;

    [Tooltip("Tube cross-section radius of the torus ring.")]
    public float ringTubeRadius = 0.012f;

    [Tooltip("Gap between arrow tip and top of object in metres.")]
    public float arrowGap = 0.04f;

    // ── Outline material ──────────────────────────────────────────────────
    // Loaded once from Resources/, shared across every ItemHighlight instance.
    // We never modify it — we just add/remove it from renderer material arrays.
    private static Material s_outlineMaterial;

    // Per-renderer original material arrays, stored so we can restore them
    // cleanly when the outline is removed (avoids stale extra slots).
    private System.Collections.Generic.Dictionary<Renderer, Material[]> _originalMats;

    private bool _outlineActive = false;

    // ── Visuals ───────────────────────────────────────────────────────────
    private GameObject _ringObj;
    private GameObject _arrowObj;

    // ── State ─────────────────────────────────────────────────────────────
    private bool _ringActive  = false;
    private bool _arrowActive = false;

    // ── References ────────────────────────────────────────────────────────
    private Transform               _playerCam;
    private KitchenHighlightManager _manager;

    // ─────────────────────────────────────────────────────────────────────
    // LIFECYCLE
    // ─────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _playerCam    = Camera.main != null ? Camera.main.transform : null;
        _manager      = FindAnyObjectByType<KitchenHighlightManager>();
        _originalMats = new System.Collections.Generic.Dictionary<Renderer, Material[]>();

        // Load the shared outline material once — subsequent instances reuse it
        if (s_outlineMaterial == null)
        {
            s_outlineMaterial = Resources.Load<Material>("ItemHighlight_Outline");
            if (s_outlineMaterial == null)
                Debug.LogError("[ItemHighlight] Could not find 'ItemHighlight_Outline.mat' " +
                               "in any Resources/ folder. Make sure the file is at " +
                               "Assets/Resources/ItemHighlight_Outline.mat");
        }
    }

    private void Start()
    {
        BuildRing();
        BuildArrow();

        _ringObj.SetActive(false);
        _arrowObj.SetActive(false);
    }

    private void OnEnable()
    {
        var interactable = GetComponentInChildren<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
        if (interactable != null)
            interactable.selectEntered.AddListener(OnGrabbed);
        else
            Debug.LogWarning($"[ItemHighlight] No XRSimpleInteractable found on " +
                             $"{gameObject.name} or its children. Grab collection will not work.");
    }

    private void OnDisable()
    {
        var interactable = GetComponentInChildren<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
        if (interactable != null)
            interactable.selectEntered.RemoveListener(OnGrabbed);
    }

    private void LateUpdate()
    {
        if (_ringActive && _playerCam != null && _ringObj != null)
        {
            PositionRing();
            Vector3 dir = _ringObj.transform.position - _playerCam.position;
            if (dir.sqrMagnitude > 0.0001f)
                _ringObj.transform.rotation = Quaternion.LookRotation(dir);
        }

        if (_arrowActive && _arrowObj != null)
            PositionArrow();
    }

    // ─────────────────────────────────────────────────────────────────────
    // GRAB → COLLECTION
    // ─────────────────────────────────────────────────────────────────────

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        if (_manager == null)
        {
            Debug.LogWarning($"[ItemHighlight] {gameObject.name} grabbed but KitchenHighlightManager not found.");
            return;
        }

        Debug.Log($"[ItemHighlight] Grabbed: {gameObject.name}");
        _manager.OnItemCollected(this);
    }

    // ─────────────────────────────────────────────────────────────────────
    // PUBLIC API
    // ─────────────────────────────────────────────────────────────────────

    public void SetCircle(bool on)
    {
        _ringActive = on;
        if (_ringObj != null) _ringObj.SetActive(on);
    }

    public void SetArrow(bool on)
    {
        _arrowActive = on;
        if (_arrowObj != null)
        {
            PositionArrow();
            _arrowObj.SetActive(on);
        }
    }

    /// <summary>
    /// Adds or removes the outline material on every Renderer in this object's
    /// hierarchy. Uses add/remove (not width=0) so non-highlighted items pay
    /// zero GPU cost — no shader passes run at all when the outline is off.
    /// </summary>
    public void SetOutline(bool on)
    {
        if (_outlineActive == on) return;   // no-op if state unchanged
        if (s_outlineMaterial == null) return;

        _outlineActive = on;

        Renderer[] renderers = GetComponentsInChildren<Renderer>();

        foreach (Renderer rend in renderers)
        {
            // Skip the ring and arrow GameObjects we created ourselves
            if (rend.gameObject == _ringObj || rend.gameObject == _arrowObj) continue;

            if (on)
            {
                // Store original array so we can restore it exactly later
                Material[] original = rend.sharedMaterials;
                _originalMats[rend] = original;

                Material[] withOutline = new Material[original.Length + 1];
                original.CopyTo(withOutline, 0);
                withOutline[original.Length] = s_outlineMaterial;
                rend.sharedMaterials = withOutline;
            }
            else
            {
                // Restore the original array if we have it; otherwise strip manually
                if (_originalMats.TryGetValue(rend, out Material[] original))
                {
                    if (rend != null) rend.sharedMaterials = original;
                    _originalMats.Remove(rend);
                }
                else
                {
                    // Fallback: strip the outline material by reference
                    RemoveOutlineMaterialFrom(rend);
                }
            }
        }
    }

    public void ClearAll()
    {
        SetCircle(false);
        SetArrow(false);
        SetOutline(false);
    }

    public void SetColour(Color col)
    {
        highlightColour = col;
        if (_ringObj != null)
        {
            var mr = _ringObj.GetComponent<Renderer>();
            if (mr != null) mr.material.color = col;
        }
        if (_arrowObj != null)
        {
            var mr = _arrowObj.GetComponent<Renderer>();
            if (mr != null) mr.material.color = col;
        }
        // Outline colour is controlled by the shared material in the Inspector
    }

    // ─────────────────────────────────────────────────────────────────────
    // RING
    // ─────────────────────────────────────────────────────────────────────

    private void BuildRing()
    {
        _ringObj = new GameObject($"{gameObject.name}_Ring");
        _ringObj.transform.SetParent(null);

        switch (gameObject.tag)
        {
            case "spice_jar":    _ringObj.transform.localScale = new Vector3(0.7f, 0.7f, 0.7f); break;
            case "can_box":      _ringObj.transform.localScale = new Vector3(0.8f, 0.8f, 0.8f); break;
            case "dry_goods_box":_ringObj.transform.localScale = new Vector3(0.9f, 0.9f, 0.9f); break;
            default:             _ringObj.transform.localScale = Vector3.one; break;
        }

        Bounds b = ComputeWorldBounds();
        _ringObj.transform.position = b.center;
        _ringObj.transform.rotation = Quaternion.identity;

        MeshFilter   mf = _ringObj.AddComponent<MeshFilter>();
        MeshRenderer mr = _ringObj.AddComponent<MeshRenderer>();

        float halfW = b.extents.x;
        float halfH = b.extents.y;
        float ringR = Mathf.Sqrt(halfW * halfW + halfH * halfH) + boundsPadding;

        mf.mesh              = BuildTorusMesh(ringR, ringTubeRadius, 48, 10);
        mr.material          = BuildUnlitMaterial(highlightColour);
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows    = false;
    }

    private Mesh BuildTorusMesh(float ringRadius, float tubeRadius,
                                 int ringSegments, int tubeSegments)
    {
        Mesh mesh      = new Mesh { name = "TorusRing" };
        int  vertCount = ringSegments * tubeSegments;
        var  verts     = new Vector3[vertCount];
        var  normals   = new Vector3[vertCount];
        var  uvs       = new Vector2[vertCount];
        var  tris      = new int[ringSegments * tubeSegments * 6];

        for (int i = 0; i < ringSegments; i++)
        {
            float   ringAngle  = 2f * Mathf.PI * i / ringSegments;
            float   cosR       = Mathf.Cos(ringAngle);
            float   sinR       = Mathf.Sin(ringAngle);
            Vector3 ringCentre = new Vector3(cosR * ringRadius, sinR * ringRadius, 0f);

            for (int j = 0; j < tubeSegments; j++)
            {
                float   tubeAngle = 2f * Mathf.PI * j / tubeSegments;
                float   cosT      = Mathf.Cos(tubeAngle);
                float   sinT      = Mathf.Sin(tubeAngle);
                Vector3 normal    = new Vector3(cosR * cosT, sinR * cosT, sinT);
                int     idx       = i * tubeSegments + j;
                verts[idx]   = ringCentre + normal * tubeRadius;
                normals[idx] = normal;
                uvs[idx]     = new Vector2((float)i / ringSegments, (float)j / tubeSegments);
            }
        }

        int ti = 0;
        for (int i = 0; i < ringSegments; i++)
        {
            int ni = (i + 1) % ringSegments;
            for (int j = 0; j < tubeSegments; j++)
            {
                int nj = (j + 1) % tubeSegments;
                int a  = i * tubeSegments + j,  b  = ni * tubeSegments + j;
                int c  = ni * tubeSegments + nj, d  = i  * tubeSegments + nj;
                tris[ti++] = a; tris[ti++] = b; tris[ti++] = c;
                tris[ti++] = a; tris[ti++] = c; tris[ti++] = d;
            }
        }

        mesh.vertices  = verts;
        mesh.normals   = normals;
        mesh.uv        = uvs;
        mesh.triangles = tris;
        return mesh;
    }

    // ─────────────────────────────────────────────────────────────────────
    // ARROW
    // ─────────────────────────────────────────────────────────────────────

    private void BuildArrow()
    {
        _arrowObj = new GameObject($"{gameObject.name}_Arrow");
        _arrowObj.transform.SetParent(null);
        _arrowObj.transform.localScale = Vector3.one;

        MeshFilter   mf = _arrowObj.AddComponent<MeshFilter>();
        MeshRenderer mr = _arrowObj.AddComponent<MeshRenderer>();

        mf.mesh              = BuildArrowMesh();
        mr.material          = BuildUnlitMaterial(highlightColour);
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows    = false;

        PositionArrow();
    }

    private void PositionRing()
    {
        if (_ringObj == null) return;
        Bounds b = ComputeWorldBounds();
        _ringObj.transform.position = b.center;
    }

    private void PositionArrow()
    {
        if (_arrowObj == null) return;
        Bounds b        = ComputeWorldBounds();
        float tipWorldY = b.max.y + arrowGap;
        _arrowObj.transform.position = new Vector3(b.center.x, tipWorldY, b.center.z);
        _arrowObj.transform.rotation = Quaternion.identity;
    }

    private Mesh BuildArrowMesh()
    {
        Mesh  mesh   = new Mesh { name = "Arrow" };
        float headW  = 0.055f, headH  = 0.075f;
        float shaftW = 0.018f, shaftH = 0.07f;
        float depth  = 0.012f;

        Vector3[] v =
        {
            new Vector3(-headW,  headH,          -depth), // 0
            new Vector3( headW,  headH,          -depth), // 1
            new Vector3( 0f,     0f,             -depth), // 2 tip
            new Vector3(-shaftW, headH,          -depth), // 3
            new Vector3( shaftW, headH,          -depth), // 4
            new Vector3( shaftW, headH + shaftH, -depth), // 5
            new Vector3(-shaftW, headH + shaftH, -depth), // 6
            new Vector3( headW,  headH,           depth), // 7
            new Vector3(-headW,  headH,           depth), // 8
            new Vector3( 0f,     0f,              depth), // 9 tip back
            new Vector3( shaftW, headH,           depth), // 10
            new Vector3(-shaftW, headH,           depth), // 11
            new Vector3(-shaftW, headH + shaftH,  depth), // 12
            new Vector3( shaftW, headH + shaftH,  depth), // 13
        };
        int[] t =
        {
            0,1,2,  3,4,5,  3,5,6,
            7,8,9,  10,11,12,  10,12,13,
            0,2,9,  0,9,8,
            1,7,9,  1,9,2,
            0,8,11, 0,11,3,
            4,10,7, 4,7,1,
            3,11,12,3,12,6,
            5,13,10,5,10,4,
            6,12,13,6,13,5,
        };
        mesh.vertices  = v;
        mesh.triangles = t;
        mesh.RecalculateNormals();
        return mesh;
    }

    // ─────────────────────────────────────────────────────────────────────
    // CLEANUP
    // ─────────────────────────────────────────────────────────────────────

    private void OnDestroy()
    {
        // Restore all renderers to their original materials before we die
        SetOutline(false);

        if (_ringObj  != null) Destroy(_ringObj);
        if (_arrowObj != null) Destroy(_arrowObj);
    }

    // ─────────────────────────────────────────────────────────────────────
    // HELPERS
    // ─────────────────────────────────────────────────────────────────────

    private Bounds ComputeWorldBounds()
    {
        Renderer[] rs = GetComponentsInChildren<Renderer>();
        if (rs.Length > 0)
        {
            Bounds b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }
        Collider[] cs = GetComponentsInChildren<Collider>();
        if (cs.Length > 0)
        {
            Bounds b = cs[0].bounds;
            foreach (var c in cs) b.Encapsulate(c.bounds);
            return b;
        }
        return new Bounds(transform.position, Vector3.one * 0.15f);
    }

    private Material BuildUnlitMaterial(Color col)
    {
        Shader   s   = Shader.Find("Custom/OverlayUnlit")
                    ?? Shader.Find("Universal Render Pipeline/Unlit")
                    ?? Shader.Find("Unlit/Color");
        Material mat = new Material(s) { color = col };
        return mat;
    }

    /// <summary>
    /// Fallback: removes the outline material by reference without a stored original.
    /// Creates a new array with the outline slot filtered out.
    /// </summary>
    private void RemoveOutlineMaterialFrom(Renderer rend)
    {
        if (rend == null || s_outlineMaterial == null) return;

        var current = rend.sharedMaterials;
        int newLen  = 0;
        foreach (var m in current) if (m != s_outlineMaterial) newLen++;
        if (newLen == current.Length) return; // wasn't there

        var cleaned = new Material[newLen];
        int idx = 0;
        foreach (var m in current)
            if (m != s_outlineMaterial) cleaned[idx++] = m;

        rend.sharedMaterials = cleaned;
    }
}