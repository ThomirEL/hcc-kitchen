using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// Robust hinge door for VR. Attach to your hinge pivot GameObject.
/// Requires: XRSimpleInteractable, Rigidbody (kinematic, position frozen), Collider
/// </summary>
[RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable))]
[RequireComponent(typeof(Rigidbody))]
public class HingeDoorInteractable : MonoBehaviour
{
    [Header("Hinge Constraints")]
    [SerializeField] private float minAngle = 0f;
    [SerializeField] private float maxAngle = 120f;

    [Tooltip("Set to -1 if door opens the wrong way")]
    [SerializeField] private float swingDirection = 1f;

    [Header("Advanced")]
    [Tooltip("Change if door swings on wrong axis. Try (1,0,0) if Y-axis swap doesn't fix it")]
    [SerializeField] private Vector3 forwardAxis = Vector3.forward;   // local Z by default
    [SerializeField] private Vector3 rightAxis   = Vector3.right;     // local X by default

    [Header("Feel")]
    [SerializeField] private float followSpeed = 10f;
    [SerializeField] private float damping = 5f;
    [SerializeField] private float snapClosedThreshold = 8f;

    // Components
    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable _interactable;
    private UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor _activeInteractor;

    // State
    private float _currentAngle = 0f;
    private float _angularVelocity = 0f;
    private bool _isGrabbed = false;

    // We calculate angle by comparing hand position relative to hinge pivot,
    // projected onto the horizontal plane. This is much more stable than deltas.
    private float _grabStartHandAngle;   // World angle of hand at moment of grab
    private float _grabStartDoorAngle;   // Door angle at moment of grab

    private void Awake()
    {
        _interactable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();

        // Ensure Rigidbody is kinematic so door doesn't fall
        var rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    private void OnEnable()
    {
        _interactable.selectEntered.AddListener(OnGrab);
        _interactable.selectExited.AddListener(OnRelease);
    }

    private void OnDisable()
    {
        _interactable.selectEntered.RemoveListener(OnGrab);
        _interactable.selectExited.RemoveListener(OnRelease);
    }

    private void OnGrab(SelectEnterEventArgs args)
    {
        _isGrabbed = true;
        _activeInteractor = args.interactorObject;
        _angularVelocity = 0f;

        // Snapshot where the hand is and what angle the door is at RIGHT NOW.
        // All future angles are offsets from this baseline — avoids any drift.
        _grabStartHandAngle = GetHandAngle();
        _grabStartDoorAngle = _currentAngle;

        //Debug.Log($"[HingeDoor] Grabbed. Hand angle baseline: {_grabStartHandAngle:F1}°, Door baseline: {_grabStartDoorAngle:F1}°");
    }

    private void OnRelease(SelectExitEventArgs args)
    {
        _isGrabbed = false;
        _activeInteractor = null;
        //Debug.Log($"[HingeDoor] Released at angle: {_currentAngle:F1}°");
    }

    private void Update()
    {
        if (_isGrabbed && _activeInteractor != null)
        {
            UpdateGrabbedDoor();
        }
        else
        {
            CoastAndDamp();
            //SnapClosed();
        }

        // Apply the angle to the transform every frame
        transform.localRotation = Quaternion.AngleAxis(_currentAngle, Vector3.up);
    }

    private void UpdateGrabbedDoor()
    {
        float currentHandAngle = GetHandAngle();

        // How far has the hand rotated around the hinge since we grabbed?
        float handDelta = Mathf.DeltaAngle(_grabStartHandAngle, currentHandAngle);

        // Target door angle = where door was when grabbed + how far hand moved
        float targetAngle = _grabStartDoorAngle + (handDelta * swingDirection);
        targetAngle = ClampAngle(targetAngle);

        // Track angular velocity for coasting after release
        float previousAngle = _currentAngle;
        _currentAngle = Mathf.Lerp(_currentAngle, targetAngle, Time.deltaTime * followSpeed);
        _angularVelocity = (_currentAngle - previousAngle) / Time.deltaTime;

        // Optional: haptic bump at limits
        if (_currentAngle >= maxAngle - 0.5f || _currentAngle <= minAngle + 0.5f)
        {
            TriggerHaptic(0.2f, 0.05f);
        }
    }

    private void CoastAndDamp()
    {
        if (Mathf.Abs(_angularVelocity) > 0.5f)
        {
            _currentAngle += _angularVelocity * Time.deltaTime;
            _currentAngle = ClampAngle(_currentAngle);
            _angularVelocity = Mathf.Lerp(_angularVelocity, 0f, Time.deltaTime * damping);
        }
    }

    private void SnapClosed()
    {
        if (Mathf.Abs(_angularVelocity) < 0.5f && _currentAngle < snapClosedThreshold)
        {
            _currentAngle = Mathf.Lerp(_currentAngle, minAngle, Time.deltaTime * followSpeed);
        }
    }

    private float ClampAngle(float angle)
{
    return Mathf.Clamp(angle, Mathf.Min(minAngle, maxAngle), Mathf.Max(minAngle, maxAngle));
}

    /// <summary>
    /// Gets the hand's angle (in degrees) around the Y axis relative to the hinge pivot.
    /// This is the core of the stable approach — one clean atan2 call, no delta accumulation.
    /// </summary>
    private float GetHandAngle()
{
    Vector3 toHand = _activeInteractor.transform.position - transform.position;
    toHand.y = 0f;

    Vector3 localToHand = transform.InverseTransformDirection(toHand);

    // Fridge forward points toward player (Z = outward), hinge on right side
    // So we measure the angle using X and Z swapped, with Z negated
    float angle = Mathf.Atan2(localToHand.x, -localToHand.z) * Mathf.Rad2Deg;
    return angle;
}

    private void TriggerHaptic(float amplitude, float duration)
    {
        if (_activeInteractor is UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInputInteractor controllerInteractor)
        {
            controllerInteractor.SendHapticImpulse(amplitude, duration);
        }
    }

    private void OnDrawGizmosSelected()
    {
        // Draw hinge axis
        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(transform.position, Vector3.up * 0.5f);

        // Draw current door facing direction
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(transform.position, transform.forward * 0.4f);

        // Draw min/max arc so you can visually confirm limits in Scene view
        DrawArcGizmo(minAngle, Color.green);
        DrawArcGizmo(maxAngle, Color.red);
    }

    private void DrawArcGizmo(float angle, Color color)
    {
        Gizmos.color = color;
        Vector3 dir = Quaternion.AngleAxis(angle, Vector3.up) * transform.parent.forward;
        Gizmos.DrawRay(transform.position, dir * 0.5f);
    }
}