using UnityEngine;


[RequireComponent(typeof(IARPart))]
[RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable))]
public class IARItem : MonoBehaviour
{

    void Awake()
    {
        IARInteractionDatabase.Instance.RegisterPart(gameObject.name, GetComponent<IARPart>());

        var grab = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        grab.selectEntered.AddListener(_ => ReportState("held", true));
        grab.selectExited.AddListener(_  => ReportState("held", false));
    }

    void ReportState(string state, bool active)
    {
        IARInteractionDatabase.Instance.OnStateChanged(gameObject.name, state, active);
    }
}