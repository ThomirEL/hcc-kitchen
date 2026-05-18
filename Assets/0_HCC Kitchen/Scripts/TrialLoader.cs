using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class TrialJson
{
    public int id;
    public List<string> sequence;
}

[Serializable]
public class TrialSequence
{
    public List<TrialJson> trials;
}

public static class TrialLoader
{
    private static TrialSequence _cache;

    private static void EnsureLoaded()
    {
        if (_cache != null) return;

        TextAsset json = Resources.Load<TextAsset>("trials");

        if (json == null)
        {
            Debug.LogError("TrialLoader: missing 'trials.json' in Resources");
            return;
        }

        _cache = JsonUtility.FromJson<TrialSequence>(json.text);
    }

    public static TrialJson GetTrial(int participantNumber)
    {
        EnsureLoaded();

        if (_cache == null || _cache.trials == null || _cache.trials.Count == 0)
            return null;

        int index = (participantNumber - 1) % _cache.trials.Count;
        if (index < 0) index = 0;

        return _cache.trials[index];
    }
}