using UnityEngine;


/// <summary>
/// Disables the Ray Interactor GameObjects on both hands at startup.
/// Works for both the XR Device Simulator and real XR Origin setups.
///
/// Attach to: your XR Origin GameObject
///
/// How it works:
///   Finds all XRRayInteractor components under this GameObject and
///   disables their GameObjects so no ray is shown and no ray selection
///   is possible. Direct interaction still works normally.
/// </summary>
public class DisableSimulatorRay : MonoBehaviour
{
    [Tooltip("If true, suppresses the log message on startup.")]
    public bool silent = false;

    private void Awake()
    {
        DisableAllRayInteractors();
    }

    private void DisableAllRayInteractors()
    {
        // Find every XRRayInteractor under this XR Origin
        UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor[] rayInteractors = GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor>(
            includeInactive: true);

        if (rayInteractors.Length == 0)
        {
            if (!silent)
                Debug.LogWarning("[DisableSimulatorRay] No XRRayInteractor components " +
                                 "found under this GameObject. Nothing disabled.");
            return;
        }

        int count = 0;
        foreach (var ray in rayInteractors)
        {
            // Disable the entire GameObject — this also kills the Line Renderer
            // that draws the visible ray beam, not just the interaction component
            ray.gameObject.SetActive(false);
            count++;

            if (!silent)
                Debug.Log($"[DisableSimulatorRay] Disabled ray: {ray.gameObject.name}");
        }

        if (!silent)
            Debug.Log($"[DisableSimulatorRay] Disabled {count} ray interactor(s).");
    }

    /// <summary>
    /// Re-enable all ray interactors at runtime (e.g. for a pause menu that needs UI rays).
    /// </summary>
    public void EnableRays()
    {
        UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor[] rayInteractors = GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor>(
            includeInactive: true);
        foreach (var ray in rayInteractors)
            ray.gameObject.SetActive(true);
    }

    /// <summary>
    /// Disable all ray interactors at runtime.
    /// </summary>
    public void DisableRays()
    {
        DisableAllRayInteractors();
    }
}