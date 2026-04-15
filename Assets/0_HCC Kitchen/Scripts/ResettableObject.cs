using UnityEngine;

/// <summary>
/// Component that stores the initial transform of an object and restores it when requested.
/// Attach this to any object that needs to be reset between trials (e.g., cabinet doors, fridge doors, drawers).
/// </summary>
public class ResettableObject : MonoBehaviour
{
    private Vector3 _initialLocalPosition;
    private Quaternion _initialLocalRotation;
    private bool _initialActiveState;

    private void Awake()
    {
        // Save the "Golden State" on startup.
        // We use local values so that objects can be grouped under parents and still reset correctly.
        _initialLocalPosition = transform.localPosition;
        _initialLocalRotation = transform.localRotation;
        _initialActiveState = gameObject.activeSelf;
    }

    /// <summary>
    /// Restores the object to its original position, rotation, and active state.
    /// </summary>
    public void ResetState()
    {
        transform.localPosition = _initialLocalPosition;
        transform.localRotation = _initialLocalRotation;
        gameObject.SetActive(_initialActiveState);
    }
}
