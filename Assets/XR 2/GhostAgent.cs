using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Drives a ghost agent through a looping task sequence.
/// The root GameObject walks between station positions; GhostBodyAnimator
/// handles all visual animation. Attach to the GhostAgent root.
/// </summary>
public class GhostAgent : MonoBehaviour
{
    // ─── Inspector ────────────────────────────────────────────────────────────

    [Header("Components")]
    [Tooltip("GhostBodyAnimator on this same GameObject")]
    [SerializeField] private GhostBodyAnimator _bodyAnimator;

    [Tooltip("XRDirectInteractor on the HandRight child")]
    [SerializeField] private UnityEngine.XR.Interaction.Toolkit.Interactors.XRDirectInteractor _rightHandInteractor;

    [Header("Home Station")]
    [Tooltip("Where the agent stands when idle between task cycles")]
    [SerializeField] private Transform _homeStation;

    [Header("Tasks")]
    [SerializeField] private List<GhostTask> _tasks = new();

    [Header("Timing")]
    [SerializeField] private float _arrivalPause   = 0.6f;
    [SerializeField] private float _reachDuration  = 0.9f;
    [SerializeField] private float _hoverDuration  = 0.25f;
    [SerializeField] private float _retractDuration = 0.6f;
    [SerializeField] private float _homePause      = 1.8f;

    // ─── Private ──────────────────────────────────────────────────────────────

    private int  _taskIndex = 0;
    private bool _isHolding = false;

    private Vector3 _lastPos;

    // ─── Lifecycle ────────────────────────────────────────────────────────────

    private void Start()
    {
        Debug.Log("[GhostAgent] Start called");

        if (_tasks == null || _tasks.Count == 0)
        {
            Debug.LogWarning("[GhostAgent] No tasks configured.");
            return;
        }

        if (_homeStation != null)
        {
            Debug.Log("[GhostAgent] Moving to home station");
            transform.SetPositionAndRotation(_homeStation.position, _homeStation.rotation);
        }

        Debug.Log("[GhostAgent] Starting RunLoop coroutine");
        StartCoroutine(RunLoop());
    }

    // private void Update()
    // {
    //     if (Vector3.Distance(transform.position, _lastPos) > 0.001f)
    //     {
    //         Debug.Log($"[GhostAgent] Position changed: {transform.position}");
    //     }

    //     _lastPos = transform.position;
    // }

    // ─── Main Loop ────────────────────────────────────────────────────────────

    // ─── Main Loop ────────────────────────────────────────────────────────────────

private IEnumerator RunLoop()
{
    Debug.Log("[GhostAgent] RunLoop started");

    while (true)
    {
        // ── Forward pass: task 0 → n-1 ────────────────────────────────────
        Debug.Log("[GhostAgent] Starting forward pass");
        for (int i = 0; i < _tasks.Count; i++)
        {
            _taskIndex = i;
            yield return StartCoroutine(ExecuteTask(_tasks[i], reversed: false));
        }

        // ── Wait at home between passes ───────────────────────────────────
        yield return StartCoroutine(WalkToStation(_homeStation));
        _bodyAnimator.ClearHeadLookTarget();
        yield return new WaitForSeconds(_homePause);

        // ── Reverse pass: task n-1 → 0, each with pickup/place swapped ───
        Debug.Log("[GhostAgent] Starting reverse pass");
        for (int i = _tasks.Count - 1; i >= 0; i--)
        {
            _taskIndex = i;
            yield return StartCoroutine(ExecuteTask(_tasks[i], reversed: true));
        }

        // ── Wait at home before repeating ─────────────────────────────────
        yield return StartCoroutine(WalkToStation(_homeStation));
        _bodyAnimator.ClearHeadLookTarget();
        yield return new WaitForSeconds(_homePause);
    }
}

/// <summary>
/// Executes a single pickup-and-place task.
/// When reversed=true, the item is at the PlacePoint and we return it to
/// the PickupPoint — so all station/point references are simply swapped.
/// </summary>
private IEnumerator ExecuteTask(GhostTask task, bool reversed)
{
    Debug.Log($"[GhostAgent] ExecuteTask '{task.TaskName}' reversed={reversed}");

    // Swap references depending on direction
    Transform fromStation = reversed ? task.PlaceStation  : task.PickupStation;
    Transform fromPoint   = reversed ? task.PlacePoint    : task.PickupPoint;
    Transform toStation   = reversed ? task.PickupStation : task.PlaceStation;
    Transform toPoint     = reversed ? task.PickupPoint   : task.PlacePoint;
    Transform item        = task.Item;

    // Validate using the appropriate "from" interactable location
    if (!ValidateTask(task))
    {
        Debug.Log($"[GhostAgent] Task '{task.TaskName}' invalid, skipping");
        yield break;
    }

    // ── 1. Walk to pickup station ──────────────────────────────────────────
    Debug.Log($"[GhostAgent] Walking to from-station: {fromStation.name}");
    yield return StartCoroutine(WalkToStation(fromStation));

    _bodyAnimator.SetHeadLookTarget(fromPoint.position);
    yield return new WaitForSeconds(_arrivalPause);

    // ── 2. Reach for item ─────────────────────────────────────────────────
    Debug.Log("[GhostAgent] Reaching for item");
    yield return StartCoroutine(_bodyAnimator.AnimateReach(fromPoint.position, _reachDuration));
    yield return new WaitForSeconds(_hoverDuration);

    // ── 3. Grab ───────────────────────────────────────────────────────────
    Debug.Log("[GhostAgent] Grabbing item");
    GrabItem(item);
    _bodyAnimator.SetHolding(true);
    yield return null;

    // ── 4. Retract arm before walking ─────────────────────────────────────
    yield return StartCoroutine(_bodyAnimator.AnimateRetract(_retractDuration));

    // ── 5. Walk to place station ──────────────────────────────────────────
    Debug.Log($"[GhostAgent] Walking to to-station: {toStation.name}");
    yield return StartCoroutine(WalkToStation(toStation));

    _bodyAnimator.SetHeadLookTarget(toPoint.position);
    yield return new WaitForSeconds(_arrivalPause);

    // ── 6. Reach to place ─────────────────────────────────────────────────
    Debug.Log("[GhostAgent] Reaching to place");
    yield return StartCoroutine(_bodyAnimator.AnimateReach(toPoint.position, _reachDuration));
    yield return new WaitForSeconds(_hoverDuration);

    // ── 7. Release ────────────────────────────────────────────────────────
    Debug.Log("[GhostAgent] Releasing item");
    ReleaseItem();
    _bodyAnimator.SetHolding(false);
    yield return null;

    // ── 8. Retract arm ────────────────────────────────────────────────────
    yield return StartCoroutine(_bodyAnimator.AnimateRetract(_retractDuration));
}

    // ─── Walking ──────────────────────────────────────────────────────────────

    private IEnumerator WalkToStation(Transform station)
    {
        if (station == null)
        {
            Debug.LogWarning("[GhostAgent] WalkToStation called with NULL station");
            yield break;
        }

        Debug.Log($"[GhostAgent] Walking from {transform.position} to {station.position}");

        Vector3 startPos = transform.position;
        Vector3 endPos   = station.position;
        float distance   = Vector3.Distance(startPos, endPos);

        float walkSpeed  = _tasks[_taskIndex].WalkSpeed;

        Debug.Log($"[GhostAgent] Distance: {distance}, WalkSpeed: {walkSpeed}");

        float duration = Mathf.Max(distance / walkSpeed, 0.4f);
        float elapsed  = 0f;

        Debug.Log($"[GhostAgent] Duration: {duration}");

        _bodyAnimator.SetWalking(true, (endPos - startPos).normalized);

        int frameCount = 0;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);

            transform.position = Vector3.Lerp(startPos, endPos, t);

            // if (frameCount % 30 == 0)
            // {
            //     Debug.Log($"[GhostAgent] Moving... t={t:F2}, pos={transform.position}");
            // }
            // frameCount++;

            yield return null;
        }

        Debug.Log("[GhostAgent] Reached destination");

        transform.SetPositionAndRotation(endPos, station.rotation);
        _bodyAnimator.SetWalking(false, Vector3.zero);
    }

    // ─── XRI Grab / Release ───────────────────────────────────────────────────

    private void GrabItem(Transform item)
    {
        if (item == null || _isHolding)
        {
            Debug.LogWarning("[GhostAgent] GrabItem failed (null or already holding)");
            return;
        }

        var interactable = item.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable>();
        if (interactable == null)
        {
            Debug.LogWarning("[GhostAgent] Item has no interactable");
            return;
        }

        _rightHandInteractor.StartManualInteraction(interactable);
        _isHolding = true;
        Debug.Log($"[GhostAgent] Grabbed: {item.name}");
    }

    private void ReleaseItem()
    {
        if (!_isHolding)
        {
            Debug.LogWarning("[GhostAgent] Release called but not holding");
            return;
        }

        _rightHandInteractor.EndManualInteraction();
        _isHolding = false;
        Debug.Log("[GhostAgent] Released item.");
    }

    // ─── Validation ───────────────────────────────────────────────────────────

    private bool ValidateTask(GhostTask task)
    {
        if (task.Item == null || task.PickupStation == null || task.PickupPoint == null ||
            task.PlaceStation == null  || task.PlacePoint == null)
        {
            Debug.LogWarning($"[GhostAgent] Task {_taskIndex} '{task.TaskName}' has null references — skipping.");
            return false;
        }

        var interactable = task.Item.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable>();
        if (interactable != null && interactable.isSelected)
        {
            Debug.Log($"[GhostAgent] '{task.Item.name}' held by real player — skipping.");
            return false;
        }

        return true;
    }
}

// ─── Task Data ────────────────────────────────────────────────────────────────

[System.Serializable]
public class GhostTask
{
    public string    TaskName      = "New Task";
    public Transform PickupStation;
    public Transform Item;
    public Transform PickupPoint;
    public Transform PlaceStation;
    public Transform PlacePoint;

    [Range(0.5f, 3f)]
    public float WalkSpeed = 1.2f;
}