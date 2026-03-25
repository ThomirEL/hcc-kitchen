using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[System.Serializable]
public struct SpiceGroup
{
    public string name;
    public Color backGroundColor;
    public Color topBannerColor;
    public Color LabelOutlineColor;
    public Color borderLineTopColor;
    public Color borderLineBotColor;
}

[System.Serializable]
public class SpiceDefinition
{
    public string name;
    public int groupIndex = 0;  // Index into SpiceManager.spiceGroups
    [HideInInspector] public SpiceGroup group;    // Filled at runtime from the selected index

    [HideInInspector] public GameObject jarObject; // Reference to the associated jar GameObject

    [System.NonSerialized]
    public Texture2D runtimeSnapshot; // snapshot at runtime
}

public class SpiceManager : MonoBehaviour
{
    [Header("References")]
    public Camera labelCamera;
    public Image backgroundImage;
    public Image topBannerImage;
    public Image labelOutlineImage;
    public Image borderLineTopImage;
    public Image borderLineBotImage;
    public TMP_Text nameText;

    [Header("Spice group definitions")]
    public SpiceGroup[] spiceGroups;

    [Header("Spice definitions — one per jar")]
    public SpiceDefinition[] spices;

    [Header("RenderTexture Settings")]
    public int renderTextureWidth = 512;
    public int renderTextureHeight = 512;

    [Header("Optional PNG Saving")]
    public bool savePNGToDisk = false; // toggle saving
    public string pngSaveFolder = "SpiceLabels"; // relative to Application.dataPath

    private void Start()
    {
        // Make sure folder exists if saving
        if (savePNGToDisk)
        {
            string folderPath = Path.Combine(Application.dataPath, pngSaveFolder);
            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);
        }

        // Find all jars with tag "spice_jar"
        GameObject[] jars = GameObject.FindGameObjectsWithTag("spice_jar");
        if (jars.Length == 0)
        {
            Debug.LogWarning("[SpiceManager] No GameObjects found with tag 'spice_jar'");
            return;
        }

        // Shuffle spices
        SpiceDefinition[] shuffledSpices = ShuffleSpices(spices);

        // Limit to the number of jars
        int count = Mathf.Min(jars.Length, shuffledSpices.Length);

        // Assign jars to spices
        for (int i = 0; i < count; i++)
        {
            // Assign group
            if (shuffledSpices[i].groupIndex >= 0 && shuffledSpices[i].groupIndex < spiceGroups.Length)
                shuffledSpices[i].group = spiceGroups[shuffledSpices[i].groupIndex];

            // Store jar GameObject in a temporary non-inspector field
            shuffledSpices[i].jarObject = jars[i];
        }

        // Start sequential rendering coroutine
        StartCoroutine(RenderAllSpicesSequential(shuffledSpices));
    }

    private SpiceDefinition[] ShuffleSpices(SpiceDefinition[] array)
    {
        SpiceDefinition[] newArray = (SpiceDefinition[])array.Clone();
        for (int i = 0; i < newArray.Length; i++)
        {
            int rnd = Random.Range(i, newArray.Length);
            var temp = newArray[i];
            newArray[i] = newArray[rnd];
            newArray[rnd] = temp;
        }
        return newArray;
    }

    private IEnumerator RenderAllSpicesSequential(SpiceDefinition[] spicesToRender)
    {
        for (int i = 0; i < spicesToRender.Length; i++)
        {
            SpiceDefinition spice = spicesToRender[i];
            if (spice.jarObject == null) continue;

            // Update canvas
            UpdateCanvasContent(spice);
            Canvas.ForceUpdateCanvases();

            // Wait multiple frames for reliable TMP/render passive update
            yield return null;
            yield return new WaitForEndOfFrame();
            yield return null;

            var jarRenderer = spice.jarObject.GetComponent<MeshRenderer>();
            if (jarRenderer == null)
            {
                Debug.LogWarning($"Jar {spice.jarObject.name} has no MeshRenderer");
                continue;
            }

            jarRenderer.material = new Material(jarRenderer.material);

            RenderTexture rt = new RenderTexture(renderTextureWidth, renderTextureHeight, 16);
            rt.Create();

            RenderToTexture(rt, jarRenderer.material);

            spice.runtimeSnapshot = CaptureRenderTexture(rt);

            if (savePNGToDisk)
            {
                string fileName = spice.name.Replace(" ", "_") + ".png";
                SaveTextureToPNG(spice.runtimeSnapshot, fileName);
            }

            yield return null; // wait a frame before next spice
        }
    }

    private void UpdateCanvasContent(SpiceDefinition spice)
    {
        if (backgroundImage != null)
            backgroundImage.color = spice.group.backGroundColor;
        if (topBannerImage != null)
            topBannerImage.color = spice.group.topBannerColor;
        if (labelOutlineImage != null)
            labelOutlineImage.color = spice.group.LabelOutlineColor;
        if (borderLineTopImage != null)
            borderLineTopImage.color = spice.group.borderLineTopColor;
        if (borderLineBotImage != null)
            borderLineBotImage.color = spice.group.borderLineBotColor;

        if (nameText != null)
        {
            nameText.text = spice.name.ToUpper();
            nameText.color = Color.black;
        }

        if (backgroundImage != null)
            backgroundImage.transform.SetAsFirstSibling();
    }

    private void RenderToTexture(RenderTexture targetRT, Material jarMaterial)
    {
        if (labelCamera == null || targetRT == null || jarMaterial == null)
        {
            Debug.LogError("[SpiceManager] Missing reference — cannot render.");
            return;
        }

        Canvas.ForceUpdateCanvases();

        RenderTexture prevActive = RenderTexture.active;
        RenderTexture prevTarget = labelCamera.targetTexture;

        RenderTexture.active = targetRT;
        labelCamera.targetTexture = targetRT;
        labelCamera.Render();

        labelCamera.targetTexture = prevTarget;
        RenderTexture.active = prevActive;

        jarMaterial.SetTexture("_BaseMap", targetRT);
    }

    public static Texture2D CaptureRenderTexture(RenderTexture rt)
    {
        if (rt == null) return null;

        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;

        Texture2D tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();

        RenderTexture.active = prev;
        return tex;
    }

    private void SaveTextureToPNG(Texture2D tex, string fileName)
    {
        if (tex == null) return;

        string path = Path.Combine(Application.dataPath, pngSaveFolder, fileName);
        byte[] pngData = tex.EncodeToPNG();
        File.WriteAllBytes(path, pngData);
        Debug.Log($"Saved PNG: {path}");
    }
}