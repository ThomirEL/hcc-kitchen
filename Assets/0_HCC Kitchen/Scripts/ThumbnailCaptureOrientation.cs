using UnityEngine;

/// <summary>
/// Add to any prefab to define its rotation when captured by ThumbnailGenerator.
/// If absent, the prefab's default rotation is used.
/// </summary>
public class ThumbnailCaptureOrientation : MonoBehaviour
{
    [Tooltip("The rotation this object will be set to during thumbnail capture")]
    public Vector3 captureEulerAngles = Vector3.zero;
}