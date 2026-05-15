using UnityEngine;

[RequireComponent(typeof(IARPart))]
public class DegradingDOI : MonoBehaviour
{
    [Header("Fridge Reference")]
    [SerializeField] private Collider fridgeTrigger;

    [Header("Outside Influence")]
    [SerializeField] private float timeToMaxInfluence = 20f;

    [SerializeField] private AnimationCurve growthCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    private IARPart iarPart;

    private bool isInsideFridge = false;
    private float timeOutside = 0f;

    private float lastValue = -1f;

    void Awake()
    {
        iarPart = GetComponent<IARPart>();

        if (fridgeTrigger == null)
            Debug.LogError($"[{name}] Fridge trigger not assigned!");
    }

    void Update()
    {
        if (isInsideFridge)
        {
            timeOutside = 0f;

            if (lastValue != 0f)
            {
                iarPart.ClearContribution("Degrading");
                lastValue = 0f;
            }

            return;
        }

        timeOutside += Time.deltaTime;

        float t = Mathf.Clamp01(timeOutside / timeToMaxInfluence);

        // Smooth curve instead of linear ramp
        float value = growthCurve.Evaluate(t);

        if (Mathf.Abs(value - lastValue) > 0.001f)
        {
            iarPart.SetContribution("Degrading", value);
            lastValue = value;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == fridgeTrigger)
            isInsideFridge = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other == fridgeTrigger)
            isInsideFridge = false;
    }
}