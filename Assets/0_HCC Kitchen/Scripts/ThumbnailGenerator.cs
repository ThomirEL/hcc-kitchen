using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// Spawns a prefab on an isolated offscreen camera, auto-frames it,
/// captures to Texture2D, creates a Sprite, and registers it in a
/// TMP_SpriteAsset for inline use in TextMeshPro.
///
/// Attach to: ThumbnailStudio (the offscreen root GameObject)
/// </summary>
public class ThumbnailGenerator : MonoBehaviour
{
    [Header("Capture Setup")]
    [Tooltip("The isolated capture camera (Culling Mask = ThumbnailCapture only, Projection = Orthographic)")]
    [SerializeField] private Camera _captureCamera;

    [Tooltip("Where spawned objects are placed during capture")]
    [SerializeField] private Transform _spawnPoint;

    [Header("Output Settings")]
    [Tooltip("Pixel dimensions of each generated sprite (square recommended for TMP)")]
    [SerializeField] private int _resolution = 256;

    [Tooltip("Padding multiplier — 1.0 = tight fit, 1.2 = 20% padding around object")]
    [SerializeField] [Range(1f, 2f)] private float _padding = 1.15f;

    [Header("TMP Integration")]
    [Tooltip("Name used to reference this asset in TMP rich text: <sprite=\"ThumbnailSprites\" name=\"mySprite\">")]
    [SerializeField] private string _spriteAssetName = "ThumbnailSprites";

    // In ThumbnailGenerator.cs — add this public getter
public TMP_SpriteAsset SpriteAsset => _spriteAsset;

    // The layer index for ThumbnailCapture — must match what you created in Project Settings
    private const string CAPTURE_LAYER = "Thumbnail";

    // The runtime TMP sprite asset we build and register
    private TMP_SpriteAsset _spriteAsset;

    // Internal sprite list — TMP_SpriteAsset uses a texture atlas; we pack sprites into it
    private List<ThumbnailEntry> _entries = new List<ThumbnailEntry>();

    // Tracks names already captured so we don't duplicate
    private Dictionary<string, int> _nameToIndex = new Dictionary<string, int>();

    private RenderTexture _rt;

    private void Awake()
    {
        // Create the RenderTexture once; ARGB32 preserves alpha (transparent background)
        _rt = new RenderTexture(_resolution, _resolution, 24, RenderTextureFormat.ARGB32);
        _rt.antiAliasing = 4; // Smooth edges on the captured geometry
        _captureCamera.targetTexture = _rt;
        _captureCamera.enabled = false; // Only render on demand

        // Initialise the TMP_SpriteAsset
        InitialiseSpriteAsset();
    }

    private void OnDestroy()
    {
        if (_rt != null) _rt.Release();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PUBLIC API
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Capture a prefab and register it in the TMP sprite asset.
    /// Returns the sprite index in the asset, or -1 if it already exists.
    ///
    /// Call this from a coroutine — it yields one frame for the camera to render.
    /// </summary>
    public IEnumerator CaptureSprite(GameObject prefab, string spriteName, Action<Sprite> onComplete = null)
    {
        if (_nameToIndex.ContainsKey(spriteName))
        {
            Debug.LogWarning($"[ThumbnailGenerator] '{spriteName}' already captured. Skipping.");
            onComplete?.Invoke(_spriteAsset.spriteCharacterTable[_nameToIndex[spriteName]].glyph.atlasIndex >= 0
                ? null : null); // Return existing if needed — extend as required
            yield break;
        }

        // 1. Spawn the prefab at the spawn point on the ThumbnailCapture layer
        GameObject instance = Instantiate(prefab, _spawnPoint.position, _spawnPoint.rotation);
        instance.SetActive(true); // Disable any scripts or animations on the prefab during capture

        // ── Orientation override ──────────────────────────────────────────
        // If the prefab has a ThumbnailCaptureOrientation component, apply
        // its rotation before we compute bounds or frame the camera.
        var orientationOverride = instance.GetComponent<ThumbnailCaptureOrientation>();
        if (orientationOverride != null)
            instance.transform.eulerAngles = orientationOverride.captureEulerAngles;
        // ─────────────────────────────────────────────────────────────────

        SetLayerRecursive(instance, LayerMask.NameToLayer(CAPTURE_LAYER));

        // 2. Auto-frame: compute world-space bounds of all renderers
        Bounds bounds = GetCompositeBounds(instance);

        // 3. Centre the object at the spawn point so it's centred in frame
        Vector3 offset = _spawnPoint.position - bounds.center;
        instance.transform.position += offset;

        // Recompute bounds after centering
        bounds = GetCompositeBounds(instance);

        // 4. Set orthographic size so the object fills the frame (with padding)
        float halfExtent = Mathf.Max(bounds.extents.x, bounds.extents.y);
        _captureCamera.orthographicSize = halfExtent * _padding;

        // Position the camera so it's looking at the object from the front
        // Adjust Z offset to be safely in front of the object along the camera's forward
        _captureCamera.transform.position = _spawnPoint.position
            - _captureCamera.transform.forward * (bounds.extents.z + 1f);

        // 5. Render one frame into the RenderTexture
        _captureCamera.enabled = true;
        _captureCamera.Render();
        _captureCamera.enabled = false;

        // 6. Read pixels from the RenderTexture into a Texture2D
        Texture2D tex = new Texture2D(_resolution, _resolution, TextureFormat.ARGB32, false);
        RenderTexture.active = _rt;
        tex.ReadPixels(new Rect(0, 0, _resolution, _resolution), 0, 0);
        tex.Apply();
        RenderTexture.active = null;

        // 7. Create a Sprite from the full texture
        Sprite sprite = Sprite.Create(
            tex,
            new Rect(0, 0, _resolution, _resolution),
            new Vector2(0.5f, 0.5f), // pivot = centre
            _resolution              // pixels per unit
        );
        sprite.name = spriteName;

        // 8. Register in the TMP_SpriteAsset
        AddSpriteToAsset(sprite, spriteName);

        // 9. Clean up the temporary instance
        //Destroy(instance);

        onComplete?.Invoke(sprite);
    }

    /// <summary>
    /// Convenience wrapper — captures multiple prefabs sequentially.
    /// </summary>
    /// <summary>
/// Captures all prefabs and populates the provided dictionary with name → Sprite.
/// Replaces the previous Action-based version for cleaner multi-item usage.
/// </summary>
public IEnumerator CaptureAll(List<(GameObject prefab, string name)> items, Dictionary<string, Sprite> results)
{
    foreach (var (prefab, name) in items)
    {
        Sprite captured = null;

        // CaptureSprite populates 'captured' via the callback
        yield return CaptureSprite(prefab, name, s => captured = s);

        if (captured != null)
            results[name] = captured;
        else
            Debug.LogWarning($"[ThumbnailGenerator] Capture failed for '{name}'");
    }

    Debug.Log($"[ThumbnailGenerator] All done — {results.Count}/{items.Count} captured.");
}

    /// <summary>
    /// Returns the TMP rich-text tag to inline this sprite in a TMP text field.
    /// e.g.  "Pick up the <sprite=\"ThumbnailSprites\" name=\"Apple\"> apple"
    /// </summary>
    public string GetRichTextTag(string spriteName)
    {
        return $"<sprite=\"{_spriteAssetName}\" name=\"{spriteName}\">";
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PRIVATE HELPERS
    // ─────────────────────────────────────────────────────────────────────────

    private void InitialiseSpriteAsset()
    {
        _spriteAsset = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
        _spriteAsset.name = _spriteAssetName;

        // Create a texture atlas — we start with a single resolution-sized texture
        // and will replace/repack as sprites are added.
        // For a simple runtime case, we use individual textures per glyph (no atlas packing).
        // TMP_SpriteAsset can reference sprites directly via spriteSheet if you set it up below.

        // Register globally so any TMP text can reference it by name
        // TMP_Settings.defaultSpriteAsset is the fallback; you can also assign per-component.
        // We add it to the fallback list in TMP_Settings.
        if (TMP_Settings.defaultSpriteAsset != null)
        {
            // Add as a fallback on the default asset
            if (TMP_Settings.defaultSpriteAsset.fallbackSpriteAssets == null)
                TMP_Settings.defaultSpriteAsset.fallbackSpriteAssets = new List<TMP_SpriteAsset>();

            TMP_Settings.defaultSpriteAsset.fallbackSpriteAssets.Add(_spriteAsset);
        }
        else
        {
            // No default set — assign ours as the default
            // Note: this requires TMP_Settings to have been initialised (it is after first TMP use)
            Debug.LogWarning("[ThumbnailGenerator] No default TMP SpriteAsset found. " +
                             "Assign the sprite asset directly to your TMP components instead.");
        }
    }

    private void AddSpriteToAsset(Sprite sprite, string spriteName)
    {
        // TMP_SpriteAsset works with a spriteInfoList and a texture atlas.
        // At runtime the cleanest approach is to maintain a list and rebuild the asset.
        // For production, consider a proper atlas packer (e.g., Texture2D.PackTextures).

        // Add to the internal sprite list
        _entries.Add(new ThumbnailEntry { sprite = sprite, name = spriteName });

        // Rebuild the sprite asset from current entries
        RebuildSpriteAsset();

        int index = _entries.Count - 1;
        _nameToIndex[spriteName] = index;

        Debug.Log($"[ThumbnailGenerator] Captured '{spriteName}' → TMP index {index}");
    }

    private void RebuildSpriteAsset()
    {
        // Collect all sprite textures
        Texture2D[] textures = new Texture2D[_entries.Count];
        for (int i = 0; i < _entries.Count; i++)
            textures[i] = _entries[i].sprite.texture;

        // Pack into an atlas
        Texture2D atlas = new Texture2D(1, 1, TextureFormat.ARGB32, false);
        Rect[] rects = atlas.PackTextures(textures, padding: 2, maximumAtlasSize: 4096);
        atlas.Apply();

        // Assign the atlas texture to the sprite asset
        _spriteAsset.spriteSheet = atlas;

        // Rebuild sprite info list
        _spriteAsset.spriteCharacterTable.Clear();
        _spriteAsset.spriteGlyphTable.Clear();

        for (int i = 0; i < _entries.Count; i++)
        {
            Rect r = rects[i];
            float w = atlas.width;
            float h = atlas.height;

            // Create glyph
            var glyph = new TMP_SpriteGlyph();
            glyph.index = (uint)i;
            glyph.metrics = new UnityEngine.TextCore.GlyphMetrics(
                r.width * w, r.height * h,       // width, height in pixels
                0, r.height * h,                  // bearingX, bearingY
                r.width * w                        // advance
            );
            glyph.glyphRect = new UnityEngine.TextCore.GlyphRect(
                Mathf.RoundToInt(r.x * w),
                Mathf.RoundToInt(r.y * h),
                Mathf.RoundToInt(r.width * w),
                Mathf.RoundToInt(r.height * h)
            );
            glyph.scale = 1f;
            glyph.atlasIndex = 0;
            _spriteAsset.spriteGlyphTable.Add(glyph);

            // Create character
            var character = new TMP_SpriteCharacter((uint)(0xE000 + i), glyph);
            character.name = _entries[i].name;
            character.scale = 1f;
            _spriteAsset.spriteCharacterTable.Add(character);
        }

        // Required: rebuild lookup tables so TMP can find sprites by name
        _spriteAsset.UpdateLookupTables();
    }

    /// <summary>
    /// Computes the combined world-space bounds of all Renderers on an object and its children.
    /// </summary>
    private static Bounds GetCompositeBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            // Fallback: unit bounds at root position
            return new Bounds(root.transform.position, Vector3.one);
        }

        Bounds combined = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            combined.Encapsulate(renderers[i].bounds);

        return combined;
    }

    /// <summary>
    /// Recursively sets the layer on a GameObject and all its children.
    /// </summary>
    private static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform)
            SetLayerRecursive(child.gameObject, layer);
    } 

    [Serializable]
    private struct ThumbnailEntry
    {
        public Sprite sprite;
        public string name;
    }
}