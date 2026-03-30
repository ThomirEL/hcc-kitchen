using System.Collections.Generic;
using UnityEngine;
using System.IO;
using UnityEditor.UIElements;


/// <summary>
/// Central manager for the kitchen highlighting experiment.
///
/// Two independent settings:
///
///   HighlightType  — HOW items are highlighted:
///     Circle       → 3D billboard ring surrounding the object
///     Arrow        → downward arrow just above the object
///     Outline      → white silhouette contour lines
///
///   TargetingMode  — WHICH items are highlighted at any time:
///     All          → every remaining target shown at once
///     Sequential   → one target at a time, nearest-neighbour order from player
///     Subset       → N targets at a time (default 5), nearest-neighbour order
///
/// Highlighting is OFF by default on game start.
/// Call StartTrial() or use ExperimentController to begin.
///
/// Attach to: an empty HighlightManager GameObject in the scene
/// </summary>
public class KitchenHighlightManager : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────
    // ENUMS
    // ─────────────────────────────────────────────────────────────────────

    public enum HighlightType
    {
        Circle,
        Arrow,
        Outline,
    }

    public enum TargetingMode
    {
        All,
        Sequential,
        Subset,
    }

    // ─────────────────────────────────────────────────────────────────────
    // INSPECTOR
    // ─────────────────────────────────────────────────────────────────────

    [Header("━━ Target List ━━━━━━━━━━━━━━━━━━━━━━━━")]
    [Tooltip("All kitchen items that can be highlighted. Assign in the Inspector.")]
    public List<ItemHighlight> targets = new List<ItemHighlight>();

    [Header("━━ Highlight Type ━━━━━━━━━━━━━━━━━━━━━")]
    public HighlightType highlightType = HighlightType.Circle;

    [Header("━━ Targeting Mode ━━━━━━━━━━━━━━━━━━━━━")]
    public TargetingMode targetingMode = TargetingMode.All;

    [Tooltip("Number of items shown simultaneously in Subset mode.")]
    [Min(1)]
    public int subsetSize = 5;

    [Header("━━ Appearance ━━━━━━━━━━━━━━━━━━━━━━━━━")]
    public Color highlightColour = new Color(0f, 0.85f, 1f, 1f);

    [Header("━━ Player Reference ━━━━━━━━━━━━━━━━━━━")]
    [Tooltip("Assign your XR Origin. Used for nearest-neighbour ordering.")]
    public Transform playerTransform;

    [SerializeField]
    [Header("Tags to include for trials")]
    private string[] tags; // Array of TagField references for validation (optional)

    // ─────────────────────────────────────────────────────────────────────
    // INTERNAL STATE
    // ─────────────────────────────────────────────────────────────────────

    private List<ItemHighlight>    _orderedTargets = new List<ItemHighlight>();
    private HashSet<ItemHighlight> _collectedItems = new HashSet<ItemHighlight>();

    // Guards all highlighting — nothing shows until StartTrial() is called
    private bool _trialActive = false;

    private HighlightType _lastHighlightType = (HighlightType)(-1);
    private TargetingMode _lastTargetingMode = (TargetingMode)(-1);

    // ─────────────────────────────────────────────────────────────────────
    // LIFECYCLE
    // ─────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        PushColourToAllItems();
    }

    private void Start()
    {
        CreateTrialList();
        // Highlights OFF by default — do not call StartTrial() here
        ClearAllHighlights();
    }

    private void CreateTrialList()
    {
        // For this experiment, we want to read the permutations from the CSV file
        string trialPermutation = ReadPermutations();

        // Find managers to get their list of items
        CanManager canManager = FindFirstObjectByType<CanManager>();
        DryGoodsManager dryGoodsManager = FindFirstObjectByType<DryGoodsManager>();
        SpiceManager spiceManager = FindFirstObjectByType<SpiceManager>();

        if (canManager == null || dryGoodsManager == null || spiceManager == null)
        {
            Debug.LogError("[Highlight] Could not find one or more item managers in the scene.");
            return;
        }

        SpiceDefinition[] spiceDefs = spiceManager.spices;
        CanDefinition[]   canDefs   = canManager.cans;
        DryGoodsDefinition[] dryDefs = dryGoodsManager.dryGoods;

        // Parse the permutation string to get the indices for each item type
        string[] indices = trialPermutation.Split(',');
        if (indices.Length != 3)
        {
            Debug.LogError($"[Highlight] Invalid permutation format: {trialPermutation}");
            return;
        }

        int canIndex   = int.Parse(indices[0]);
        int dryIndex   = int.Parse(indices[1]);
        int spiceIndex = int.Parse(indices[2]);

        targets = new List<ItemHighlight>();
        // Add the selected can, dry good, and spice to the trial targets
        for (int i = 0; i < canDefs.Length; i++)
        {
            if (canDefs[i].groupIndex == canIndex && canDefs[i].canObject != null)
                targets.Add(canDefs[i].canObject.GetComponent<ItemHighlight>());
        }
        for (int i = 0; i < dryDefs.Length; i++)
        {
            if (dryDefs[i].groupIndex == dryIndex && dryDefs[i].boxObject != null)
                targets.Add(dryDefs[i].boxObject.GetComponent<ItemHighlight>());
        }
        for (int i = 0; i < spiceDefs.Length; i++)
        {
            if (spiceDefs[i].groupIndex == spiceIndex && spiceDefs[i].jarObject != null)
                targets.Add(spiceDefs[i].jarObject.GetComponent<ItemHighlight>());
        }

        // Add 4 random fridge items
        AddRandomTaggedItems("fridge_item", 4);
        // Add 4 random kitchen items
        AddRandomTaggedItems("kitchen_item", 4);
    }

    private void AddRandomTaggedItems(string tag, int count)
    {
        GameObject[] allTagged = GameObject.FindGameObjectsWithTag(tag);
        if (allTagged.Length == 0)
        {
            Debug.LogWarning($"[Highlight] No items found with tag '{tag}'.");
            return;
        }

        // Shuffle and pick up to count items that have ItemHighlight
        List<GameObject> shuffled = new List<GameObject>(allTagged);
        ShuffleList(shuffled);

        int added = 0;
        foreach (GameObject obj in shuffled)
        {
            if (added >= count) break;
            ItemHighlight ih = obj.GetComponent<ItemHighlight>();
            if (ih != null && !targets.Contains(ih))
            {
                targets.Add(ih);
                added++;
            }
        }

        Debug.Log($"[Highlight] Added {added} random items with tag '{tag}'.");
    }

    private void ShuffleList<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
    private string ReadPermutations()
    {

        // Get permutations.csv file
        string path = Path.Combine(Application.dataPath, "Experiment Data");
        string fullPath = Path.Combine(path, "permutations.csv");
        if (!File.Exists(fullPath))
        {
            Debug.LogError($"[Highlight] permutations.csv not found at path: {fullPath}");
            return "";
        }

        // Parse CSV to get list of permutations
        List<(int, int, int)> permutations = new List<(int, int, int)>();
        string[] lines = File.ReadAllLines(fullPath);
        Debug.Log($"[Highlight] Loaded permutations.csv with {lines.Length - 1} permutations.");
        int randomIndex = Random.Range(1, lines.Length); // Skip header line
        
        return lines[randomIndex];

    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying || !_trialActive) return;
        bool typeChanged = highlightType != _lastHighlightType;
        bool modeChanged = targetingMode != _lastTargetingMode;
        if (typeChanged || modeChanged)
            RefreshHighlights();
    }
#endif

    // ─────────────────────────────────────────────────────────────────────
    // PUBLIC API
    // ─────────────────────────────────────────────────────────────────────

    public void SetHighlightType(HighlightType type)
    {
        highlightType = type;
        if (_trialActive) RefreshHighlights();
    }

    public void SetTargetingMode(TargetingMode mode)
    {
        targetingMode = mode;
        if (_trialActive) RefreshHighlights();
    }

    public void SetCondition(HighlightType type, TargetingMode mode)
    {
        highlightType = type;
        targetingMode = mode;
        if (_trialActive) RefreshHighlights();
    }

    /// <summary>
    /// Call when the participant grabs/collects a target item.
    /// The item is hidden and removed from the active pool.
    /// Advances Sequential or Subset automatically.
    /// </summary>
    public void OnItemCollected(ItemHighlight item)
    {
        if (item == null || !targets.Contains(item)) return;
        if (_collectedItems.Contains(item)) return;
        if (!_trialActive) return;

        _collectedItems.Add(item);

        // Clear visual highlights then hide the item
        if (item.disappearOnCollect) {
            item.ClearAll();
            item.gameObject.SetActive(false);
        }
        
        Debug.Log($"[Highlight] Collected: {item.gameObject.name} " +
                  $"({_collectedItems.Count}/{targets.Count})");

        if (_collectedItems.Count >= targets.Count)
        {
            _trialActive = false;
            Debug.Log("[Highlight] Trial complete — all targets collected.");
            return;
        }

        RefreshHighlights();
    }

    /// <summary>
    /// Starts a new trial. Re-activates all item GameObjects, resets state,
    /// rebuilds ordering, then begins highlighting.
    /// </summary>
    public void StartTrial(List<ItemHighlight> newTargets = null)
    {
        if (newTargets != null)
            targets = newTargets;

        _collectedItems.Clear();
        _trialActive = true;

        // Re-show all items that were hidden by previous trial
        foreach (var t in targets)
            if (t != null) t.gameObject.SetActive(true);

        PushColourToAllItems();
        ClearAllHighlights();
        BuildOrderedList();
        RefreshHighlights();

        Debug.Log($"[Highlight] Trial started | Type: {highlightType} | " +
                  $"Mode: {targetingMode} | Targets: {targets.Count}");
    }

    /// <summary>Stops trial and clears all highlights. Items stay visible.</summary>
    public void StopTrial()
    {
        _trialActive = false;
        ClearAllHighlights();
        Debug.Log("[Highlight] Trial stopped.");
    }

    public bool IsTrialComplete() => !_trialActive && _collectedItems.Count > 0
                                     || _collectedItems.Count >= targets.Count;
    public bool IsTrialActive()   => _trialActive;
    public int  RemainingCount()  => Mathf.Max(0, targets.Count - _collectedItems.Count);

    /// <summary>Returns the next uncollected item in ordered sequence, or null.</summary>
    public ItemHighlight GetNextTarget()
    {
        var remaining = GetRemainingOrdered();
        return remaining.Count > 0 ? remaining[0] : null;
    }

    // ─────────────────────────────────────────────────────────────────────
    // REFRESH
    // ─────────────────────────────────────────────────────────────────────

    private void RefreshHighlights()
    {
        ClearAllHighlights();
        _lastHighlightType = highlightType;
        _lastTargetingMode = targetingMode;

        if (!_trialActive) return;

        foreach (var item in GetItemsToHighlight())
            ApplyHighlightType(item, highlightType);
    }

    private List<ItemHighlight> GetItemsToHighlight()
    {
        List<ItemHighlight> remaining = GetRemainingOrdered();

        switch (targetingMode)
        {
            case TargetingMode.All:
                return remaining;

            case TargetingMode.Sequential:
                return remaining.Count > 0
                    ? new List<ItemHighlight> { remaining[0] }
                    : new List<ItemHighlight>();

            case TargetingMode.Subset:
                int count = Mathf.Min(subsetSize, remaining.Count);
                return remaining.GetRange(0, count);

            default:
                return new List<ItemHighlight>();
        }
    }

    private void ApplyHighlightType(ItemHighlight item, HighlightType type)
    {
        switch (type)
        {
            case HighlightType.Circle:  item.SetCircle(true);  break;
            case HighlightType.Arrow:   item.SetArrow(true);   break;
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // NEAREST-NEIGHBOUR
    // ─────────────────────────────────────────────────────────────────────

    private void BuildOrderedList()
    {
        _orderedTargets.Clear();
        if (targets.Count == 0) return;

        if (playerTransform == null)
        {
            _orderedTargets.AddRange(targets);
            Debug.LogWarning("[Highlight] No playerTransform — using Inspector order.");
            return;
        }

        var     unvisited  = new List<ItemHighlight>(targets);
        Vector3 currentPos = playerTransform.position;

        while (unvisited.Count > 0)
        {
            ItemHighlight nearest     = null;
            float         nearestDist = float.MaxValue;

            foreach (var item in unvisited)
            {
                if (item == null) continue;
                float d = Vector3.Distance(currentPos, item.transform.position);
                if (d < nearestDist) { nearestDist = d; nearest = item; }
            }

            if (nearest == null) break;
            _orderedTargets.Add(nearest);
            currentPos = nearest.transform.position;
            unvisited.Remove(nearest);
        }
    }

    public List<ItemHighlight> GetRemainingOrdered()
    {
        var remaining = new List<ItemHighlight>();
        foreach (var item in _orderedTargets)
            if (item != null && !_collectedItems.Contains(item))
                remaining.Add(item);
        return remaining;
    }

    // ─────────────────────────────────────────────────────────────────────
    // HELPERS
    // ─────────────────────────────────────────────────────────────────────

    private void ClearAllHighlights()
    {
        foreach (var t in targets)
            if (t != null) t.ClearAll();
    }

    private void PushColourToAllItems()
    {
        foreach (var t in targets)
            if (t != null) t.SetColour(highlightColour);
    }
}