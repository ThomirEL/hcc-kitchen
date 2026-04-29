using System.Collections.Generic;
using UnityEngine;
using System.IO;
using TMPro;
using System;
using JetBrains.Annotations;
using System.Linq;

/// <summary>
/// Central manager for the kitchen highlighting experiment.
///
/// Two independent settings:
///
///   HighlightType  — HOW items are highlighted:
///     Circle       → 3D billboard ring surrounding the object
///     Arrow        → downward arrow just above the object
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

    [Header("━━ Appearance ━━━━━━━━━━━━━━━━━━━━━━━━━")]
    public Color highlightColour = new Color(0f, 0.85f, 1f, 1f);

    [Header("━━ Player Reference ━━━━━━━━━━━━━━━━━━━")]
    [Tooltip("Assign your XR Origin. Used for nearest-neighbour ordering.")]
    public Transform playerTransform;

    [Header("━━ Mini Clones ━━━━━━━━━━━━━━━━━━━━━━━━")]
    [Tooltip("Uniform scale applied to each mini clone relative to its original.")]
    [SerializeField] private float miniCloneScale = 0.05f;

    [Tooltip("Offset in TMP local space to place the mini clone relative to the text line centre. " +
             "Positive X moves it to the right of the text on the panel.")]
    [SerializeField] private Vector3 miniCloneLocalOffset = new Vector3(0.12f, 0f, -0.01f);

    [SerializeField]
    [Header("Tags to include for trials")]
    private string[] tags; // Array of TagField references for validation (optional)

    [SerializeField]
    private TextMeshProUGUI menuTextFirstPage; // For debugging: formatted string of current targets

    [SerializeField]
    private TextMeshProUGUI menuTextSecondPage; // For debugging: formatted string of current targets

    // ─────────────────────────────────────────────────────────────────────
    // INTERNAL STATE
    // ─────────────────────────────────────────────────────────────────────

    private bool _initializedLogging = false;
    private List<ItemHighlight>    _orderedTargets = new List<ItemHighlight>();
    private HashSet<ItemHighlight> _collectedItems = new HashSet<ItemHighlight>();
    private List<string[]> _targetGroups = new List<string[]>(); // Store for text updates

    // Guards all highlighting — nothing shows until StartTrial() is called
    private bool _trialActive = false;

    private HighlightType _lastHighlightType = (HighlightType)(-1);
    private TargetingMode _lastTargetingMode = (TargetingMode)(-1);

    // Tracks all spawned mini clones so they can be destroyed on next CreateTrialList call
    private List<GameObject> _miniClones = new List<GameObject>();

    private static readonly string[] ColumnNamesStudy = { "U_Frame", "HighlightType", "TargetingMode", "RemainingTargets", "CollectedItem", "CollectedItemTag", "CollectedItemPosition", "WasHighlighted"};

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
        Logging.Logger.ParticipantIDSet.AddListener(OnParticipantIDSet);
    }

    private void OnParticipantIDSet()
    {
        Logging.Logger.RecordItemHighlights(ColumnNamesStudy);
        _initializedLogging = true;
        Debug.Log($"[Highlight] Logging initialized for participant {Logging.Logger.participantID}");
    }

    private void Log(ItemHighlight item)
    {
        if (!_initializedLogging)
        {
            Debug.LogWarning("[Highlight] Log() called before participant ID was set — skipping.");
            return;
        }
        // private static readonly string[] ColumnNamesStudy = { "U_Frame", "HighlightType", "TargetingMode", "RemainingTargets", "CollectedItem", "CollectedItemTag", "CollectedItemPosition", "WasHighlighted"};
        string[] msg = new string[ColumnNamesStudy.Length];
        msg[0] = Time.frameCount.ToString();    
        msg[1] = highlightType.ToString();
        msg[2] = targetingMode.ToString();
        msg[3] = RemainingCount().ToString();
        msg[4] = item != null ? item.gameObject.name : "null";
        msg[5] = item != null ? item.gameObject.tag : "null";
        msg[6] = item != null ? item.transform.position.ToString("F3") : "null";
        msg[7] = item != null && GetItemsToHighlight().Contains(item) ? "True" : "False";

        Logging.Logger.RecordItemHighlights(msg);
    }

    public void CreateTrialList(int permutationIndex = -1)
    {
        // Destroy any mini clones from a previous call
        ClearMiniClones();

        // For this experiment, we want to read the permutations from the CSV file
        string trialPermutation = ReadPermutations(permutationIndex);

        // Use singleton instances
        CanManager canManager = CanManager.Instance;
        DryGoodsManager dryGoodsManager = DryGoodsManager.Instance;
        SpiceManager spiceManager = SpiceManager.Instance;

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

        // Collect items from each group
        List<ItemHighlight> canItems = new List<ItemHighlight>();
        List<ItemHighlight> dryItems = new List<ItemHighlight>();
        List<ItemHighlight> spiceItems = new List<ItemHighlight>();

        for (int i = 0; i < canDefs.Length; i++)
        {
            if (canDefs[i].groupIndex == canIndex && canDefs[i].canObject != null)
                canItems.Add(canDefs[i].canObject.GetComponent<ItemHighlight>());
        }
        for (int i = 0; i < dryDefs.Length; i++)
        {
            if (dryDefs[i].groupIndex == dryIndex && dryDefs[i].boxObject != null)
                dryItems.Add(dryDefs[i].boxObject.GetComponent<ItemHighlight>());
        }
        for (int i = 0; i < spiceDefs.Length; i++)
        {
            if (spiceDefs[i].groupIndex == spiceIndex && spiceDefs[i].jarObject != null)
                spiceItems.Add(spiceDefs[i].jarObject.GetComponent<ItemHighlight>());
        }

        // Add 4 random fridge items
        List<ItemHighlight> fridgeItems = AddRandomTaggedItems("fridge_item", 4);
        // Add 4 random kitchen items
        List<ItemHighlight> kitchenItems = AddRandomTaggedItems("kitchen_item", 4);

        // Randomize the order of the three groups
        List<List<ItemHighlight>> groupOrder = new List<List<ItemHighlight>> { canItems, dryItems, spiceItems, fridgeItems, kitchenItems };
        ShuffleList(groupOrder);

        // Find positions of each group after shuffling
        int canPosition   = groupOrder.IndexOf(canItems);
        int dryPosition   = groupOrder.IndexOf(dryItems);
        int spicePosition = groupOrder.IndexOf(spiceItems);

        // Add items in randomized group order
        foreach (var group in groupOrder)
            targets.AddRange(group);

        _targetGroups = groupOrder
            .Select(group => group.Select(item => item.gameObject.name).ToArray())
            .ToList();

        // Update TMP text — first 3 groups on page 1, last 2 on page 2
        if (menuTextFirstPage != null)
            menuTextFirstPage.text = FormatTargetList(_targetGroups.Take(3).ToList());
        if (menuTextSecondPage != null)
            menuTextSecondPage.text = FormatTargetList(_targetGroups.Skip(3).Take(2).ToList());

        // ── Decide which TMP holds which group ──────────────────────────
        Vector3 GetWorldPosForGroup(int groupIndex)
        {
            if (groupIndex < 3)
                return GetGroupCenterFromTMP(menuTextFirstPage, groupIndex);
            else
                return GetGroupCenterFromTMP(menuTextSecondPage, groupIndex - 3);
        }

        Vector3 canPos   = GetWorldPosForGroup(canPosition);
        Vector3 dryPos   = GetWorldPosForGroup(dryPosition);
        Vector3 spicePos = GetWorldPosForGroup(spicePosition);

        canManager.InstantiateBasicGroupGameObjects(canIndex, canPos);
        dryGoodsManager.InstantiateBasicGroupGameObjects(dryIndex, dryPos);
        spiceManager.InstantiateBasicGroupGameObjects(spiceIndex, spicePos);

        // ── Spawn mini clones for fridge and kitchen items ───────────────
        // Iterate every group; for fridge/kitchen groups find the correct TMP page
        for (int groupIdx = 0; groupIdx < groupOrder.Count; groupIdx++)
        {
            List<ItemHighlight> group = groupOrder[groupIdx];

            // Cans / dry goods / spices already have their own visual via InstantiateBasicGroupGameObjects
            bool isFridgeGroup  = group == fridgeItems;
            bool isKitchenGroup = group == kitchenItems;
            if (!isFridgeGroup && !isKitchenGroup) continue;

            // Determine which TMP page this group ended up on
            TextMeshProUGUI targetTMP = groupIdx < 3 ? menuTextFirstPage : menuTextSecondPage;

            //if (targetTMP != null)
                //SpawnMiniClonesForGroup(group, targetTMP);
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // MINI CLONE HELPERS
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Destroys all previously spawned mini clones.
    /// Called at the start of CreateTrialList so re-runs don't stack clones.
    /// </summary>
    private void ClearMiniClones()
    {
        foreach (var clone in _miniClones)
        {
            if (clone != null)
                Destroy(clone);
        }
        _miniClones.Clear();
    }

    /// <summary>
    /// For each item in the group, spawns a scaled-down clone of the scene object
    /// positioned next to its name line in the given TMP.
    /// </summary>
    private void SpawnMiniClonesForGroup(List<ItemHighlight> group, TextMeshProUGUI tmp)
    {
        // Force the mesh to be up to date before querying character positions
        tmp.ForceMeshUpdate();

        foreach (ItemHighlight item in group)
        {
            if (item == null) continue;

            Vector3 lineWorldPos = GetWorldPosForLineContaining(tmp, item.gameObject.name);

            // Apply a local-space offset (converted to world space via the TMP's transform)
            Vector3 worldOffset = tmp.transform.TransformVector(miniCloneLocalOffset);
            Vector3 spawnPos    = lineWorldPos + worldOffset;

            GameObject clone = CreateMiniClone(item.gameObject, spawnPos, miniCloneScale);
            if (clone != null)
                _miniClones.Add(clone);
        }
    }

    /// <summary>
    /// Finds the world-space centre of the TMP line that contains <paramref name="itemName"/>.
    /// Falls back to the TMP transform position if the text cannot be located.
    /// </summary>
    private Vector3 GetWorldPosForLineContaining(TextMeshProUGUI tmp, string itemName)
    {
        tmp.ForceMeshUpdate();
        TMP_TextInfo textInfo = tmp.textInfo;

        // Find where this item name starts in the raw text string
        int charIndex = tmp.text.IndexOf(itemName, StringComparison.Ordinal);
        if (charIndex < 0)
        {
            Debug.LogWarning($"[MiniClone] Could not find '{itemName}' in TMP text.");
            return tmp.transform.position;
        }

        // Identify which rendered line contains that character index
        int targetLine = -1;
        for (int i = 0; i < textInfo.lineCount; i++)
        {
            TMP_LineInfo li = textInfo.lineInfo[i];
            int lineEnd = li.firstCharacterIndex + li.characterCount;
            if (charIndex >= li.firstCharacterIndex && charIndex < lineEnd)
            {
                targetLine = i;
                break;
            }
        }

        if (targetLine < 0)
        {
            Debug.LogWarning($"[MiniClone] Could not map '{itemName}' to a TMP line.");
            return tmp.transform.position;
        }

        TMP_LineInfo targetLineInfo = textInfo.lineInfo[targetLine];

        // Compute the bounding box of visible characters on that line
        Vector3 min = Vector3.positiveInfinity;
        Vector3 max = Vector3.negativeInfinity;

        int lastChar = targetLineInfo.firstCharacterIndex + targetLineInfo.characterCount;
        for (int j = targetLineInfo.firstCharacterIndex; j < lastChar && j < textInfo.characterCount; j++)
        {
            if (!textInfo.characterInfo[j].isVisible) continue;
            min = Vector3.Min(min, textInfo.characterInfo[j].bottomLeft);
            max = Vector3.Max(max, textInfo.characterInfo[j].topRight);
        }

        // If no visible characters were found (e.g. blank line guard), use baseline
        if (min == Vector3.positiveInfinity)
        {
            float baseline = targetLineInfo.baseline;
            min = new Vector3(targetLineInfo.lineExtents.min.x, baseline, 0f);
            max = new Vector3(targetLineInfo.lineExtents.max.x, baseline, 0f);
        }

        Vector3 localCentre = (min + max) / 2f;
        return tmp.transform.TransformPoint(localCentre);
    }

    /// <summary>
    /// Instantiates a stripped-down clone of <paramref name="original"/> at
    /// <paramref name="worldPos"/>. All colliders, rigidbodies, and MonoBehaviour
    /// components are disabled/made kinematic so the clone is purely visual.
    /// </summary>
    private GameObject CreateMiniClone(GameObject original, Vector3 worldPos, float scale)
    {
        if (original == null) return null;

        // Clone with the original's world rotation so the model faces a natural direction
        GameObject clone = Instantiate(original, worldPos, original.transform.rotation);

        // Scale uniformly relative to the original's existing scale
        clone.transform.localScale = original.transform.localScale * scale;

        // ── Strip interactive components ─────────────────────────────────

        // Disable all MonoBehaviours (ItemHighlight, XR interactables, custom scripts, etc.)
        foreach (MonoBehaviour mb in clone.GetComponentsInChildren<MonoBehaviour>(includeInactive: true))
            mb.enabled = false;

        // Disable colliders so the clone doesn't interfere with physics or XR raycasts
        foreach (Collider col in clone.GetComponentsInChildren<Collider>(includeInactive: true))
            col.enabled = false;

        // Make any Rigidbody kinematic so gravity / physics don't move it
        foreach (Rigidbody rb in clone.GetComponentsInChildren<Rigidbody>(includeInactive: true))
        {
            rb.isKinematic = true;
            rb.detectCollisions = false;
        }

        // Give the clone a descriptive name for easier debugging in the hierarchy
        clone.name = $"[MiniClone] {original.name}";

        return clone;
    }

    // ─────────────────────────────────────────────────────────────────────
    // EXISTING TMP HELPERS (unchanged)
    // ─────────────────────────────────────────────────────────────────────

    private Vector3 GetGroupCenterFromTMP(TextMeshProUGUI tmp, int groupIndex)
    {
        tmp.ForceMeshUpdate();
        TMP_TextInfo textInfo = tmp.textInfo;

        List<(int start, int end)> groups = new List<(int, int)>();

        int currentStart = -1;

        // Build groups based on empty lines
        for (int i = 0; i < textInfo.lineCount; i++)
        {
            TMP_LineInfo line = textInfo.lineInfo[i];

            string lineText = tmp.text.Substring(
                line.firstCharacterIndex,
                line.characterCount
            ).Trim();

            if (string.IsNullOrEmpty(lineText))
            {
                if (currentStart != -1)
                {
                    groups.Add((currentStart, i - 1));
                    currentStart = -1;
                }
            }
            else
            {
                if (currentStart == -1)
                    currentStart = i;
            }
        }

        if (currentStart != -1)
            groups.Add((currentStart, textInfo.lineCount - 1));

        if (groupIndex >= groups.Count)
        {
            Debug.LogWarning($"TMP group index {groupIndex} out of range.");
            return tmp.transform.position;
        }

        var (startLine, endLine) = groups[groupIndex];

        Vector3 min = Vector3.positiveInfinity;
        Vector3 max = Vector3.negativeInfinity;

        for (int i = startLine; i <= endLine; i++)
        {
            TMP_LineInfo line = textInfo.lineInfo[i];

            for (int j = line.firstCharacterIndex; j <= line.lastCharacterIndex; j++)
            {
                if (!textInfo.characterInfo[j].isVisible) continue;

                var charInfo = textInfo.characterInfo[j];

                min = Vector3.Min(min, charInfo.bottomLeft);
                max = Vector3.Max(max, charInfo.topRight);
            }
        }

        Vector3 localCenter = (min + max) / 2f;
        // Push to the right
        localCenter += new Vector3(0.3f, 0.025f, 0.2f);

        return tmp.transform.TransformPoint(localCenter);
    }

    private string FormatTargetList(List<string[]> targetGroups)
    {
        // Returns a string that is one line per item inside each group, with groups separated by blank lines
        string result = "";
        foreach (var group in targetGroups)
        {
            foreach (var itemName in group)
                result += itemName + "\n";
            result += "\n"; // Blank line between groups
        }
        return result;
    }

    private string FormatTargetListWithCollected(List<string[]> targetGroups)
    {
        // Returns a string with collected items in green color
        string result = "";
        foreach (var group in targetGroups)
        {
            foreach (var itemName in group)
            {
                bool isCollected = _collectedItems.Any(item => item.gameObject.name == itemName);
                if (isCollected)
                    result += $"<color=#00FF00>{itemName}</color>\n";
                else
                    result += itemName + "\n";
            }
            result += "\n"; // Blank line between groups
        }
        return result;
    }

    private void UpdateTargetTextDisplay()
    {
        if (menuTextFirstPage != null)
            menuTextFirstPage.text = FormatTargetListWithCollected(_targetGroups.Take(3).ToList());
        if (menuTextSecondPage != null)
            menuTextSecondPage.text = FormatTargetListWithCollected(_targetGroups.Skip(3).Take(2).ToList());
    }

    private List<ItemHighlight> AddRandomTaggedItems(string tag, int count)
    {
        GameObject[] allTagged = GameObject.FindGameObjectsWithTag(tag);
        if (allTagged.Length == 0)
        {
            Debug.LogWarning($"[Highlight] No items found with tag '{tag}'.");
            return new List<ItemHighlight>();
        }

        List<GameObject> shuffled = new List<GameObject>(allTagged);
        ShuffleList(shuffled);

        int added = 0;
        List<ItemHighlight> addedItems = new List<ItemHighlight>();
        foreach (GameObject obj in shuffled)
        {
            if (added >= count) break;
            ItemHighlight ih = obj.GetComponent<ItemHighlight>();
            if (ih != null && !targets.Contains(ih))
            {
                addedItems.Add(ih);
                added++;
            }
        }

        return addedItems;
    }

    private void ShuffleList<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private string ReadPermutations(int permutationIndex = -1)
    {
        string path = Path.Combine(Application.persistentDataPath, "Experiment Data");
        string fullPath = Path.Combine(path, "permutations.csv");
        if (!File.Exists(fullPath))
        {
            Debug.LogError($"[Highlight] permutations.csv not found at path: {fullPath}");
            return "";
        }

        string[] lines = File.ReadAllLines(fullPath);

        if (permutationIndex < 0)
            permutationIndex = UnityEngine.Random.Range(1, lines.Length);
        else
            permutationIndex += 1; // Adjust for header row

        permutationIndex = Mathf.Clamp(permutationIndex, 1, lines.Length - 1);

        return lines[permutationIndex];
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

        Log(item);

        _collectedItems.Add(item);

        // Clear visual highlights then hide the item
        if (item.disappearOnCollect)
        {
            item.ClearAll();
            item.gameObject.SetActive(false);
        }

        Debug.Log($"[Highlight] Collected: {item.gameObject.name} " +
                  $"({_collectedItems.Count}/{targets.Count})");

        // Update text display with collected item in green
        UpdateTargetTextDisplay();

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
        switch (targetingMode)
        {
            case TargetingMode.All:
                return GetRemainingOrdered();

            case TargetingMode.Sequential:
                {
                    ItemHighlight nearest = GetNearestRemainingTarget();
                    return nearest != null
                        ? new List<ItemHighlight> { nearest }
                        : new List<ItemHighlight>();
                }

            case TargetingMode.Subset:
                return GetSubsetTargetsByTag();

            default:
                return new List<ItemHighlight>();
        }
    }

    public void EnableCollectedItems()
    {
        foreach (var item in _collectedItems)
        {
            if (item != null)
            {
                item.gameObject.SetActive(true);
                item.ClearAll();
            }
        }
        _collectedItems.Clear();
        RefreshHighlights();
    }

    /// <summary>
    /// In subset mode, highlight all remaining targets sharing the tag of the first remaining target
    /// based on the original targets list order.
    /// </summary>
    private List<ItemHighlight> GetSubsetTargetsByTag()
    {
        var remainingByTargetList = new List<ItemHighlight>();
        foreach (var item in targets)
        {
            if (item != null && !_collectedItems.Contains(item))
                remainingByTargetList.Add(item);
        }

        if (remainingByTargetList.Count == 0)
            return new List<ItemHighlight>();

        string currentTag = remainingByTargetList[0].gameObject.tag;

        var sameTag = new List<ItemHighlight>();
        foreach (var item in remainingByTargetList)
        {
            if (item != null && item.gameObject.tag == currentTag)
                sameTag.Add(item);
        }

        return sameTag;
    }

    private void ApplyHighlightType(ItemHighlight item, HighlightType type)
    {
        switch (type)
        {
            case HighlightType.Circle: item.SetCircle(true); break;
            case HighlightType.Arrow:  item.SetArrow(true);  break;
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

    /// <summary>
    /// Finds the single nearest uncollected target from the last collected item's position.
    /// For the first target, returns the first item in the list.
    /// Used for Sequential targeting mode.
    /// </summary>
    private ItemHighlight GetNearestRemainingTarget()
    {
        var remaining = GetRemainingOrdered();
        if (remaining.Count == 0)
            return null;

        if (_collectedItems.Count == 0)
            return remaining[0];

        Vector3 searchFromPos = Vector3.zero;
        bool foundLastCollected = false;

        for (int i = _orderedTargets.Count - 1; i >= 0; i--)
        {
            if (_orderedTargets[i] != null && _collectedItems.Contains(_orderedTargets[i]))
            {
                searchFromPos = _orderedTargets[i].transform.position;
                foundLastCollected = true;
                break;
            }
        }

        if (!foundLastCollected)
            return remaining[0];

        ItemHighlight nearest = null;
        float nearestDist = float.MaxValue;

        foreach (var item in remaining)
        {
            if (item == null) continue;
            float dist = Vector3.Distance(searchFromPos, item.transform.position);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = item;
            }
        }

        return nearest;
    }

    // ─────────────────────────────────────────────────────────────────────
    // HELPERS
    // ─────────────────────────────────────────────────────────────────────

    private void ClearAllHighlights()
    {
        foreach (var t in UnityEngine.Object.FindObjectsByType<ItemHighlight>(FindObjectsSortMode.None))
            if (t != null) t.ClearAll();
    }

    private void PushColourToAllItems()
    {
        foreach (var t in targets)
            if (t != null) t.SetColour(highlightColour);
    }
}