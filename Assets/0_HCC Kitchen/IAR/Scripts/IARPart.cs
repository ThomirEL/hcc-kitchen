using System.Collections.Generic;
using UnityEngine;
using System.Collections;
using System;

/// <summary>
/// Loads item_properties.json once from Resources and caches all entries
/// as a name → ItemProperty lookup. Place item_properties.json inside
/// any Resources/ folder (e.g. Assets/Resources/item_properties.json).
/// </summary>
public static class ItemPropertiesLoader
{
    [Serializable]
    private class ItemProperty
    {
        public string name;
        public float  commonality;
        public float  howDangerous;
    }

    [Serializable]
    private class ItemPropertyList
    {
        public List<ItemProperty> items;
    }

    private static Dictionary<string, ItemProperty> _cache;

    private static void EnsureLoaded()
    {
        if (_cache != null) return;

        _cache = new Dictionary<string, ItemProperty>(StringComparer.OrdinalIgnoreCase);

        TextAsset json = Resources.Load<TextAsset>("kitchen_parts");
        if (json == null)
        {
            Debug.LogError("ItemPropertiesLoader: could not find 'kitchen_parts.json' in any Resources folder.");
            return;
        }

        ItemPropertyList list = JsonUtility.FromJson<ItemPropertyList>(json.text);
        if (list?.items == null)
        {
            Debug.LogError("ItemPropertiesLoader: failed to parse kitchen_parts.json.");
            return;
        }

        foreach (var item in list.items)
            _cache[item.name] = item;

        Debug.Log($"ItemPropertiesLoader: loaded {_cache.Count} item definitions.");
    }

    /// <summary>
    /// Returns true and writes out commonality/danger if the item name is found.
    /// Matching is case-insensitive.
    /// </summary>
    public static bool TryGetProperties(string itemName, out float commonality, out float howDangerous)
    {
        EnsureLoaded();

        if (_cache != null && _cache.TryGetValue(itemName, out var prop))
        {
            commonality  = prop.commonality;
            howDangerous = prop.howDangerous;
            return true;
        }

        commonality  = 0.5f;
        howDangerous = 0f;
        return false;
    }
}


public class IARPart : MonoBehaviour
{
    [SerializeField]
    [Header("How common is this item. More common = more interest. Most common is 5, least common is 1")]
    [Range(0f,1f)] private float _howCommon = 0.5f;

    public float HowCommon
    {
        get {return _howCommon;}
        set {
            if (_howCommon == value) return;
            _howCommon = value;
            calculateThisDOI("HowCommon");
        }
    }

    [SerializeField]
    [Range(0f,1f)] private float _howDangerous = 0.0f;

    public float HowDangerous
    {
        get {return _howDangerous;}
        set {
            if (_howDangerous == value) return;
            _howDangerous = value;
            calculateThisDOI("HowDangerous");
        }
    }

    // Add these two fields near your other serialized fields
[SerializeField] private float _doiLerpDuration = 5f;
private Coroutine _lerpCoroutine;

    private float _dangerLevelToRise = 0f;
    private float _dangerLevelRiseDuration = 0f;

    private bool _isInCurrentStep;

    public bool IsInCurrentStep
    {
        get {return _isInCurrentStep;}
        set
        {
            if (_isInCurrentStep == value) return;
            _isInCurrentStep = value;
            calculateThisDOI("IsInCurrentStep");
        }
    }

    private int? _stepsInToFuture;

    public int? StepsInToFuture
    {
        get {return _stepsInToFuture;}
        set
        {
            if (_stepsInToFuture == value) return;
            _stepsInToFuture = value;
            calculateThisDOI("StepsInToFuture");
        }
    }

    public bool log = false;

    public bool overrideDOI = false;

    private Dictionary<string, float> _contributions = new();

    [HideInInspector] public float currentDoI = 0.5f;
    [HideInInspector] public Renderer   cachedRenderer;
    [HideInInspector] public Collider   cachedCollider;

    [SerializeField] private string selfContributionKey;
    [SerializeField] private float  selfContrubutionDuration;

    IARManager   manager;
    TaskManager  taskManager;

    void Awake()
    {
        cachedRenderer = GetComponent<Renderer>();
        cachedCollider = GetComponent<Collider>();
        GetManager();
        GetTaskManager();

        // ── Load commonality & danger from JSON ──────────────────────────────
        // The lookup uses the GameObject name. If your GameObject names differ
        // from the JSON "name" field, adjust the string passed here.
        LoadPropertiesFromJson(gameObject.name);
    }

    void Start()
    {
        EnsureProximityCollider();
        // Calculate DOI after all Awake calls complete to ensure materials are properly initialized
        calculateThisDOI("Start");
    }


    /// <summary>
    /// Looks up this GameObject's name in item_properties.json and applies
    /// commonality and howDangerous if a match is found. Always uses the base name (first word)
    /// so that variants like "Tomato" and "Tomato Chunk" load the same properties.
    /// The values are written directly to the backing fields to avoid triggering DOI recalculation.
    /// Awake will call calculateThisDOI() once after this method completes.
    /// </summary>
    private void LoadPropertiesFromJson(string itemName)
    {
        // Always use base name (first word) for variants like "Tomato" and "Tomato Chunk"
        string baseName = itemName;
        
        if (ItemPropertiesLoader.TryGetProperties(baseName, out float commonality, out float howDangerous))
        {
            // Write directly to backing fields — don't use properties to avoid triggering calculateThisDOI.
            // Awake() will call calculateThisDOI() once after all properties are loaded.
            _howCommon = commonality;
            _howDangerous = howDangerous;

            if (log)
                Debug.Log($"IARPart '{name}': loaded from JSON (base name '{baseName}') → commonality={commonality}, howDangerous={howDangerous}");
        }
        else
        {
            Debug.LogWarning($"IARPart '{name}': no JSON entry found for '{baseName}', using Inspector values.");
        }
    }

    private string GetBaseItemName(string itemName)
    {
        if (string.IsNullOrEmpty(itemName)) return itemName;
        return itemName.Split(' ')[0];
    }

    private void GetTaskManager()
    {
        if (taskManager != null) return;

        taskManager = TaskManager.Instance;
        if (taskManager == null)
        {
            taskManager = FindObjectOfType<TaskManager>();
            if (taskManager == null)
                Debug.LogError($"IARPart '{name}' cannot find TaskManager in scene!");
        }
    }

    private void GetManager()
    {
        if (manager != null) return;

        manager = IARManager.Instance;
        if (manager == null)
        {
            manager = FindObjectOfType<IARManager>();
            if (manager == null)
                Debug.LogError($"IARPart '{name}' cannot find IARManager in scene!");
        }
    }

    public void SetContribution(string key, float value)
    {
        _contributions[key] = value;
        calculateThisDOI($"Contribution:{key}");
    }

    public void SetDangerLevel(float dangerLevel)
    {
        _dangerLevelToRise = dangerLevel;
    }

    public void SetDangerDuration(float duration)
    {
        _dangerLevelRiseDuration = duration;
    }

    public void StartDangerRise()
    {
        StartCoroutine(LerpDangerLevel());
    }

    private IEnumerator LerpDangerLevel()
    {
        float time = 0f;
        float initialDanger = HowDangerous;

        while (time < _dangerLevelRiseDuration)
        {
            float t = time / _dangerLevelRiseDuration;
            HowDangerous = Mathf.Lerp(initialDanger, _dangerLevelToRise, t);
            time += Time.deltaTime;
            yield return null;
        }

        HowDangerous = _dangerLevelToRise;
    }

    /// <summary>
    /// Public method to recalculate DOI. Called by IARManager when settings change.
    /// </summary>
    public void RecalculateDOI(string reason = "External recalculation")
    {
        calculateThisDOI(reason);
    }

    private void EnsureProximityCollider()
{
    // Check if one already exists (e.g. set up in Editor)
    Transform existing = transform.Find("_ProximityTrigger");
    if (existing != null) return;

    GameObject trigger = new GameObject("_ProximityTrigger");
    trigger.transform.SetParent(transform, worldPositionStays: false);
    trigger.layer = LayerMask.NameToLayer("IARPart");

    // Small sphere — just needs to be detectable by OverlapSphere.
    // Actual distance filtering is done in CalculateProximity().
    SphereCollider sc = trigger.AddComponent<SphereCollider>();
    sc.isTrigger = true;
    sc.radius = 0.1f;
}

    private IEnumerator LerpDOI(float targetDOI, float initialDOI, float duration, Renderer[] renderers)
{
    float time = 0f;

    while (time < duration)
    {
        float t = time / duration;
        currentDoI = Mathf.Lerp(initialDOI, targetDOI, t);
        if (log)
            Debug.Log($"Lerping DOI for {name}: {currentDoI:F2} (target: {targetDOI:F2}, time: {time:F2}/{duration:F2})");

        foreach (Renderer r in renderers)
            if (r != null && r.material != null)
                r.material.SetFloat("_DOI", currentDoI);

        time += Time.deltaTime;
        yield return null;
    }

    currentDoI = targetDOI;
    foreach (Renderer r in renderers)
        if (r != null && r.material != null)
            r.material.SetFloat("_DOI", currentDoI);

    _lerpCoroutine = null;
}

 [HideInInspector] public bool IsInProximityRange = false;

void OnEnable()  { IARManager.Instance?.RegisterPart(this);   }
void OnDisable() { IARManager.Instance?.UnregisterPart(this);  }

// ─── MODIFY CalculateProximity() ──────────────────────────────────────────
    private float CalculateProximity()
    {
        // Fast path: skip the sqrt entirely when out of range
        if (!IsInProximityRange) return 0f;

        if (manager.LeftController == null || manager.RightController == null)
            return 0f;

        float distToLeft  = Vector3.Distance(transform.position, manager.LeftController.position);
        float distToRight = Vector3.Distance(transform.position, manager.RightController.position);
        float closest     = Mathf.Min(distToLeft, distToRight);

        float proximityDOI = Mathf.Clamp01(1f - (closest / manager.ProximityRadius));

        //if (log) Debug.Log($"{gameObject.name} proximity DOI: {proximityDOI:F2} (dist: {closest:F2}m)");

        return manager.ProximityWeight * proximityDOI;
    }
    private void calculateThisDOI(string changedAspect = null)
    {
        if (log)
            Debug.Log($"[{name}] [DOI] === START calculateThisDOI === (reason: {changedAspect})");

        float prevDOI = currentDoI;

        if (log)
            Debug.Log($"[{name}] [DOI] Previous DOI: {prevDOI:F4}");

        currentDoI = CalculateDOI();

        if (log)
            Debug.Log($"[{name}] [DOI] New DOI after CalculateDOI(): {currentDoI:F4}");

        if (log)
            Debug.Log($"[{name}] [DOI] ΔDOI: {(currentDoI - prevDOI):F4}");

        // 🔍 Proximity debug (important for your issue)
        if (log)
        {
            float proximity = CalculateProximity();
            Debug.Log($"[{name}] [DOI] Proximity: {proximity:F4} | IsInProximityRange: {IsInProximityRange}");
        }

        // Collect all renderers to update — root first, then children as fallback
        Renderer[] renderers = null;

        if (cachedRenderer != null)
        {
            if (log)
                Debug.Log($"[{name}] [DOI] Found cachedRenderer");

            if (cachedRenderer.material != null)
            {
                if (log)
                    Debug.Log($"[{name}] [DOI] cachedRenderer has valid material: {cachedRenderer.material.name}");

                renderers = new Renderer[] { cachedRenderer };
            }
            else
            {
                if (log)
                    Debug.LogWarning($"[{name}] [DOI] cachedRenderer exists but has NO material!");
            }
        }
        else
        {
            if (log)
                Debug.LogWarning($"[{name}] [DOI] No cachedRenderer found");
        }

        // Fallback to children
        if (renderers == null)
        {
            if (log)
                Debug.Log($"[{name}] [DOI] Falling back to GetComponentsInChildren<Renderer>()");

            Renderer[] all = GetComponentsInChildren<Renderer>();

            if (log)
                Debug.Log($"[{name}] [DOI] Found {all.Length} total renderers in children");

            var valid = new System.Collections.Generic.List<Renderer>();

            foreach (Renderer r in all)
            {
                if (r == null)
                {
                    if (log)
                        Debug.LogWarning($"[{name}] [DOI] Found NULL renderer in children!");
                    continue;
                }

                if (r.sharedMaterial == null)
                {
                    if (log)
                        Debug.LogWarning($"[{name}] [DOI] Renderer '{r.name}' has NO sharedMaterial");
                    continue;
                }

                if (log)
                    Debug.Log($"[{name}] [DOI] Valid renderer: '{r.name}' (material: {r.sharedMaterial.name})");

                valid.Add(r);
            }

            if (valid.Count > 0)
            {
                renderers = valid.ToArray();

                if (log)
                    Debug.Log($"[{name}] [DOI] Using {renderers.Length} valid child renderers");
            }
            else
            {
                if (log)
                    Debug.LogWarning($"[{name}] [DOI] No valid child renderers found!");
            }
        }

        // Final safety check
        if (renderers == null || renderers.Length == 0)
        {
            Debug.LogWarning($"[{name}] [DOI] NO VALID RENDERERS → aborting DOI update.");
            return;
        }

        // Coroutine handling
        if (_lerpCoroutine != null)
        {
            if (log)
                Debug.Log($"[{name}] [DOI] Stopping existing lerp coroutine");
            return;

            //StopCoroutine(_lerpCoroutine);
        }
        else
        {
            if (log)
                Debug.Log($"[{name}] [DOI] No existing lerp coroutine running");
        }

        if (log)
        {
            Debug.Log($"[{name}] [DOI] Starting LerpDOI coroutine:");
            Debug.Log($"[{name}] [DOI]    From: {prevDOI:F4}");
            Debug.Log($"[{name}] [DOI]    To:   {currentDoI:F4}");
            Debug.Log($"[{name}] [DOI]    Duration: {_doiLerpDuration:F2}s");
            Debug.Log($"[{name}] [DOI]    Renderer count: {renderers.Length}");
        }

        _lerpCoroutine = StartCoroutine(LerpDOI(currentDoI, prevDOI, _doiLerpDuration, renderers));

        if (log)
            Debug.Log($"[{name}] [DOI] === END calculateThisDOI ===");
    }

    public float CalculateDOI()
    {

        if (overrideDOI) return 1f; // If override is enabled, skip calculation and return current DOI

        GetManager();

        if (manager == null)
        {
            Debug.LogError($"IARPart '{name}' cannot calculate DOI - IARManager not found!");
            return 0f;
        }

        float DOI = 0f;

        if (manager.Commonality)            DOI += CalculateCommonality();
        if (manager.Danger)                 DOI += CalculateDanger();
        if (manager.Intent)                 DOI += CalculateIntent();
        if (manager.Proximity)              DOI += CalculateProximity();
        
        // Only calculate task relevance if this item is in the current recipe
        GetTaskManager();
        if (taskManager != null && taskManager.IsItemInCurrentRecipe(gameObject.name))
        {
            if (manager.CurrentTaskRelevance)   DOI += AddDOIForCurrentStep();
            if (manager.FutureTaskRelevance)    DOI += AddDOIForFutureSteps(_stepsInToFuture ?? 0);
        }

        return Mathf.Clamp01(DOI);
    }


    private float CalculateDanger()
    {
        return manager.DangerWeight * Mathf.Lerp(manager.DANGER_MIN, manager.DANGER_MAX, HowDangerous);
    }

    private float CalculateCommonality()
    {
        return manager.CommonalityWeight * Mathf.Lerp(manager.COMMON_MIN, manager.COMMON_MAX, HowCommon);
    }

    private float CalculateIntent()
    {
        return manager.PairingWeight * GetCombinedIntentInterest();
    }

    public void ClearContribution(string key)
    {
        _contributions.Remove(key);
    }

    public void ClearAllContributions()
    {
        _contributions.Clear();
        calculateThisDOI("ClearAllContributions");
    }

    public float GetCombinedIntentInterest()
    {
        float total = 0f;
        foreach (var v in _contributions.Values)
            total += v;
        return Mathf.Clamp01(total);
    }

    public float GetContribution(string key)
    {
        return _contributions.TryGetValue(key, out float v) ? v : 0f;
    }

    public float AddDOIForCurrentStep()
    {
        GetTaskManager();
        if (taskManager == null)
        {
            Debug.LogWarning($"IARPart '{name}' cannot find TaskManager!");
            return 0f;
        }

        List<string> currentStepObjects = taskManager.GetCurrentStepObjects();
        string basePartName = GetBaseItemName(gameObject.name);
        
        if (gameObject.name == "Chopping Knife")
        {
            Debug.Log($"IARPart '{name}' checking current step relevance. Current step objects: {string.Join(", ", currentStepObjects)}");
        }
        
        foreach (string objectName in currentStepObjects)
        {
            string baseObjectName = GetBaseItemName(objectName);
            if (basePartName == baseObjectName)
            {
                if (log) Debug.Log($"{gameObject.name} is used in current step: {taskManager.CurrentStep.description}");
                return manager.CurrentTaskRelevanceWeight * 0.5f;
            }
        }
        return 0f;
    }

    public float AddDOIForFutureSteps(int howManyStepsAhead)
    {
        if (howManyStepsAhead <= 0) return 0f;
        // Smooth exponential decay: 1 step → 0.70, 2 steps → 0.50, 3 steps → 0.35, 8 steps → 0.07
        float distanceFactor = Mathf.Exp(-howManyStepsAhead * 0.35f);

        if (log) Debug.Log($"{gameObject.name} used in {howManyStepsAhead} step(s) ahead (bonus: {distanceFactor})");
        return manager.FutureTaskRelevanceWeight * distanceFactor;
    }

    private float CalculateStepDistanceFalloff(int stepDistance)
    {
        if (stepDistance == 0) return 1f;
        return Mathf.Exp(-stepDistance * stepDistance / (2f * manager.gaussianSigma * manager.gaussianSigma));
    }
}