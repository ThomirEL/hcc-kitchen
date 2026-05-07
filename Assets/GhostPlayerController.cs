using System.Collections;
using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// GhostPlayerController — drives ghost hands through XRI's full interaction
/// pipeline using StartManualInteraction / EndManualInteraction.
/// All XRGrabInteractable events (selectEntered, selectExited, etc.) fire normally.
/// Attach to the GhostPlayer root GameObject.
/// </summary>
public class GhostPlayerController : MonoBehaviour
{
    // ─── Inspector ────────────────────────────────────────────────────────────

    [Header("Ghost Hands")]
    [Tooltip("The XRDirectInteractor on the right ghost hand GameObject")]
    [SerializeField] private UnityEngine.XR.Interaction.Toolkit.Interactors.XRDirectInteractor _rightHandInteractor;

    [Tooltip("The right hand transform used for movement")]
    [SerializeField] private Transform _rightHand;

    [Header("Home Position")]
    [Tooltip("Where the right hand idles between tasks")]
    [SerializeField] private Transform _homePosition;

    [Header("Task List")]
    [SerializeField] private List<GhostPickupTask> _tasks = new List<GhostPickupTask>();

    [Header("Timing")]
    [SerializeField] private float _idlePauseDuration = 0.8f;
    [SerializeField] private float _approachHoverDuration = 0.3f;

    [Header("Movement")]
    [SerializeField] private AnimationCurve _moveCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    // ─── Private State ────────────────────────────────────────────────────────

    private int _currentTaskIndex = 0;
    private bool _isHolding = false;

    // ─── Unity Lifecycle ──────────────────────────────────────────────────────

    private void Start()
    {
        if (_tasks == null || _tasks.Count == 0)
        {
            Debug.LogWarning("[GhostPlayer] No tasks assigned.");
            return;
        }

        if (_rightHandInteractor == null)
        {
            Debug.LogError("[GhostPlayer] XRDirectInteractor not assigned!");
            return;
        }

        if (_homePosition != null)
            _rightHand.position = _homePosition.position;

        StartCoroutine(RunLoop());
    }

    // ─── Main Loop ────────────────────────────────────────────────────────────

    private IEnumerator RunLoop()
    {
        while (true)
        {
            GhostPickupTask task = _tasks[_currentTaskIndex];

            if (!ValidateTask(task))
            {
                AdvanceTask();
                yield return null;
                continue;
            }

            // Phase 1: Move hand to pickup point
            yield return StartCoroutine(
                MoveHandTo(_rightHand, task.PickupPoint.position, task.ApproachDuration)
            );

            // Phase 2: Hover pause
            yield return new WaitForSeconds(_approachHoverDuration);

            // Phase 3: Grab via XRI — fires selectEntered on the interactable
            GrabItem(task.Item);

            // Short wait to let XRI process the grab this frame before we move
            yield return null;

            // Phase 4: Move hand (XRI carries the item automatically)
            yield return StartCoroutine(
                MoveHandTo(_rightHand, task.PlacePoint.position, task.PlaceDuration)
            );

            // Phase 5: Hover at destination
            yield return new WaitForSeconds(_approachHoverDuration);

            // Phase 6: Release via XRI — fires selectExited on the interactable
            ReleaseItem();

            // Short wait to let XRI process the release before we move away
            yield return null;

            // Phase 7: Return home
            if (_homePosition != null)
            {
                yield return StartCoroutine(
                    MoveHandTo(_rightHand, _homePosition.position, task.ApproachDuration * 0.8f)
                );
            }

            // Phase 8: Idle
            yield return new WaitForSeconds(_idlePauseDuration);

            AdvanceTask();
        }
    }

    // ─── Movement ─────────────────────────────────────────────────────────────

    private IEnumerator MoveHandTo(Transform hand, Vector3 target, float duration)
    {
        Vector3 startPos = hand.position;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            hand.position = Vector3.Lerp(startPos, target, _moveCurve.Evaluate(t));
            yield return null;
        }

        hand.position = target;
    }

    // ─── Grab / Release via XRI ───────────────────────────────────────────────

    /// <summary>
    /// Calls StartManualInteraction on the ghost interactor.
    /// This runs the full XRI select pipeline:
    ///   → XRInteractionManager.SelectEnter()
    ///   → XRGrabInteractable.OnSelectEntered()
    ///   → interactable.selectEntered fires (all listeners notified)
    ///   → XRI handles attachment/parenting internally
    /// No Rigidbody or transform manipulation needed here.
    /// </summary>
    private void GrabItem(Transform item)
    {
        if (item == null || _isHolding) return;

        // Get the XRI interactable interface — works on XRGrabInteractable
        // and any custom interactable that implements IXRSelectInteractable
        UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable interactable = item.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable>();

        if (interactable == null)
        {
            Debug.LogWarning($"[GhostPlayer] {item.name} has no IXRSelectInteractable. Cannot XRI-grab.");
            return;
        }

        // StartManualInteraction bypasses controller input checks and directly
        // enters the XRI select state — identical result to a real controller grab
        _rightHandInteractor.StartManualInteraction(interactable);
        _isHolding = true;

        Debug.Log($"[GhostPlayer] XRI Grabbed: {item.name}");
    }

    /// <summary>
    /// Calls EndManualInteraction, which runs the full XRI deselect pipeline:
    ///   → XRInteractionManager.SelectExit()
    ///   → XRGrabInteractable.OnSelectExited()
    ///   → interactable.selectExited fires (all listeners notified)
    ///   → XRI releases attachment, re-enables Rigidbody physics
    /// The item drops at wherever the ghost hand currently is — so move the
    /// hand to the place point BEFORE calling this.
    /// </summary>
    private void ReleaseItem()
    {
        if (!_isHolding) return;

        _rightHandInteractor.EndManualInteraction();
        _isHolding = false;

        Debug.Log("[GhostPlayer] XRI Released item.");
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private bool ValidateTask(GhostPickupTask task)
    {
        if (task.Item == null || task.PickupPoint == null || task.PlacePoint == null)
        {
            Debug.LogWarning($"[GhostPlayer] Task {_currentTaskIndex} has null references, skipping.");
            return false;
        }

        // Don't try to grab an item already selected by the real player
        UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable interactable = task.Item.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable>();
        if (interactable != null && interactable.isSelected)
        {
            Debug.Log($"[GhostPlayer] {task.Item.name} is already held by real player, skipping.");
            return false;
        }

        return true;
    }

    private void AdvanceTask()
    {
        _currentTaskIndex = (_currentTaskIndex + 1) % _tasks.Count;
    }
}

/// <summary>
/// A single pickup-and-place task for the ghost player.
/// </summary>
[System.Serializable]
public class GhostPickupTask
{
    public Transform Item;
    public Transform PickupPoint;
    public Transform PlacePoint;
    public float ApproachDuration = 1.2f;
    public float PlaceDuration = 1.5f;
}