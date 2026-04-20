using System.Collections.Generic;
using UnityEngine;
using TMPro;

[System.Serializable]
public class KitchenTask
{
    public int id;
    public string name;
    public string description;
    public List<TaskStep> steps;
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

    public KitchenTask CurrentRecipe => _taskList.recipes[_currentRecipeIndex];
    public TaskStep CurrentStep => CurrentRecipe.steps[_currentStepIndex];

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
    }

    public void CompleteStep()
    {
        _currentStepIndex++;

        if (_currentStepIndex >= CurrentRecipe.steps.Count)
        {
            CompleteRecipe();
            return;
        }

        DisplayCurrentStep();
    }

    public void CompleteRecipe()
    {
        Debug.Log($"Recipe '{CurrentRecipe.name}' completed!");
        _currentRecipeIndex++;
        _currentStepIndex = 0;

        if (_currentRecipeIndex >= _taskList.recipes.Count)
        {
            Debug.Log("All recipes completed!");
            _currentRecipeIndex = 0; // Loop back to first
        }

        DisplayCurrentStep();
    }

    public List<string> GetCurrentStepObjects()
    {
        var objects = new List<string>();
        foreach (var obj in CurrentStep.objectsUsed)
        {
            objects.Add(obj.objectName);
        }
        return objects;
    }

    public string GetCurrentStepAction(string objectName)
    {
        foreach (var obj in CurrentStep.objectsUsed)
        {
            if (obj.objectName == objectName)
                return obj.action;
        }
        return "";
    }
}
