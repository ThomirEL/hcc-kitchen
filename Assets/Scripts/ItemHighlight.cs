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
/// Attach to: each kitchen item GameObject
/// </summary>
public class ItemHighlight : MonoBehaviour
{
    [Header("Appearance")]
    [Tooltip("Colour used for the ring and arrow highlights.")]
    public Color highlightColour = new Color(0f, 0.85f, 1f, 1f);

    [Tooltip("Extra padding around bounds for ring radius.")]
    public float boundsPadding = 0.05f;

    [Tooltip("Tube cross-section radius of the torus ring.")]
    public float ringTubeRadius = 0.012f;

    [Tooltip("Gap between arrow tip and top of object in metres.")]
    public float arrowGap = 0.04f;

    // ── Visuals ───────────────────────────────────────────────────────────
    private GameObject        _ringObj;
    private GameObject        _arrowObj;
    private SilhouetteOutline _outline;

    // ── State ─────────────────────────────────────────────────────────────
    private bool _ringActive    = false;
    private bool _arrowActive   = false;

    // ── References ────────────────────────────────────────────────────────
    private Transform               _playerCam;
    private KitchenHighlightManager _manager;

    // ─────────────────────────────────────────────────────────────────────
    // LIFECYCLE
    // ─────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _playerCam = Camera.main != null ? Camera.main.transform : null;
        _manager   = FindObjectOfType<KitchenHighlightManager>();

        BuildRing();
        BuildArrow();
        SetupOutline();

        _ringObj.SetActive(false);
        _arrowObj.SetActive(false);
        if (_outline != null) _outline.enabled = false;
    }

    private void OnEnable()
    {
        // Find existing XRSimpleInteractable — do not add a new one
        // Using GetComponentInChildren in case interactable is on a child (handle etc.)
        var interactable = GetComponentInChildren<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
        if (interactable != null)
        {
            interactable.selectEntered.AddListener(OnGrabbed);
            Debug.Log($"[ItemHighlight] Hooked grab on: {gameObject.name}");
        }
        else
        {
            Debug.LogWarning($"[ItemHighlight] No XRSimpleInteractable found on " +
                             $"{gameObject.name} or its children. Grab collection " +
                             $"will not work for this item.");
        }
    }

    private void OnDisable()
    {
        var interactable = GetComponentInChildren<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
        if (interactable != null)
            interactable.selectEntered.RemoveListener(OnGrabbed);
    }

    private void LateUpdate()
    {
        // Billboard: ring faces camera every frame
        if (_ringActive && _playerCam != null && _ringObj != null)
        {
            Vector3 dir = _ringObj.transform.position - _playerCam.position;
            if (dir.sqrMagnitude > 0.0001f)
                _ringObj.transform.rotation = Quaternion.LookRotation(dir);
        }

        // Arrow re-positions every frame in case object moves
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
            Debug.LogWarning($"[ItemHighlight] {gameObject.name} grabbed but " +
                             "KitchenHighlightManager not found.");
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
            PositionArrow(); // position before activating to avoid one-frame wrong pos
            _arrowObj.SetActive(on);
        }
    }

    public void SetOutline(bool on)
    {
        if (_outline != null) _outline.enabled = on;
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
        // Outline stays white
    }

    // ─────────────────────────────────────────────────────────────────────
    // RING
    // ─────────────────────────────────────────────────────────────────────

    private void BuildRing()
    {
        _ringObj = new GameObject($"{gameObject.name}_Ring");
        _ringObj.transform.SetParent(transform);
        _ringObj.transform.localScale = Vector3.one;

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
        Mesh mesh     = new Mesh { name = "TorusRing" };
        int  vertCount = ringSegments * tubeSegments;
        var  verts     = new Vector3[vertCount];
        var  normals   = new Vector3[vertCount];
        var  uvs       = new Vector2[vertCount];
        var  tris      = new int[ringSegments * tubeSegments * 6];

        for (int i = 0; i < ringSegments; i++)
        {
            float ringAngle = 2f * Mathf.PI * i / ringSegments;
            float cosR = Mathf.Cos(ringAngle);
            float sinR = Mathf.Sin(ringAngle);
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
                int a = i * tubeSegments + j,   b = ni * tubeSegments + j;
                int c = ni * tubeSegments + nj, d = i  * tubeSegments + nj;
                tris[ti++] = a; tris[ti++] = b; tris[ti++] = c;
                tris[ti++] = a; tris[ti++] = c; tris[ti++] = d;
            }
        }

        mesh.vertices = verts; mesh.normals = normals;
        mesh.uv = uvs;         mesh.triangles = tris;
        return mesh;
    }

    // ─────────────────────────────────────────────────────────────────────
    // ARROW
    // ─────────────────────────────────────────────────────────────────────

    private void BuildArrow()
    {
        _arrowObj = new GameObject($"{gameObject.name}_Arrow");

        // Parent to scene root NOT to this transform — avoids inheriting
        // the item's local scale which causes the arrow to fly upward
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

    private void PositionArrow()
    {
        if (_arrowObj == null) return;
        Bounds  b         = ComputeWorldBounds();
        float   tipWorldY = b.max.y + arrowGap;
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
        mesh.vertices = v; mesh.triangles = t;
        mesh.RecalculateNormals();
        return mesh;
    }

    // ─────────────────────────────────────────────────────────────────────
    // OUTLINE SETUP
    // ─────────────────────────────────────────────────────────────────────

    private void SetupOutline()
    {
        _outline               = gameObject.AddComponent<SilhouetteOutline>();
        _outline.outlineColour = Color.white;
        _outline.enabled       = false;
    }

    // ─────────────────────────────────────────────────────────────────────
    // CLEANUP
    // ─────────────────────────────────────────────────────────────────────

    private void OnDestroy()
    {
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
        Shader   s   = Shader.Find("Universal Render Pipeline/Unlit")
                    ?? Shader.Find("Unlit/Color");
        Material mat = new Material(s) { color = col };
        return mat;
    }
}