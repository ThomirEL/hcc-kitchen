using System.Collections;
using UnityEngine;

/// <summary>
/// Procedural animation for the ghost agent's body parts.
/// </summary>
public class GhostBodyAnimator : MonoBehaviour
{
    [Header("Body Parts")]
    [SerializeField] private Transform _body;
    [SerializeField] private Transform _head;
    [SerializeField] private Transform _rightHand;
    [SerializeField] private Transform _leftHand;

    [Header("Body Proportions")]
    [SerializeField] private Vector3 _bodyOffset      = new(0f,    0.9f, 0f);
    [SerializeField] private Vector3 _headOffset      = new(0f,    1.7f, 0f);
    [SerializeField] private Vector3 _rightHandRest   = new(0.28f, 0.95f, 0.08f);
    [SerializeField] private Vector3 _leftHandRest    = new(-0.28f,0.95f, 0.08f);

    [Header("Walk Animation")]
    [SerializeField] private float _walkBobFreq      = 5.5f;
    [SerializeField] private float _walkBobAmplitude = 0.035f;
    [SerializeField] private float _armSwingFreq     = 2.75f;
    [SerializeField] private float _armSwingAmplitude = 0.14f;

    [Header("Idle / Breathing")]
    [SerializeField] private float _breathFreq      = 0.28f;
    [SerializeField] private float _breathAmplitude = 0.012f;
    [SerializeField] private float _idleSwayAmplitude = 0.018f;

    [Header("Head Look")]
    [SerializeField] private float _headLookSpeed       = 4f;
    [SerializeField] private float _headIdleInterval    = 3.5f;
    [SerializeField] private float _headIdleYawRange    = 35f;
    [SerializeField] private float _headIdlePitchRange  = 20f;

    private bool    _isWalking  = false;
    private bool    _isHolding  = false;
    private float   _walkPhase  = 0f;
    private float   _breathPhase = 0f;

    private Vector3 _reachTarget;
    private float   _reachWeight       = 0f;
    private float   _reachWeightTarget = 0f;
    private float   _reachLerpSpeed    = 4f;

    private Vector3 _carryOffset = new(0.18f, 1.15f, 0.28f);

    private Vector3    _headLookTargetWorld;
    private bool       _hasHeadLookTarget = false;
    private Quaternion _headLocalTarget   = Quaternion.identity;
    private float      _headIdleTimer     = 0f;

    // ─────────────────────────────────────────────────────────────

    public void SetWalking(bool walking, Vector3 direction)
    {
        Debug.Log($"[BodyAnimator] SetWalking: {walking}");
        _isWalking = walking;
    }

    public void SetHolding(bool holding)
    {
        Debug.Log($"[BodyAnimator] SetHolding: {holding}");

        _isHolding = holding;

        if (!holding)
        {
            Debug.Log("[BodyAnimator] Stopping hold → resetting reach target");
            _reachWeightTarget = 0f;
        }
    }

    public IEnumerator AnimateReach(Vector3 worldTarget, float duration)
    {
        Debug.Log($"[BodyAnimator] AnimateReach START → Target: {worldTarget}, Duration: {duration}");

        _reachTarget       = worldTarget;
        _reachWeightTarget = 1f;

        yield return new WaitForSeconds(duration);

        Debug.Log($"[BodyAnimator] AnimateReach END → Weight: {_reachWeight}");
    }

    public IEnumerator AnimateRetract(float duration)
    {
        Debug.Log($"[BodyAnimator] AnimateRetract START → Duration: {duration}");

        _reachWeightTarget = 0f;

        yield return new WaitForSeconds(duration);

        Debug.Log($"[BodyAnimator] AnimateRetract END → Weight: {_reachWeight}");
    }

    public void SetHeadLookTarget(Vector3 worldPos)
    {
        Debug.Log($"[BodyAnimator] Head look target set: {worldPos}");
        _headLookTargetWorld = worldPos;
        _hasHeadLookTarget   = true;
    }

    public void ClearHeadLookTarget()
    {
        Debug.Log("[BodyAnimator] Head look target cleared");
        _hasHeadLookTarget = false;
    }

    // ─────────────────────────────────────────────────────────────

    private void Update()
    {
        float dt = Time.deltaTime;

        _breathPhase += dt * _breathFreq * Mathf.PI * 2f;

        if (_isWalking)
            _walkPhase += dt * _walkBobFreq * Mathf.PI * 2f;
        else
            _walkPhase = Mathf.Lerp(_walkPhase, 0f, dt * 4f);

        float prevWeight = _reachWeight;
        _reachWeight = Mathf.Lerp(_reachWeight, _reachWeightTarget, dt * _reachLerpSpeed);

        if (Mathf.Abs(prevWeight - _reachWeight) > 0.01f)
        {
            Debug.Log($"[BodyAnimator] ReachWeight: {_reachWeight:F2} (target: {_reachWeightTarget})");
        }

        AnimateBody(dt);
        AnimateHead(dt);
        AnimateHands(dt);
    }

    // ─────────────────────────────────────────────────────────────

    private void AnimateBody(float dt)
    {
        float bob = _isWalking
            ? Mathf.Abs(Mathf.Sin(_walkPhase)) * _walkBobAmplitude
            : Mathf.Sin(_breathPhase) * _breathAmplitude;

        _body.position = transform.TransformPoint(_bodyOffset) + Vector3.up * bob;
        _body.rotation = transform.rotation;
    }

    private void AnimateHead(float dt)
    {
        float bob = _isWalking
            ? Mathf.Abs(Mathf.Sin(_walkPhase)) * _walkBobAmplitude * 0.6f
            : Mathf.Sin(_breathPhase) * _breathAmplitude * 0.4f;

        _head.position = transform.TransformPoint(_headOffset) + Vector3.up * bob;

        if (_hasHeadLookTarget)
        {
            Vector3 dir = (_headLookTargetWorld - _head.position).normalized;
            if (dir.sqrMagnitude > 0.001f)
            {
                Quaternion worldTarget = Quaternion.LookRotation(dir, Vector3.up);
                _headLocalTarget = Quaternion.Inverse(transform.rotation) * worldTarget;
            }
        }
        else
        {
            _headIdleTimer -= dt;
            if (_headIdleTimer <= 0f)
            {
                float yaw   = Random.Range(-_headIdleYawRange, _headIdleYawRange);
                float pitch = Random.Range(-_headIdlePitchRange * 0.3f, _headIdlePitchRange);

                Debug.Log($"[BodyAnimator] Idle head movement → yaw:{yaw}, pitch:{pitch}");

                _headLocalTarget = Quaternion.Euler(pitch, yaw, 0f);
                _headIdleTimer   = Random.Range(_headIdleInterval * 0.6f, _headIdleInterval * 1.4f);
            }
        }

        Quaternion currentLocal = Quaternion.Inverse(transform.rotation) * _head.rotation;
        Quaternion smoothed     = Quaternion.Slerp(currentLocal, _headLocalTarget, dt * _headLookSpeed);
        _head.rotation          = transform.rotation * smoothed;
    }

    private void AnimateHands(float dt)
    {
        Vector3 rightRestWorld = transform.TransformPoint(_rightHandRest);

        Vector3 rightTarget;
        Quaternion rightRot;

        // if (_isHolding)
        // {
        //     Debug.Log("[BodyAnimator] Right hand in HOLD state");

        //     float bob = Mathf.Abs(Mathf.Sin(_walkPhase)) * _walkBobAmplitude * 0.5f;
        //     rightTarget = transform.TransformPoint(_carryOffset) + Vector3.up * bob;
        //     rightRot    = transform.rotation * Quaternion.Euler(-15f, 0f, 0f);
        // }
        // else 
        if (_reachWeight > 0.01f)
        {
            Debug.Log($"[BodyAnimator] Right hand REACHING → weight: {_reachWeight:F2}");

            rightTarget = Vector3.Lerp(rightRestWorld, _reachTarget, _reachWeight);
            Vector3 dir = (_reachTarget - rightRestWorld).normalized;

            rightRot = dir.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(dir, Vector3.up)
                : transform.rotation;
        }
        else if (_isWalking)
        {
            float swing = Mathf.Sin(_walkPhase / 2f) * _armSwingAmplitude;
            rightTarget = rightRestWorld + transform.forward * swing;
            rightRot    = transform.rotation * Quaternion.Euler(-swing * 180f, 0f, 0f);
        }
        else
        {
            float sway  = Mathf.Sin(_breathPhase * 0.9f) * _idleSwayAmplitude;
            rightTarget = rightRestWorld + transform.right * sway;
            rightRot    = transform.rotation;
        }

        _rightHand.position = rightTarget;
        _rightHand.rotation = Quaternion.Slerp(_rightHand.rotation, rightRot, dt * 8f);

        // LEFT HAND unchanged (no bug there)
        Vector3 leftRestWorld = transform.TransformPoint(_leftHandRest);
        _leftHand.position = leftRestWorld;
    }
}