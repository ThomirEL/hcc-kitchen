using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class IARManager : MonoBehaviour
{
    [Header("Scene References")]
    public List<IARPart> allParts = new();

    [Header("Source Weights")]
    [Range(0, 1)] public float CommonalityWeight          = 1f;
    [Range(0, 1)] public float CurrentTaskRelevanceWeight = 1f;
    [Range(0, 1)] public float FutureTaskRelevanceWeight  = 1f;
    [Range(0, 1)] public float DangerWeight               = 1f;
    [Range(0, 1)] public float PairingWeight              = 1f;
    [Range(0, 1)] public float ProximityWeight            = 1f;

    [Header("Weight Sliders")]
    public Slider CommonalitySlider;
    public Slider CurrentTaskRelevanceSlider;
    public Slider FutureTaskRelevanceSlider;
    public Slider DangerSlider;
    public Slider PairingSlider;

    [Header("DOI ranges")]
    public float COMMON_MIN = 0.0f;
    public float COMMON_MAX = 0.3f;
    public float DANGER_MIN = 0.1f;
    public float DANGER_MAX = 0.8f;
    public int   STEPS_IN_FUTURE = 5;

    [Header("What aspects should affect the DOI?")]
    public bool Commonality          = true;
    public bool CurrentTaskRelevance = true;
    public bool FutureTaskRelevance  = true;
    public bool Danger               = true;
    public bool Intrinsic            = true;
    [Tooltip("Intent for instance if you pick up a knife, a chopping board is more interesting")]
    public bool Intent    = true;
    public bool Proximity = true;

    [Header("Controller References (for Proximity DOI)")]
    public Transform LeftController;
    public Transform RightController;

    [Header("Proximity Settings")]
    [Tooltip("Radius around each controller to consider items 'nearby' (metres). Must match the falloff distance in IARPart.CalculateProximity().")]
    public float ProximityRadius = 2f;
    [Tooltip("Layer mask for IARPart colliders — assign the 'IARPart' layer here so the overlap sphere ignores unrelated geometry.")]
    public LayerMask ProximityLayerMask;

    [Header("DOI Toggles")]
    public Toggle CommonalityToggle;
    public Toggle CurrentTaskRelevanceToggle;
    public Toggle FutureTaskRelevanceToggle;
    public Toggle DangerToggle;
    public Toggle IntrinsicToggle;
    public Toggle IntentToggle;

    [Header("Shader Calculations")]
    public bool ShaderDesaturation    = true;
    public bool ShaderDarkening       = true;
    public bool ShaderSaturationBoost = true;
    public bool ShaderBrightnessBoost = true;
    public bool ShaderContrast        = true;
    public bool ShaderEmission        = true;
    public bool ShaderHighResBlend    = true;
    public bool ShaderAlpha           = true;

    [Header("Shader Toggles")]
    public Toggle ShaderDesaturationToggle;
    public Toggle ShaderDarkeningToggle;
    public Toggle ShaderSaturationBoostToggle;
    public Toggle ShaderBrightnessBoostToggle;
    public Toggle ShaderContrastToggle;
    public Toggle ShaderEmissionToggle;
    public Toggle ShaderHighResBlendToggle;
    public Toggle ShaderAlphaToggle;

    [Header("Falloff")]
    public float gaussianSigma = 3f;

    public static IARManager Instance { get; private set; }

    // ── Proximity system state ─────────────────────────────────────────────
    // Parts inside the proximity radius on the previous frame.
    private readonly HashSet<IARPart> _nearbyParts = new();
    // Scratch set filled each frame by GatherNearby — reused to avoid allocs.
    private readonly HashSet<IARPart> _frameNearby = new();
    // Pre-allocated overlap buffer — avoids a new Collider[] every frame.
    // Raise the capacity if you have very dense scenes (>64 items within 2 m).
    private readonly Collider[] _overlapBuffer = new Collider[64];

    // ── Inspector change-detection caches ─────────────────────────────────
    private float _prevCommonality;
    private float _prevCurrentTask;
    private float _prevFutureTask;
    private float _prevDanger;
    private float _prevPairing;

    private bool _prevCommonalityBool;
    private bool _prevCurrentTaskBool;
    private bool _prevFutureTaskBool;
    private bool _prevDangerBool;
    private bool _prevIntrinsicBool;
    private bool _prevIntentBool;

    private bool _prevShaderDesaturation;
    private bool _prevShaderDarkening;
    private bool _prevShaderSaturationBoost;
    private bool _prevShaderBrightnessBoost;
    private bool _prevShaderContrast;
    private bool _prevShaderEmission;
    private bool _prevShaderHighResBlend;
    private bool _prevShaderAlpha;

    private bool _updatingSliders = false;
    private bool _updatingToggles = false;

    // ──────────────────────────────────────────────────────────────────────
    // Lifecycle
    // ──────────────────────────────────────────────────────────────────────

    void Awake()
    {
        Instance = this;
        allParts = new List<IARPart>(FindObjectsOfType<IARPart>());
    }

    void Start()
    {
        // Sliders
        InitSlider(CommonalitySlider,          CommonalityWeight,          v => { CommonalityWeight          = v; RecalculateAllDOIs(); });
        InitSlider(CurrentTaskRelevanceSlider, CurrentTaskRelevanceWeight, v => { CurrentTaskRelevanceWeight = v; RecalculateAllDOIs(); });
        InitSlider(FutureTaskRelevanceSlider,  FutureTaskRelevanceWeight,  v => { FutureTaskRelevanceWeight  = v; RecalculateAllDOIs(); });
        InitSlider(DangerSlider,               DangerWeight,               v => { DangerWeight               = v; RecalculateAllDOIs(); });
        InitSlider(PairingSlider,              PairingWeight,              v => { PairingWeight              = v; RecalculateAllDOIs(); });

        // DOI toggles
        InitToggle(CommonalityToggle,          Commonality,          v => { Commonality          = v; RecalculateAllDOIs(); });
        InitToggle(CurrentTaskRelevanceToggle, CurrentTaskRelevance, v => { CurrentTaskRelevance = v; RecalculateAllDOIs(); });
        InitToggle(FutureTaskRelevanceToggle,  FutureTaskRelevance,  v => { FutureTaskRelevance  = v; RecalculateAllDOIs(); });
        InitToggle(DangerToggle,               Danger,               v => { Danger               = v; RecalculateAllDOIs(); });
        InitToggle(IntrinsicToggle,            Intrinsic,            v => { Intrinsic            = v; RecalculateAllDOIs(); });
        InitToggle(IntentToggle,               Intent,               v => { Intent               = v; RecalculateAllDOIs(); });

        // Shader toggles
        InitToggle(ShaderDesaturationToggle,    ShaderDesaturation,    v => { ShaderDesaturation    = v; UpdateShaderSettings(); });
        InitToggle(ShaderDarkeningToggle,       ShaderDarkening,       v => { ShaderDarkening       = v; UpdateShaderSettings(); });
        InitToggle(ShaderSaturationBoostToggle, ShaderSaturationBoost, v => { ShaderSaturationBoost = v; UpdateShaderSettings(); });
        InitToggle(ShaderBrightnessBoostToggle, ShaderBrightnessBoost, v => { ShaderBrightnessBoost = v; UpdateShaderSettings(); });
        InitToggle(ShaderContrastToggle,        ShaderContrast,        v => { ShaderContrast        = v; UpdateShaderSettings(); });
        InitToggle(ShaderEmissionToggle,        ShaderEmission,        v => { ShaderEmission        = v; UpdateShaderSettings(); });
        InitToggle(ShaderHighResBlendToggle,    ShaderHighResBlend,    v => { ShaderHighResBlend    = v; UpdateShaderSettings(); });
        InitToggle(ShaderAlphaToggle,           ShaderAlpha,           v => { ShaderAlpha           = v; UpdateShaderSettings(); });

        CacheAllValues();
        UpdateShaderSettings();
        SyncSlidersToWeights();
        SyncTogglesToBools();
    }

    void Update()
    {
        // Only run the proximity driver when Proximity DOI is enabled and
        // we have controller references to work with.
        if (Proximity && LeftController != null && RightController != null)
            DriveProximityDOI();
    }

    // ──────────────────────────────────────────────────────────────────────
    // Proximity driver
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Called every frame when proximity is enabled.
    /// Uses Physics.OverlapSphereNonAlloc so Unity's BVH finds nearby items —
    /// no manual O(n) distance loop over every item in the scene.
    ///
    /// Items outside the radius are never touched this frame.
    /// Items that just left the radius get one final RecalculateDOI() so
    /// their proximity contribution correctly drops to 0.
    /// </summary>
    private void DriveProximityDOI()
    {
        _frameNearby.Clear();

        // Gather items near each controller using Unity's physics BVH.
        GatherNearby(LeftController.position,  _frameNearby);
        GatherNearby(RightController.position, _frameNearby);

        // Items that JUST LEFT the radius this frame:
        // clear their flag and do one final recalc so proximity drops to 0.
        foreach (IARPart part in _nearbyParts)
        {
            if (!_frameNearby.Contains(part))
            {
                part.IsInProximityRange = false;
                part.RecalculateDOI("Left proximity range");
            }
        }

        // Items currently IN range: flag them and recalc with live proximity.
        foreach (IARPart part in _frameNearby)
        {
            part.IsInProximityRange = true;
            part.RecalculateDOI("Entered proximity range");
        }

        // Swap — reuse both sets next frame, no heap allocation.
        _nearbyParts.Clear();
        foreach (IARPart part in _frameNearby)
            _nearbyParts.Add(part);

#if UNITY_EDITOR
        // Warn loudly in editor if the buffer was too small (silent truncation otherwise).
        if (_frameNearby.Count >= _overlapBuffer.Length)
            Debug.LogWarning($"IARManager: overlap buffer is full ({_overlapBuffer.Length} slots). " +
                             "Increase the _overlapBuffer array size to avoid missed items.");
#endif
    }

    /// <summary>
    /// Fills <paramref name="result"/> with every IARPart whose collider
    /// overlaps a sphere of <see cref="ProximityRadius"/> around <paramref name="center"/>.
    /// Uses NonAlloc to write into a pre-allocated buffer — zero GC pressure.
    /// The HashSet deduplicates hits from left + right controller passes automatically.
    /// </summary>
    private void GatherNearby(Vector3 center, HashSet<IARPart> result)
    {
        int hitCount = Physics.OverlapSphereNonAlloc(
            center,
            ProximityRadius,
            _overlapBuffer,
            ProximityLayerMask,
            QueryTriggerInteraction.Collide   // include triggers if your items use trigger colliders
        );

        for (int i = 0; i < hitCount; i++)
        {
            IARPart part = _overlapBuffer[i].GetComponentInParent<IARPart>();
            if (part != null) {
                result.Add(part);
                //Debug.Log($"Proximity hit: {part.name} at distance {Vector3.Distance(center, part.transform.position):F2}m");
                }
        }
    }

    // ──────────────────────────────────────────────────────────────────────
    // Registration
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by IARPart.OnEnable(). Keeps allParts in sync with runtime
    /// spawned/enabled objects beyond the initial FindObjectsOfType in Awake.
    /// </summary>
    public void RegisterPart(IARPart part)
    {
        if (!allParts.Contains(part)) allParts.Add(part);
    }

    /// <summary>
    /// Called by IARPart.OnDisable(). Removes destroyed or disabled parts
    /// so we never call RecalculateDOI() on a null/inactive object.
    /// Also clears it from the proximity sets immediately.
    /// </summary>
    public void UnregisterPart(IARPart part)
    {
        allParts.Remove(part);
        _nearbyParts.Remove(part);
        _frameNearby.Remove(part);
    }

    // ──────────────────────────────────────────────────────────────────────
    // DOI & Shader helpers
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Forces a full DOI refresh on every registered item regardless of proximity.
    /// Called by sliders, toggles, and OnValidate.
    /// </summary>
    public void RecalculateAllDOIs()
    {
        if (allParts == null || allParts.Count == 0)
            allParts = new List<IARPart>(FindObjectsOfType<IARPart>());

        foreach (var part in allParts)
            if (part != null) part.RecalculateDOI();
    }

    public void UpdateShaderSettings()
    {
        Shader.SetGlobalFloat("_EnableDesaturation",    ShaderDesaturation    ? 1f : 0f);
        Shader.SetGlobalFloat("_EnableDarkening",       ShaderDarkening       ? 1f : 0f);
        Shader.SetGlobalFloat("_EnableSaturationBoost", ShaderSaturationBoost ? 1f : 0f);
        Shader.SetGlobalFloat("_EnableBrightnessBoost", ShaderBrightnessBoost ? 1f : 0f);
        Shader.SetGlobalFloat("_EnableContrast",        ShaderContrast        ? 1f : 0f);
        Shader.SetGlobalFloat("_EnableEmission",        ShaderEmission        ? 1f : 0f);
        Shader.SetGlobalFloat("_EnableHighResBlend",    ShaderHighResBlend    ? 1f : 0f);
        Shader.SetGlobalFloat("_EnableAlpha",           ShaderAlpha           ? 1f : 0f);
    }

    // ──────────────────────────────────────────────────────────────────────
    // Inspector change detection (OnValidate)
    // ──────────────────────────────────────────────────────────────────────

    void OnValidate()
    {
        bool weightsChanged = CommonalityWeight          != _prevCommonality ||
                              CurrentTaskRelevanceWeight != _prevCurrentTask  ||
                              FutureTaskRelevanceWeight  != _prevFutureTask   ||
                              DangerWeight               != _prevDanger       ||
                              PairingWeight              != _prevPairing;

        bool doiBoolsChanged = Commonality          != _prevCommonalityBool ||
                               CurrentTaskRelevance != _prevCurrentTaskBool  ||
                               FutureTaskRelevance  != _prevFutureTaskBool   ||
                               Danger               != _prevDangerBool       ||
                               Intrinsic            != _prevIntrinsicBool    ||
                               Intent               != _prevIntentBool;

        bool shaderBoolsChanged = ShaderDesaturation    != _prevShaderDesaturation    ||
                                  ShaderDarkening       != _prevShaderDarkening       ||
                                  ShaderSaturationBoost != _prevShaderSaturationBoost ||
                                  ShaderBrightnessBoost != _prevShaderBrightnessBoost ||
                                  ShaderContrast        != _prevShaderContrast        ||
                                  ShaderEmission        != _prevShaderEmission        ||
                                  ShaderHighResBlend    != _prevShaderHighResBlend    ||
                                  ShaderAlpha           != _prevShaderAlpha;

        if (weightsChanged || doiBoolsChanged) RecalculateAllDOIs();
        if (shaderBoolsChanged)                UpdateShaderSettings();
        if (weightsChanged)                    SyncSlidersToWeights();
        if (doiBoolsChanged || shaderBoolsChanged) SyncTogglesToBools();

        CacheAllValues();
    }

    // ──────────────────────────────────────────────────────────────────────
    // UI helpers
    // ──────────────────────────────────────────────────────────────────────

    private void InitSlider(Slider slider, float initialValue, Action<float> onChanged)
    {
        if (slider == null) return;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value    = initialValue;
        slider.onValueChanged.AddListener(v => { if (!_updatingSliders) onChanged(v); });
    }

    private void InitToggle(Toggle toggle, bool initialValue, Action<bool> onChanged)
    {
        if (toggle == null) return;
        toggle.isOn = initialValue;
        toggle.onValueChanged.AddListener(v => { if (!_updatingToggles) onChanged(v); });
    }

    private void SyncSlidersToWeights()
    {
        _updatingSliders = true;
        SetSlider(CommonalitySlider,          CommonalityWeight);
        SetSlider(CurrentTaskRelevanceSlider, CurrentTaskRelevanceWeight);
        SetSlider(FutureTaskRelevanceSlider,  FutureTaskRelevanceWeight);
        SetSlider(DangerSlider,               DangerWeight);
        SetSlider(PairingSlider,              PairingWeight);
        _updatingSliders = false;
    }

    private void SyncTogglesToBools()
    {
        _updatingToggles = true;

        SetToggle(CommonalityToggle,          Commonality);
        SetToggle(CurrentTaskRelevanceToggle, CurrentTaskRelevance);
        SetToggle(FutureTaskRelevanceToggle,  FutureTaskRelevance);
        SetToggle(DangerToggle,               Danger);
        SetToggle(IntrinsicToggle,            Intrinsic);
        SetToggle(IntentToggle,               Intent);

        SetToggle(ShaderDesaturationToggle,    ShaderDesaturation);
        SetToggle(ShaderDarkeningToggle,       ShaderDarkening);
        SetToggle(ShaderSaturationBoostToggle, ShaderSaturationBoost);
        SetToggle(ShaderBrightnessBoostToggle, ShaderBrightnessBoost);
        SetToggle(ShaderContrastToggle,        ShaderContrast);
        SetToggle(ShaderEmissionToggle,        ShaderEmission);
        SetToggle(ShaderHighResBlendToggle,    ShaderHighResBlend);
        SetToggle(ShaderAlphaToggle,           ShaderAlpha);

        _updatingToggles = false;
    }

    private void SetSlider(Slider slider, float value) { if (slider != null) slider.value = value; }
    private void SetToggle(Toggle toggle, bool  value) { if (toggle != null) toggle.isOn  = value; }

    private void CacheAllValues()
    {
        _prevCommonality = CommonalityWeight;
        _prevCurrentTask = CurrentTaskRelevanceWeight;
        _prevFutureTask  = FutureTaskRelevanceWeight;
        _prevDanger      = DangerWeight;
        _prevPairing     = PairingWeight;

        _prevCommonalityBool = Commonality;
        _prevCurrentTaskBool = CurrentTaskRelevance;
        _prevFutureTaskBool  = FutureTaskRelevance;
        _prevDangerBool      = Danger;
        _prevIntrinsicBool   = Intrinsic;
        _prevIntentBool      = Intent;

        _prevShaderDesaturation    = ShaderDesaturation;
        _prevShaderDarkening       = ShaderDarkening;
        _prevShaderSaturationBoost = ShaderSaturationBoost;
        _prevShaderBrightnessBoost = ShaderBrightnessBoost;
        _prevShaderContrast        = ShaderContrast;
        _prevShaderEmission        = ShaderEmission;
        _prevShaderHighResBlend    = ShaderHighResBlend;
        _prevShaderAlpha           = ShaderAlpha;
    }

    // ──────────────────────────────────────────────────────────────────────
    // Editor debug
    // ──────────────────────────────────────────────────────────────────────

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (LeftController != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(LeftController.position,  ProximityRadius);
        }
        if (RightController != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(RightController.position, ProximityRadius);
        }
    }
#endif
}