using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Builds a vertical list of (Label + Icon) rows at runtime.
/// Attach to: ItemList (the Vertical Layout Group GameObject)
/// </summary>
public class ItemListUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ThumbnailGenerator _generator;

    [Tooltip("The row prefab — Horizontal Layout Group with a TMP_Text and an Image child")]
    [SerializeField] private GameObject _rowPrefab;

    [Tooltip("Prefabs to display in the list — their names become the row labels")]
    [SerializeField] private List<GameObject> _itemPrefabs;

    private IEnumerator Start()
    {
        // Step 1 — Capture all thumbnails first
        var captureList = new List<(GameObject prefab, string name)>();
        foreach (var prefab in _itemPrefabs)
            captureList.Add((prefab, prefab.name));

        // Dictionary to collect results as they come in
        var sprites = new Dictionary<string, Sprite>();

        yield return _generator.CaptureAll(captureList, sprites);

        // Step 2 — Build the list rows now that all sprites are ready
        foreach (var prefab in _itemPrefabs)
        {
            Sprite icon = sprites.TryGetValue(prefab.name, out var s) ? s : null;
            SpawnRow(prefab.name, icon);
        }
    }

    /// <summary>
    /// Spawns a single row with a label and icon.
    /// Can also be called at any time to add rows dynamically.
    /// </summary>
    public void SpawnRow(string label, Sprite icon)
    {
        GameObject row = Instantiate(_rowPrefab, transform);
        row.name = $"Row_{label}";
        row.SetActive(true);

        // Get the TMP label — first child
        TMP_Text text = row.GetComponentInChildren<TMP_Text>();
        if (text != null)
            text.text = label;

        // Get the Image icon — find by component (will find the Image child)
        Image image = row.GetComponentInChildren<Image>();
        if (image != null)
        {
            image.sprite = icon;
            // Hide the image slot entirely if no sprite was captured
            image.gameObject.SetActive(icon != null);
        }
    }

    /// <summary>
    /// Clears all rows — useful if the list needs to be rebuilt mid-experiment.
    /// </summary>
    public void ClearList()
    {
        foreach (Transform child in transform)
            Destroy(child.gameObject);
    }
}