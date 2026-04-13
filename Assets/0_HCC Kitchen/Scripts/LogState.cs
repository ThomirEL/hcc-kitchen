// LogState.cs
using UnityEngine;

public class LogState : MonoBehaviour
{
    public bool IsInitialized { get; private set; } = false;

    void OnEnable()  => Logging.Logger.ParticipantIDSet.AddListener(OnParticipantIDSet);
    void OnDisable() => Logging.Logger.ParticipantIDSet.RemoveListener(OnParticipantIDSet);

    void OnParticipantIDSet() => IsInitialized = true;
}