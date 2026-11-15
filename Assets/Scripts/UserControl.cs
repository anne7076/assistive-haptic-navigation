using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// First-person keyboard locomotion controller (TP7/TP8 requirement).
/// Supports smooth, framerate-independent Rigidbody physics walking as well as
/// a kinematic fallback mode. Works with both the new Input System and legacy Input Manager.
/// </summary>
public class UserControl : MonoBehaviour
{
    [Header("Locomotion Speeds")]
    [Tooltip("Forward/backward walking speed in meters per second (comfortable brisk navigation).")]
    [Min(0.1f)]
    public float walkSpeed = 4.2f;

    [Tooltip("Speed multiplier when holding Left Shift.")]
    [Min(1f)]
    public float sprintMultiplier = 1.7f;

    [Tooltip("Yaw rotation speed in degrees per second.")]
    [Min(10f)]
    public float angularSpeed = 100f;

    [Header("Physics Tuning")]
    [Tooltip("When enabled, movement uses Rigidbody velocity integration for accurate wall collisions.")]
    public bool usePhysicsMovement = true;

    [Tooltip("Horizontal acceleration rate (m/s^2) when starting to walk.")]
    [Min(1f)]
    public float acceleration = 30f;

    [Tooltip("Horizontal deceleration rate (m/s^2) when input is released.")]
    [Min(1f)]
    public float brakingDeceleration = 40f;

    [Tooltip("Maximum step/threshold/stair height (m) the avatar can automatically step up.")]
    [Range(0f, 0.6f)]
    public float maxStepHeight = 0.45f;

    [Header("Debug")]
    [Tooltip("Draw a forward obstacle ray in Scene view.")]
    public bool showDebugRay = false;

    private Rigidbody _rb;
    private CapsuleCollider _capsule;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _capsule = GetComponent<CapsuleCollider>();
        if (_rb != null)
        {
            // Keep rotation constrained to yaw via script so wall impacts never tip the avatar over.
            _rb.constraints |= RigidbodyConstraints.FreezeRotation;
            _rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        }
    }

    private void Update()
    {
        if (showDebugRay)
        {
            DrawForwardDebugRay();
        }

        if (!usePhysicsMovement || _rb == null)
        {
            ReadInputs(out float horizontal, out float vertical, out bool sprinting);
            float speed = walkSpeed * (sprinting ? sprintMultiplier : 1f);
            transform.Rotate(Vector3.up, horizontal * angularSpeed * Time.deltaTime, Space.Self);
            transform.Translate(Vector3.forward * (vertical * speed * Time.deltaTime), Space.Self);
        }
    }

    private void FixedUpdate()
    {
        if (!usePhysicsMovement || _rb == null)
            return;

        ReadInputs(out float horizontal, out float vertical, out bool sprinting);

        // Physics-friendly yaw turn keeps collisions deterministic without slipping.
        if (Mathf.Abs(horizontal) > 0.01f)
        {
            Quaternion deltaRot = Quaternion.Euler(0f, horizontal * angularSpeed * Time.fixedDeltaTime, 0f);
            _rb.MoveRotation(_rb.rotation * deltaRot);
        }

        float targetSpeed = walkSpeed * (sprinting ? sprintMultiplier : 1f);
        Vector3 desiredHorizontalVel = transform.forward * (vertical * targetSpeed);

        Vector3 currentVel = _rb.linearVelocity;
        Vector3 currentHorizontalVel = new Vector3(currentVel.x, 0f, currentVel.z);

        float rate = Mathf.Abs(vertical) > 0.01f ? acceleration : brakingDeceleration;
        Vector3 nextHorizontalVel = Vector3.MoveTowards(currentHorizontalVel, desiredHorizontalVel, rate * Time.fixedDeltaTime);

        if (maxStepHeight > 0.01f && Mathf.Abs(vertical) > 0.01f && _capsule != null)
        {
            TryClimbStep(desiredHorizontalVel.normalized, targetSpeed);
        }

        _rb.linearVelocity = new Vector3(nextHorizontalVel.x, _rb.linearVelocity.y, nextHorizontalVel.z);
    }

    private void TryClimbStep(Vector3 moveDir, float currentSpeed)
    {
        if (moveDir.sqrMagnitude < 0.001f) return;

        Bounds b = _capsule.bounds;
        float feetY = b.min.y;
        float topY = b.max.y;
        float radius = _capsule.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.z);
        float castRadius = Mathf.Max(0.05f, radius * 0.92f);

        // Sweep the entire lower step zone (from 2 cm above feet up to maxStepHeight)
        // so floating bars, curbs, doorsteps, and stair risers at any ankle/shin height are caught.
        Vector3 lowerBottom = new Vector3(b.center.x, feetY + 0.02f + castRadius, b.center.z);
        Vector3 lowerTop = new Vector3(b.center.x, Mathf.Max(lowerBottom.y, feetY + maxStepHeight - castRadius), b.center.z);

        float probeDist = Mathf.Max(0.24f, currentSpeed * Time.fixedDeltaTime + 0.14f);
        RaycastHit[] lowHits = Physics.CapsuleCastAll(lowerBottom, lowerTop, castRadius, moveDir, probeDist, ~0, QueryTriggerInteraction.Ignore);
        if (lowHits == null || lowHits.Length == 0)
            return;

        float bestRise = 0f;
        for (int i = 0; i < lowHits.Length; i++)
        {
            RaycastHit lowHit = lowHits[i];
            if (lowHit.collider == null || lowHit.collider.transform.IsChildOf(transform))
                continue;

            // Ignore flat floor surfaces under the feet
            if (lowHit.normal.y > 0.75f && lowHit.distance <= 0.001f)
                continue;

            float candidateRise = 0f;

            // Case 1: The entire obstacle's top is within step height (e.g., doorsteps, curbs, low bars/plates)
            float colliderTopRise = lowHit.collider.bounds.max.y - feetY;
            if (colliderTopRise > 0.01f && colliderTopRise <= maxStepHeight)
            {
                candidateRise = colliderTopRise;
            }
            else
            {
                // Case 2: Larger mesh (e.g., staircase or raised floor slab) — probe downward just ahead of the hit point
                Vector3 hitPoint = lowHit.distance > 0.001f
                    ? lowHit.point
                    : new Vector3(b.center.x, feetY + 0.1f, b.center.z) + moveDir * radius;

                Vector3 downOrigin = new Vector3(hitPoint.x, feetY + maxStepHeight + 0.08f, hitPoint.z) + moveDir * 0.05f;
                if (Physics.SphereCast(downOrigin, 0.05f, Vector3.down, out RaycastHit topHit, maxStepHeight + 0.10f, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (!topHit.collider.transform.IsChildOf(transform) && topHit.normal.y > 0.55f)
                    {
                        float stepRise = topHit.point.y - feetY;
                        if (stepRise > 0.01f && stepRise <= maxStepHeight)
                        {
                            candidateRise = stepRise;
                        }
                    }
                }
            }

            if (candidateRise > bestRise && candidateRise <= maxStepHeight)
            {
                bestRise = candidateRise;
            }
        }

        if (bestRise <= 0.01f)
            return;

        // Verify that the upper body (shifted up by bestRise) has clear room to move forward
        Vector3 upperBottom = new Vector3(b.center.x, feetY + bestRise + 0.04f + castRadius, b.center.z);
        Vector3 upperTop = new Vector3(b.center.x, Mathf.Max(upperBottom.y, topY + bestRise - castRadius), b.center.z);
        RaycastHit[] upperHits = Physics.CapsuleCastAll(upperBottom, upperTop, castRadius * 0.9f, moveDir, 0.16f, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < upperHits.Length; i++)
        {
            if (upperHits[i].collider != null && !upperHits[i].collider.transform.IsChildOf(transform) && upperHits[i].distance > 0.001f)
            {
                return;
            }
        }

        _rb.position += new Vector3(moveDir.x * 0.06f, bestRise + 0.02f, moveDir.z * 0.06f);
        Physics.SyncTransforms();
        if (_rb.linearVelocity.y < 0f)
        {
            _rb.linearVelocity = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z);
        }
    }

    private void ReadInputs(out float horizontal, out float vertical, out bool sprinting)
    {
        horizontal = 0f;
        vertical = 0f;
        sprinting = false;

#if ENABLE_INPUT_SYSTEM
        Keyboard kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) horizontal += 1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed)  horizontal -= 1f;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed)    vertical += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed)  vertical -= 1f;
            sprinting = kb.leftShiftKey.isPressed;
        }
#else
        horizontal = Input.GetAxis("Horizontal");
        vertical = Input.GetAxis("Vertical");
        sprinting = Input.GetKey(KeyCode.LeftShift);
#endif
    }

    private void DrawForwardDebugRay()
    {
        Vector3 origin = transform.position + Vector3.up * 0.1f;
        Vector3 fwd = transform.forward;
        if (Physics.Raycast(origin, fwd, out RaycastHit hit, 4f, ~0, QueryTriggerInteraction.Ignore))
        {
            Debug.DrawRay(origin, fwd * hit.distance, Color.red);
        }
        else
        {
            Debug.DrawRay(origin, fwd * 4f, Color.green);
        }
    }
}
