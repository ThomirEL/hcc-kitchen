using System.Collections.Generic;
using UnityEngine;

public class IARManager : MonoBehaviour
{
    [Header("Scene References")]
    public List<IARPart> allParts = new();
    public Transform     userGaze;   // assign your Main Camera transform
    public Transform     userHand;   // assign your dominant hand transform

    [Header("Source Weights")]
    [Range(0, 2)] public float gamma1_part = 1f;
    [Range(0, 2)] public float gamma2_user = 1f;

    [Header("Part Weights")]
    [Range(0, 2)] public float alpha1_intrinsic = 1f;
    [Range(0, 2)] public float beta1_relation   = 0.8f;

    [Header("User Weights")]
    [Range(0, 2)] public float alpha2_intent = 1f;
    [Range(0, 2)] public float beta2_action  = 0.8f;

    [Header("Falloff")]
    public float gaussianSigma = 3f;

    public static IARManager Instance { get; private set; }

    void Awake()
    {
        Instance = this;
    }

    void Update()
    {
        Vector3 gazePos = userGaze ? userGaze.position : Vector3.zero;
        Vector3 handPos = userHand ? userHand.position : Vector3.zero;

        foreach (var part in allParts)
        {
            if (part == null) continue;

            float PI = CalcPI(part, gazePos);
            float UI = CalcUI(part, handPos);
            
            Debug.Log("Item: " + part.gameObject.name + ", PI:" + PI + ", UI" + UI);

            float raw = gamma1_part * PI + gamma2_user * UI;

            if (part.isStructural) raw = Mathf.Max(raw, 0.15f);

            part.currentDoI = Mathf.Clamp01(raw);
        }
    }

    float CalcPI(IARPart part, Vector3 gazePos)
    {
        float I = part.intrinsicInterest;
        return alpha1_intrinsic * I;
    }

    float CalcUI(IARPart part, Vector3 handPos)
    {
        float T = part.GetCombinedIntentInterest();
        return alpha2_intent * T;
    }

    float Gaussian(Vector3 partPos, Vector3 focusPos)
    {
        float dist = Vector3.Distance(partPos, focusPos);
        return Mathf.Exp(-(dist * dist) / (2f * gaussianSigma * gaussianSigma));
    }

    // Call this from IARItem so allParts stays up to date automatically
    public void RegisterPart(IARPart part)
    {
        if (!allParts.Contains(part))
            allParts.Add(part);
    }
}