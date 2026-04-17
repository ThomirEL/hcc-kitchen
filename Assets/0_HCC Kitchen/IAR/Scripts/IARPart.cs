using System.Collections.Generic;
using UnityEngine;

public class IARPart : MonoBehaviour
{
    [Header("Base Properties")]
    [Range(0f,1f)] public float intrinsicInterest = 0.5f;
    public bool isStructural = false;
    public bool isDangerous  = false;

    // Named contributions — any system can write a value under its own key.
    // IARManager sums these to produce intentInterest.
    // e.g. "recipe.step3" = 1f, "safety.hot" = 1f, "entity.robot" = 0.6f
    private Dictionary<string, float> _contributions = new();

    [HideInInspector] public float currentDoI = 1f;
    [HideInInspector] public Renderer   cachedRenderer;
    [HideInInspector] public Collider   cachedCollider;

    void Awake()
    {
        cachedRenderer = GetComponent<Renderer>();
        cachedCollider = GetComponent<Collider>();
        if (isDangerous) intrinsicInterest = 1f;
    }

    /// Any system sets its own interest contribution independently.
    public void SetContribution(string key, float value)
    {
        _contributions[key] = value;
    }

    public void ClearContribution(string key)
    {
        _contributions.Remove(key);
    }

    /// IARManager calls this — sums all contributions, clamped to 0-1.
    public float GetCombinedIntentInterest()
    {
        float total = 0.5f;
        if (gameObject.name == "Juice")
            Debug.Log($"Juice Contributions: {string.Join(", ", _contributions)}");
        foreach (var v in _contributions.Values)
            total += v;
        return Mathf.Clamp01(total);
    }

    public float GetContribution(string key)
    {
        return _contributions.TryGetValue(key, out float v) ? v : 0f;
    }
}