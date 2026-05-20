using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using TMPro;
using UnityEngine.XR.ARSubsystems;
using Unity.XR.CoreUtils;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Experiment controller — manages multiple trials with automatic progression.
///
/// Trial flow:
///   Trial 0  →  no timer, no questionnaire — next trial starts immediately after collection
///   Trial 1+ →  60s timer (ends early if all items collected)
///               → scene resets + player teleported immediately
///               → 5s buffer
///               → questionnaire shown
///               → next trial starts immediately on submit
///
/// Attach alongside KitchenHighlightManager.
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

    [Tooltip("Buffer (seconds) between trial end and questionnaire appearing.")]
    [Min(0.1f)]
    public float trialCompletionDelay = 5f;

    [Tooltip("Time limit per trial in seconds. Trial 0 is exempt.")]
    [Min(1f)]
    public float trialDuration = 60f;

    [Header("━━ Trials (Auto-generated) ━━━━━━━━━━━━━━━━━━━━")]
    public List<Trial> trials = new List<Trial>();

    [SerializeField]
    private int randomSeed = 42;

    public bool practiceRoundEnabled = true;

    public TMP_InputField questionnaireInputField;
    public GameObject questionnairePanel;

    // ── Internal State ────────────────────────────────────────────────────

    private int  _currentTrialIndex = -1;
    private bool _experimentActive  = false;

    // Questionnaire gate — flipped to true by SubmitQuestionnaire()
    private bool _questionnaireSubmitted = false;

    // Exposed so external UI (e.g. TimerUI.cs) can read remaining time each frame
    public float TrialTimeRemaining { get; private set; }

    private Vector3    _playerStartPos;
    private Quaternion _playerStartRot;

    private bool _loggingInitialized = false;

    public NearFarInteractor leftHandRayInteractor;
    public NearFarInteractor rightHandRayInteractor;

    private static readonly string[] ColumnNamesStudy =
        { "U_Frame", "Trial", "TrialPermutation", "HighlightType", "TargetingMode" };

    // ─────────────────────────────────────────────────────────────────────
    // LIFECYCLE
    // ─────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (playerStartPosition != null)
        {
            _playerStartPos = playerStartPosition.position;
            _playerStartRot = playerStartPosition.rotation;
        }

        Random.InitState(randomSeed);

        participantIDInput      = GameObject.Find("ID_Input").GetComponent<TMP_InputField>();
        questionnaireInputField = GameObject.Find("Questionnaire_Input").GetComponent<TMP_InputField>();
        questionnairePanel      = GameObject.Find("Questionnaire");


        // Must be hidden at startup
        if (questionnairePanel != null)
            questionnairePanel.SetActive(false);
    }

    private void Start() { }

    // ─────────────────────────────────────────────────────────────────────
    // LOGGING
    // ─────────────────────────────────────────────────────────────────────

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

    private void Log()
    {
        if (!_loggingInitialized)
        {
            Debug.LogWarning("[Experiment] Log() called before participant ID was set — skipping.");
            return;
        }

        Trial currentTrial = trials[_currentTrialIndex];
        string[] msg = new string[ColumnNamesStudy.Length];
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
    /// Called by the questionnaire Submit button.
    /// Hides the panel and unblocks the trial coroutine.
    /// </summary>
    public void SubmitQuestionnaire()
    {
        string answer = questionnaireInputField != null ? questionnaireInputField.text : "(no field)";
        Debug.Log($"[Experiment] Questionnaire submitted for trial {_currentTrialIndex}: \"{answer}\"");

        // TODO: pipe answer into your Logging system here if needed

        if (questionnaireInputField != null)
            questionnaireInputField.text = string.Empty;

        if (questionnairePanel != null)
            questionnairePanel.SetActive(false);

        // Unblocks the while loop in WaitForTrialCompletion
        _questionnaireSubmitted = true;
    }

    public void GenerateRandomTrials()
    {
        trials.Clear();

        for (int i = 0; i < numTrials; i++)
        {
            Trial trial = new Trial
            {
                highlightType    = (KitchenHighlightManager.HighlightType)Random.Range(0, 2),
                targetingMode    = (KitchenHighlightManager.TargetingMode)Random.Range(0, 3),
                permutationIndex = Random.Range(0, 100)
            };
            trials.Add(trial);
        }

        Debug.Log($"[Experiment] Generated {numTrials} random trials.");
    }

    public void PracticeRound()
    {
        if (!ValidateManager()) return;

        Trial practiceTrial = new Trial
        {
            highlightType    = KitchenHighlightManager.HighlightType.Circle,
            targetingMode    = KitchenHighlightManager.TargetingMode.All,
            permutationIndex = 0
        };

        trials.Clear();
        trials.Add(practiceTrial);
        _currentTrialIndex = 0;
        StartNextTrial();
    }

    private void GenerateExperimentTrials(int participantNumber)
{
    if (participantNumber <= 0)
    {
        Debug.LogError("[Experiment] Participant ID is 0 or invalid — cannot generate trials. " +
                       "Make sure the ID input field is filled in before starting.");
        return;
    }

    TrialJson trial = TrialLoader.GetTrial(participantNumber);

    if (trial == null || trial.sequence == null)
    {
        Debug.LogError($"[Experiment] No trial data found for participant {participantNumber}.");
        return;
    }

    var highlightType = KitchenHighlightManager.HighlightType.Circle;

    foreach (string letter in trial.sequence)
    {
        Trial newTrial = new Trial
        {
            highlightType    = highlightType,
            targetingMode    = ConditionMapper.FromLetter(letter),
            permutationIndex = participantNumber - 1
        };
        trials.Add(newTrial);
    }
}

    public void StartRandomTrials()
    {
        trials.Clear();

        if (loggingEnabled)
            Logging.Logger.ParticipantIDSet.AddListener(OnParticipantIDSet);

        if (!ValidateManager()) return;

        if (trials.Count == 0)
            GenerateRandomTrials();

        _experimentActive  = true;
        _currentTrialIndex = 0;

        Debug.Log($"[Experiment] Starting random experiment with {trials.Count} trials.");
        StartNextTrial();
    }

    public void StartExperimentTrials()
{
    trials.Clear();

    // Parse and set participant ID from the input field right now,
    // before anything else — don't rely on it having been set earlier
    SetParticipantID();

    if (loggingEnabled)
        Logging.Logger.ParticipantIDSet.AddListener(OnParticipantIDSet);

    if (!ValidateManager()) return;

    Debug.Log($"[Experiment] Starting experiment for participant {Logging.Logger.participantID}...");

    if (trials.Count == 0)
        GenerateExperimentTrials(Logging.Logger.participantID);

    _experimentActive  = true;
    _currentTrialIndex = 0;

    Debug.Log($"[Experiment] Starting experiment with {trials.Count} trials.");
    StartNextTrial();
}

    public void StopExperiment()
    {
        if (!ValidateManager()) return;

        _experimentActive = false;

        if (questionnairePanel != null)
            questionnairePanel.SetActive(false);

        highlightManager.StopTrial();
        StopAllCoroutines();

        Debug.Log("[Experiment] Experiment stopped.");
    }

    /// <summary>
    /// Skips the current trial and jumps straight to the next one.
    /// Closes any open questionnaire without recording an answer.
    /// </summary>
    public void SkipToNextTrial()
    {
        if (!_experimentActive)
        {
            Debug.Log("[Experiment] Experiment not active.");
            return;
        }

        StopAllCoroutines();
        highlightManager.StopTrial();

        if (questionnairePanel != null)
            questionnairePanel.SetActive(false);

        _questionnaireSubmitted = false;
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
    /// Simulates collecting the next highlighted item — for editor debugging.
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

    /// <summary>
    /// Resets hinges, sliders, player position, and collected items.
    /// Called immediately when a trial ends — before the 5s buffer.
    /// </summary>
    private void ResetSceneForNextTrial()
    {
        HingeDoorInteractable[] hinges = FindObjectsByType<HingeDoorInteractable>();
        foreach (var h in hinges) h.ResetRotation();

        XRSlideInteractable[] sliders = FindObjectsByType<XRSlideInteractable>();
        foreach (var s in sliders) s.ResetPosition();

        if (playerStartPosition != null)
        {
            XROrigin xr = _player.GetComponent<XROrigin>();
            if (xr != null)
            {
                float cameraYOffset = xr.Camera.transform.position.y - xr.transform.position.y;
                Vector3 adjustedTarget = new Vector3(
                    _playerStartPos.x,
                    _playerStartPos.y + cameraYOffset,
                    _playerStartPos.z);
                xr.MoveCameraToWorldLocation(adjustedTarget);
            }
            else
            {
                Debug.LogWarning("[Experiment] Player has no XROrigin — cannot reset position.");
            }
        }

        highlightManager.EnableCollectedItems();
        RenderManager.Instance.RefreshAndRenderAll();

        Debug.Log("[Experiment] Scene reset and player teleported to start.");
    }

    /// <summary>
    /// Configures and starts the highlight system for the current trial index.
    /// Called after the questionnaire is submitted (or immediately for trial 0).
    /// </summary>
    private void BeginTrial()
    {
        if (_currentTrialIndex >= trials.Count)
        {
            EndExperiment();
            return;
        }

        Trial trial = trials[_currentTrialIndex];

        highlightManager.CreateTrialList(_currentTrialIndex, Logging.Logger.participantID);
        highlightManager.SetCondition(trial.highlightType, trial.targetingMode);
        highlightManager.StartTrial();

        StopAllCoroutines();
        StartCoroutine(WaitForTrialCompletion());

        Debug.Log($"[Experiment] Trial {_currentTrialIndex + 1}/{trials.Count} begun | " +
                  $"{trial.highlightType} + {trial.targetingMode} " +
                  $"(permutation: {trial.permutationIndex}) | " +
                  $"{(_currentTrialIndex == 0 ? "no timer" : $"{trialDuration}s timer")}");

        if (loggingEnabled) Log();
    }

    /// <summary>
    /// Entry point for each trial. Resets the scene then begins gameplay.
    /// </summary>
    private void StartNextTrial()
    {
        if (_currentTrialIndex >= trials.Count)
        {
            EndExperiment();
            return;
        }

        ResetSceneForNextTrial();
        BeginTrial();
    }

    /// <summary>
    /// Full per-trial coroutine sequence:
    ///
    ///   Phase 1 — gameplay    wait for collection OR 60s timeout (no timeout on trial 0)
    ///   Phase 2 — reset       scene + teleport immediately on trial end
    ///   Phase 3 — buffer      WaitForSeconds(trialCompletionDelay) — default 5s
    ///   Phase 4 — questionnaire show panel, wait for SubmitQuestionnaire() (skipped on trial 0)
    ///   Phase 5 — advance     BeginTrial() called immediately after submit
    /// </summary>
    private IEnumerator WaitForTrialCompletion()
{
    bool isFirstTrial = (_currentTrialIndex == 0);

    // ── Phase 1: active gameplay ─────────────────────────────────────
    if (isFirstTrial)
    {
        TrialTimeRemaining = Mathf.Infinity;

        while (!highlightManager.IsTrialComplete())
            yield return null;

        Debug.Log("[Experiment] Trial 0 complete (no timer).");
    }
    else
    {
        TrialTimeRemaining = trialDuration;

        while (!highlightManager.IsTrialComplete() && TrialTimeRemaining > 0f)
        {
            TrialTimeRemaining -= Time.deltaTime;
            yield return null;
        }

        if (TrialTimeRemaining <= 0f)
        {
            Debug.Log($"[Experiment] Trial {_currentTrialIndex + 1} timed out after {trialDuration}s.");
            highlightManager.StopTrial();
        }
        else
        {
            Debug.Log($"[Experiment] Trial {_currentTrialIndex + 1} complete " +
                      $"({trialDuration - TrialTimeRemaining:F1}s elapsed).");
        }

        TrialTimeRemaining = 0f;
    }

    // ── Phase 2: 5s buffer while player is still standing there ─────
    Debug.Log($"[Experiment] Waiting {trialCompletionDelay}s before reset...");
    yield return new WaitForSeconds(trialCompletionDelay);

    // ── Phase 3: reset scene + teleport player ───────────────────────
    ResetSceneForNextTrial();

        leftHandRayInteractor.enableFarCasting = true;

        rightHandRayInteractor.enableFarCasting = true;

    // ── Phase 4: questionnaire (trials 1+ only) ──────────────────────
    if (!isFirstTrial)
    {
        _questionnaireSubmitted = false;

        if (questionnairePanel != null)
            questionnairePanel.SetActive(true);
        else
            Debug.LogWarning("[Experiment] questionnairePanel is null.");

        Debug.Log("[Experiment] Waiting for questionnaire submission...");

        while (!_questionnaireSubmitted)
            yield return null;
    }

    leftHandRayInteractor.enableFarCasting = false;
    rightHandRayInteractor.enableFarCasting = false;

    // ── Phase 5: advance to next trial immediately ───────────────────
    _currentTrialIndex++;

    if (_currentTrialIndex < trials.Count)
        BeginTrial();
    else
        EndExperiment();
}

    private void EndExperiment()
    {
        _experimentActive = false;

        if (questionnairePanel != null)
            questionnairePanel.SetActive(false);

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