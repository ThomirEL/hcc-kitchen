using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Detects when tomato chunks are tossed into the bowl via its trigger collider.
/// Attach to: Bowl GameObject.
/// The bowl collider must have Is Trigger = true.
/// </summary>
public class BowlReceiver : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("How many chunks must land in bowl to fire OnBowlComplete")]
    [SerializeField] private int chunksRequiredToComplete = 3;

    [Header("Events")]
    public UnityEvent<int> OnChunkAdded;        // fires each time a chunk lands
    public UnityEvent OnBowlComplete;           // fires when enough chunks collected

    private List<GameObject> chunksInBowl = new List<GameObject>();

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"[Bowl] Trigger entered by {other.gameObject.name}");
        Debug.Log($"[Bowl] Current chunk count: {other.gameObject.tag}");
        // Only accept chunks
        if (!other.CompareTag("Chunk")) return;

        // Ignore if already counted (e.g. chunk rolling inside trigger repeatedly)
        if (chunksInBowl.Contains(other.gameObject)) return;

        chunksInBowl.Add(other.gameObject);

        // Settle the chunk — kill velocity so it stays in the bowl
        Rigidbody rb = other.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            // Optional: make kinematic so it doesn't roll out
            rb.isKinematic = true;
        }

        // Optionally unregister XRGrabInteractable so chunk can't be grabbed out
        // Remove this if you want players to be able to retrieve chunks
        XRGrabInteractable grab = other.GetComponent<XRGrabInteractable>();
        if (grab != null) grab.enabled = false;

        int count = chunksInBowl.Count;
        Debug.Log($"[Bowl] Chunk received. Total: {count}/{chunksRequiredToComplete}");
        OnChunkAdded?.Invoke(count);

        if (count >= chunksRequiredToComplete)
        {
            Debug.Log("[Bowl] Bowl complete!");
            OnBowlComplete?.Invoke();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        // If a chunk rolls or is grabbed back out, uncount it
        if (other.CompareTag("Chunk"))
            chunksInBowl.Remove(other.gameObject);
    }

    public int ChunkCount => chunksInBowl.Count;
    public void ClearBowl() => chunksInBowl.Clear();
}