using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[System.Serializable]
public struct CanGroup
{
    public string name;
    public Color backGroundColor;
    public Color topBannerColor;
}

[System.Serializable]
public class CanDefinition
{
    public string name;
    public int groupIndex = 0;  // Index into CanManager.canGroups
    [HideInInspector] public CanGroup group;    // Filled at runtime from the selected index

    [HideInInspector] public GameObject canObject; // Reference to the associated can GameObject

    [System.NonSerialized]
    public Texture2D runtimeSnapshot; // snapshot at runtime
}

public class CanManager : MonoBehaviour
{
    [Header("References")]
    public Camera labelCamera;
    public Image backgroundImage;
    public Image topBannerImage;
    public TMP_Text nameText;

    [Header("Can group definitions")]
    public CanGroup[] canGroups = new CanGroup[]
    {
        new CanGroup() { name = "Canned Vegetables", backGroundColor = new Color(0.44f, 0.67f, 0.32f), topBannerColor = new Color(0.25f, 0.47f, 0.18f) },
        new CanGroup() { name = "Canned Proteins", backGroundColor = new Color(0.74f, 0.47f, 0.21f), topBannerColor = new Color(0.52f, 0.28f, 0.12f) },
        new CanGroup() { name = "Savory Meals", backGroundColor = new Color(0.63f, 0.25f, 0.15f), topBannerColor = new Color(0.42f, 0.17f, 0.09f) },
        new CanGroup() { name = "Fruit & Dessert", backGroundColor = new Color(0.92f, 0.62f, 0.22f), topBannerColor = new Color(0.73f, 0.37f, 0.06f) },
    };

    [Header("Can definitions — one per can")]
    public CanDefinition[] cans = new CanDefinition[]
    {
        // Group 0: Canned Vegetables ( pantry basics )
        new CanDefinition() { name = "Canned Peas", groupIndex = 0 },
        new CanDefinition() { name = "Canned Carrots", groupIndex = 0 },
        new CanDefinition() { name = "Green Beans", groupIndex = 0 },
        new CanDefinition() { name = "Sweet Corn", groupIndex = 0 },

        // Group 1: Canned Proteins & Pantry Staples
        new CanDefinition() { name = "Tuna Chunks", groupIndex = 1 },
        new CanDefinition() { name = "Chickpeas", groupIndex = 1 },
        new CanDefinition() { name = "Kidney Beans", groupIndex = 1 },
        new CanDefinition() { name = "Lentils", groupIndex = 1 },

        // Group 2: Vegetables & Savory
        new CanDefinition() { name = "Tomato Soup", groupIndex = 2 },
        new CanDefinition() { name = "Baked Beans", groupIndex = 2 },
        new CanDefinition() { name = "Corn Kernels", groupIndex = 2 },
        new CanDefinition() { name = "Mushroom Stew", groupIndex = 2 },

        // Group 3: Fruit & Dessert
        new CanDefinition() { name = "Peach Slices", groupIndex = 3 },
        new CanDefinition() { name = "Pineapple Chunks", groupIndex = 3 },
        new CanDefinition() { name = "Fruit Cocktail", groupIndex = 3 },
        new CanDefinition() { name = "Cherry Compote", groupIndex = 3 },
    };

    [Header("RenderTexture Settings")]
    public int renderTextureWidth = 512;
    public int renderTextureHeight = 512;

    [Header("Optional PNG Saving")]
    public bool savePNGToDisk = false;
    public string pngSaveFolder = "CanLabels";

    private void Start()
    {
        if (savePNGToDisk)
        {
            string folderPath = Path.Combine(Application.dataPath, pngSaveFolder);
            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);
        }

        GameObject[] cansObjects = GameObject.FindGameObjectsWithTag("can_box");
        if (cansObjects.Length == 0)
        {
            Debug.LogWarning("[CanManager] No GameObjects found with tag 'can_box'");
            return;
        }

        CanDefinition[] shuffledCans = ShuffleCans(cans);

        int count = Mathf.Min(cansObjects.Length, shuffledCans.Length);

        for (int i = 0; i < count; i++)
        {
            if (shuffledCans[i].groupIndex >= 0 && shuffledCans[i].groupIndex < canGroups.Length)
                shuffledCans[i].group = canGroups[shuffledCans[i].groupIndex];

            shuffledCans[i].canObject = cansObjects[i];
            shuffledCans[i].canObject.name = shuffledCans[i].name + "_Can";
        }

        StartCoroutine(RenderAllCansSequential(shuffledCans));
    }

    private CanDefinition[] ShuffleCans(CanDefinition[] array)
    {
        CanDefinition[] newArray = (CanDefinition[])array.Clone();
        for (int i = 0; i < newArray.Length; i++)
        {
            int rnd = Random.Range(i, newArray.Length);
            var temp = newArray[i];
            newArray[i] = newArray[rnd];
            newArray[rnd] = temp;
        }
        return newArray;
    }

    private IEnumerator RenderAllCansSequential(CanDefinition[] selection)
    {
        for (int i = 0; i < selection.Length; i++)
        {
            CanDefinition can = selection[i];
            if (can.canObject == null) continue;

            UpdateCanvasContent(can);
            Canvas.ForceUpdateCanvases();
            yield return null;
            yield return new WaitForEndOfFrame();
            yield return null;

            var renderer = can.canObject.GetComponent<MeshRenderer>();
            if (renderer == null)
            {
                Debug.LogWarning($"Can {can.canObject.name} has no MeshRenderer");
                continue;
            }

            renderer.material = new Material(renderer.material);

            RenderTexture rt = new RenderTexture(renderTextureWidth, renderTextureHeight, 16);
            rt.Create();

            RenderToTexture(rt, renderer.material);

            can.runtimeSnapshot = CaptureRenderTexture(rt);

            if (savePNGToDisk)
            {
                string fileName = can.name.Replace(" ", "_") + ".png";
                SaveTextureToPNG(can.runtimeSnapshot, fileName);
            }

            yield return null;
            yield return new WaitForEndOfFrame();
        }
    }

    private void UpdateCanvasContent(CanDefinition can)
    {
        if (backgroundImage != null)
            backgroundImage.color = can.group.backGroundColor;
        if (topBannerImage != null)
            topBannerImage.color = can.group.topBannerColor;

        if (nameText != null)
        {
            nameText.text = can.name.ToUpper();
            nameText.color = Color.white;
        }

        if (backgroundImage != null)
            backgroundImage.transform.SetAsFirstSibling();
    }

    private void RenderToTexture(RenderTexture targetRT, Material canMaterial)
    {
        if (labelCamera == null || targetRT == null || canMaterial == null)
        {
            Debug.LogError("[CanManager] Missing reference — cannot render.");
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

        canMaterial.SetTexture("_BaseMap", targetRT);
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
