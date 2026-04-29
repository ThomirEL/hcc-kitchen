using System.Collections.Generic;
using System.Linq.Expressions;
using UnityEngine;
using System.Linq;

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
    

    // trigger key "Knife|held" → list of effects to apply
    private Dictionary<string, List<IAREffect>> _lookup = new();

    // All registered parts by name — IARItem registers itself on Awake
    private Dictionary<string, List<IARPart>> _parts = new();

    public static IARInteractionDatabase Instance { get; private set; }

    void Awake()
    {
        Instance = this;
        Load();
    }

    void Load()
    {
        TextAsset json = Resources.Load<TextAsset>("kitchen_interactions");

        if (json == null)
        {
            Debug.LogError("ItemPropertiesLoader: could not find 'kitchen_interactions.json' in any Resources folder.");
            return;
        }

        var data = JsonUtility.FromJson<IARInteractionList>(json.text);
        foreach (var interaction in data.interactions)
        {
            string key = TriggerKey(interaction.trigger.item, interaction.trigger.state);
            _lookup[key] = interaction.effects;
        }
    }

    // Called by IARItem on Awake so the database knows about every part
    // RegisterPart: append instead of overwrite
public void RegisterPart(string itemName, IARPart part)
{
    if (!_parts.TryGetValue(itemName, out var list))
    {
        list = new List<IARPart>();
        _parts[itemName] = list;
    }
    list.Add(part);
}

// Helper to get all parts by name (replaces GetPart)
public List<IARPart> GetParts(string itemName)
{
    return _parts.TryGetValue(itemName, out var list) ? list : new List<IARPart>();
}

// Keep GetPart for single lookups (returns first match)
public IARPart GetPart(string itemName)
{
    return _parts.TryGetValue(itemName, out var list) && list.Count > 0 ? list[0] : null;
}

    // Called by IARItem when its state changes
    public void OnStateChanged(string itemName, string state, bool active, GameObject itemObject = null)
{
    string key = TriggerKey(itemName, state);
    if (!_lookup.TryGetValue(key, out var effects)) return;

    itemObject.GetComponent<IARPart>().overrideDOI = active;
    itemObject.GetComponent<IARPart>().RecalculateDOI();

    // If held, clear contributions from all OTHER parts
    if (state == "held" && active)
    {
        List<IARPart> selfParts = GetParts(itemName);
        foreach (var kvp in _parts)
        {
            foreach (var part in kvp.Value)
            {
                if (!selfParts.Contains(part))
                    part.ClearAllContributions();
            }
        }
    }

    foreach (var effect in effects)
    {
        List<IARPart> targetParts = GetParts(effect.item); // All parts sharing this name

        if (active)
        {
            foreach (var part in targetParts)
                part.SetContribution(effect.key, effect.value);
        }
        else
        {
            foreach (var part in targetParts)
            {
                part.ClearContribution(effect.key);
                part.RecalculateDOI();
            }
        }
    }
}



    string TriggerKey(string item, string state) => $"{item}|{state}";
}