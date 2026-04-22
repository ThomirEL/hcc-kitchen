using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// Detects when the knife blade collides with a choppable object.
/// Requires the knife to be moving fast enough to count as a chop.
/// Attach to: Knife GameObject.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class KnifeChopper : MonoBehaviour
{
    [Header("Chop Settings")]
    [Tooltip("Minimum velocity (m/s) the knife must be moving to register a chop")]
    [SerializeField] private float minChopVelocity = 0.8f;

    [Tooltip("Blade direction in knife's local space — usually local down or forward")]
    [SerializeField] private Vector3 bladeLocalDirection = Vector3.down;

    private Rigidbody rb;
    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grabInteractable;
    private bool isHeld = false;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        grabInteractable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
    }

    private void OnEnable()
    {
        grabInteractable.selectEntered.AddListener(OnGrabbed);
        grabInteractable.selectExited.AddListener(OnReleased);
    }

    private void OnDisable()
    {
        grabInteractable.selectEntered.RemoveListener(OnGrabbed);
        grabInteractable.selectExited.RemoveListener(OnReleased);
    }

    private void OnGrabbed(SelectEnterEventArgs args) => isHeld = true;
    private void OnReleased(SelectExitEventArgs args) => isHeld = false;

    private void OnCollisionEnter(Collision collision)
    {
        // Only chop when held — prevent accidents from dropping knife on tomato
        if (!isHeld) return;

        // Check speed threshold
        float speed = rb.linearVelocity.magnitude;
        Debug.Log($"Knife collision detected with speed: {speed:F2} m/s");
        //if (speed < minChopVelocity) return;

        // Check we hit something choppable
        ChoppableObject choppableObject = collision.gameObject.GetComponent<ChoppableObject>();
        if (choppableObject == null) return;

        // Tell the choppable object it's been chopped, pass slice direction
        choppableObject.Chop(collision.GetContact(0).point, rb.linearVelocity);
    }

    public bool IsHeld => isHeld;
}