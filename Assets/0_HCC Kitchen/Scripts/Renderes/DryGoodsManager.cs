using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq;

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
    public static DryGoodsManager Instance { get; private set; }

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

    private bool _isRenderingSequence = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Initialize dry goods definitions and find GameObjects BEFORE Start methods run
        foreach (DryGoodsDefinition dryGood in dryGoods)
        {
            GameObject box = GameObject.Find(dryGood.name) ?? GameObject.Find(dryGood.name + "_Box");

            if (box == null)
            {
                Debug.LogWarning($"[DryGoodsManager] Could not find GameObject with name '{dryGood.name}' or '{dryGood.name}_Box'.");
                continue;
            }

            if (dryGood.groupIndex >= 0 && dryGood.groupIndex < dryGoodsGroups.Length)
                dryGood.group = dryGoodsGroups[dryGood.groupIndex];

            dryGood.boxObject = box;
            //Debug.Log($"[DryGoodsManager] Awake: Assigned boxObject '{box.name}' (parent: {box.transform.parent?.name ?? "null"}) to definition '{dryGood.name}'");
            //dryGood.boxObject.name = dryGood.name + "_Box";
        }
    }

    private void Start()
    {
        // Make sure folder exists if saving
        if (savePNGToDisk)
        {
            string folderPath = Path.Combine(Application.dataPath, pngSaveFolder);
            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);
        }

        List<DryGoodsDefinition> matchedDryGoods = new List<DryGoodsDefinition>();

        foreach (DryGoodsDefinition dryGood in dryGoods)
        {
            // Skip the base group indicator object - it gets rendered separately
            if (dryGood.boxObject != null && dryGood.boxObject.name == "Base Dry Good")
                continue;

            if (dryGood.boxObject == null)
            {
                continue;
            }

            matchedDryGoods.Add(dryGood);
        }

        if (matchedDryGoods.Count == 0)
        {
            Debug.LogWarning("[DryGoodsManager] No matching dry goods GameObjects found in scene by definition names.");
            return;
        }

        // Start sequential rendering coroutine
        StartCoroutine(RenderAllSequential(matchedDryGoods.ToArray()));
    }

    /// <summary>Renders only the base dry good for the current trial (2D sprite).</summary>
    public void RenderBaseCans()
    {
        DryGoodsDefinition baseDef = new DryGoodsDefinition { name = "", groupIndex = -1 };
        baseDef.boxObject = GameObject.Find("Base Dry Good");
        
        if (baseDef.boxObject == null)
        {
            Debug.LogWarning("[DryGoodsManager] Base Dry Good not found in scene.");
            return;
        }

        // Try to determine the group from nearby definitions
        foreach (DryGoodsDefinition dryGood in dryGoods)
        {
            if (dryGood.boxObject != null && dryGood.boxObject.name == "Base Dry Good")
            {
                baseDef.groupIndex = dryGood.groupIndex;
                if (baseDef.groupIndex >= 0 && baseDef.groupIndex < dryGoodsGroups.Length)
                    baseDef.group = dryGoodsGroups[baseDef.groupIndex];
                break;
            }
        }

        StartCoroutine(RenderBaseDryGood(baseDef));
    }

    public void RenderAllDryGoodsNow()
    {
        if (_isRenderingSequence)
        {
            Debug.LogWarning("[DryGoodsManager] Render already in progress.");
            return;
        }

        List<DryGoodsDefinition> matchedDryGoods = new List<DryGoodsDefinition>();

        foreach (DryGoodsDefinition dryGood in dryGoods)
        {
            if (dryGood.boxObject != null && dryGood.boxObject.name == "Base Dry Good")
                continue;

            if (dryGood.boxObject == null)
                continue;

            matchedDryGoods.Add(dryGood);
        }

        if (matchedDryGoods.Count == 0)
        {
            Debug.LogWarning("[DryGoodsManager] No matching dry goods found.");
            return;
        }

        StartCoroutine(RenderAllSequential(matchedDryGoods.ToArray()));
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

    public void InstantiateBasicGroupGameObjects(int groupIndex, Vector3 worldPosition)
    {
        StartCoroutine(InstantiateWhenRenderingDone(groupIndex, worldPosition));
    }

    private IEnumerator InstantiateWhenRenderingDone(int groupIndex, Vector3 worldPosition)
    {
        // Wait until rendering is not happening
        while (_isRenderingSequence)
            yield return null;

        // Find all the positions
        GameObject[] positions = GameObject.FindGameObjectsWithTag("locations").OrderBy(go => go.name).ToArray();
        // Find first dry good with this group index to get the group name
        DryGoodsGroup group = dryGoodsGroups[groupIndex];
        DryGoodsDefinition baseDef = new DryGoodsDefinition { name = "", groupIndex = groupIndex, group = group };
        baseDef.boxObject = GameObject.Find("Base Dry Good");
        baseDef.boxObject.transform.position = worldPosition;
        if (baseDef.boxObject == null)
        {
            Debug.LogWarning($"[DryGoodsManager] Could not find GameObject for group '{group.name}' with expected name 'Base Dry Good'.");
            yield break;
        }

        StartCoroutine(RenderBaseDryGood(baseDef));
    }

    private IEnumerator RenderAllSequential(DryGoodsDefinition[] selection)
    {
        //Debug.Log("[DryGoodsManager] RenderAllSequential START - processing " + selection.Length + " items");
        for (int i = 0; i < selection.Length && i < 3; i++)
        {
            //Debug.Log($"  Item {i}: {selection[i].name} -> boxObject: {selection[i].boxObject?.name ?? "null"}");
        }
        _isRenderingSequence = true;

        for (int i = 0; i < selection.Length; i++)
        {
            DryGoodsDefinition dryGood = selection[i];
            if (dryGood.boxObject == null) continue;

            UpdateCanvasContent(dryGood);
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

            RenderToTexture(rt, renderer.material);

            dryGood.runtimeSnapshot = CaptureRenderTexture(rt);

            if (savePNGToDisk)
            {
                string fileName = dryGood.name.Replace(" ", "_") + ".png";
                SaveTextureToPNG(dryGood.runtimeSnapshot, fileName);
            }

            yield return null;
            yield return new WaitForEndOfFrame();
        }

        //Debug.Log("[DryGoodsManager] RenderAllSequential - About to shuffle");
        ShuffleObjectLocations(selection);

        _isRenderingSequence = false;
        Debug.Log("[DryGoodsManager] RenderAllSequential END");
    }

    private IEnumerator RenderBaseDryGood(DryGoodsDefinition baseDryGood)
    {
        //Debug.Log("[DryGoodsManager] RenderBaseDryGood START");
        _isRenderingSequence = true;

        if (baseDryGood.boxObject == null)
        {
            Debug.LogError("[DryGoodsManager] Base dry good object is null");
            _isRenderingSequence = false;
            yield break;
        }

        UpdateCanvasContent(baseDryGood);
        Canvas.ForceUpdateCanvases();
        yield return null;
        yield return new WaitForEndOfFrame();
        yield return null;

        var spriteRenderer = baseDryGood.boxObject.GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            Debug.LogWarning($"Base dry good {baseDryGood.boxObject.name} has no SpriteRenderer");
            _isRenderingSequence = false;
            yield break;
        }

        RenderTexture rt = new RenderTexture(renderTextureWidth, renderTextureHeight, 16);
        rt.Create();

        RenderToTexture(rt, null);

        baseDryGood.runtimeSnapshot = CaptureRenderTexture(rt);
        rt.Release();

        if (baseDryGood.runtimeSnapshot != null)
        {
            Sprite sprite = Sprite.Create(
                baseDryGood.runtimeSnapshot,
                new Rect(0, 0, baseDryGood.runtimeSnapshot.width, baseDryGood.runtimeSnapshot.height),
                new Vector2(0.5f, 0.5f)
            );

            if (sprite != null)
            {
                spriteRenderer.sprite = sprite;
            }
        }

        if (savePNGToDisk)
        {
            string fileName = baseDryGood.name.Replace(" ", "_") + "_Base.png";
            SaveTextureToPNG(baseDryGood.runtimeSnapshot, fileName);
        }

        yield return null;
        yield return new WaitForEndOfFrame();

        _isRenderingSequence = false;
        //Debug.Log("[DryGoodsManager] RenderBaseDryGood END");
    }

private void ShuffleObjectLocations(DryGoodsDefinition[] definitions)
{
    var objects = new List<GameObject>();
    var positions = new List<Vector3>();

    //Debug.Log($"[DryGoodsManager] ShuffleObjectLocations called with {definitions.Length} definitions");
    
    foreach (var def in definitions)
    {
        if (def.boxObject != null)
        {
            objects.Add(def.boxObject);
            positions.Add(def.boxObject.transform.localPosition);
            //Debug.Log($"  Collecting: '{def.boxObject.name}' (parent: {def.boxObject.transform.parent?.name ?? "null"}, y={def.boxObject.transform.localPosition.y}, tag={def.boxObject.tag})");
            
            // Safety check: warn if this looks like a Can instead of a dry good box
            if (def.boxObject.name.Contains("Can") || def.boxObject.name == "Base Can")
            {
                Debug.LogError($"[DryGoodsManager] ERROR! Found a Can object in DryGoods shuffle: {def.boxObject.name}!");
            }
        }
    }

    if (objects.Count < 2)
        return;

    Debug.Log($"[DryGoodsManager] ShuffleObjectLocations: Shuffling {objects.Count} dry goods");

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
        //Debug.Log($"  [DryGoodsManager] Moved '{objects[i].name}' to local position {positions[i]}");
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
        if (labelCamera == null || targetRT == null)
        {
            Debug.LogError("[DryGoodsManager] Missing references — cannot render.");
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

        if (boxMaterial != null)
        {
            boxMaterial.SetTexture("_BaseMap", targetRT);
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