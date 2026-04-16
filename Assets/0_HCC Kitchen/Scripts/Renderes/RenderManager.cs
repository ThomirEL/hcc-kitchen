using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Central render coordinator that refreshes and rerenders all ingredient managers.
/// 
/// Calls:
/// - CanManager.RenderAll()
/// - DryGoodsManager.RenderAll()
/// - SpiceManager.RenderAll()
/// 
/// Use this when you need to update all rendered items at once.
/// </summary>
public class RenderManager : MonoBehaviour
{
    public static RenderManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    /// <summary>
    /// Renders only the base items for the current trial (2D sprites).
    /// Base items: Base Can, Base Dry Good, Base Jar
    /// </summary>
    public void RefreshAndRenderAll()
    {
        Debug.Log("[RenderManager] Rendering base items for current trial...");

        if (CanManager.Instance != null)
        {
            Debug.Log("[RenderManager] Calling CanManager.RenderAllCansNow()");
            CanManager.Instance.RenderAllCansNow();
            //CanManager.Instance.RenderBaseCans();
        }
        else
        {
            Debug.LogWarning("[RenderManager] CanManager.Instance not found!");
        }

        if (DryGoodsManager.Instance != null)
        {
            Debug.Log("[RenderManager] Calling DryGoodsManager.RenderAllDryGoodsNow()");
            DryGoodsManager.Instance.RenderAllDryGoodsNow();
            //DryGoodsManager.Instance.RenderBaseCans();
        }
        else
        {
            Debug.LogWarning("[RenderManager] DryGoodsManager.Instance not found!");
        }

        if (SpiceManager.Instance != null)
        {
            Debug.Log("[RenderManager] Calling SpiceManager.RenderAllSpicesNow()");
            SpiceManager.Instance.RenderAllSpicesNow();
            //SpiceManager.Instance.RenderBaseCans();
        }
        else
        {
            Debug.LogWarning("[RenderManager] SpiceManager.Instance not found!");
        }

        Debug.Log("[RenderManager] Base item rendering complete.");
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Update()
    {
        if (Keyboard.current.rKey.wasPressedThisFrame)
        {
            RefreshAndRenderAll();
        }
    } 
}
