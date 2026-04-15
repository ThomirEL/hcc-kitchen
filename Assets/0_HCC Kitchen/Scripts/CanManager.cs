using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq;

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
    public static CanManager Instance { get; private set; }

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

    private bool _isRenderingSequence = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Initialize can definitions and find GameObjects BEFORE Start methods run
        foreach (CanDefinition can in cans)
        {
            GameObject canObject = GameObject.Find(can.name) ?? GameObject.Find(can.name + "_Can");

            if (canObject == null)
            {
                Debug.LogWarning($"[CanManager] Could not find GameObject with name '{can.name}' or '{can.name}_Can'.");
                continue;
            }

            if (can.groupIndex >= 0 && can.groupIndex < canGroups.Length)
                can.group = canGroups[can.groupIndex];

            can.canObject = canObject;
        }
    }

    private void Start()
    {
        if (savePNGToDisk)
        {
            string folderPath = Path.Combine(Application.dataPath, pngSaveFolder);
            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);
        }

        List<CanDefinition> matchedCans = new List<CanDefinition>();

        foreach (CanDefinition can in cans)
        {
            // Skip the base group indicator object - it gets rendered separately
            if (can.canObject != null && can.canObject.name == "Base Can")
                continue;

            if (can.canObject == null)
            {
                continue;
            }

            matchedCans.Add(can);
        }

        if (matchedCans.Count == 0)
        {
            Debug.LogWarning("[CanManager] No matching can GameObjects found in scene by definition names.");
            return;
        }

        StartCoroutine(RenderAllCansSequential(matchedCans.ToArray()));
    }

    /// <summary>Renders only the base can for the current trial (2D sprite).</summary>
    public void RenderBaseCans()
    {
        CanDefinition baseDef = new CanDefinition { name = "", groupIndex = -1 };
        baseDef.canObject = GameObject.Find("Base Can");
        
        if (baseDef.canObject == null)
        {
            Debug.LogWarning("[CanManager] Base Can not found in scene.");
            return;
        }

        // Try to determine the group from nearby definitions
        foreach (CanDefinition can in cans)
        {
            if (can.canObject != null && can.canObject.name == "Base Can")
            {
                baseDef.groupIndex = can.groupIndex;
                if (baseDef.groupIndex >= 0 && baseDef.groupIndex < canGroups.Length)
                    baseDef.group = canGroups[baseDef.groupIndex];
                break;
            }
        }

        StartCoroutine(RenderBaseCan(baseDef));
    }

    public void InstantiateBasicGroupGameObjects(int groupIndex, Vector3 worldPosition)
    {
        StartCoroutine(InstantiateWhenRenderingDone(groupIndex, worldPosition));
    }

    private IEnumerator InstantiateWhenRenderingDone(int groupIndex, Vector3 worldPosition)
    {
        // Wait until rendering is not happening
        while (_isRenderingSequence)
            yield return null;

        // Find the can group at this index to get the group info
        CanGroup group = canGroups[groupIndex];
        CanDefinition baseDef = new CanDefinition { name = "", groupIndex = groupIndex, group = group };
        baseDef.canObject = GameObject.Find("Base Can");
        baseDef.canObject.transform.position = worldPosition;
        if (baseDef.canObject == null)
        {
            Debug.LogWarning($"[CanManager] Could not find GameObject for group '{group.name}' with expected name 'Base Can'.");
            yield break;
        }

        StartCoroutine(RenderBaseCan(baseDef));
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
        Debug.Log("[CanManager] RenderAllCansSequential START");
        _isRenderingSequence = true;

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

        // After all texture assignments are complete, shuffle object positions in the scene
        Debug.Log("[CanManager] RenderAllCansSequential - About to shuffle");
        ShuffleObjectLocations(selection);

        _isRenderingSequence = false;
        Debug.Log("[CanManager] RenderAllCansSequential END");
    }

    private IEnumerator RenderBaseCan(CanDefinition baseCan)
    {
        //Debug.Log("[CanManager] RenderBaseCan START");
        _isRenderingSequence = true;

        if (baseCan.canObject == null)
        {
            Debug.LogError("[CanManager] Base can object is null");
            _isRenderingSequence = false;
            yield break;
        }

        UpdateCanvasContent(baseCan);
        Canvas.ForceUpdateCanvases();
        yield return null;
        yield return new WaitForEndOfFrame();
        yield return null;

        var spriteRenderer = baseCan.canObject.GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            Debug.LogWarning($"Base can {baseCan.canObject.name} has no SpriteRenderer");
            _isRenderingSequence = false;
            yield break;
        }

        RenderTexture rt = new RenderTexture(renderTextureWidth, renderTextureHeight, 16);
        rt.Create();

        RenderToTexture(rt, null);

        baseCan.runtimeSnapshot = CaptureRenderTexture(rt);
        rt.Release();

        if (baseCan.runtimeSnapshot != null)
        {
            Sprite sprite = Sprite.Create(
                baseCan.runtimeSnapshot,
                new Rect(0, 0, baseCan.runtimeSnapshot.width, baseCan.runtimeSnapshot.height),
                new Vector2(0.5f, 0.5f)
            );

            if (sprite != null)
            {
                spriteRenderer.sprite = sprite;
            }
        }

        if (savePNGToDisk)
        {
            string fileName = baseCan.name.Replace(" ", "_") + "_Base.png";
            SaveTextureToPNG(baseCan.runtimeSnapshot, fileName);
        }

        yield return null;
        yield return new WaitForEndOfFrame();

        _isRenderingSequence = false;
        //Debug.Log("[CanManager] RenderBaseCan END");
    }

    private void ShuffleObjectLocations(CanDefinition[] definitions)
    {
        var objects = new List<GameObject>();
        var positions = new List<Vector3>();

        foreach (var def in definitions)
        {
            if (def.canObject != null)
            {
                objects.Add(def.canObject);
                positions.Add(def.canObject.transform.localPosition);
            }
        }

        if (objects.Count < 2)
            return;

        Debug.Log($"[CanManager] ShuffleObjectLocations: Shuffling {objects.Count} cans - {string.Join(", ", objects.Select(o => o.name))}");

        for (int i = 0; i < positions.Count; i++)
        {
            int rnd = Random.Range(i, positions.Count);
            Vector3 temp = positions[i];
            positions[i] = positions[rnd];
            positions[rnd] = temp;
        }

        for (int i = 0; i < objects.Count; i++)
        {
            objects[i].transform.localPosition = positions[i];
            //Debug.Log($"[CanManager] Moved {objects[i].name} to local position {positions[i]}");
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
        if (labelCamera == null || targetRT == null)
        {
            Debug.LogError("[CanManager] Missing references — cannot render.");
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

        if (canMaterial != null)
        {
            canMaterial.SetTexture("_BaseMap", targetRT);
        }
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
