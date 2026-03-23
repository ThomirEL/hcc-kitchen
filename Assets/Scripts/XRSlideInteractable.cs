using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// Constrains an XR grab to slide along one local axis within a min/max range.
/// Works for drawers, shelves, sliding doors — anything that translates on one axis.
/// 
/// Attach to: the drawer/shelf GameObject directly
/// Requires: XRSimpleInteractable, Rigidbody (kinematic), Collider
/// </summary>
[RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable))]
[RequireComponent(typeof(Rigidbody))]
public class XRSlideInteractable : MonoBehaviour
{
    [Header("━━ Slide Settings ━━━━━━━━━━━━━━━━━━━━")]
    [Tooltip("Local axis the object slides along. Z = forward/back (drawer pull).")]
    public Vector3 slideAxis = Vector3.forward;

    [Tooltip("Closed/resting position offset along the slide axis (usually 0).")]
    public float closedPosition = 0f;

    [Tooltip("How far the drawer can slide out (in Unity units/metres). e.g. 0.4 = 40cm")]
    public float openDistance = 0.4f;

    [Header("━━ Feel ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━")]
    [Range(1f, 20f)]
    public float followSpeed = 12f;

    [Range(1f, 15f)]
    public float damping = 6f;

    [Tooltip("Haptic bump when drawer hits open or closed limit.")]
    public bool hapticOnLimit = true;

    // ─────────────────────────────────────────────
    // Internal state
    // ─────────────────────────────────────────────
    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable _interactable;
    private UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor _activeInteractor;

    private float _currentOffset = 0f;   // Current position along slide axis
    private float _velocity = 0f;         // For coasting after release
    private bool _isGrabbed = false;

    // Snapshots taken at grab moment
    private float _grabStartOffset;       // Where drawer was when grabbed
    private float _grabStartHandProject;  // Where hand was (projected onto axis) when grabbed

    private Vector3 _worldSlideAxis;      // Cached world-space slide direction

    // ─────────────────────────────────────────────
    // Lifecycle
    // ─────────────────────────────────────────────
    private void Awake()
    {
        _interactable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();

        var rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        // Cache world-space axis once at startup
        _worldSlideAxis = transform.TransformDirection(slideAxis).normalized;

        // Start at closed position
        _currentOffset = closedPosition;
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

    // ─────────────────────────────────────────────
    // Grab / Release
    // ─────────────────────────────────────────────
    private void OnGrab(SelectEnterEventArgs args)
    {
        _isGrabbed = true;
        _activeInteractor = args.interactorObject;
        _velocity = 0f;

        // Snapshot the hand's projection onto the slide axis right now.
        // All movement is measured as a delta from this baseline —
        // so the drawer doesn't jump to the hand position on grab.
        _grabStartHandProject = GetHandProjection();
        _grabStartOffset = _currentOffset;
    }

    private void OnRelease(SelectExitEventArgs args)
    {
        _isGrabbed = false;
        _activeInteractor = null;
        // _velocity is retained — drawer coasts to a stop naturally
    }

    // ─────────────────────────────────────────────
    // Update
    // ─────────────────────────────────────────────
    private void Update()
    {
        if (_isGrabbed && _activeInteractor != null)
            UpdateGrabbed();
        else
            UpdateReleased();

        ApplyPosition();
    }

    private void UpdateGrabbed()
    {
        // How far has the hand moved along the slide axis since we grabbed?
        float handDelta = GetHandProjection() - _grabStartHandProject;

        // Target = where drawer was + how far hand moved
        float targetOffset = _grabStartOffset + handDelta;
        targetOffset = ClampOffset(targetOffset);

        // Smoothly follow
        float previousOffset = _currentOffset;
        _currentOffset = Mathf.Lerp(_currentOffset, targetOffset, Time.deltaTime * followSpeed);
        _velocity = (_currentOffset - previousOffset) / Time.deltaTime;

        // Haptic bump at limits
        if (hapticOnLimit)
        {
            float min = Mathf.Min(closedPosition, closedPosition + openDistance);
            float max = Mathf.Max(closedPosition, closedPosition + openDistance);
            bool atLimit = _currentOffset >= max - 0.002f || _currentOffset <= min + 0.002f;
            if (atLimit) TriggerHaptic(0.25f, 0.06f);
        }
    }

    private void UpdateReleased()
    {
        // Coast to a stop using velocity retained from last grabbed frame
        if (Mathf.Abs(_velocity) > 0.001f)
        {
            _currentOffset += _velocity * Time.deltaTime;
            _currentOffset = ClampOffset(_currentOffset);
            _velocity = Mathf.Lerp(_velocity, 0f, Time.deltaTime * damping);
        }
        else
        {
            _velocity = 0f;
        }
    }

    /// <summary>
    /// Moves the transform to match _currentOffset along the slide axis,
    /// keeping all other axes locked to their original local position.
    /// </summary>
    private void ApplyPosition()
    {
        // Only move along the slide axis — all other axes stay frozen
        Vector3 localPos = transform.localPosition;
        float axisComponent = _currentOffset;

        // Apply only to the relevant local axis component
        if (slideAxis == Vector3.forward || slideAxis == -Vector3.forward)
            localPos.z = axisComponent;
        else if (slideAxis == Vector3.right || slideAxis == -Vector3.right)
            localPos.x = axisComponent;
        else if (slideAxis == Vector3.up || slideAxis == -Vector3.up)
            localPos.y = axisComponent;
        else
        {
            // Custom axis — project onto it
            localPos = slideAxis.normalized * axisComponent;
        }

        transform.localPosition = localPos;
    }

    // ─────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────

    /// <summary>
    /// Projects the interactor's world position onto the world slide axis.
    /// Returns a scalar (signed distance along axis) — this is what we diff
    /// between grab start and now to get hand delta.
    /// </summary>
    private float GetHandProjection()
    {
        return Vector3.Dot(_activeInteractor.transform.position, _worldSlideAxis);
    }

    private float ClampOffset(float offset)
    {
        float min = Mathf.Min(closedPosition, closedPosition + openDistance);
        float max = Mathf.Max(closedPosition, closedPosition + openDistance);
        return Mathf.Clamp(offset, min, max);
    }

    private void TriggerHaptic(float amplitude, float duration)
    {
        if (_activeInteractor is UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInputInteractor c)
            c.SendHapticImpulse(amplitude, duration);
    }

    // ─────────────────────────────────────────────
    // Gizmos
    // ─────────────────────────────────────────────
    private void OnDrawGizmosSelected()
    {
        Vector3 worldAxis = transform.TransformDirection(slideAxis).normalized;
        Vector3 origin = transform.position;

        // Closed position marker
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(origin, 0.03f);

        // Open position marker
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(origin + worldAxis * openDistance, 0.03f);

        // Slide range line
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(origin, origin + worldAxis * openDistance);
    }
}