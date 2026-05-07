using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ItemListUI : MonoBehaviour
{
    [SerializeField] private ThumbnailGenerator _generator;
    [SerializeField] private GameObject _rowPrefab;

    // Optional — if assigned, auto-scales content to fit after building
    [SerializeField] private PageAutoScaler _autoScaler;

    public IEnumerator BuildList(List<ItemHighlight> targets)
    {
        ClearList();

        // Step 1 — capture all target sprites at once
        var captureList = new List<(GameObject prefab, string name)>();
        foreach (var item in targets)
            captureList.Add((item.gameObject, item.gameObject.name));

        var sprites = new Dictionary<string, Sprite>();
        yield return _generator.CaptureAll(captureList, sprites);

        // Step 2 — spawn rows now that all sprites are ready
        foreach (var item in targets)
        {
            Sprite icon = sprites.TryGetValue(item.gameObject.name, out var s) ? s : null;
            SpawnRow(item.gameObject.name, icon);
        }

        // Step 3 — wait one frame for layout to settle, then fit to page
        yield return null;
        _autoScaler?.FitToPage();
    }

    public void SpawnRow(string label, Sprite icon)
    {
        GameObject row = Instantiate(_rowPrefab, transform);
        row.SetActive(true);

        TMP_Text text = row.GetComponentInChildren<TMP_Text>();
        if (text != null)
            text.text = label;

        Image image = row.GetComponentInChildren<Image>();
        if (image != null)
        {
            image.sprite = icon;
            image.gameObject.SetActive(icon != null);
        }
    }

    public void ClearList()
    {
        foreach (Transform child in transform)
            Destroy(child.gameObject);
    }
    public void Start()
    {
        this.gameObject.transform.localScale = new Vector3(0.001f, 0.001f, 0.001f);
    }
}