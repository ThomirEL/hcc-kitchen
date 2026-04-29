using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;

[System.Serializable]
public class TaskRecipeItem
{
    public int stepNumber;
    public List<string> itemNames;
}

[System.Serializable]
public class KitchenTask
{
    public int id;
    public string name;
    public string description;
    public List<TaskStep> steps;
    public List<TaskRecipeItem> items;
}

[System.Serializable]
public class TaskStep
{
    public int stepNumber;
    public string description;
    public List<TaskObject> objectsUsed;
}

[System.Serializable]
public class TaskObject
{
    public string objectName;
    public string action;
}

[System.Serializable]
public class TaskList
{
    public List<KitchenTask> recipes;
}

public class TaskManager : MonoBehaviour
{
    
    [SerializeField] private TextMeshProUGUI stepDisplayUI;
    [SerializeField] private TextMeshProUGUI taskNameUI;
    [SerializeField] private TextMeshProUGUI progressUI;

    private TaskList _taskList;
    private int _currentRecipeIndex = 0;
    private int _currentStepIndex = 0;

    public static TaskManager Instance { get; private set; }

    public KitchenTask CurrentRecipe => _taskList?.recipes?[_currentRecipeIndex];
    public TaskStep CurrentStep => CurrentRecipe?.steps[_currentStepIndex];

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        LoadTasks();
        DisplayCurrentStep();
    }

    void Start()
    {
        // Called after all Awake() methods - IARPart components are now initialized
        UpdateCurrentStepParts();
    }

    void LoadTasks()
    {
        TextAsset json = Resources.Load<TextAsset>("kitchen_tasks");
        if (json == null)
        {
            Debug.LogError("Task JSON file not assigned!");
            return;
        }
        _taskList = JsonUtility.FromJson<TaskList>(json.text);
    }

    public void DisplayCurrentStep()
    {
        if (_taskList == null || _taskList.recipes.Count == 0) return;

        var task = CurrentRecipe;
        var step = CurrentStep;

        if (taskNameUI != null)
            taskNameUI.text = task.name;

        if (stepDisplayUI != null)
            stepDisplayUI.text = $"<b>Step {step.stepNumber}:</b>\n{step.description}";

        if (progressUI != null)
            progressUI.text = $"{_currentStepIndex + 1}/{task.steps.Count}";

        Debug.Log($"Current Step: {step.description}");
        
        UpdateStepsInToFuture();
    }

    /// <summary>
    /// Updates IARPart components based on the current step.
    /// Sets IsInCurrentStep to true for objects used in current step,
    /// and false for all other IARPart objects.
    /// Matches objects by base name (first word) - e.g. "Onion" matches "Onion" and "Onion Chunk".
    /// </summary>
    private void UpdateCurrentStepParts()
    {
        // Get all IARPart components in the scene
        IARPart[] allParts = FindObjectsOfType<IARPart>();
        
        // Get object names used in current step
        List<string> currentStepObjectNames = GetCurrentStepObjects();
        
        // Update each IARPart
        foreach (IARPart part in allParts)
        {
            // Check if this part's game object name contains any of the required object names (by base name)
            bool isInStep = false;
            string basePartName = GetBaseItemName(part.gameObject.name);
            
            foreach (string objectName in currentStepObjectNames)
            {
                string baseObjectName = GetBaseItemName(objectName);
                if (basePartName == baseObjectName)
                {
                    isInStep = true;
                    break;
                }
            }
            part.IsInCurrentStep = isInStep;
        }
    }

    public void CompleteStep()
    {
        if (CurrentRecipe?.steps == null)
            return;
        
        _currentStepIndex++;

        if (_currentStepIndex >= CurrentRecipe.steps.Count)
        {
            CompleteRecipe();
            return;
        }

        DisplayCurrentStep();
        UpdateCurrentStepParts();
    }

    void Update()
    {
        // For testing: Press Space to complete current step
        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            CompleteStep();
        }
    }

    public void CompleteRecipe()
    {
        if (CurrentRecipe == null)
            return;
        
        Debug.Log($"Recipe '{CurrentRecipe.name}' completed!");
        _currentRecipeIndex++;
        _currentStepIndex = 0;

        if (_taskList?.recipes != null && _currentRecipeIndex >= _taskList.recipes.Count)
        {
            Debug.Log("All recipes completed!");
            _currentRecipeIndex = 0; // Loop back to first
        }

        DisplayCurrentStep();
        UpdateCurrentStepParts();
    }

    /// <summary>
    /// Extracts the base item name (first word) to normalize variants like "Onion" and "Onion Chunk"
    /// </summary>
    private string GetBaseItemName(string itemName)
    {
        if (string.IsNullOrEmpty(itemName)) return itemName;
        return itemName.Split(' ')[0];
    }

    public List<string> GetCurrentStepObjects()
    {
        var objects = new List<string>();
        if (CurrentStep?.objectsUsed != null)
        {
            foreach (var obj in CurrentStep.objectsUsed)
            {
                objects.Add(obj.objectName);
            }
        }
        return objects;
    }

    public string GetCurrentStepAction(string objectName)
    {
        if (CurrentStep?.objectsUsed == null)
            return "";
        
        string baseObjectName = GetBaseItemName(objectName);
        
        foreach (var obj in CurrentStep.objectsUsed)
        {
            string baseStepObjectName = GetBaseItemName(obj.objectName);
            if (baseObjectName == baseStepObjectName)
                return obj.action;
        }
        return "";
    }

    /// <summary>
    /// Checks if an item name is used anywhere in the current recipe
    /// </summary>
    public bool IsItemInCurrentRecipe(string itemName)
    {
        if (CurrentRecipe == null || CurrentRecipe.items == null)
            return true; // If items list doesn't exist, assume it's in the recipe

        foreach (var recipeItem in CurrentRecipe.items)
        {
            if (recipeItem.itemNames.Contains(itemName))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Updates StepsInToFuture for all IARPart components based on all remaining steps.
    /// For each item, finds the soonest step it appears in and sets StepsInToFuture to the distance.
    /// Matches objects by base name (first word) - e.g. "Onion" matches "Onion" and "Onion Chunk".
    /// </summary>
    private void UpdateStepsInToFuture()
    {
        if (CurrentRecipe?.steps == null)
            return;
        
        // Get all IARPart components in the scene
        IARPart[] allParts = FindObjectsOfType<IARPart>();

        // Dictionary to cache which base items need which steps
        var itemToNearestStep = new Dictionary<string, int>();

        // Look ahead through all remaining steps
        for (int i = 1; (_currentStepIndex + i) < CurrentRecipe.steps.Count; i++)
        {
            TaskStep futureStep = CurrentRecipe.steps[_currentStepIndex + i];
            
            foreach (var obj in futureStep.objectsUsed)
            {
                string baseItemName = GetBaseItemName(obj.objectName);
                // Only record the soonest step for each base item
                if (!itemToNearestStep.ContainsKey(baseItemName))
                {
                    itemToNearestStep[baseItemName] = i;
                }
            }
        }

        // Update each IARPart's StepsInToFuture
        foreach (IARPart part in allParts)
        {
            int? stepsIntoFuture = null;
            string basePartName = GetBaseItemName(part.gameObject.name);
            
            // Check if this part appears in any of the next steps
            foreach (var kvp in itemToNearestStep)
            {
                if (basePartName == kvp.Key)
                {
                    stepsIntoFuture = kvp.Value;
                    break;
                }
            }

            part.StepsInToFuture = stepsIntoFuture;
        }
    }
}
