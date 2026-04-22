using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using EzySlice;

/// <summary>
/// Handles chop response. Supports two modes:
///   - Chunk Swap: spawns pre-made chunk prefabs (Option A)
///   - EzySlice:   performs real mesh slicing (Option B — requires EzySlice plugin)
/// Attach to: Tomato GameObject.
/// Inspector: assign chunkPrefabs OR enable useEzySlice.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(XRGrabInteractable))]
public class ChoppableTomato : MonoBehaviour
{
    public enum ChopMode { ChunkSwap, EzySlice }

    [Header("Chop Mode")]
    [SerializeField] private ChopMode chopMode = ChopMode.ChunkSwap;

    [Header("Option A — Chunk Swap")]
    [Tooltip("Pre-made chunk prefabs to spawn. Each gets a random velocity outward.")]
    [SerializeField] private GameObject[] chunkPrefabs;

    [Header("Option B — EzySlice")]
    [Tooltip("Material applied to the cross-section cut face")]
    [SerializeField] private Material sliceMaterial;
    [Tooltip("How many slices to perform in one chop (1 = halve, 2 = quarter, etc.)")]
    [SerializeField] private int sliceCount = 1;

    [Header("Shared Settings")]
    [Tooltip("Force applied to chunks after spawn so they scatter")]
    [SerializeField] private float chunkScatterForce = 1.5f;
    [Tooltip("Can this tomato be chopped more than once?")]
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
    public void Chop(Vector3 contactPoint, Vector3 bladeDirection, Vector3 knifeVelocity)
    {
        if (hasBeenChopped && !allowReChop) return;
        hasBeenChopped = true;

        // Drop the tomato if it's currently being held
        if (grabInteractable.isSelected)
            grabInteractable.interactionManager.CancelInteractableSelection(
                (IXRSelectInteractable)grabInteractable);

        if (chopMode == ChopMode.ChunkSwap)
            SpawnChunks(contactPoint, knifeVelocity);
        else
            SliceWithEzySlice(contactPoint, bladeDirection, knifeVelocity);

        Destroy(gameObject);
    }

    // ─── Option A: Chunk Swap ────────────────────────────────────────────────

    private void SpawnChunks(Vector3 spawnOrigin, Vector3 knifeVelocity)
    {
        if (chunkPrefabs == null || chunkPrefabs.Length == 0)
        {
            Debug.LogWarning("[ChoppableTomato] No chunk prefabs assigned!");
            return;
        }

        foreach (GameObject prefab in chunkPrefabs)
        {
            // Spawn each chunk near the contact point with a slight random offset
            Vector3 offset = Random.insideUnitSphere * 0.03f;
            GameObject chunk = Instantiate(prefab, spawnOrigin + offset, 
                                           Random.rotation);

            // Make sure chunk has Rigidbody so it can be tossed into bowl
            Rigidbody chunkRb = chunk.GetComponent<Rigidbody>();
            if (chunkRb == null) chunkRb = chunk.AddComponent<Rigidbody>();

            // Add XRGrabInteractable so player can pick up and toss chunks
            if (chunk.GetComponent<XRGrabInteractable>() == null)
                chunk.AddComponent<XRGrabInteractable>();

            // Tag chunks so bowl can identify them
            chunk.tag = "TomatoChunk";

            // Scatter force: inherit knife direction + random spread
            Vector3 scatter = (knifeVelocity.normalized + Random.insideUnitSphere * 0.4f)
                              * chunkScatterForce;
            chunkRb.AddForce(scatter, ForceMode.Impulse);
        }
    }

    // ─── Option B: EzySlice ─────────────────────────────────────────────────
    // Requires EzySlice plugin in Assets/Plugins/EzySlice
    // If you don't have it, this block won't compile — wrap in #if or remove it

    private void SliceWithEzySlice(Vector3 contactPoint, Vector3 bladeDirection, Vector3 knifeVelocity)
{
    // Get the child that actually has the MeshFilter
    MeshFilter mf = GetComponentInChildren<MeshFilter>();
    if (mf == null)
    {
        Debug.LogError("[EzySlice] No MeshFilter found in children.");
        SpawnChunks(contactPoint, knifeVelocity);
        return;
    }

    Vector3 planeNormal = Vector3.Cross(bladeDirection, Vector3.right).normalized;
    if (planeNormal == Vector3.zero)
        planeNormal = Vector3.Cross(bladeDirection, Vector3.forward).normalized;

    // Slice the child mesh object, not the root gameObject
    GameObject[] slices = mf.gameObject.SliceInstantiate(contactPoint, planeNormal, sliceMaterial);

    if (slices == null)
    {
        Debug.LogWarning("[EzySlice] Slice returned null — check Read/Write is enabled on mesh.");
        SpawnChunks(contactPoint, knifeVelocity);
        return;
    }

    foreach (GameObject slice in slices)
    {
        Rigidbody sliceRb = slice.AddComponent<Rigidbody>();
        MeshCollider mc = slice.AddComponent<MeshCollider>();
        mc.convex = true;  // must be convex for dynamic Rigidbody
        slice.AddComponent<XRGrabInteractable>();
        slice.tag = "TomatoChunk";

        Vector3 scatter = (knifeVelocity.normalized + Random.insideUnitSphere * 0.3f)
                          * chunkScatterForce;
        sliceRb.AddForce(scatter, ForceMode.Impulse);
    }
}
}