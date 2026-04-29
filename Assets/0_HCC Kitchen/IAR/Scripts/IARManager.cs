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
    public bool Intent = true;

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

    // Cached inspector values for weights
    private float _prevCommonality;
    private float _prevCurrentTask;
    private float _prevFutureTask;
    private float _prevDanger;
    private float _prevPairing;

    // Cached inspector values for DOI bools
    private bool _prevCommonalityBool;
    private bool _prevCurrentTaskBool;
    private bool _prevFutureTaskBool;
    private bool _prevDangerBool;
    private bool _prevIntrinsicBool;
    private bool _prevIntentBool;

    // Cached inspector values for shader bools
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

    // Explicitly push manager values → UI on start
    SyncSlidersToWeights();
    SyncTogglesToBools();   // ← this is what was missing
}

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

        // DOI toggles
        SetToggle(CommonalityToggle,          Commonality);
        SetToggle(CurrentTaskRelevanceToggle, CurrentTaskRelevance);
        SetToggle(FutureTaskRelevanceToggle,  FutureTaskRelevance);
        SetToggle(DangerToggle,               Danger);
        SetToggle(IntrinsicToggle,            Intrinsic);
        SetToggle(IntentToggle,               Intent);

        // Shader toggles
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

    private void SetSlider(Slider slider, float value) { if (slider != null) slider.value    = value; }
    private void SetToggle(Toggle toggle, bool  value) { if (toggle != null) toggle.isOn     = value; }

    private void CacheAllValues()
    {
        _prevCommonality  = CommonalityWeight;
        _prevCurrentTask  = CurrentTaskRelevanceWeight;
        _prevFutureTask   = FutureTaskRelevanceWeight;
        _prevDanger       = DangerWeight;
        _prevPairing      = PairingWeight;

        _prevCommonalityBool  = Commonality;
        _prevCurrentTaskBool  = CurrentTaskRelevance;
        _prevFutureTaskBool   = FutureTaskRelevance;
        _prevDangerBool       = Danger;
        _prevIntrinsicBool    = Intrinsic;
        _prevIntentBool       = Intent;

        _prevShaderDesaturation    = ShaderDesaturation;
        _prevShaderDarkening       = ShaderDarkening;
        _prevShaderSaturationBoost = ShaderSaturationBoost;
        _prevShaderBrightnessBoost = ShaderBrightnessBoost;
        _prevShaderContrast        = ShaderContrast;
        _prevShaderEmission        = ShaderEmission;
        _prevShaderHighResBlend    = ShaderHighResBlend;
        _prevShaderAlpha           = ShaderAlpha;
    }

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

    public void RegisterPart(IARPart part)
    {
        if (!allParts.Contains(part)) allParts.Add(part);
    }
}