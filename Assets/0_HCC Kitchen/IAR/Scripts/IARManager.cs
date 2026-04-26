using System;
using System.Collections.Generic;
using UnityEngine;

public class IARManager : MonoBehaviour
{
    [Header("Scene References")]
    public List<IARPart> allParts = new();

    [Header("Source Weights")]
    [Range(0, 1)] public float CommonalityWeight = 1f;
    [Range(0, 1)] public float CurrentTaskRelevanceWeight = 1f;
    [Range(0, 1)] public float FutureTaskRelevanceWeight = 1f;
    [Range(0, 1)] public float DangerWeight   = 1f;
    [Range(0, 1)] public float PairingWeight = 1f;


    [Header("DOI ranges")]
    public float COMMON_MIN = 0.0f;
    public float COMMON_MAX = 0.3f;
    public float DANGER_MIN = 0.1f;
    public float DANGER_MAX = 0.8f;

    public int STEPS_IN_FUTURE = 5;

    [Header("What aspects should affect the DOI?")]
    public bool Commonality = true;
    public bool CurrentTaskRelevance = true;
    public bool FutureTaskRelevance = true;
    public bool Danger = true;
    public bool Intrinsic = true;

    [Tooltip("Intent for instance if you pick up a knife, a chopping board is more interesting")]
    public bool Intent = true;

    [Header("Shader Calculations - Which effects should the DOI shader apply?")]
    public bool ShaderDesaturation = true;
    public bool ShaderDarkening = true;
    public bool ShaderSaturationBoost = true;
    public bool ShaderBrightnessBoost = true;
    public bool ShaderContrast = true;
    public bool ShaderEmission = true;
    public bool ShaderHighResBlend = true;
    public bool ShaderAlpha = true;

    [Header("Falloff")]
    public float gaussianSigma = 3f;

    public static IARManager Instance { get; private set; }

    void Awake()
    {
        Instance = this;
        allParts = new List<IARPart>(FindObjectsOfType<IARPart>());
    }

    void Start()
    {
        // Called after all Awake()s — materials are guaranteed loaded now
        UpdateShaderSettings();
    }

    void OnValidate()
    {
        // Update shader settings whenever values change in the editor
        UpdateShaderSettings();
        Debug.Log("IARManager: OnValidate called, updating shader settings.");
        // Recalculate DOI for all parts when any DOI settings change
        RecalculateAllDOIs();
    }

    /// <summary>
    /// Recalculates DOI for all registered parts.
    /// Call this after changing any DOI weights, ranges, or toggles.
    /// </summary>
    public void RecalculateAllDOIs()
    {
        if (allParts == null || allParts.Count == 0)
        {
            // Try to find all parts if not already cached
            allParts = new List<IARPart>(FindObjectsOfType<IARPart>());
        }

        foreach (var part in allParts)
        {
            if (part != null)
                part.RecalculateDOI();
        }
    }


    /// <summary>
    /// Updates all shader global variables with current toggle values.
    /// Call this at runtime after changing any ShaderXxx settings.
    /// </summary>
    public void UpdateShaderSettings()
    {
        Shader.SetGlobalFloat("_EnableDesaturation", ShaderDesaturation ? 1f : 0f);
        Shader.SetGlobalFloat("_EnableDarkening", ShaderDarkening ? 1f : 0f);
        Shader.SetGlobalFloat("_EnableSaturationBoost", ShaderSaturationBoost ? 1f : 0f);
        Shader.SetGlobalFloat("_EnableBrightnessBoost", ShaderBrightnessBoost ? 1f : 0f);
        Shader.SetGlobalFloat("_EnableContrast", ShaderContrast ? 1f : 0f);
        Shader.SetGlobalFloat("_EnableEmission", ShaderEmission ? 1f : 0f);
        Shader.SetGlobalFloat("_EnableHighResBlend", ShaderHighResBlend ? 1f : 0f);
        Shader.SetGlobalFloat("_EnableAlpha", ShaderAlpha ? 1f : 0f);
    }

    

    // Call this from IARItem so allParts stays up to date automatically
    public void RegisterPart(IARPart part)
    {
        if (!allParts.Contains(part))
            allParts.Add(part);
    }
}