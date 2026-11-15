using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Standalone, VR-portable 360-degree obstacle proximity belt controller.
/// Focused strictly on spatial obstacle awareness (no navigation goals required),
/// so users can freely explore any VR scene (TP5 room, INSA campus VR, XR Origin, etc.).
/// Measures true clearance from the user's physical body radius to surrounding obstacles.
/// </summary>
public class HapticObstacleFeedback : MonoBehaviour
{
    [Header("Activation & Experiment Toggle")]
    [Tooltip("Enable/disable motor output at runtime (hotkey H) for A/B testing (Haptics ON vs OFF).")]
    public bool hapticsEnabled = true;

    [Tooltip("Keyboard key to toggle haptic output at runtime.")]
    public bool enableHotkeyToggle = true;

    [Header("Haptic Driver")]
    [Tooltip("Auto-resolved if left empty.")]
    public VibrationManager_minimal vibrationManager;

    [Header("VR Reference Frame")]
    [Tooltip("Transform providing position & facing (assign XR Main Camera/Head in VR; defaults to this transform).")]
    public Transform sensorOrigin;

    [Tooltip("Primary scan height offset from sensorOrigin in meters (e.g., -0.35 from head camera puts scan at chest height).")]
    public float sensorHeightOffset = 0.0f;

    [Tooltip("Also check an upper height layer to catch overhangs, open doors, and signs.")]
    public bool detectHeadOverhangs = true;

    [Tooltip("Upper scan offset relative to sensorHeightOffset in meters.")]
    public float upperHeightOffset = 0.45f;

    [Tooltip("Also check a lower knee-level layer to catch low benches, curbs, or furniture.")]
    public bool detectLowObstacles = true;

    [Tooltip("Lower scan offset relative to sensorHeightOffset in meters (stay well above floor).")]
    public float lowerHeightOffset = -0.55f;

    [Header("Body Clearance Geometry (Meters)")]
    [Tooltip("Auto-read body radius from CapsuleCollider on Start if found.")]
    public bool autoDetectBodyRadius = true;

    [Tooltip("Physical radius of the user/avatar collider (m). Distances below measure clearance from this surface.")]
    public float bodyRadius = 0.50f;

    [Header("Asymmetric Clearance Ranges (Meters From Body Surface)")]
    [Tooltip("Longer forward clearance gives reaction time while walking.")]
    public float frontMaxDistance = 2.0f;
    [Tooltip("Front clearance below this triggers maximum/solid danger response.")]
    public float frontDangerDistance = 0.25f;

    [Tooltip("Shorter side clearance avoids constant buzzing in narrow corridors/doorways.")]
    public float sideMaxDistance = 0.80f;
    public float sideDangerDistance = 0.15f;

    [Tooltip("Short rear clearance buffer; can be muted while walking forward.")]
    public float backMaxDistance = 0.60f;
    public float backDangerDistance = 0.15f;

    [Tooltip("Suppresses rear alerts while actively moving forward to prevent distracting back buzzes.")]
    public bool muteBackWhileMovingForward = true;

    [Header("Diagonal & Multi-Sensor Geometry")]
    [Tooltip("SphereCast radius (m) so thin rails, chair legs, and doorframes are caught reliably.")]
    public float sensorRadius = 0.16f;

    [Tooltip("Angle (degrees) of front-left / front-right diagonal feelers that close the 45-degree blind spot.")]
    [Range(15f, 60f)]
    public float frontDiagonalAngle = 32f;

    [Tooltip("Weight applied when blending diagonal hits into Front and Left/Right channels.")]
    [Range(0.2f, 1.0f)]
    public float diagonalBlendWeight = 0.75f;

    [Tooltip("Layers treated as physical obstacles.")]
    public LayerMask obstacleLayer = ~0;

    [Header("Haptic Response & Anti-Numbness Pulsing")]
    [Tooltip("Maps normalized proximity [0 = far edge, 1 = danger zone] to output strength [0..1].")]
    public AnimationCurve responseCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("Minimum PWM sent when an obstacle enters range (compensates for ERM motor deadzone).")]
    [Range(0, 100)]
    public byte minMotorPwm = 35;

    [Tooltip("Use rhythmic pulses in the caution zone and solid vibration only when close (reduces skin numbness).")]
    public bool enablePulseModulation = true;

    [Tooltip("Normalized proximity [0..1] above which vibration becomes solid continuous instead of pulsing.")]
    [Range(0.4f, 0.95f)]
    public float dangerSolidThreshold = 0.72f;

    [SerializeField] private float minPulseHz = 2.0f;
    [SerializeField] private float maxPulseHz = 8.0f;
    [SerializeField, Range(0.2f, 0.8f)] private float pulseDutyCycle = 0.45f;

    [Header("Visualization & Debug")]
    public bool showProximityRays = true;
    public bool showHapticHUD = true;

    [Header("Live Directional Output (0 - 254)")]
    [Range(0, 254)] public byte frontIntensity = 0;
    [Range(0, 254)] public byte backIntensity = 0;
    [Range(0, 254)] public byte leftIntensity = 0;
    [Range(0, 254)] public byte rightIntensity = 0;

    private readonly RaycastHit[] _hitBuffer = new RaycastHit[16];
    private Vector3 _prevOriginPos;
    private bool _hasPrevPos;

    private float _frontDist = -1f, _backDist = -1f, _leftDist = -1f, _rightDist = -1f;
    private float _frontProx, _backProx, _leftProx, _rightProx;
    private readonly float[] _pulsePhases = new float[4];

    private void Start()
    {
        if (vibrationManager == null)
        {
            vibrationManager = GetComponent<VibrationManager_minimal>();
            if (vibrationManager == null)
                vibrationManager = FindAnyObjectByType<VibrationManager_minimal>();
        }

        if (sensorOrigin == null)
        {
            sensorOrigin = transform;
        }

        if (autoDetectBodyRadius)
        {
            CapsuleCollider capsule = GetComponentInParent<CapsuleCollider>();
            if (capsule != null)
            {
                bodyRadius = capsule.radius * Mathf.Max(capsule.transform.lossyScale.x, capsule.transform.lossyScale.z);
            }
        }

        _prevOriginPos = sensorOrigin.position;
        _hasPrevPos = true;
    }

    private void Update()
    {
        if (enableHotkeyToggle && WasTogglePressed())
        {
            SetHapticsEnabled(!hapticsEnabled);
        }

        if (!hapticsEnabled)
        {
            frontIntensity = backIntensity = leftIntensity = rightIntensity = 0;
            _frontDist = _backDist = _leftDist = _rightDist = -1f;
            _frontProx = _backProx = _leftProx = _rightProx = 0f;
            if (vibrationManager != null) vibrationManager.StopAllMotors();
            return;
        }

        Transform frame = sensorOrigin != null ? sensorOrigin : transform;
        Vector3 origin = frame.position + Vector3.up * sensorHeightOffset;

        // Horizontal XZ frame ensures VR head pitch/roll never aims sensors into the floor or ceiling.
        Vector3 forward = Vector3.ProjectOnPlane(frame.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        forward.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

        // Frame-displacement velocity works equally for XR Origin room-scale walking and Rigidbody movement.
        float forwardSpeed = 0f;
        if (_hasPrevPos && Time.deltaTime > 0.0001f)
        {
            Vector3 velocity = (frame.position - _prevOriginPos) / Time.deltaTime;
            forwardSpeed = Vector3.Dot(velocity, forward);
        }
        _prevOriginPos = frame.position;
        _hasPrevPos = true;

        // 1. Scan cardinal sectors with asymmetric ranges
        _frontProx = EvaluateSector(origin, forward, frontMaxDistance, frontDangerDistance, out _frontDist);
        _leftProx  = EvaluateSector(origin, -right,  sideMaxDistance,  sideDangerDistance,  out _leftDist);
        _rightProx = EvaluateSector(origin, right,   sideMaxDistance,  sideDangerDistance,  out _rightDist);

        if (muteBackWhileMovingForward && forwardSpeed > 0.15f)
        {
            _backProx = 0f;
            _backDist = -1f;
        }
        else
        {
            _backProx = EvaluateSector(origin, -forward, backMaxDistance, backDangerDistance, out _backDist);
        }

        // 2. Scan front-left and front-right diagonals so doorframes/pillars at ~30-45 deg are never missed
        Vector3 diagLeftDir = Quaternion.AngleAxis(-frontDiagonalAngle, Vector3.up) * forward;
        Vector3 diagRightDir = Quaternion.AngleAxis(frontDiagonalAngle, Vector3.up) * forward;

        float diagLeftProx = EvaluateSector(origin, diagLeftDir, frontMaxDistance * 0.85f, frontDangerDistance, out float diagLeftDist);
        float diagRightProx = EvaluateSector(origin, diagRightDir, frontMaxDistance * 0.85f, frontDangerDistance, out float diagRightDist);

        BlendDiagonal(diagLeftProx, diagLeftDist, ref _frontProx, ref _frontDist, ref _leftProx, ref _leftDist);
        BlendDiagonal(diagRightProx, diagRightDist, ref _frontProx, ref _frontDist, ref _rightProx, ref _rightDist);

        // 3. Convert normalized proximities to PWM (with anti-numbness pulse modulation)
        frontIntensity = ComputeMotorOutput(0, _frontProx);
        backIntensity  = ComputeMotorOutput(1, _backProx);
        leftIntensity  = ComputeMotorOutput(2, _leftProx);
        rightIntensity = ComputeMotorOutput(3, _rightProx);

        if (vibrationManager != null)
        {
            vibrationManager.SetDirectionalIntensities(frontIntensity, backIntensity, leftIntensity, rightIntensity);
        }
    }

    private void BlendDiagonal(float diagProx, float diagDist, ref float frontProx, ref float frontDist, ref float sideProx, ref float sideDist)
    {
        if (diagProx <= 0f) return;

        float weighted = diagProx * diagonalBlendWeight;
        if (weighted > frontProx)
        {
            frontProx = weighted;
            frontDist = diagDist;
        }
        if (weighted > sideProx)
        {
            sideProx = weighted;
            sideDist = diagDist;
        }
    }

    /// <summary>
    /// Enables or disables motor vibration output cleanly.
    /// </summary>
    public void SetHapticsEnabled(bool enabled)
    {
        hapticsEnabled = enabled;
        if (!hapticsEnabled && vibrationManager != null)
        {
            vibrationManager.StopAllMotors();
        }
    }

    private bool WasTogglePressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.hKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.H);
#endif
    }

    private float EvaluateSector(Vector3 origin, Vector3 dir, float maxClearance, float dangerClearance, out float closestClearance)
    {
        // Convert desired body-surface clearance into SphereCast center travel distance.
        float castDistance = Mathf.Max(0.1f, (maxClearance + bodyRadius) - sensorRadius);

        closestClearance = CastClearance(origin, dir, castDistance);

        if (detectHeadOverhangs)
        {
            float upperClearance = CastClearance(origin + Vector3.up * upperHeightOffset, dir, castDistance);
            if (upperClearance >= 0f && (closestClearance < 0f || upperClearance < closestClearance))
            {
                closestClearance = upperClearance;
            }
        }

        if (detectLowObstacles)
        {
            Vector3 lowOrigin = origin + Vector3.up * lowerHeightOffset;
            float lowClearance = CastClearance(lowOrigin, dir, castDistance);
            if (lowClearance >= 0f && (closestClearance < 0f || lowClearance < closestClearance))
            {
                closestClearance = lowClearance;
            }
        }

        if (closestClearance < 0f || closestClearance > maxClearance)
        {
            closestClearance = -1f;
            if (showProximityRays)
                Debug.DrawRay(origin, dir * (maxClearance + bodyRadius), new Color(0.2f, 0.8f, 0.2f, 0.30f));
            return 0f;
        }

        float span = Mathf.Max(0.01f, maxClearance - dangerClearance);
        float proximity = Mathf.Clamp01(1.0f - ((closestClearance - dangerClearance) / span));

        if (showProximityRays)
        {
            Color rayColor = Color.Lerp(Color.yellow, Color.red, proximity);
            Debug.DrawRay(origin, dir * (closestClearance + bodyRadius), rayColor);
        }

        return proximity;
    }

    private float CastClearance(Vector3 origin, Vector3 dir, float castDistance)
    {
        int hitCount = Physics.SphereCastNonAlloc(
            origin,
            sensorRadius,
            dir,
            _hitBuffer,
            castDistance,
            obstacleLayer,
            QueryTriggerInteraction.Ignore
        );

        Transform selfRoot = transform.root;
        float closestWallFromOrigin = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            Collider col = _hitBuffer[i].collider;
            if (col == null || col.transform.IsChildOf(selfRoot))
                continue;

            // hit.distance is center travel; adding sensorRadius gives exact wall distance from origin.
            float wallFromOrigin = _hitBuffer[i].distance + sensorRadius;
            if (wallFromOrigin < closestWallFromOrigin)
                closestWallFromOrigin = wallFromOrigin;
        }

        if (closestWallFromOrigin >= float.MaxValue)
            return -1f;

        return Mathf.Max(0f, closestWallFromOrigin - bodyRadius);
    }

    private byte ComputeMotorOutput(int channelIdx, float normalizedProximity)
    {
        if (normalizedProximity <= 0.001f)
        {
            _pulsePhases[channelIdx] = 0f;
            return 0;
        }

        float curved = Mathf.Clamp01(responseCurve.Evaluate(normalizedProximity));
        byte pwm = (byte)Mathf.RoundToInt(Mathf.Lerp(minMotorPwm, 254f, curved));

        if (enablePulseModulation && normalizedProximity < dangerSolidThreshold)
        {
            float t = Mathf.Clamp01(normalizedProximity / Mathf.Max(0.01f, dangerSolidThreshold));
            float freqHz = Mathf.Lerp(minPulseHz, maxPulseHz, t);
            _pulsePhases[channelIdx] = (_pulsePhases[channelIdx] + Time.deltaTime * freqHz) % 1.0f;

            if (_pulsePhases[channelIdx] > pulseDutyCycle)
                return 0;
        }

        return pwm;
    }

    private void OnDisable()
    {
        if (vibrationManager != null)
            vibrationManager.StopAllMotors();
    }

    private void OnGUI()
    {
        if (!showHapticHUD) return;

        string statusTag = hapticsEnabled ? "<color=#66FF66>[ON]</color>" : "<color=#FF6666>[MUTED - H]</color>";
        GUILayout.BeginArea(new Rect(15, 15, 310, 130), GUI.skin.box);
        GUILayout.Label($"<b>Haptic Obstacle Belt</b> {statusTag}");
        GUILayout.Label(FormatLine("Front", frontIntensity, _frontDist, _frontProx));
        GUILayout.Label(FormatLine("Back ", backIntensity, _backDist, _backProx));
        GUILayout.Label(FormatLine("Left ", leftIntensity, _leftDist, _leftProx));
        GUILayout.Label(FormatLine("Right", rightIntensity, _rightDist, _rightProx));
        GUILayout.EndArea();
    }

    private string FormatLine(string label, byte pwm, float dist, float prox)
    {
        if (dist < 0f) return $"{label}:   0/254  (clear)";
        string zone = prox >= dangerSolidThreshold ? "DANGER" : "caution";
        return $"{label}: {pwm,3}/254  ({dist:0.00} m - {zone})";
    }
}
