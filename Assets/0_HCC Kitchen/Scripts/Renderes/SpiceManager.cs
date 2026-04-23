using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq;

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
    public static SpiceManager Instance { get; private set; }

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

    private bool _isRenderingSequence = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Initialize spice definitions and find GameObjects BEFORE Start methods run
        foreach (SpiceDefinition spice in spices)
        {
            GameObject jar = GameObject.Find(spice.name) ?? GameObject.Find(spice.name + "_Jar");

            if (jar == null)
            {
                Debug.LogWarning($"[SpiceManager] Could not find GameObject with name '{spice.name}' or '{spice.name}_Jar'.");
                continue;
            }

            if (spice.groupIndex >= 0 && spice.groupIndex < spiceGroups.Length)
                spice.group = spiceGroups[spice.groupIndex];

            spice.jarObject = jar;
            //spice.jarObject.name = spice.name + "_Jar";
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

        List<SpiceDefinition> matchedSpices = new List<SpiceDefinition>();

        foreach (SpiceDefinition spice in spices)
        {
            // Skip the base group indicator object - it gets rendered separately
            if (spice.jarObject != null && spice.jarObject.name == "Base Jar")
                continue;

            if (spice.jarObject == null)
            {
                continue;
            }

            matchedSpices.Add(spice);
        }

        if (matchedSpices.Count == 0)
        {
            Debug.LogWarning("[SpiceManager] No matching spice GameObjects found in scene by definition names.");
            return;
        }

        StartCoroutine(RenderAllSpicesSequential(matchedSpices.ToArray()));
    }

    /// <summary>Renders only the base spice for the current trial (2D sprite).</summary>
    public void RenderBaseCans()
    {
        SpiceDefinition baseDef = new SpiceDefinition { name = "", groupIndex = -1 };
        baseDef.jarObject = GameObject.Find("Base Jar");
        
        if (baseDef.jarObject == null)
        {
            Debug.LogWarning("[SpiceManager] Base Jar not found in scene.");
            return;
        }

        // Try to determine the group from nearby definitions
        foreach (SpiceDefinition spice in spices)
        {
            if (spice.jarObject != null && spice.jarObject.name == "Base Jar")
            {
                baseDef.groupIndex = spice.groupIndex;
                if (baseDef.groupIndex >= 0 && baseDef.groupIndex < spiceGroups.Length)
                    baseDef.group = spiceGroups[baseDef.groupIndex];
                break;
            }
        }

        StartCoroutine(RenderBaseSpice(baseDef));
    }

    public void RenderAllSpicesNow()
    {
        if (_isRenderingSequence)
        {
            Debug.LogWarning("[SpiceManager] Render already in progress.");
            return;
        }

        List<SpiceDefinition> matchedSpices = new List<SpiceDefinition>();

        foreach (SpiceDefinition spice in spices)
        {
            if (spice.jarObject != null && spice.jarObject.name == "Base Jar")
                continue;

            if (spice.jarObject == null)
                continue;

            matchedSpices.Add(spice);
        }

        if (matchedSpices.Count == 0)
        {
            Debug.LogWarning("[SpiceManager] No matching spices found.");
            return;
        }

        StartCoroutine(RenderAllSpicesSequential(matchedSpices.ToArray()));
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
        // Find the spice group at this index to get the group info
        SpiceGroup group = spiceGroups[groupIndex];
        SpiceDefinition baseDef = new SpiceDefinition { name = "", groupIndex = groupIndex, group = group };
        baseDef.jarObject = GameObject.Find("Base Jar");
        baseDef.jarObject.transform.position = worldPosition;
        if (baseDef.jarObject == null)
        {
            Debug.LogWarning($"[SpiceManager] Could not find GameObject for group '{group.name}' with expected name 'Base Jar'.");
            yield break;
        }

        StartCoroutine(RenderBaseSpice(baseDef));
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
        //Debug.Log("[SpiceManager] RenderAllSpicesSequential START");
        _isRenderingSequence = true;

        for (int i = 0; i < spicesToRender.Length; i++)
        {
            SpiceDefinition spice = spicesToRender[i];
            if (spice.jarObject == null) continue;

            UpdateCanvasContent(spice);
            Canvas.ForceUpdateCanvases();
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

            yield return null;
            yield return new WaitForEndOfFrame();
        }

//        Debug.Log("[SpiceManager] RenderAllSpicesSequential - About to shuffle");
        ShuffleObjectLocations(spicesToRender);

        _isRenderingSequence = false;
        //Debug.Log("[SpiceManager] RenderAllSpicesSequential END");
    }

    private IEnumerator RenderBaseSpice(SpiceDefinition baseSpice)
    {
        //Debug.Log("[SpiceManager] RenderBaseSpice START");
        _isRenderingSequence = true;

        if (baseSpice.jarObject == null)
        {
            Debug.LogError("[SpiceManager] Base spice object is null");
            _isRenderingSequence = false;
            yield break;
        }

        UpdateCanvasContent(baseSpice);
        Canvas.ForceUpdateCanvases();
        yield return null;
        yield return new WaitForEndOfFrame();
        yield return null;

        var spriteRenderer = baseSpice.jarObject.GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            Debug.LogWarning($"Base spice {baseSpice.jarObject.name} has no SpriteRenderer");
            _isRenderingSequence = false;
            yield break;
        }

        RenderTexture rt = new RenderTexture(renderTextureWidth, renderTextureHeight, 16);
        rt.Create();

        RenderToTexture(rt, null);

        baseSpice.runtimeSnapshot = CaptureRenderTexture(rt);
        rt.Release();

        if (baseSpice.runtimeSnapshot != null)
        {
            Sprite sprite = Sprite.Create(
                baseSpice.runtimeSnapshot,
                new Rect(0, 0, baseSpice.runtimeSnapshot.width, baseSpice.runtimeSnapshot.height),
                new Vector2(0.5f, 0.5f)
            );

            if (sprite != null)
            {
                spriteRenderer.sprite = sprite;
            }
        }

        if (savePNGToDisk)
        {
            string fileName = baseSpice.name.Replace(" ", "_") + "_Base.png";
            SaveTextureToPNG(baseSpice.runtimeSnapshot, fileName);
        }

        yield return null;
        yield return new WaitForEndOfFrame();

        _isRenderingSequence = false;
        //Debug.Log("[SpiceManager] RenderBaseSpice END");
    }

    private void ShuffleObjectLocations(SpiceDefinition[] definitions)
    {
        var objects = new List<GameObject>();
        var positions = new List<Vector3>();

        foreach (var def in definitions)
        {
            if (def.jarObject != null)
            {
                objects.Add(def.jarObject);
                positions.Add(def.jarObject.transform.localPosition);
            }
        }

        if (objects.Count < 2)
            return;

        //Debug.Log($"[SpiceManager] ShuffleObjectLocations: Shuffling {objects.Count} spices - {string.Join(", ", objects.Select(o => o.name))}");

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
            //Debug.Log($"[SpiceManager] Moved {objects[i].name} to local position {positions[i]}");
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
        if (labelCamera == null || targetRT == null)
        {
            Debug.LogError("[SpiceManager] Missing references — cannot render.");
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

        if (jarMaterial != null)
        {
            jarMaterial.SetTexture("_BaseMap", targetRT);
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