using System.Collections.Generic;
using UnityEngine;

public class StepValidator : MonoBehaviour
{
    [Header("Validation Settings")]
    [Range(0f, 5f)] public float requiredProximityDistance = 1f;
    [Range(0f, 10f)] public float requiredInteractionDuration = 2f;

    private Dictionary<string, float> _objectInteractionTime = new();
    private Dictionary<string, Vector3> _lastObjectPosition = new();

    public static StepValidator Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Update()
    {
        TrackObjectInteractions();
    }

    /// <summary>
    /// Checks if the current step has been completed using a hybrid approach
    /// </summary>
    public bool IsStepCompleted()
    {
        var step = TaskManager.Instance.CurrentStep;
        var requiredObjects = TaskManager.Instance.GetCurrentStepObjects();

        if (requiredObjects.Count == 0) return true;

        // Strategy 1: Check if all required objects are present and used
        bool allObjectsPresent = CheckObjectsPresent(requiredObjects);
        if (!allObjectsPresent) return false;

        // Strategy 2: Check proximity between objects
        bool objectsInProximity = CheckObjectsProximity(requiredObjects);
        if (!objectsInProximity) return false;

        // Strategy 3: Check interaction time with objects
        bool sufficientInteraction = CheckInteractionDuration(requiredObjects);
        return sufficientInteraction;
    }

    /// <summary>
    /// Strategy 1: Check if required objects are in the scene and grabbable
    /// </summary>
    private bool CheckObjectsPresent(List<string> requiredObjects)
    {
        foreach (var objName in requiredObjects)
        {
            var part = IARInteractionDatabase.Instance.GetPart(objName);
            if (part == null) return false;
        }
        return true;
    }

    /// <summary>
    /// Strategy 2: Check if objects are within proximity of each other
    /// </summary>
    private bool CheckObjectsProximity(List<string> requiredObjects)
    {
        if (requiredObjects.Count < 2) return true; // Single object steps always pass proximity

        var positions = new List<Vector3>();
        foreach (var objName in requiredObjects)
        {
            var part = IARInteractionDatabase.Instance.GetPart(objName);
            if (part != null)
                positions.Add(part.transform.position);
        }

        // Check if all objects are within proximity distance of each other
        for (int i = 0; i < positions.Count; i++)
        {
            for (int j = i + 1; j < positions.Count; j++)
            {
                float dist = Vector3.Distance(positions[i], positions[j]);
                if (dist > requiredProximityDistance)
                    return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Strategy 3: Track how long objects have been used together
    /// </summary>
    private bool CheckInteractionDuration(List<string> requiredObjects)
    {
        float minInteractionTime = float.MaxValue;

        foreach (var objName in requiredObjects)
        {
            if (_objectInteractionTime.TryGetValue(objName, out float time))
            {
                minInteractionTime = Mathf.Min(minInteractionTime, time);
            }
            else
            {
                return false; // Object hasn't been tracked yet
            }
        }

        return minInteractionTime >= requiredInteractionDuration;
    }

    /// <summary>
    /// Tracks interaction time for each object (called from Update)
    /// </summary>
    private void TrackObjectInteractions()
    {
        var requiredObjects = TaskManager.Instance.GetCurrentStepObjects();

        foreach (var objName in requiredObjects)
        {
            var part = IARInteractionDatabase.Instance.GetPart(objName);
            if (part == null) continue;

            // Initialize if not tracked
            if (!_objectInteractionTime.ContainsKey(objName))
                _objectInteractionTime[objName] = 0f;

            // Increase interaction time if object has high DoI (user is focusing on it)
            if (part.currentDoI > 0.5f)
            {
                _objectInteractionTime[objName] += Time.deltaTime;
            }
            else
            {
                // Reset if focus is lost
                _objectInteractionTime[objName] *= 0.95f;
            }
        }
    }

    /// <summary>
    /// Reset tracking when moving to next step
    /// </summary>
    public void ResetTracking()
    {
        _objectInteractionTime.Clear();
        _lastObjectPosition.Clear();
    }

    /// <summary>
    /// Get debug info about current step validation
    /// </summary>
    public string GetDebugInfo()
    {
        var step = TaskManager.Instance.CurrentStep;
        var requiredObjects = TaskManager.Instance.GetCurrentStepObjects();
        
        var info = $"Step: {step.description}\n";
        info += $"Proximity OK: {CheckObjectsProximity(requiredObjects)}\n";

        foreach (var objName in requiredObjects)
        {
            if (_objectInteractionTime.TryGetValue(objName, out float time))
                info += $"{objName}: {time:F1}s / {requiredInteractionDuration:F1}s\n";
        }

        return info;
    }
}
