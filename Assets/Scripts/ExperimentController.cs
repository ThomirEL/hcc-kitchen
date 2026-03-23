using UnityEngine;

/// <summary>
/// Experiment controller — configure and run trials entirely from the Inspector.
/// Attach to any GameObject in the scene alongside the HighlightManager.
/// </summary>
public class ExperimentController : MonoBehaviour
{
    [Header("━━ References ━━━━━━━━━━━━━━━━━━━━━━━━━")]
    public KitchenHighlightManager highlightManager;

    [Header("━━ Condition Setup ━━━━━━━━━━━━━━━━━━━━")]
    public KitchenHighlightManager.HighlightType highlightType
        = KitchenHighlightManager.HighlightType.Circle;

    public KitchenHighlightManager.TargetingMode targetingMode
        = KitchenHighlightManager.TargetingMode.All;

    [Tooltip("Only used when Targeting Mode is Subset.")]
    [Min(1)]
    public int subsetSize = 5;

    // ── Called by Inspector buttons ───────────────────────────────────────

    /// <summary>
    /// Applies current condition settings and starts a fresh trial.
    /// All previously hidden items are re-shown. Highlighting begins immediately.
    /// </summary>
    public void StartTrial()
    {
        if (!ValidateManager()) return;

        highlightManager.subsetSize = subsetSize;
        highlightManager.SetCondition(highlightType, targetingMode);
        highlightManager.StartTrial();

        Debug.Log($"[Experiment] Trial started | {highlightType} + {targetingMode}" +
                  (targetingMode == KitchenHighlightManager.TargetingMode.Subset
                      ? $" (subset: {subsetSize})" : ""));
    }

    /// <summary>
    /// Changes condition mid-trial without resetting collected items or re-showing
    /// hidden objects. Useful for switching between conditions in the same trial.
    /// </summary>
    public void ApplyConditionOnly()
    {
        if (!ValidateManager()) return;

        highlightManager.subsetSize = subsetSize;
        highlightManager.SetCondition(highlightType, targetingMode);

        Debug.Log($"[Experiment] Condition changed → {highlightType} + {targetingMode}");
    }

    /// <summary>
    /// Clears all highlights and stops the trial. Items remain visible.
    /// </summary>
    public void StopTrial()
    {
        if (!ValidateManager()) return;
        highlightManager.StopTrial();
        Debug.Log("[Experiment] Trial stopped.");
    }

    /// <summary>
    /// Simulates collecting the next target in the sequence.
    /// Uses GetNextTarget() so it always picks the correct next item —
    /// not just the first item in the list regardless of what's been collected.
    /// </summary>
    public void SimulateCollectNext()
    {
        if (!ValidateManager()) return;

        if (!highlightManager.IsTrialActive())
        {
            Debug.Log("[Experiment] No trial active — click Start Trial first.");
            return;
        }

        if (highlightManager.IsTrialComplete())
        {
            Debug.Log("[Experiment] Trial already complete.");
            return;
        }

        // GetNextTarget() returns the first item in the remaining ordered list
        // This is always correct regardless of which items have already been collected
        ItemHighlight next = highlightManager.GetNextTarget();

        if (next == null)
        {
            Debug.Log("[Experiment] No next target found.");
            return;
        }

        Debug.Log($"[Experiment] Simulating collect: {next.gameObject.name}");
        highlightManager.OnItemCollected(next);
    }

    private bool ValidateManager()
    {
        if (highlightManager != null) return true;
        Debug.LogError("[Experiment] No KitchenHighlightManager assigned!");
        return false;
    }
}