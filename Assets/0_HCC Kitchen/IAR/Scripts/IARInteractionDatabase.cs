using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class IARTrigger
{
    public string item;
    public string state;
}

[System.Serializable]
public class IAREffect
{
    public string item;
    public string key;
    public float  value;
}

[System.Serializable]
public class IARInteraction
{
    public IARTrigger    trigger;
    public List<IAREffect> effects;
}

[System.Serializable]
public class IARInteractionList
{
    public List<IARInteraction> interactions;
}

public class IARInteractionDatabase : MonoBehaviour
{
    public TextAsset jsonFile;  // drag kitchen_interactions.json in Inspector

    // trigger key "Knife|held" → list of effects to apply
    private Dictionary<string, List<IAREffect>> _lookup = new();

    // All registered parts by name — IARItem registers itself on Awake
    private Dictionary<string, IARPart> _parts = new();

    public static IARInteractionDatabase Instance { get; private set; }

    void Awake()
    {
        Instance = this;
        Load();
    }

    void Load()
    {
        var data = JsonUtility.FromJson<IARInteractionList>(jsonFile.text);
        foreach (var interaction in data.interactions)
        {
            string key = TriggerKey(interaction.trigger.item, interaction.trigger.state);
            _lookup[key] = interaction.effects;
        }
    }

    // Called by IARItem on Awake so the database knows about every part
    public void RegisterPart(string itemName, IARPart part)
    {
        _parts[itemName] = part;
    }

    // Called by IARItem when its state changes
    public void OnStateChanged(string itemName, string state, bool active)
    {
        string key = TriggerKey(itemName, state);
        if (!_lookup.TryGetValue(key, out var effects)) return;
        IARPart self = GetPart(itemName);

        // If this item is being picked up (held state), clear all other items' contributions first
        if (state == "held" && active)
        {
            foreach (var part in _parts.Values)
            {
                if (part != self)
                {
                    // Clear all contributions from all other items
                    part.ClearAllContributions();
                }
            }
        }

        foreach (var effect in effects)
        {
            if (!_parts.TryGetValue(effect.item, out var targetPart)) continue;

            if (active) {
                targetPart.SetContribution(effect.key, effect.value);
                self.SetContribution("self", 1f); // Apply effect to self as well if active
            } else {
                targetPart.ClearContribution(effect.key);
            }
        }
    }

    /// Get a part by its registered name
    public IARPart GetPart(string itemName)
    {
        return _parts.TryGetValue(itemName, out var part) ? part : null;
    }

    string TriggerKey(string item, string state) => $"{item}|{state}";
}