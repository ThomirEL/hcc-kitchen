using System;
using System.Collections.Generic;
using UnityEditor.ShaderGraph.Internal;
using UnityEngine;
using UnityEngine.Rendering;

public class IARManager : MonoBehaviour
{
    [Header("Scene References")]
    public List<IARPart> allParts = new();

    [Header("Source Weights")]
    [Range(0, 2)] static public float gamma1_part = 1f;
    [Range(0, 2)] static public float gamma2_user = 1f;

    [Header("Part Weights")]
    [Range(0, 2)] static public float alpha1_intrinsic = 1f;
    [Range(0, 2)] static public float beta1_relation   = 0.8f;

    [Header("User Weights")]
    [Range(0, 2)] static public float alpha2_intent = 1f;
    [Range(0, 2)] static public float beta2_action  = 0.8f;

    [Header("DOI ranges")]
    static public float COMMON_MIN = 0.0f;
    static public float COMMON_MAX = 0.3f;
    static public float DANGER_MIN = 0.1f;
    static public float DANGER_MAX = 0.8f;

    static public int STEPS_IN_FUTURE = 5;

    [Header("What aspects should affect the DOI?")]
    static public bool Commonality = true;
    static public bool CurrentTaskRelevance = true;
    static public bool FutureTaskRelevance = true;
    static public bool Danger = true;
    static public bool Intrinsic = true;

    [Tooltip("Intent for instance if you pick up a knife, a chopping board is more interesting")]
    static public bool Intent = true;


    [Header("Falloff")]
    public float gaussianSigma = 3f;

    public static IARManager Instance { get; private set; }

    void Awake()
    {
        Instance = this;
        allParts = new List<IARPart>(FindObjectsOfType<IARPart>());
    }


    static public float calculateDOI(IARPart part)
    {
        float DOI = 0f;
        if (Commonality)
            DOI += CalculateCommonality(part);
        if (Danger)
            DOI += CalculateDanger(part);
        if (Intrinsic)
            DOI += CalcIntrinsic(part);
        if (Intent)
            DOI += CalcIntent(part);
        if (CurrentTaskRelevance)
            DOI = DOI;
        if (FutureTaskRelevance)
            DOI = DOI;
        return Mathf.Clamp01(DOI);
    }

    static private float CalculateCommonality(IARPart part)
    {
        float C = part.HowCommon;
        return Mathf.Lerp(COMMON_MIN, COMMON_MAX, C);
    }

    static private float CalculateDanger(IARPart part)
    {
        float D = part.HowDangerous;
        return Mathf.Lerp(DANGER_MIN, DANGER_MAX, D);
    }

    static private float CalcIntrinsic(IARPart part)
    {
        float I = part.intrinsicInterest;
        return alpha1_intrinsic * I;
    }

    static private float CalcIntent(IARPart part)
    {
        float T = part.GetCombinedIntentInterest();
        return alpha2_intent * T;
    }

    // Call this from IARItem so allParts stays up to date automatically
    public void RegisterPart(IARPart part)
    {
        if (!allParts.Contains(part))
            allParts.Add(part);
    }
}