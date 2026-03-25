using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[System.Serializable]
public struct DryGoodsGroup
{
    public string name;
    public Color backGroundColor;
    public Color topBannerColor;
}

[System.Serializable]
public class DryGoodsDefinition
{
    public string name;
    public int groupIndex = 0;  // Index into DryGoodsManager.dryGoodsGroups
    [HideInInspector] public DryGoodsGroup group;    // Filled at runtime from the selected index

    [HideInInspector] public GameObject boxObject; // Reference to the associated box GameObject

    [System.NonSerialized]
    public Texture2D runtimeSnapshot; // snapshot at runtime
}

public class DryGoodsManager : MonoBehaviour
{
    [Header("References")]
    public Camera labelCamera;
    public Image backgroundImage;
    public Image topBannerImage;
    public TMP_Text nameText;

    [Header("Dry Goods group definitions")]
    public DryGoodsGroup[] dryGoodsGroups;

    [Header("Dry Goods definitions — one per box")]
    public DryGoodsDefinition[] dryGoods;

    [Header("RenderTexture Settings")]
    public int renderTextureWidth = 512;
    public int renderTextureHeight = 512;

    [Header("Optional PNG Saving")]
    public bool savePNGToDisk = false; // toggle saving
    public string pngSaveFolder = "DryGoodsLabels"; // relative to Application.dataPath

    private void Start()
    {
        // Make sure folder exists if saving
        if (savePNGToDisk)
        {
            string folderPath = Path.Combine(Application.dataPath, pngSaveFolder);
            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);
        }

        // Find all boxes with tag "dry_goods_box"
        GameObject[] boxes = GameObject.FindGameObjectsWithTag("dry_goods_box");
        if (boxes.Length == 0)
        {
            Debug.LogWarning("[DryGoodsManager] No GameObjects found with tag 'dry_goods_box'");
            return;
        }

        // Shuffle dry goods
        DryGoodsDefinition[] shuffledDryGoods = ShuffleDryGoods(dryGoods);

        // Limit to the number of boxes
        int count = Mathf.Min(boxes.Length, shuffledDryGoods.Length);

        // Assign boxes to dry goods
        for (int i = 0; i < count; i++)
        {
            // Assign group
            if (shuffledDryGoods[i].groupIndex >= 0 && shuffledDryGoods[i].groupIndex < dryGoodsGroups.Length)
                shuffledDryGoods[i].group = dryGoodsGroups[shuffledDryGoods[i].groupIndex];

            // Store box GameObject in a temporary non-inspector field
            shuffledDryGoods[i].boxObject = boxes[i];
            shuffledDryGoods[i].boxObject.name = shuffledDryGoods[i].name + "_Box"; // rename for clarity
        }

        // Start sequential rendering coroutine
        StartCoroutine(RenderAllSequential(shuffledDryGoods));
    }

    private DryGoodsDefinition[] ShuffleDryGoods(DryGoodsDefinition[] array)
    {
        DryGoodsDefinition[] newArray = (DryGoodsDefinition[])array.Clone();
        for (int i = 0; i < newArray.Length; i++)
        {
            int rnd = Random.Range(i, newArray.Length);
            var temp = newArray[i];
            newArray[i] = newArray[rnd];
            newArray[rnd] = temp;
        }
        return newArray;
    }

    private IEnumerator RenderAllSequential(DryGoodsDefinition[] selection)
{
    for (int i = 0; i < selection.Length; i++)
    {
        DryGoodsDefinition dryGood = selection[i];
        if (dryGood.boxObject == null) continue;

        // STEP 1: Update UI for this dryGood
        UpdateCanvasContent(dryGood);

        // Force canvas rebuild and wait 1-2 frames to guarantee TMP update finished
        Canvas.ForceUpdateCanvases();
        yield return null;
        yield return new WaitForEndOfFrame();
        yield return null;

        var renderer = dryGood.boxObject.GetComponent<MeshRenderer>();
        if (renderer == null)
        {
            Debug.LogWarning($"Box {dryGood.boxObject.name} has no MeshRenderer");
            continue;
        }

        renderer.material = new Material(renderer.material);

        RenderTexture rt = new RenderTexture(renderTextureWidth, renderTextureHeight, 16);
        rt.Create();

        // STEP 3: Render AFTER UI is stable
        RenderToTexture(rt, renderer.material);

        // STEP 4: Capture
        dryGood.runtimeSnapshot = CaptureRenderTexture(rt);

        if (savePNGToDisk)
        {
            string fileName = dryGood.name.Replace(" ", "_") + ".png";
            SaveTextureToPNG(dryGood.runtimeSnapshot, fileName);
        }

        // 🚨 CRITICAL: Wait before next item
        yield return null;
        yield return new WaitForEndOfFrame();
    }
}

    private void UpdateCanvasContent(DryGoodsDefinition dryGood)
    {
        if (backgroundImage != null)
            backgroundImage.color = dryGood.group.backGroundColor;
        if (topBannerImage != null)
            topBannerImage.color = dryGood.group.topBannerColor;

        if (nameText != null)
        {
            nameText.text = dryGood.name.ToUpper();
            nameText.color = Color.white;
        }

        if (backgroundImage != null)
            backgroundImage.transform.SetAsFirstSibling();
    }

    private void RenderToTexture(RenderTexture targetRT, Material boxMaterial)
    {
        if (labelCamera == null || targetRT == null || boxMaterial == null)
        {
            Debug.LogError("[DryGoodsManager] Missing reference — cannot render.");
            return;
        }

        Canvas.ForceUpdateCanvases();

        RenderTexture prevActive = RenderTexture.active;
        RenderTexture prevTarget = labelCamera.targetTexture;

        RenderTexture.active = targetRT;
        labelCamera.targetTexture = targetRT;
        labelCamera.Render();

        // restore render targets to avoid leaking target assignment to later frames
        labelCamera.targetTexture = prevTarget;
        RenderTexture.active = prevActive;

        boxMaterial.SetTexture("_BaseMap", targetRT);
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