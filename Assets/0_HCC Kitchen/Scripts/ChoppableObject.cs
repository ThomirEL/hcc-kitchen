using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Handles chop response for any object by spawning multiple copies of a single chunk prefab.
/// Attach to: Any GameObject you want to make choppable.
/// Inspector: assign chunkPrefab and set spawnCount.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(XRGrabInteractable))]
public class ChoppableObject : MonoBehaviour
{
    [Header("Chunk Spawning")]
    [Tooltip("Single prefab to spawn copies of")]
    [SerializeField] private GameObject chunkPrefab;
    [Tooltip("Number of copies to spawn on chop")]
    [SerializeField] private int spawnCount = 2;

    [Header("Shared Settings")]
    [Tooltip("Force applied to chunks after spawn so they scatter")]
    [SerializeField] private float chunkScatterForce = 1.5f;
    [Tooltip("Can this object be chopped more than once?")]
    [SerializeField] private bool allowReChop = false;

    private bool hasBeenChopped = false;
    private XRGrabInteractable grabInteractable;
    private Rigidbody rb;

    private void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
        rb = GetComponent<Rigidbody>();
    }

    /// <summary>
    /// Called by KnifeChopper when a valid chop collision is detected.
    /// </summary>
    public void Chop(Vector3 contactPoint, Vector3 knifeVelocity)
    {
        if (hasBeenChopped && !allowReChop) return;
        hasBeenChopped = true;

        // Drop the object if it's currently being held
        if (grabInteractable.isSelected)
            grabInteractable.interactionManager.CancelInteractableSelection(
                (IXRSelectInteractable)grabInteractable);

        SpawnChunks(contactPoint, knifeVelocity);

        Destroy(gameObject);
    }

    private void SpawnChunks(Vector3 spawnOrigin, Vector3 knifeVelocity)
    {
        if (chunkPrefab == null)
        {
            Debug.LogWarning("[ChoppableObject] No chunk prefab assigned!");
            return;
        }

        for (int i = 0; i < spawnCount; i++)
        {
            // Spawn each chunk near the contact point with a slight random offset
            Vector3 offset = Random.insideUnitSphere * 0.03f;
            GameObject chunk = Instantiate(chunkPrefab, spawnOrigin + offset, 
                                           Random.rotation);

            // Make sure chunk has Rigidbody so it can be interacted with
            Rigidbody chunkRb = chunk.GetComponent<Rigidbody>();
            if (chunkRb == null) chunkRb = chunk.AddComponent<Rigidbody>();

            // Add XRGrabInteractable so player can pick up and toss chunks
            if (chunk.GetComponent<XRGrabInteractable>() == null)
                chunk.AddComponent<XRGrabInteractable>();

            // Tag chunks for identification by other systems (e.g., bowl detection) to it and all children
            chunk.tag = "Chunk";
            foreach (Transform child in chunk.transform)
                child.gameObject.tag = "Chunk";

            // Scatter force: inherit knife direction + random spread
            Vector3 scatter = (knifeVelocity.normalized + Random.insideUnitSphere * 0.4f)
                              * chunkScatterForce;

            chunk.SetActive(true);
            chunkRb.AddForce(scatter, ForceMode.Impulse);
        }
    }
}
