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
    // ─── Serialized fields ────────────────────────────────────────────────────

    [Header("How common is this item. More common = more interest. Most common is 5, least common is 1")]
    [SerializeField] [Range(0f, 1f)] private float _howCommon = 0.5f;

    [SerializeField] [Range(0f, 1f)] private float _howDangerous = 0.0f;

    [SerializeField] private float _doiLerpDuration = 5f;

    [SerializeField] private string selfContributionKey;
    [SerializeField] private float  selfContrubutionDuration;

    // ─── Public properties ────────────────────────────────────────────────────

    public float HowCommon
    {
        get => _howCommon;
        set
        {
            if (_howCommon == value) return;
            _howCommon = value;
            calculateThisDOI("HowCommon");
        }
    }

    public float HowDangerous
    {
        get => _howDangerous;
        set
        {
            if (_howDangerous == value) return;
            _howDangerous = value;
            calculateThisDOI("HowDangerous");
        }
    }

    public bool IsInCurrentStep
    {
        get => _isInCurrentStep;
        set
        {
            if (_isInCurrentStep == value) return;
            _isInCurrentStep = value;
            calculateThisDOI("IsInCurrentStep");
        }
    }

    public int? StepsInToFuture
    {
        get => _stepsInToFuture;
        set
        {
            if (_stepsInToFuture == value) return;
            _stepsInToFuture = value;
            calculateThisDOI("StepsInToFuture");
        }
    }

    // ─── Public state ─────────────────────────────────────────────────────────

    public bool log         = false;
    public bool overrideDOI = false;

    [HideInInspector] public float    currentDoI = 0.5f;
    [HideInInspector] public Renderer cachedRenderer;
    [HideInInspector] public Collider cachedCollider;
    [HideInInspector] public bool     IsInProximityRange = false;

    // ─── Private state ────────────────────────────────────────────────────────

    private bool  _isInCurrentStep;
    private int?  _stepsInToFuture;

    private float _targetDoI;                    // lerp chases this every Update
    private Renderer[] _cachedRenderers;         // collected once in Start, reused every frame

    private float _dangerLevelToRise      = 0f;
    private float _dangerLevelRiseDuration = 0f;

    private Dictionary<string, float> _contributions = new();

    private IARManager  manager;
    private TaskManager taskManager;
    [Header("Velocity influence on proximity")]
[SerializeField] private float _velocityMax = 2.0f;        // speed at which boost caps
[SerializeField] private float _velocityInfluence = 1.0f;  // how much velocity affects DOI

[Header("DEBUG DOI Breakdown")]
[SerializeField] private bool debugDOI = true;

[ReadOnly] public float dbg_commonality;
[ReadOnly] public float dbg_danger;
[ReadOnly] public float dbg_intent;
[ReadOnly] public float dbg_proximity;
[ReadOnly] public float dbg_currentStep;
[ReadOnly] public float dbg_futureSteps;
[ReadOnly] public float dbg_total;

    // ─── Lifecycle ────────────────────────────────────────────────────────────

    

    void Awake()
    {
        cachedRenderer = GetComponent<Renderer>();
        cachedCollider = GetComponent<Collider>();
        GetManager();
        GetTaskManager();
        LoadPropertiesFromJson(gameObject.name);
    }

    void Start()
    {
        EnsureProximityCollider();
        CacheRenderers();
        calculateThisDOI("Start");
        // Snap DOI immediately on start — no lerp from 0
        currentDoI = _targetDoI;
    }

    void Update()
    {
        // ── DOI lerp ──────────────────────────────────────────────────────────
        // Runs every frame. Cost: one float compare, one Lerp, foreach over
        // 1–3 renderers. Equivalent to the old per-frame coroutine while-loop,
        // but without any coroutine start/stop overhead or race conditions.
        var currentShaderDoi = _cachedRenderers != null && _cachedRenderers.Length > 0 && _cachedRenderers[0].material != null
            ? _cachedRenderers[0].material.GetFloat("_DOI")
            : -1f;
        if (_cachedRenderers != null && Mathf.Abs(currentShaderDoi - _targetDoI) > 0.0001f)
        {
            if (log)
                Debug.Log($"[{name}] Lerp DOI: {currentDoI:F4} → {_targetDoI:F4} (delta: {Mathf.Abs(currentDoI - _targetDoI):F6})");
            currentDoI = Mathf.Lerp(currentDoI, _targetDoI, Time.deltaTime / _doiLerpDuration);

            // Snap when close enough to avoid infinite asymptotic crawl
            if (Mathf.Abs(currentDoI - _targetDoI) < 0.001f)
                currentDoI = _targetDoI;

            foreach (Renderer r in _cachedRenderers)
                if (r != null && r.material != null)
                    r.material.SetFloat("_DOI", currentDoI);
        }
    }

    void OnEnable()  { IARManager.Instance?.RegisterPart(this);  }
    void OnDisable() { IARManager.Instance?.UnregisterPart(this); }

    // ─── Renderer caching ─────────────────────────────────────────────────────

    /// <summary>
    /// Collects all valid renderers once at Start so Update never calls
    /// GetComponentsInChildren (which is expensive per-frame).
    /// Root renderer takes priority; falls back to children if root has none.
    /// </summary>
    private void CacheRenderers()
    {
        if (cachedRenderer != null && cachedRenderer.material != null)
        {
            _cachedRenderers = new Renderer[] { cachedRenderer };
            return;
        }

        Renderer[] all   = GetComponentsInChildren<Renderer>();
        var        valid = new List<Renderer>();

        foreach (Renderer r in all)
            if (r != null && r.sharedMaterial != null)
                valid.Add(r);

        if (valid.Count > 0)
        {
            _cachedRenderers = valid.ToArray();
            if (log)
                Debug.Log($"[{name}] CacheRenderers: using {_cachedRenderers.Length} child renderer(s).");
        }
        else
        {
            _cachedRenderers = null;
            Debug.LogWarning($"[{name}] CacheRenderers: no valid renderers found.");
        }
    }

    // ─── Proximity collider ───────────────────────────────────────────────────

    /// <summary>
    /// Adds a small trigger child on the IARPart layer for proximity overlap
    /// detection. Kept separate from the root so the root's physics layer
    /// (and therefore XRI grab detection) is untouched.
    /// </summary>
    private void EnsureProximityCollider()
    {
        if (transform.Find("_ProximityTrigger") != null) return;

        GameObject trigger = new GameObject("_ProximityTrigger");
        trigger.transform.SetParent(transform, worldPositionStays: false);
        trigger.layer = LayerMask.NameToLayer("IARPart");

        SphereCollider sc = trigger.AddComponent<SphereCollider>();
        sc.isTrigger = true;
        sc.radius    = 0.1f;
    }

    // ─── JSON loading ─────────────────────────────────────────────────────────

    /// <summary>
    /// Looks up this item in kitchen_parts.json. Writes directly to backing
    /// fields to avoid triggering calculateThisDOI before Start is ready.
    /// </summary>
    private void LoadPropertiesFromJson(string itemName)
    {
        if (ItemPropertiesLoader.TryGetProperties(itemName, out float commonality, out float howDangerous))
        {
            _howCommon    = commonality;
            _howDangerous = howDangerous;

            if (log)
                Debug.Log($"[{name}] Loaded from JSON → commonality={commonality}, howDangerous={howDangerous}");
        }
        else
        {
            Debug.LogWarning($"[{name}] No JSON entry found for '{itemName}', using Inspector values.");
        }
    }

    // ─── Manager accessors ────────────────────────────────────────────────────

    private void GetManager()
    {
        if (manager != null) return;
        manager = IARManager.Instance ?? FindObjectOfType<IARManager>();
        if (manager == null)
            Debug.LogError($"[{name}] Cannot find IARManager in scene!");
    }

    private void GetTaskManager()
    {
        if (taskManager != null) return;
        taskManager = TaskManager.Instance ?? FindObjectOfType<TaskManager>();
        if (taskManager == null)
            Debug.LogError($"[{name}] Cannot find TaskManager in scene!");
    }

    // ─── DOI calculation ──────────────────────────────────────────────────────

    /// <summary>
    /// Sets _targetDoI to the newly calculated value. Update() lerps
    /// currentDoI toward it every frame — no coroutines needed.
    /// </summary>
    private void calculateThisDOI(string changedAspect = null)
    {
        _targetDoI = CalculateDOI();

        if (log)
            Debug.Log($"[{name}] DOI target → {_targetDoI:F4} (reason: {changedAspect})");
    }

    public void RecalculateDOI(string reason = "External recalculation")
    {
        calculateThisDOI(reason);
    }

    

    public float CalculateDOI()
{
    if (overrideDOI) return 1f;

    GetManager();
    if (manager == null)
    {
        Debug.LogError($"[{name}] Cannot calculate DOI — IARManager not found!");
        return 0f;
    }

    float DOI = 0f;

    // ── Individual contributions ─────────────────────
    dbg_commonality = manager.Commonality ? CalculateCommonality() : 0f;
    dbg_danger      = manager.Danger      ? CalculateDanger()      : 0f;
    dbg_intent      = manager.Intent      ? CalculateIntent()      : 0f;
    dbg_proximity   = manager.Proximity   ? CalculateProximity()   : 0f;

    dbg_currentStep = 0f;
    dbg_futureSteps = 0f;

    GetTaskManager();
    if (taskManager != null && taskManager.IsItemInCurrentRecipe(gameObject.name))
    {
        if (manager.CurrentTaskRelevance)
            dbg_currentStep = AddDOIForCurrentStep();

        if (manager.FutureTaskRelevance)
            dbg_futureSteps = AddDOIForFutureSteps(_stepsInToFuture ?? 0);
    }

    DOI = dbg_commonality
        + dbg_danger
        + dbg_intent
        + dbg_proximity
        + dbg_currentStep
        + dbg_futureSteps;

    dbg_total = Mathf.Clamp01(DOI);

    // ── Optional logging ─────────────────────────────
    if (debugDOI && log)
    {
        Debug.Log(
            $"[{name}] DOI Breakdown:\n" +
            $"  Commonality : {dbg_commonality:F3}\n" +
            $"  Danger      : {dbg_danger:F3}\n" +
            $"  Intent      : {dbg_intent:F3}\n" +
            $"  Proximity   : {dbg_proximity:F3}\n" +
            $"  CurrentStep : {dbg_currentStep:F3}\n" +
            $"  FutureSteps : {dbg_futureSteps:F3}\n" +
            $"  TOTAL       : {dbg_total:F3}"
        );
    }

    return dbg_total;
}

    // ─── DOI components ───────────────────────────────────────────────────────

    private float CalculateCommonality()
    {
        return manager.CommonalityWeight * Mathf.Lerp(manager.COMMON_MIN, manager.COMMON_MAX, HowCommon);
    }

    private float CalculateDanger()
    {
        return manager.DangerWeight * Mathf.Lerp(manager.DANGER_MIN, manager.DANGER_MAX, HowDangerous);
    }

    private float CalculateIntent()
    {
        return manager.PairingWeight * GetCombinedIntentInterest();
    }

    private float CalculateProximity()
{
    if (!IsInProximityRange) return 0f;
    if (manager.LeftController == null || manager.RightController == null) return 0f;

    // ── Distance ─────────────────────────────────────
    float distToLeft  = Vector3.Distance(transform.position, manager.LeftController.position);
    float distToRight = Vector3.Distance(transform.position, manager.RightController.position);
    float closest     = Mathf.Min(distToLeft, distToRight);

    float proximityDOI = Mathf.Clamp01(1f - (closest / manager.ProximityRadius));

    // ── Velocity (NEW) ───────────────────────────────
    float leftVelocity  = GetVelocity(manager.LeftController);
    float rightVelocity = GetVelocity(manager.RightController);

    float maxVelocity = Mathf.Max(leftVelocity, rightVelocity);

    // Normalize velocity (0 → 1)
    float velocityFactor = Mathf.Clamp01(maxVelocity / _velocityMax);
    velocityFactor = Mathf.SmoothStep(0f, 1f, velocityFactor);

    // Apply influence (can exceed 1 before clamp later)
    float velocityBoost = 1f + (velocityFactor * _velocityInfluence);

    if (log)
        Debug.Log($"[{name}] Proximity: {proximityDOI:F2}, Velocity: {maxVelocity:F2}, Boost: {velocityBoost:F2}");

    return manager.ProximityWeight * proximityDOI * velocityBoost;
}

    private float CalculateStepDistanceFalloff(int stepDistance)
    {
        if (stepDistance == 0) return 1f;
        return Mathf.Exp(-stepDistance * stepDistance / (2f * manager.gaussianSigma * manager.gaussianSigma));
    }

    private Vector3 _prevLeftPos;
private Vector3 _prevRightPos;
private bool _hasPrev = false;

private float GetVelocity(Transform controller)
{
    if (!_hasPrev)
    {
        _prevLeftPos = manager.LeftController.position;
        _prevRightPos = manager.RightController.position;
        _hasPrev = true;
        return 0f;
    }

    float velocity;

    if (controller == manager.LeftController)
    {
        velocity = (_prevLeftPos - controller.position).magnitude / Time.deltaTime;
        _prevLeftPos = controller.position;
    }
    else
    {
        velocity = (_prevRightPos - controller.position).magnitude / Time.deltaTime;
        _prevRightPos = controller.position;
    }

    return velocity;
}

    // ─── Task relevance ───────────────────────────────────────────────────────

    public float AddDOIForCurrentStep()
    {
        GetTaskManager();
        if (taskManager == null) return 0f;

        List<string> currentStepObjects = taskManager.GetCurrentStepObjects();
        string basePartName = GetBaseItemName(gameObject.name);

        foreach (string objectName in currentStepObjects)
        {
            if (basePartName == GetBaseItemName(objectName))
            {
                if (log) Debug.Log($"[{name}] In current step: {taskManager.CurrentStep.description}");
                return manager.CurrentTaskRelevanceWeight * 0.5f;
            }
        }
        return 0f;
    }

    public float AddDOIForFutureSteps(int howManyStepsAhead)
    {
        if (howManyStepsAhead <= 0) return 0f;
        float distanceFactor = Mathf.Exp(-howManyStepsAhead * 0.35f);
        if (log) Debug.Log($"[{name}] {howManyStepsAhead} step(s) ahead (bonus: {distanceFactor:F3})");
        return manager.FutureTaskRelevanceWeight * distanceFactor;
    }

    // ─── Contributions ────────────────────────────────────────────────────────

    public void SetContribution(string key, float value)
    {
        _contributions[key] = value;
        calculateThisDOI($"Contribution:{key}");
    }

    public void ClearContribution(string key)
    {
        _contributions.Remove(key);
        calculateThisDOI($"ClearedContribution:{key}");
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

    // ─── Danger rise ──────────────────────────────────────────────────────────

    public void SetDangerLevel(float dangerLevel)       { _dangerLevelToRise       = dangerLevel; }
    public void SetDangerDuration(float duration)       { _dangerLevelRiseDuration = duration;    }
    public void StartDangerRise()                       { StartCoroutine(LerpDangerLevel());       }

    private IEnumerator LerpDangerLevel()
    {
        float time          = 0f;
        float initialDanger = HowDangerous;

        while (time < _dangerLevelRiseDuration)
        {
            HowDangerous = Mathf.Lerp(initialDanger, _dangerLevelToRise, time / _dangerLevelRiseDuration);
            time        += Time.deltaTime;
            yield return null;
        }

        HowDangerous = _dangerLevelToRise;
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private string GetBaseItemName(string itemName)
    {
        if (string.IsNullOrEmpty(itemName)) return itemName;
        return itemName.Split(' ')[0];
    }
}


public class ReadOnlyAttribute : PropertyAttribute { }