using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// Attach to a persistent GameObject (e.g. RuntimeSpriteManager).
/// Builds and maintains a single TMP_SpriteAsset at runtime,
/// populated from Texture2Ds produced by your RenderTexture scripts.
/// 
/// Usage from your other scripts:
///   RuntimeSpriteAssetManager.Instance.RegisterTexture(myTex, "spriteName");
/// 
/// In TMP text fields:
///   "Hello <sprite name="spriteName"> world"
/// </summary>
public class SpriteRuntimeManager : MonoBehaviour
{
    public static SpriteRuntimeManager Instance { get; private set; }

    // The single shared sprite asset — assign to TMP_Text components
    // in the Inspector, or let AutoAssignToTextComponents() handle it.
    [Tooltip("Auto-populated at runtime. You can also assign this to TMP_Text components manually.")]
    public TMP_SpriteAsset spriteAsset;

    // Tracks registered sprite names so we can update existing entries
    private readonly Dictionary<string, int> _spriteIndexByName = new();

    // Internal atlas texture — we composite all sprites onto this
    private Texture2D _atlasTexture;

    // Each registered sprite's source texture and metadata
    private readonly List<SpriteEntry> _entries = new();

    // Fixed atlas size — increase if you need more/larger sprites.
    // Keep power-of-two for GPU compatibility.
    [SerializeField] private int atlasWidth = 1024;
    [SerializeField] private int atlasHeight = 512;

    // If true, automatically assigns this sprite asset to ALL TMP_Text
    // components found in the scene when a sprite is registered.
    [SerializeField] private bool autoAssignToAllText = true;

    private struct SpriteEntry
    {
        public string name;
        public Texture2D texture;
        public Rect atlasRect; // UV rect in the atlas (0..1 space)
    }

    // -------------------------------------------------------------------------
    // Unity Lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        InitialiseAsset();
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Register or update a Texture2D as a named inline sprite.
    /// Call this AFTER your WaitForEndOfFrame() coroutine has rendered
    /// the RenderTexture to the Texture2D — same timing as your _BaseMap setter.
    /// 
    /// If a sprite with this name already exists, its texture is updated in place.
    /// </summary>
    public void RegisterTexture(Texture2D source, string spriteName)
    {
        if (source == null)
        {
            Debug.LogWarning($"[RuntimeSpriteAssetManager] Null texture passed for sprite '{spriteName}'");
            return;
        }

        if (_spriteIndexByName.TryGetValue(spriteName, out int existingIndex))
        {
            // Update existing entry's texture and rebuild
            var entry = _entries[existingIndex];
            entry.texture = source;
            _entries[existingIndex] = entry;
        }
        else
        {
            // Queue a new entry — rect is assigned during RebuildAtlas()
            _entries.Add(new SpriteEntry { name = spriteName, texture = source });
            _spriteIndexByName[spriteName] = _entries.Count - 1;
        }

        // Defer the rebuild one frame so multiple RegisterTexture calls
        // in the same frame are batched into a single atlas rebuild.
        StopCoroutine(nameof(DeferredRebuild));
        StartCoroutine(nameof(DeferredRebuild));
    }

    // -------------------------------------------------------------------------
    // Internal
    // -------------------------------------------------------------------------

    private void InitialiseAsset()
{
    _atlasTexture = new Texture2D(atlasWidth, atlasHeight, TextureFormat.RGBA32, false);
    _atlasTexture.name = "RuntimeSpriteAtlas";

    spriteAsset = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
    spriteAsset.name = "RuntimeSpriteAsset";

    Material mat = new Material(Shader.Find("TextMeshPro/Sprite"));
    mat.mainTexture = _atlasTexture;
    spriteAsset.material = mat;
    spriteAsset.spriteSheet = _atlasTexture;

    // ✅ DO NOT assign the lists — they are get-only in TMP 3.x.
    // The lists are initialised internally by TMP_SpriteAsset on creation.
    // Just clear them to ensure a clean state before first use.
    spriteAsset.spriteGlyphTable.Clear();
    spriteAsset.spriteCharacterTable.Clear();

    Debug.Log("[RuntimeSpriteAssetManager] Sprite asset initialised.");
}

    /// <summary>
    /// Waits one frame so multiple same-frame RegisterTexture calls
    /// are batched into a single atlas rebuild.
    /// </summary>
    private IEnumerator DeferredRebuild()
    {
        yield return new WaitForEndOfFrame();
        RebuildAtlas();
    }

    private void RebuildAtlas()
    {
        if (_entries.Count == 0) return;

        // --- 1. Simple row-packing layout ---
        // Sprites are packed left-to-right at their native size,
        // scaled down if they exceed the atlas height.
        int cursorX = 0;
        int padding = 4;

        // Clear atlas to transparent
        Color32[] blank = new Color32[atlasWidth * atlasHeight];
        _atlasTexture.SetPixels32(blank);

        // Rebuild the sprite/glyph tables from scratch
        spriteAsset.spriteGlyphTable.Clear();
        spriteAsset.spriteCharacterTable.Clear();
        _spriteIndexByName.Clear();

        for (int i = 0; i < _entries.Count; i++)
        {
            SpriteEntry entry = _entries[i];
            Texture2D src = entry.texture;

            // Scale sprite to fit atlas height if necessary
            int spriteH = Mathf.Min(src.height, atlasHeight);
            int spriteW = Mathf.RoundToInt(src.width * ((float)spriteH / src.height));

            // Wrap to next row is NOT implemented here — if you exceed atlasWidth,
            // increase atlasWidth or reduce individual texture sizes.
            if (cursorX + spriteW > atlasWidth)
            {
                Debug.LogWarning($"[RuntimeSpriteAssetManager] Atlas full — sprite '{entry.name}' clipped. Increase atlasWidth.");
                break;
            }

            // Resize source texture into a temporary copy if needed
            Texture2D blitSrc = ResizeTexture(src, spriteW, spriteH);

            // Blit into atlas — TMP expects bottom-left origin
            _atlasTexture.SetPixels(cursorX, 0, spriteW, spriteH, blitSrc.GetPixels());

            if (blitSrc != src) Destroy(blitSrc); // Clean up temp copy

            // --- 2. Build TMP glyph ---
            // GlyphRect uses pixel coordinates (bottom-left origin)
            var glyphRect = new UnityEngine.TextCore.GlyphRect(cursorX, 0, spriteW, spriteH);
            var glyphMetrics = new UnityEngine.TextCore.GlyphMetrics(
                spriteW,        // width
                spriteH,        // height
                0,              // horizontalBearingX
                spriteH,        // horizontalBearingY  (ascender = full height)
                spriteW         // horizontalAdvance
            );

            var glyph = new TMP_SpriteGlyph
            {
                index = (uint)i,
                glyphRect = glyphRect,
                metrics = glyphMetrics,
                atlasIndex = 0,
                scale = 1f
            };

            // --- 3. Build TMP sprite character ---
            var spriteChar = new TMP_SpriteCharacter
            {
                // Unicode value — TMP uses 0xFFFE + index as a safe private-use sentinel
                unicode = (uint)(0xFFFE - i),
                name = entry.name,
                scale = 1f,
                glyph = glyph
            };

            spriteAsset.spriteGlyphTable.Add(glyph);
            spriteAsset.spriteCharacterTable.Add(spriteChar);
            _spriteIndexByName[entry.name] = i;

            // Update rect for external reference (optional)
            entry.atlasRect = new Rect(
                (float)cursorX / atlasWidth, 0f,
                (float)spriteW / atlasWidth, (float)spriteH / atlasHeight);
            _entries[i] = entry;

            cursorX += spriteW + padding;
        }

        // --- 4. Upload atlas to GPU ---
        _atlasTexture.Apply(false); // false = don't generate mipmaps

        // --- 5. Notify TMP to rebuild lookups ---
        spriteAsset.UpdateLookupTables();

        // --- 6. Force all active TMP text to re-evaluate ---
        TMPro_EventManager.ON_SPRITE_ASSET_PROPERTY_CHANGED(true, spriteAsset);

        if (autoAssignToAllText)
            AutoAssignToTextComponents();

        Debug.Log($"[RuntimeSpriteAssetManager] Atlas rebuilt with {_entries.Count} sprite(s).");
    }

    /// <summary>
    /// Assigns this sprite asset to every TMP_Text in the scene.
    /// Only needed if you don't assign it manually in the Inspector.
    /// </summary>
    private void AutoAssignToTextComponents()
    {
        foreach (var tmp in FindObjectsByType<TMP_Text>(FindObjectsSortMode.None))
        {
            // Don't overwrite if they've already been given a different sprite asset
            if (tmp.spriteAsset == null)
                tmp.spriteAsset = spriteAsset;
        }
    }

    /// <summary>
    /// Bilinear-resize a Texture2D to targetW x targetH.
    /// Returns the original if dimensions already match.
    /// </summary>
    private Texture2D ResizeTexture(Texture2D source, int targetW, int targetH)
    {
        if (source.width == targetW && source.height == targetH)
            return source;

        RenderTexture rt = RenderTexture.GetTemporary(targetW, targetH);
        Graphics.Blit(source, rt);
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;

        Texture2D result = new Texture2D(targetW, targetH, TextureFormat.RGBA32, false);
        result.ReadPixels(new Rect(0, 0, targetW, targetH), 0, 0);
        result.Apply();

        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        return result;
    }
}