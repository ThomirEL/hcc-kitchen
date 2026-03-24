using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Randomly places all child GameObjects on the surface faces of this cube.
/// Uses per-face 2D overlap checks so objects on different faces never
/// block each other, and scales correctly to the actual face size.
/// </summary>
public class RandomSurfacePlacement : MonoBehaviour
{
    [Header("Placement Settings")]
    [Tooltip("Which faces children can be placed on.")]
    public FaceMask allowedFaces = FaceMask.Top | FaceMask.Front | FaceMask.Back
                                              | FaceMask.Left  | FaceMask.Right;

    [Tooltip("Flat padding added between object footprints. 0.01–0.05 is usually enough.")]
    public float minSpacing = 0.02f;

    [Tooltip("How far to push children off the surface so they sit on it.")]
    public float surfaceOffset = 0.01f;

    [Tooltip("How many random positions to try per child before giving up.")]
    public int maxAttempts = 200;

    [Tooltip("Seed for reproducible placement. 0 = random each run.")]
    public int randomSeed = 0;

    [System.Flags]
    public enum FaceMask
    {
        Top    = 1 << 0,
        Bottom = 1 << 1,
        Front  = 1 << 2,
        Back   = 1 << 3,
        Left   = 1 << 4,
        Right  = 1 << 5,
    }

    private struct Face
    {
        public Vector3  normal;
        public Vector3  tangentU;
        public Vector3  tangentV;
        public FaceMask mask;
    }

    private static readonly Face[] AllFaces =
    {
        new Face { normal=Vector3.up,      tangentU=Vector3.right,   tangentV=Vector3.forward,  mask=FaceMask.Top    },
        new Face { normal=Vector3.down,    tangentU=Vector3.right,   tangentV=Vector3.forward,  mask=FaceMask.Bottom },
        new Face { normal=Vector3.forward, tangentU=Vector3.right,   tangentV=Vector3.up,       mask=FaceMask.Front  },
        new Face { normal=Vector3.back,    tangentU=Vector3.right,   tangentV=Vector3.up,       mask=FaceMask.Back   },
        new Face { normal=Vector3.left,    tangentU=Vector3.forward, tangentV=Vector3.up,       mask=FaceMask.Left   },
        new Face { normal=Vector3.right,   tangentU=Vector3.forward, tangentV=Vector3.up,       mask=FaceMask.Right  },
    };

    // Per-face 2D placement tracker
    private class FacePlacement
    {
        public Face face;
        // (u, v, radius) — world-scale units in face-local 2D space
        public List<(float u, float v, float r)> items = new List<(float, float, float)>();
    }

    // ─────────────────────────────────────────────────────────────────────
    // LIFECYCLE
    // ─────────────────────────────────────────────────────────────────────

    private void Start()
    {
        PlaceChildren();
    }

    // ─────────────────────────────────────────────────────────────────────
    // MAIN PLACEMENT
    // ─────────────────────────────────────────────────────────────────────

    public void PlaceChildren()
    {
        if (randomSeed != 0)
            Random.InitState(randomSeed);

        // World-space half extents of this cube
        Vector3 halfSize = new Vector3(
            transform.lossyScale.x * 0.5f,
            transform.lossyScale.y * 0.5f,
            transform.lossyScale.z * 0.5f);

        // Build active face list
        List<Face> activeFaces = new List<Face>();
        foreach (var face in AllFaces)
            if ((allowedFaces & face.mask) != 0)
                activeFaces.Add(face);

        if (activeFaces.Count == 0)
        {
            Debug.LogWarning("[RandomSurface] No faces selected.");
            return;
        }

        // Per-face placement tracker — overlap is only checked within the same face
        var facePlacements = new Dictionary<FaceMask, FacePlacement>();
        foreach (var face in activeFaces)
            facePlacements[face.mask] = new FacePlacement { face = face };

        // Collect and shuffle children so fill order is random
        List<Transform> children = new List<Transform>();
        foreach (Transform child in transform)
            children.Add(child);

        if (children.Count == 0)
        {
            Debug.LogWarning("[RandomSurface] No children found.");
            return;
        }

        Shuffle(children);

        int successCount = 0;

        foreach (Transform child in children)
        {
            float childRadius = GetColliderRadius(child);
            bool  placed      = false;

            // Fresh shuffled face order per child — distributes load evenly
            List<Face> faceOrder = new List<Face>(activeFaces);
            Shuffle(faceOrder);

            for (int attempt = 0; attempt < maxAttempts && !placed; attempt++)
            {
                Face face = faceOrder[attempt % faceOrder.Count];

                // Face half-extents in world units
                float halfU = FaceHalfExtent(face.tangentU, halfSize);
                float halfV = FaceHalfExtent(face.tangentV, halfSize);

                // Pull edges in by childRadius + spacing so nothing hangs off the edge
                float marginU = childRadius + minSpacing;
                float marginV = childRadius + minSpacing;
                float rangeU  = halfU - marginU;
                float rangeV  = halfV - marginV;

                // If face is smaller than the object, try centre
                float u = rangeU > 0f ? Random.Range(-rangeU, rangeU) : 0f;
                float v = rangeV > 0f ? Random.Range(-rangeV, rangeV) : 0f;

                // 2D overlap check — only against items already on THIS face
                FacePlacement fp = facePlacements[face.mask];
                if (Is2DOverlap(u, v, childRadius, fp.items))
                    continue;

                // Build world position:
                //   start at cube centre + push out along normal by half-size
                //   + slide along U and V axes
                //   + final nudge along normal for surfaceOffset
                Vector3 worldPos =
                    transform.position
                    + transform.TransformDirection(Vector3.Scale(face.normal, halfSize))
                    + transform.TransformDirection(face.tangentU) * u
                    + transform.TransformDirection(face.tangentV) * v
                    + transform.TransformDirection(face.normal)   * surfaceOffset;

                child.position = worldPos;

                fp.items.Add((u, v, childRadius));
                successCount++;
                placed = true;
            }

            if (!placed)
                Debug.LogWarning(
                    $"[RandomSurface] Could not place '{child.name}' after {maxAttempts} attempts. " +
                    $"Try reducing minSpacing ({minSpacing}), increasing the cube scale, " +
                    $"or enabling more faces.");
        }

        Debug.Log($"[RandomSurface] Placed {successCount}/{children.Count} children.");
    }

    // ─────────────────────────────────────────────────────────────────────
    // 2D OVERLAP CHECK — face-local coordinates only
    // ─────────────────────────────────────────────────────────────────────

    private bool Is2DOverlap(float u, float v, float radius,
                              List<(float u, float v, float r)> placed)
    {
        foreach (var item in placed)
        {
            float du      = u - item.u;
            float dv      = v - item.v;
            // Only minSpacing padding — not doubling radii — to avoid over-conservative gaps
            float minDist = radius + item.r + minSpacing;
            if (du * du + dv * dv < minDist * minDist)
                return true;
        }
        return false;
    }

    // ─────────────────────────────────────────────────────────────────────
    // COLLIDER RADIUS
    // ─────────────────────────────────────────────────────────────────────

    private float GetColliderRadius(Transform child)
    {
        // Check grandchildren first (collider might be on a handle/mesh child)
        foreach (Transform gc in child)
        {
            Collider c = gc.GetComponent<Collider>();
            if (c != null) return ColRadius(c.bounds);
        }

        // Collider directly on this child
        Collider dc = child.GetComponent<Collider>();
        if (dc != null) return ColRadius(dc.bounds);

        // Renderer bounds fallback
        Renderer[] renderers = child.GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            Bounds b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            return ColRadius(b);
        }

        // Last resort
        return minSpacing * 0.5f;
    }

    /// <summary>2D footprint radius = largest XZ half-extent of the bounds.</summary>
    private float ColRadius(Bounds b)
    {
        return Mathf.Max(b.extents.x, b.extents.z, 0.001f);
    }

    // ─────────────────────────────────────────────────────────────────────
    // HELPERS
    // ─────────────────────────────────────────────────────────────────────

    private float FaceHalfExtent(Vector3 tangent, Vector3 halfSize)
    {
        return Mathf.Abs(tangent.x * halfSize.x
                       + tangent.y * halfSize.y
                       + tangent.z * halfSize.z);
    }

    private void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // GIZMOS
    // ─────────────────────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        Gizmos.color  = new Color(0f, 1f, 0.5f, 0.3f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
        Gizmos.matrix = Matrix4x4.identity;

        Gizmos.color = new Color(1f, 0.5f, 0f, 0.6f);
        foreach (Transform child in transform)
            Gizmos.DrawWireSphere(child.position, GetColliderRadius(child));
    }
}