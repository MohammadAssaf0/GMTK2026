using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// First-person "Drifter" controller for GMTK2026.
///
/// Controls:
///   WASD / Arrows  - walk
///   Left Shift     - sprint
///   Space          - jump
///   Command / Ctrl - crouch (hold)
///   Right mouse    - zoom (hold)
///   Mouse          - look around
///   Esc            - release cursor, click to re-capture
///
/// Walks on ANY collider (not just terrain), slides down slopes steeper
/// than <see cref="slideAngle"/>, and walking speed is affected by slope
/// (slower uphill, faster downhill).
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class DrifterController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Pivot that pitches up/down and moves when crouching. The camera should be a child of this.")]
    public Transform cameraHolder;
    public Camera playerCamera;
    [Tooltip("Visible capsule body. Scales down automatically when crouching.")]
    public Transform bodyVisual;

    [Header("Movement")]
    public float walkSpeed = 4.5f;
    public float sprintSpeed = 8.0f;
    public float crouchSpeed = 2.2f;
    [Tooltip("How quickly the drifter reaches target speed on the ground.")]
    public float acceleration = 14f;
    [Range(0f, 1f), Tooltip("Fraction of control kept while airborne.")]
    public float airControl = 0.4f;

    [Header("Jump & Gravity")]
    public float jumpHeight = 1.2f;
    public float gravity = -22f;
    [Tooltip("Grace period after stepping off a ledge in which a jump is still allowed.")]
    public float coyoteTime = 0.12f;

    [Header("Slopes")]
    [Tooltip("Above this ground angle (degrees) the drifter slides downhill.")]
    public float slideAngle = 45f;
    public float slideAcceleration = 20f;
    public float maxSlideSpeed = 14f;
    [Range(0f, 1f), Tooltip("How much slope affects walk speed: slower uphill, faster downhill.")]
    public float slopeSpeedInfluence = 0.35f;

    [Header("Crouch")]
    public float standHeight = 1.8f;
    public float crouchHeight = 1.0f;
    public float crouchTransitionSpeed = 10f;
    [Tooltip("Camera height as a fraction of body height.")]
    public float eyeHeightRatio = 0.9f;

    [Header("Mouse Look")]
    public float mouseSensitivity = 0.12f;
    public float maxLookAngle = 89f;

    [Header("Zoom (Right Mouse)")]
    public float normalFov = 60f;
    public float zoomFov = 30f;
    public float zoomSpeed = 12f;

    // ---- Public state (read by DrifterFootsteps and anything else) ----
    public bool IsGrounded { get; private set; }
    public bool IsSliding { get; private set; }
    public bool IsCrouching { get; private set; }
    public bool IsSprinting { get; private set; }
    public bool IsZooming { get; private set; }
    /// <summary>Horizontal speed in m/s.</summary>
    public float HorizontalSpeed { get; private set; }
    /// <summary>Vertical velocity right before the last landing (negative = falling).</summary>
    public float LastLandingSpeed { get; private set; }
    /// <summary>Fired the frame the drifter jumps.</summary>
    public event System.Action Jumped;
    /// <summary>Fired the frame the drifter lands. Argument = fall speed (m/s, positive).</summary>
    public event System.Action<float> Landed;

    CharacterController controller;
    Vector3 horizontalVelocity;
    Vector3 slideVelocity;
    float verticalVelocity;
    float pitch;
    float lastGroundedTime = -99f;
    bool wasGrounded;
    Vector3 groundNormal = Vector3.up;
    float currentHeight;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        currentHeight = standHeight;
        ApplyHeight(currentHeight);

        if (playerCamera == null) playerCamera = GetComponentInChildren<Camera>();
        if (cameraHolder == null && playerCamera != null) cameraHolder = playerCamera.transform.parent;
        if (playerCamera != null) playerCamera.fieldOfView = normalFov;
    }

    void Start()
    {
        LockCursor(true);
    }

    void Update()
    {
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        if (kb == null) return;

        HandleCursor(kb, mouse);
        HandleLook(mouse);
        HandleZoom(mouse);

        ReadGround();
        HandleCrouch(kb);
        HandleMovement(kb);

        // Combine and move.
        Vector3 motion = horizontalVelocity + slideVelocity + Vector3.up * verticalVelocity;
        controller.Move(motion * Time.deltaTime);

        // Grounded state AFTER Move (Unity updates isGrounded inside Move).
        bool groundedNow = controller.isGrounded;
        if (groundedNow) lastGroundedTime = Time.time;
        if (groundedNow && !wasGrounded)
        {
            LastLandingSpeed = Mathf.Max(0f, -verticalVelocity);
            Landed?.Invoke(LastLandingSpeed);
        }
        wasGrounded = groundedNow;
        IsGrounded = groundedNow;
        HorizontalSpeed = new Vector3(controller.velocity.x, 0f, controller.velocity.z).magnitude;
    }

    // ------------------------------------------------------------------ look

    void HandleCursor(Keyboard kb, Mouse mouse)
    {
        if (kb.escapeKey.wasPressedThisFrame) LockCursor(false);
        if (mouse != null && mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
            LockCursor(true);
    }

    void LockCursor(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    void HandleLook(Mouse mouse)
    {
        if (mouse == null || Cursor.lockState != CursorLockMode.Locked) return;

        // Lower sensitivity while zoomed so aiming stays comfortable.
        float fovScale = playerCamera != null ? playerCamera.fieldOfView / normalFov : 1f;
        Vector2 delta = mouse.delta.ReadValue() * mouseSensitivity * fovScale;

        transform.Rotate(0f, delta.x, 0f);
        pitch = Mathf.Clamp(pitch - delta.y, -maxLookAngle, maxLookAngle);
        if (cameraHolder != null)
            cameraHolder.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    void HandleZoom(Mouse mouse)
    {
        IsZooming = mouse != null && mouse.rightButton.isPressed && Cursor.lockState == CursorLockMode.Locked;
        if (playerCamera == null) return;
        float target = IsZooming ? zoomFov : normalFov;
        playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, target, zoomSpeed * Time.deltaTime);
    }

    // ---------------------------------------------------------------- ground

    void ReadGround()
    {
        groundNormal = Vector3.up;
        float castRadius = controller.radius * 0.95f;
        Vector3 origin = transform.position + controller.center;
        float castDistance = controller.height * 0.5f + 0.3f;
        if (Physics.SphereCast(origin, castRadius, Vector3.down, out RaycastHit hit,
                castDistance, ~0, QueryTriggerInteraction.Ignore))
        {
            groundNormal = hit.normal;
        }
    }

    float GroundAngle => Vector3.Angle(groundNormal, Vector3.up);

    // ---------------------------------------------------------------- crouch

    void HandleCrouch(Keyboard kb)
    {
        // Command on Mac; Ctrl works too (e.g. when Command is captured by the OS, or on Windows).
        bool wantCrouch = kb.leftCommandKey.isPressed || kb.rightCommandKey.isPressed
                       || kb.leftCtrlKey.isPressed;

        if (!wantCrouch && IsCrouching && !CanStandUp())
            wantCrouch = true; // blocked by a ceiling - stay crouched

        IsCrouching = wantCrouch;
        float targetHeight = IsCrouching ? crouchHeight : standHeight;
        currentHeight = Mathf.Lerp(currentHeight, targetHeight, crouchTransitionSpeed * Time.deltaTime);
        ApplyHeight(currentHeight);
    }

    bool CanStandUp()
    {
        Vector3 bottom = transform.position + Vector3.up * controller.radius;
        float clearance = standHeight - controller.radius * 2f + 0.05f;
        return !Physics.SphereCast(bottom, controller.radius * 0.95f, Vector3.up,
            out _, clearance, ~0, QueryTriggerInteraction.Ignore);
    }

    void ApplyHeight(float height)
    {
        controller.height = height;
        controller.center = new Vector3(0f, height * 0.5f, 0f);
        if (cameraHolder != null)
            cameraHolder.localPosition = new Vector3(0f, height * eyeHeightRatio, 0f);
        if (bodyVisual != null)
        {
            // Unity's capsule mesh is 2m tall at scale 1, so scaleY = height / 2.
            Vector3 s = bodyVisual.localScale;
            bodyVisual.localScale = new Vector3(s.x, height * 0.5f, s.z);
            bodyVisual.localPosition = new Vector3(0f, height * 0.5f, 0f);
        }
    }

    // -------------------------------------------------------------- movement

    void HandleMovement(Keyboard kb)
    {
        // --- input direction ---
        float x = (kb.dKey.isPressed || kb.rightArrowKey.isPressed ? 1f : 0f)
                - (kb.aKey.isPressed || kb.leftArrowKey.isPressed ? 1f : 0f);
        float z = (kb.wKey.isPressed || kb.upArrowKey.isPressed ? 1f : 0f)
                - (kb.sKey.isPressed || kb.downArrowKey.isPressed ? 1f : 0f);
        Vector3 wishDir = Vector3.ClampMagnitude(transform.right * x + transform.forward * z, 1f);

        IsSprinting = kb.leftShiftKey.isPressed && !IsCrouching && z > 0f;
        float targetSpeed = IsCrouching ? crouchSpeed : (IsSprinting ? sprintSpeed : walkSpeed);

        bool onGround = controller.isGrounded;
        float angle = GroundAngle;
        Vector3 slopeDown = Vector3.ProjectOnPlane(Vector3.down, groundNormal);
        if (slopeDown.sqrMagnitude > 0.0001f) slopeDown.Normalize(); else slopeDown = Vector3.zero;

        // --- walk speed relative to slope: slower uphill, faster downhill ---
        if (onGround && wishDir.sqrMagnitude > 0.01f && angle > 1f && angle <= slideAngle)
        {
            float downhill = Vector3.Dot(wishDir.normalized, slopeDown); // 1 downhill, -1 uphill
            float steepness = Mathf.Clamp01(angle / slideAngle);
            targetSpeed *= 1f + downhill * steepness * slopeSpeedInfluence;
        }

        // --- steep slope: slide downhill ---
        IsSliding = onGround && angle > slideAngle;
        if (IsSliding)
        {
            slideVelocity += slopeDown * slideAcceleration * Time.deltaTime;
            slideVelocity = Vector3.ClampMagnitude(slideVelocity, maxSlideSpeed);
            targetSpeed *= 0.4f; // little control while sliding
        }
        else
        {
            // Decay the slide quickly once back on walkable ground.
            slideVelocity = Vector3.MoveTowards(slideVelocity, Vector3.zero,
                slideAcceleration * 2f * Time.deltaTime);
        }

        // --- accelerate toward wish velocity ---
        float control = onGround ? 1f : airControl;
        horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, wishDir * targetSpeed,
            acceleration * control * Time.deltaTime);

        // --- gravity & jump ---
        if (onGround && verticalVelocity < 0f)
            verticalVelocity = -3f; // stick to the ground / slopes

        bool jumpPressed = kb.spaceKey.wasPressedThisFrame;
        bool canJump = (onGround || Time.time - lastGroundedTime <= coyoteTime) && !IsSliding;
        if (jumpPressed && canJump)
        {
            verticalVelocity = Mathf.Sqrt(2f * -gravity * jumpHeight);
            lastGroundedTime = -99f; // consume coyote time
            Jumped?.Invoke();
        }

        verticalVelocity += gravity * Time.deltaTime;
    }
}
