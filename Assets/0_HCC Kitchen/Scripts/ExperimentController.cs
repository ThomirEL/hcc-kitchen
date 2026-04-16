using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using TMPro;
using UnityEngine.XR.ARSubsystems;
using Unity.XR.CoreUtils;

/// <summary>
/// Experiment controller — manages multiple trials with automatic progression.
/// 
/// A trial consists of:
/// - Highlight type (Circle or Arrow)
/// - Targeting mode (All, Sequential, Subset)
/// - Permutation index (row in permutations.csv to use)
/// 
/// After all items are collected, a 5 second timer begins before the next trial starts.
/// Player position is reset to the starting point between trials.
/// 
/// Attach to any GameObject in the scene alongside the HighlightManager.
/// </summary>
public class ExperimentController : MonoBehaviour
{
    [System.Serializable]
    public struct Trial
    {
        public KitchenHighlightManager.HighlightType highlightType;
        public KitchenHighlightManager.TargetingMode targetingMode;
        
        [Tooltip("Index into the permutations.csv file (0-based, skips header).")]
        [Min(0)]
        public int permutationIndex;
    }

    private TMP_InputField participantIDInput;

    [SerializeField]
    public bool loggingEnabled = true;

    [Header("━━ References ━━━━━━━━━━━━━━━━━━━━━━━━━")]
    public KitchenHighlightManager highlightManager;
    
    [Tooltip("Player transform to reset position between trials.")]
    public Transform playerStartPosition;

    [SerializeField]
    public GameObject _player;

    [Header("━━ Trial Configuration ━━━━━━━━━━━━━━━━━━━━")]
    [Min(1)]
    public int numTrials = 10;
    
    [Tooltip("Delay (in seconds) between trial completion and next trial start.")]
    [Min(0.1f)]
    public float trialCompletionDelay = 5f;

    [Header("━━ Trials (Auto-generated) ━━━━━━━━━━━━━━━━━━━━")]
    public List<Trial> trials = new List<Trial>();

    [SerializeField]
    private int randomSeed = 42;



    // ── Internal State ────────────────────────────────────────────────────
    private int _currentTrialIndex = -1;
    private bool _experimentActive = false;
    private bool _trialWaitingForCompletion = false;
    private float _trialCompletionTimer = 0f;

    private Vector3 _playerStartPos;
    private Quaternion _playerStartRot;

    private bool _loggingInitialized = false;

    private static readonly string[] ColumnNamesStudy = { "U_Frame", "Trial", "TrialPermutation", "HighlightType", "TargetingMode" };

    // ─────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        // Store starting player position
        if (playerStartPosition != null)
        {
            _playerStartPos = playerStartPosition.position;
            _playerStartRot = playerStartPosition.rotation;
        }

        Random.InitState(randomSeed);

        participantIDInput = GameObject.Find("ID_Input").GetComponent<TMP_InputField>();
    }

    private void Start()
    {
        // Auto-generate trials if not already set up
        if (trials.Count == 0)
            GenerateTrials();
        if (loggingEnabled)
            Logging.Logger.ParticipantIDSet.AddListener(OnParticipantIDSet);
    }

    private void OnParticipantIDSet()
    {
        if (loggingEnabled)
            Logging.Logger.RecordExperimentController(ColumnNamesStudy);
        
        _loggingInitialized = true;
        Debug.Log($"[Experiment] Logging initialized for participant {Logging.Logger.participantID}");
    }

    public void SetParticipantID()
    {
        if (int.TryParse(participantIDInput.text, out int parsedID))
        {
            Logging.Logger.participantID = parsedID;
            Debug.Log($"[Experiment] Participant ID set to {Logging.Logger.participantID}");
            Logging.Logger.ParticipantIDSet?.Invoke();
        }
        else
        {
            Debug.LogError($"[Experiment] Invalid Participant ID: {participantIDInput.text}");
        }
    }


    private void Update()
    {
        if (!_experimentActive || !_trialWaitingForCompletion)
            return;
        
        // Count down timer between trials
        _trialCompletionTimer -= Time.deltaTime;
        if (_trialCompletionTimer <= 0f)
        {
            _trialWaitingForCompletion = false;
            _currentTrialIndex++;
            
            if (_currentTrialIndex < trials.Count)
            {
                StartNextTrial();
            }
            else
            {
                EndExperiment();
            }
        }



    }

    private void Log()
    {

        if (!_loggingInitialized)
        {
            Debug.LogWarning("[Experiment] Log() called before participant ID was set — skipping.");
            return;
        }
        Trial currentTrial = trials[_currentTrialIndex];
        string [] msg = new string[ColumnNamesStudy.Length];
        msg[0] = Time.frameCount.ToString();
        msg[1] = _currentTrialIndex.ToString();
        msg[2] = currentTrial.permutationIndex.ToString();
        msg[3] = currentTrial.highlightType.ToString();
        msg[4] = currentTrial.targetingMode.ToString();
        Logging.Logger.RecordExperimentController(msg);
    }

    // ─────────────────────────────────────────────────────────────────────
    // PUBLIC API
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Generate X random trials based on numTrials setting.
    /// Assumes at least numTrials permutations exist in permutations.csv.
    /// </summary>
    public void GenerateTrials()
    {
        trials.Clear();
        
        for (int i = 0; i < numTrials; i++)
        {
            Trial trial = new Trial
            {
                highlightType = (KitchenHighlightManager.HighlightType)
                    Random.Range(0, 2), // Circle(0) or Arrow(1)
                targetingMode = (KitchenHighlightManager.TargetingMode)
                    Random.Range(0, 3), // All(0), Sequential(1), Subset(2)
                permutationIndex = Random.Range(0, 100) // Random permutation index
            };
            
            trials.Add(trial);
        }
        
        Debug.Log($"[Experiment] Generated {numTrials} trials.");
    }

    /// <summary>
    /// Starts the entire experiment (all trials in sequence).
    /// </summary>
    public void StartExperiment()
    {
        if (!ValidateManager()) return;

        if (trials.Count == 0)
            GenerateTrials();

        _experimentActive = true;
        _currentTrialIndex = 0;
        _trialWaitingForCompletion = false;

        Debug.Log($"[Experiment] Starting experiment with {trials.Count} trials.");
        
        StartNextTrial();
    }

    /// <summary>
    /// Stops the current experiment and clears highlights.
    /// </summary>
    public void StopExperiment()
    {
        if (!ValidateManager()) return;
        
        _experimentActive = false;
        _trialWaitingForCompletion = false;
        highlightManager.StopTrial();
        
        Debug.Log("[Experiment] Experiment stopped.");
    }

    /// <summary>
    /// Manually progress to the next trial (skips completion timer).
    /// </summary>
    public void SkipToNextTrial()
    {
        if (!_experimentActive)
        {
            Debug.Log("[Experiment] Experiment not active.");
            return;
        }

        _trialWaitingForCompletion = false;
        // Clear all 
        _currentTrialIndex++;

        if (_currentTrialIndex < trials.Count)
        {
            Debug.Log($"[Experiment] Skipping to trial {_currentTrialIndex + 1}");
            StartNextTrial();
        }
        else
        {
            EndExperiment();
        }
    }

    /// <summary>
    /// Simulates collecting the next target (for testing).
    /// </summary>
    public void SimulateCollectNext()
    {
        if (!ValidateManager()) return;

        if (!highlightManager.IsTrialActive())
        {
            Debug.Log("[Experiment] No trial active.");
            return;
        }

        ItemHighlight next = highlightManager.GetNextTarget();
        if (next == null)
        {
            Debug.Log("[Experiment] No next target found.");
            return;
        }

        Debug.Log($"[Experiment] Simulating collect: {next.gameObject.name}");
        highlightManager.OnItemCollected(next);
    }

    // ─────────────────────────────────────────────────────────────────────
    // INTERNAL
    // ─────────────────────────────────────────────────────────────────────

    private void StartNextTrial()
    {
        if (_currentTrialIndex >= trials.Count)
        {
            EndExperiment();
            return;
        }

        

        Trial trial = trials[_currentTrialIndex];

        HingeDoorInteractable[] hinges = FindObjectsByType<HingeDoorInteractable>();
        foreach (var obj in hinges)
        {
            obj.ResetRotation();
        }

        XRSlideInteractable[] sliders = FindObjectsByType<XRSlideInteractable>();
        foreach (var slider in sliders)
        {
            slider.ResetPosition();
        }




        if (playerStartPosition != null)
        {
            XROrigin xr = _player.GetComponent<XROrigin>();
            if (xr != null)
            {
                float cameraYOffset = xr.Camera.transform.position.y - xr.transform.position.y;
                Vector3 adjustedTarget = new Vector3(_playerStartPos.x, _playerStartPos.y + cameraYOffset, _playerStartPos.z);
                xr.MoveCameraToWorldLocation(adjustedTarget);
            }
            else
            {
                Debug.LogWarning("[Experiment] Player does not have XROrigin component — cannot reset position.");
            }

        }

        

        // Reset all collected items
        highlightManager.EnableCollectedItems();
        // Generate new trial list for this trial using the permutation index

        // Refresh and render all managers
        RenderManager.Instance.RefreshAndRenderAll();

        
        highlightManager.CreateTrialList(trial.permutationIndex);
        
        
        
        // Configure and start trial
        highlightManager.SetCondition(trial.highlightType, trial.targetingMode);
        highlightManager.StartTrial();

        // Hook into completion event
        StopAllCoroutines();
        StartCoroutine(WaitForTrialCompletion());

        Debug.Log($"[Experiment] Trial {_currentTrialIndex + 1}/{trials.Count} started | " +
                  $"{trial.highlightType} + {trial.targetingMode} (permutation: {trial.permutationIndex})");

        if (loggingEnabled)
            Log();
    }

    private IEnumerator WaitForTrialCompletion()
    {
        // Wait until trial is complete
        while (!highlightManager.IsTrialComplete())
            yield return null;

        // Trial is complete — start countdown
        _trialWaitingForCompletion = true;
        _trialCompletionTimer = trialCompletionDelay;

        Debug.Log($"[Experiment] Trial complete. Next trial in {trialCompletionDelay} seconds...");
    }

    private void EndExperiment()
    {
        _experimentActive = false;
        _trialWaitingForCompletion = false;
        highlightManager.StopTrial();

        Debug.Log("[Experiment] All trials complete!");
    }

    private bool ValidateManager()
    {
        if (highlightManager != null) return true;
        Debug.LogError("[Experiment] No KitchenHighlightManager assigned!");
        return false;
    }
}