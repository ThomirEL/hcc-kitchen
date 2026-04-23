using UnityEngine;

/// <summary>
/// Records the positions of the VR Headset and Controllers every frame.
/// Follows the asynchronous logging pattern established in Logging.cs.
/// </summary>
public class VRTrackingLogger : MonoBehaviour
{
    [Header("━━ Tracking Targets ━━━━━━━━━━━━━━━━━━━━━━━━━")]
    [Tooltip("Reference to the VR Headset (HMD) transform.")]
    public Transform hmdTransform;

    [Tooltip("Reference to the Left VR Controller transform.")]
    public Transform leftControllerTransform;

    [Tooltip("Reference to the Right VR Controller transform.")]
    public Transform rightControllerTransform;

    [Header("━━ Settings ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━")]
    [Tooltip("Whether logging is enabled.")]
    public bool loggingEnabled = true;

    [Tooltip("Log every Nth frame. Set to 1 for every frame.")]
    [Min(1)]
    public int logIntervalFrames = 1;

    private bool _loggingInitialized = false;
    private static readonly string LoggerCategory = "VRTracking";
    private static readonly string[] ColumnNames = { "U_Frame", "HMD_X", "HMD_Y", "HMD_Z", "HMD_Rotation_X", "HMD_Rotation_Y", "HMD_Rotation_Z", "Left_X", "Left_Y", "Left_Z", "Right_X", "Right_Y", "Right_Z" };

    private void Start()
    {
        Logging.Logger.ParticipantIDSet.AddListener(OnParticipantIDSet);
    }

    private void OnParticipantIDSet()
    {
        // Record the header row
        Logging.Logger.RecordVRStats(ColumnNames);
        _loggingInitialized = true;
        Debug.Log($"[VRTracking] Logging initialized for participant {Logging.Logger.participantID}");
    }

    private void Update()
    {
        if (!_loggingInitialized || !loggingEnabled)
            return;

        if (Time.frameCount % logIntervalFrames != 0)
            return;

        LogPositions();
    }

    private void LogPositions()
    {
        // Prepare data for 10 columns: Frame + 3*3 coordinates
        string[] msg = new string[ColumnNames.Length];

        msg[0] = Time.frameCount.ToString();

        // HMD Position
        Vector3 hmdPos = hmdTransform != null ? hmdTransform.position : Vector3.zero;
        msg[1] = hmdPos.x.ToString("F4");
        msg[2] = hmdPos.y.ToString("F4");
        msg[3] = hmdPos.z.ToString("F4");
        msg[4] = hmdTransform != null ? hmdTransform.rotation.eulerAngles.x.ToString("F4") : "0.0000";
        msg[5] = hmdTransform != null ? hmdTransform.rotation.eulerAngles.y.ToString("F4") : "0.0000";
        msg[6] = hmdTransform != null ? hmdTransform.rotation.eulerAngles.z.ToString("F4") : "0.0000";

        // Left Controller Position
        Vector3 leftPos = leftControllerTransform != null ? leftControllerTransform.position : Vector3.zero;
        msg[7] = leftPos.x.ToString("F4");
        msg[8] = leftPos.y.ToString("F4");
        msg[9] = leftPos.z.ToString("F4");

        // Right Controller Position
        Vector3 rightPos = rightControllerTransform != null ? rightControllerTransform.position : Vector3.zero;
        msg[10] = rightPos.x.ToString("F4");
        msg[11  ] = rightPos.y.ToString("F4");
        msg[12] = rightPos.z.ToString("F4");

        Logging.Logger.RecordVRStats(msg);
    }
}
