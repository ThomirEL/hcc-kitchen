using UnityEngine;

/// <summary>
/// Attach to any GameObject using URP_DOI_Master.
/// On Awake it reads the existing texture from the material, assigns it as
/// _BaseMap, then generates a downscaled _LowResMap via a RenderTexture blit.
/// Objects with no texture (color-only materials) are left untouched.
/// </summary>
[RequireComponent(typeof(Renderer))]
public class DOITextureSetup : MonoBehaviour
{
    [Header("Low-Res Settings")]
    [Tooltip("Divisor applied to original resolution. 8 = 1/8th size, giving a blurry pixelated look.")]
    [Range(2, 32)]
    public int downsampleFactor = 8;

    [Tooltip("How many times to ping-pong the low-res texture through itself for extra blur.")]
    [Range(0, 4)]
    public int blurPasses = 2;

    private Renderer _renderer;
    private Material _material;
    private RenderTexture _lowResRT;

    void Awake()
{
    _renderer = GetComponent<Renderer>();
    _material = _renderer.material;

    Texture2D existingTexture = _material.GetTexture("_BaseMap") as Texture2D;

    if (existingTexture == null)
    {
        Debug.Log($"[DOITextureSetup] {gameObject.name}: No texture found, using base color only.");
        return;
    }

    _lowResRT = GenerateLowResTexture(existingTexture);
    _material.SetTexture("_LowResMap", _lowResRT);

    Debug.Log($"[DOITextureSetup] {gameObject.name}: Set up {existingTexture.width}x{existingTexture.height} " +
              $"→ low-res {existingTexture.width / downsampleFactor}x{existingTexture.height / downsampleFactor}");
}

    /// <summary>
    /// Downsamples the source texture by blitting it into a small RenderTexture,
    /// then optionally blurs it with additional ping-pong passes.
    /// </summary>
    RenderTexture GenerateLowResTexture(Texture2D source)
    {
        int lowW = Mathf.Max(4, source.width  / downsampleFactor);
        int lowH = Mathf.Max(4, source.height / downsampleFactor);

        // Target low-res RT — bilinear filtering on a small RT gives natural blur when upsampled in shader
        RenderTexture lowRT = new RenderTexture(lowW, lowH, 0, RenderTextureFormat.ARGB32);
        lowRT.filterMode = FilterMode.Bilinear;
        lowRT.wrapMode   = source.wrapMode;
        lowRT.Create();

        // First blit: full-res → low-res (the downscale itself creates blur)
        Graphics.Blit(source, lowRT);

        // Optional extra blur passes: ping-pong between two small RTs
        if (blurPasses > 0)
        {
            RenderTexture pingPong = new RenderTexture(lowW, lowH, 0, RenderTextureFormat.ARGB32);
            pingPong.filterMode = FilterMode.Bilinear;
            pingPong.wrapMode   = source.wrapMode;
            pingPong.Create();

            for (int i = 0; i < blurPasses; i++)
            {
                Graphics.Blit(lowRT, pingPong);
                Graphics.Blit(pingPong, lowRT);
            }

            pingPong.Release();
            Object.Destroy(pingPong);
        }

        return lowRT;
    }

    void OnDestroy()
    {
        // Clean up GPU memory when the object is destroyed
        if (_lowResRT != null)
        {
            _lowResRT.Release();
            Object.Destroy(_lowResRT);
        }
    }
}