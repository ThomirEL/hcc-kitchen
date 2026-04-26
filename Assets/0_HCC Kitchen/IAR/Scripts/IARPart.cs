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

    [Range(1,5)] private int? _stepsInToFuture;

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
        // Calculate DOI after all Awake calls complete to ensure materials are properly initialized
        calculateThisDOI("Start");
    }


    /// <summary>
    /// Looks up this GameObject's name in item_properties.json and applies
    /// commonality and howDangerous if a match is found. The values are written
    /// directly to the backing fields to avoid triggering DOI recalculation.
    /// Awake will call calculateThisDOI() once after this method completes.
    /// </summary>
    private void LoadPropertiesFromJson(string itemName)
    {
        if (ItemPropertiesLoader.TryGetProperties(itemName, out float commonality, out float howDangerous))
        {
            // Write directly to backing fields — don't use properties to avoid triggering calculateThisDOI.
            // Awake() will call calculateThisDOI() once after all properties are loaded.
            _howCommon = commonality;
            _howDangerous = howDangerous;

            if (log)
                Debug.Log($"IARPart '{name}': loaded from JSON → commonality={commonality}, howDangerous={howDangerous}");
        }
        else
        {
            Debug.LogWarning($"IARPart '{name}': no JSON entry found for '{itemName}', using Inspector values.");
        }
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
    public void RecalculateDOI()
    {
        calculateThisDOI("External recalculation");
    }

    private void calculateThisDOI(string changedAspect = null)
    {
        var prevDOI = currentDoI;
        currentDoI = CalculateDOI();
        if (log)
        Debug.Log($" Calculating DOI for {name} (changed aspect: {changedAspect}), DOI was {prevDOI} -> new DOI {currentDoI}, /n  Commonality: {HowCommon}, Danger: {HowDangerous}, Intent: {GetCombinedIntentInterest()}, InCurrentStep: {IsInCurrentStep}, StepsInToFuture: {StepsInToFuture}");

        if (cachedRenderer != null && cachedRenderer.material != null)
        {
            cachedRenderer.material.SetFloat("_DOI", currentDoI);
        }
        else
        {
            if (gameObject.name == "Chopping Knife")
            {
                Debug.LogWarning($"IARPart '{name}' cannot update material - Renderer or material not found on object! Attempting to update children renderers as fallback.");
            }
            Renderer[] childRenderers = GetComponentsInChildren<Renderer>();
            if (gameObject.name == "Chopping Knife")
            {
                Debug.Log($"Found {childRenderers.Length} child renderers for '{name}'.");
            }
            if (childRenderers.Length > 0)
            {
                foreach (Renderer childRenderer in childRenderers)
                {
                    if (childRenderer != null && childRenderer.sharedMaterial != null)
                        childRenderer.sharedMaterial.SetFloat("_DOI", currentDoI);
                }
            }
            else
                Debug.LogWarning($"IARPart '{name}' cannot update material - Renderer or material not found on object or children!");
        }
    }

    public float CalculateDOI()
    {
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
        //Debug.Log($"IARPart '{name}' checking current step relevance. Current step objects: {string.Join(", ", currentStepObjects)}");
        if (gameObject.name == "Chopping Knife")
        {
            Debug.Log($"IARPart '{name}' checking current step relevance. Current step objects: {string.Join(", ", currentStepObjects)}");
        }
        if (currentStepObjects.Contains(gameObject.name))
        {
            if (log) Debug.Log($"{gameObject.name} is used in current step: {taskManager.CurrentStep.description}");
            return 0.5f;
        }
        return 0f;
    }

    public float AddDOIForFutureSteps(int howManyStepsAhead)
    {
        // Maps: 1 step ahead → 1.0,  2 steps → 0.5,  3 steps → 0.0
        // (howManyStepsAhead - 1) normalized over the range [1..3]
        float t = (howManyStepsAhead - 1f) / 2f;
        float distanceFactor = Mathf.Lerp(1f, 0f, t);

        if (log) Debug.Log($"{gameObject.name} used in {howManyStepsAhead} step(s) ahead (bonus: {distanceFactor})");
        return distanceFactor;
    }

    private float CalculateStepDistanceFalloff(int stepDistance)
    {
        if (stepDistance == 0) return 1f;
        return Mathf.Exp(-stepDistance * stepDistance / (2f * manager.gaussianSigma * manager.gaussianSigma));
    }
}