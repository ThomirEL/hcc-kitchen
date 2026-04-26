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
    [SerializeField] private TextAsset taskJsonFile;
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
        if (taskJsonFile == null)
        {
            Debug.LogError("Task JSON file not assigned!");
            return;
        }
        _taskList = JsonUtility.FromJson<TaskList>(taskJsonFile.text);
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
    /// Matches objects by substring - e.g. "broccoli" matches "broccoli" and "broccoli chunk".
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
            // Check if this part's game object name contains any of the required object names
            bool isInStep = false;
            foreach (string objectName in currentStepObjectNames)
            {
                if (part.gameObject.name.Contains(objectName))
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
        
        foreach (var obj in CurrentStep.objectsUsed)
        {
            if (objectName.Contains(obj.objectName))
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
    /// Updates StepsInToFuture for all IARPart components based on the next n=3 steps.
    /// For each item, finds the soonest step it appears in and sets StepsInToFuture to the distance.
    /// E.g., if knife appears in step 2 and 4, and we're at step 1, StepsInToFuture = 1.
    /// </summary>
    private void UpdateStepsInToFuture(int stepsAhead = 3)
    {
        if (CurrentRecipe?.steps == null)
            return;
        
        // Get all IARPart components in the scene
        IARPart[] allParts = FindObjectsOfType<IARPart>();

        // Dictionary to cache which parts need which steps
        var itemToNearestStep = new Dictionary<string, int>();

        // Look ahead n steps from current step
        for (int i = 1; i <= stepsAhead && (_currentStepIndex + i) < CurrentRecipe.steps.Count; i++)
        {
            TaskStep futureStep = CurrentRecipe.steps[_currentStepIndex + i];
            
            foreach (var obj in futureStep.objectsUsed)
            {
                // Only record the soonest step for each item
                if (!itemToNearestStep.ContainsKey(obj.objectName))
                {
                    itemToNearestStep[obj.objectName] = i;
                }
            }
        }

        // Update each IARPart's StepsInToFuture
        foreach (IARPart part in allParts)
        {
            int? stepsIntoFuture = null;
            
            // Check if this part appears in any of the next steps
            foreach (var kvp in itemToNearestStep)
            {
                if (part.gameObject.name.Contains(kvp.Key))
                {
                    stepsIntoFuture = kvp.Value;
                    break; // Use the first match (soonest step)
                }
            }

            part.StepsInToFuture = stepsIntoFuture;
        }
    }
}
