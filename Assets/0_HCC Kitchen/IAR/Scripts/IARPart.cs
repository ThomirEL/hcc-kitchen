using System.Collections.Generic;
using UnityEngine;
using System.Collections;
using System;

public class IARPart : MonoBehaviour
{
    [Header("Base Properties")]
    [Range(0f,1f)] public float intrinsicInterest = 0.5f;


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


    public bool log = false;  // enable to print debug info for this part

    // Named contributions — any system can write a value under its own key.
    // IARManager sums these to produce intentInterest.
    // e.g. "recipe.step3" = 1f, "safety.hot" = 1f,
    private Dictionary<string, float> _contributions = new();

    [HideInInspector] public float currentDoI = 0.5f;
    [HideInInspector] public Renderer   cachedRenderer;
    [HideInInspector] public Collider   cachedCollider;


// ---------------------------- If a part needs to contribute to its own DOI, for instance if its heating up (setup to work with UnityEvents) ---------------
    [SerializeField] private string selfContributionKey;
    [SerializeField] private float selfContrubutionDuration;

    void Awake()
    {
        cachedRenderer = GetComponent<Renderer>();
        cachedCollider = GetComponent<Collider>();
        calculateThisDOI("Awake");
    }

    /// Any system sets its own interest contribution independently.
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
            float newDanger = Mathf.Lerp(initialDanger, _dangerLevelToRise, t);
            HowDangerous = newDanger;

            time += Time.deltaTime;
            yield return null;
        }

        HowDangerous = _dangerLevelToRise;
    }

    void Update()
    {
        //IARManager.calculateDOI(this);
    }

    private void calculateThisDOI(string changedAspect = null)
    {
        Debug.Log($"Calculating DOI for {name} (changed aspect: {changedAspect})");
        Debug.Log($"  Commonality: {HowCommon}, Danger: {HowDangerous}, Intrinsic: {intrinsicInterest}, InCurrentStep: {IsInCurrentStep}, StepsInToFuture: {StepsInToFuture}");
        currentDoI = IARManager.calculateDOI(this);
        Debug.Log($"  → New DOI: {currentDoI}");
    }
    
    public void ClearContribution(string key)
    {
        _contributions.Remove(key);
    }

    /// IARManager calls this — sums all contributions, clamped to 0-1.
    public float GetCombinedIntentInterest()
    {
        float total = 0.0f;
        foreach (var v in _contributions.Values)
            total += v;
        return Mathf.Clamp01(total);
    }

    public float GetContribution(string key)
    {
        return _contributions.TryGetValue(key, out float v) ? v : 0f;
    }
}