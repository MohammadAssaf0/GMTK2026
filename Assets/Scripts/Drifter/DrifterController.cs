using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// First-person Drifter controller - a faithful port of the classic
/// FirstPersonDrifter (Assets/Drifter/Prefabs/Player.prefab) to the new
/// Input System, with crouch + zoom + slope-relative speed added.
///
/// Matches the Player prefab exactly:
///   - Center-pivot CharacterController: height 2, radius 0.5, center (0,0,0)
///   - walkSpeed 5, runSpeed 10, jumpSpeed 4, gravity 10, full air control
///   - Instant velocity changes (no acceleration ramp), diagonal speed limited
///   - antiBumpFactor 0.75 so walking down slopes doesn't bump
///   - Slides down slopes steeper than the controller's slope limit
///   - Pushes rigidbodies it walks into
///   - Camera at local (0, 0.7, 0) - eye 0.3 below the capsule top
///   - Resets the player if it falls below resetBelowY (like CheckIfBelowLevel)
///
/// Controls:
///   WASD / Arrows  - walk        | Left Shift  - run
///   Space          - jump        | Command/Ctrl - crouch (hold)
///   Right mouse    - zoom (hold) | Esc - release cursor, click re-captures
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

    [Header("Movement (Player prefab values)")]
    public float walkSpeed = 5f;
    public float runSpeed = 10f;
    public bool enableRunning = true;
    public float crouchSpeed = 2.5f;
    [Tooltip("If true, direction changes mid-air work exactly like on the ground.")]
    public bool airControl = true;
    [Tooltip("Downward push while grounded so walking down slopes doesn't bump. 0.75 = prefab value.")]
    public float antiBumpFactor = 0.75f;

    [Header("Jump & Gravity (Player prefab values)")]
    [Tooltip("Vertical launch velocity, exactly like the prefab (not jump height).")]
    public float jumpSpeed = 4f;
    [Tooltip("Positive value, like the prefab. 10 = the floaty drifter feel.")]
    public float gravity = 10f;
    [Tooltip("Grace period after stepping off a ledge in which a jump is still allowed.")]
    public float coyoteTime = 0.12f;
    [Tooltip("How far below the feet to search for ground when walking downhill, so the drifter hugs dune slopes instead of floating off them. 0 = off.")]
    public float groundSnapDistance = 1.8f;

    [Header("Slopes")]
    [Tooltip("Slide down slopes steeper than the CharacterController's Slope Limit.")]
    public bool slideWhenOverSlopeLimit = true;
    [Tooltip("Slide speed, same model as the prefab's FirstPersonDrifter.")]
    public float slideSpeed = 5f;
    [Range(0f, 1f), Tooltip("How much slope affects walk speed: slower uphill, faster downhill. 0 = off (prefab behavior).")]
    public float slopeSpeedInfluence = 0.35f;

    [Header("Crouch")]
    public float standHeight = 2f;
    public float crouchHeight = 1.2f;
    public float crouchTransitionSpeed = 10f;
    [Tooltip("Eye distance below the top of the capsule. 0.3 = prefab camera position.")]
    public float eyeOffsetFromTop = 0.3f;

    [Header("Mouse Look (prefab: +-85 degrees)")]
    public float mouseSensitivity = 0.12f;
    public float maxLookAngle = 85f;

    [Header("Zoom (Player prefab values)")]
    public float normalFov = 60f;
    public float zoomFov = 30f;
    public float zoomSpeed = 9f;
    [Tooltip("Extra FOV while sprinting - makes running FEEL fast. 0 = off.")]
    public float sprintFovBoost = 10f;

    [Header("Slide Tumble")]
    [Tooltip("When sliding down a steep slope, tumble head-over-heels and then get back up.")]
    public bool tumbleEnabled = true;
    [Tooltip("The slide must last this long (seconds) before the tumble kicks in.")]
    public float tumbleAfterSliding = 0.45f;
    [Tooltip("Tumble spin speed, degrees per second.")]
    public float tumbleSpinSpeed = 380f;
    [Tooltip("How long getting back up takes, seconds.")]
    public float getUpDuration = 0.9f;

    [Header("Safety")]
    [Tooltip("Like the prefab's CheckIfBelowLevel: falling below this Y teleports back to the start.")]
    public float resetBelowY = -20f;

    // ---- Public state (read by DrifterFootsteps and anything else) ----
    public bool IsGrounded { get; private set; }
    public bool IsSliding { get; private set; }
    public bool IsCrouching { get; private set; }
    public bool IsSprinting { get; private set; }
    public bool IsZooming { get; private set; }
    /// <summary>True while tumbling down a slope or getting back up.</summary>
    public bool IsTumbling => tumbleState != 0;
    /// <summary>Angle (degrees) of the ground under the player right now.</summary>
    public float CurrentGroundAngle { get; private set; }
    public float HorizontalSpeed { get; private set; }
    public event System.Action Jumped;
    /// <summary>Fired on landing. Argument = fall speed (m/s, positive).</summary>
    public event System.Action<float> Landed;

    CharacterController controller;
    Vector3 moveDirection = Vector3.zero;   // same model as FirstPersonDrifter
    bool grounded;
    bool playerControl;
    float speed;
    float pitch;
    float lastGroundedTime = -99f;
    float currentHeight;
    Vector3 contactPoint;
    Vector3 startPosition;
    Quaternion startRotation;

    // tumble state
    int tumbleState;          // 0 = none, 1 = tumbling, 2 = getting up
    float slidingTime;
    float tumblePitch;
    float tumblePitchAtGetUp;
    float getUpT;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        currentHeight = standHeight;
        ApplyHeight(currentHeight);

        if (playerCamera == null) playerCamera = GetComponentInChildren<Camera>();
        if (cameraHolder == null && playerCamera != null) cameraHolder = playerCamera.transform.parent;
        if (playerCamera != null) playerCamera.fieldOfView = normalFov;

        startPosition = transform.position;
        startRotation = transform.rotation;

        // Safety: if the drifter STARTS below the reset line, push the line
        // further down - otherwise it teleports to start every frame and
        // appears completely frozen.
        if (startPosition.y <= resetBelowY + 2f)
            resetBelowY = startPosition.y - 100f;
    }

    void Start()
    {
        LockCursor(true);
        speed = walkSpeed;
        FixSpawnIfUnderground();
    }

    // If the saved spawn point ended up under the terrain (easy to do when
    // moving scenes/models around), find the ground surface above and stand
    // on it - otherwise the player falls forever and the reset loop makes
    // the whole world (and the sandstorm) appear to "restart" every few
    // seconds.
    void FixSpawnIfUnderground()
    {
        // Scan BOTH directions with a MARCHING raycast. Two Unity gotchas:
        // 1. rays skip backfaces, and imported models are often flipped;
        // 2. a ray reports only the FIRST hit per collider - so a mesh with
        //    an inner shell hides its real top surface. Marching steps past
        //    each hit and keeps going, so every layer is found.
        float groundY = float.NegativeInfinity;
        Vector3 xz = startPosition;
        groundY = Mathf.Max(groundY, MarchScan(new Vector3(xz.x, xz.y + 1000f, xz.z), Vector3.down, 3000f));
        groundY = Mathf.Max(groundY, MarchScan(new Vector3(xz.x, xz.y - 1000f, xz.z), Vector3.up, 3000f));
        if (float.IsNegativeInfinity(groundY)) return; // no ground at all - nothing to do

        float feetY = startPosition.y - controller.height * 0.5f;
        if (feetY < groundY - 0.25f)
        {
            startPosition.y = groundY + controller.height * 0.5f + 0.6f;
            transform.position = startPosition;
            Physics.SyncTransforms();
            Debug.Log($"Drifter spawn was under the ground - moved up onto the surface at y={startPosition.y:F1}.");
        }

        // Keep the fall-reset line safely below the actual start height.
        if (startPosition.y <= resetBelowY + 2f)
            resetBelowY = startPosition.y - 100f;
    }

    // Marching raycast: steps THROUGH every surface along the ray (a normal
    // raycast stops at the first hit per collider) and returns the highest
    // hit point, ignoring our own colliders.
    float MarchScan(Vector3 origin, Vector3 dir, float maxDistance)
    {
        float best = float.NegativeInfinity;
        Vector3 o = origin;
        float remaining = maxDistance;
        for (int i = 0; i < 16 && remaining > 0f; i++)
        {
            if (!Physics.Raycast(o, dir, out RaycastHit hit, remaining, ~0, QueryTriggerInteraction.Ignore))
                break;
            if (hit.collider.transform.root != transform.root && hit.point.y > best)
                best = hit.point.y;
            float step = hit.distance + 0.1f;
            o += dir * step;
            remaining -= step;
        }
        return best;
    }

    void Update()
    {
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        if (kb == null) return;

        HandleCursor(kb, mouse);
        HandleLook(mouse);
        HandleZoom(mouse);
        HandleCrouch(kb);
        HandleMovement(kb);
        UpdateTumble();
        CheckBelowLevel();
    }

    // ---------------------------------------------------------------- tumble
    // Slide long enough -> the camera tumbles head-over-heels and drops to
    // ground level; when the slide ends, a smooth "get up" recovery plays.
    void UpdateTumble()
    {
        if (!tumbleEnabled || cameraHolder == null) { tumbleState = 0; return; }

        // What ApplyHeight set this frame = the normal standing pose.
        Vector3 normalPos = cameraHolder.localPosition;
        float groundLocalY = -controller.height * 0.5f + 0.35f; // camera at ground level

        switch (tumbleState)
        {
            case 0: // watching for a long slide
                if (IsSliding && HorizontalSpeed > 3f) slidingTime += Time.deltaTime;
                else slidingTime = 0f;
                if (slidingTime >= tumbleAfterSliding)
                {
                    tumbleState = 1;
                    tumblePitch = 0f;
                }
                break;

            case 1: // tumbling
                tumblePitch += tumbleSpinSpeed * Time.deltaTime;
                float wobble = Mathf.Sin(Time.time * 9f) * 10f;
                cameraHolder.localRotation = Quaternion.Euler(tumblePitch, 0f, wobble);
                cameraHolder.localPosition = Vector3.Lerp(normalPos,
                    new Vector3(0f, groundLocalY, 0f), 0.85f);

                if (!IsSliding && HorizontalSpeed < 3.5f)
                {
                    tumbleState = 2;
                    getUpT = 0f;
                    // land the spin on the nearest upright angle
                    tumblePitchAtGetUp = Mathf.Repeat(tumblePitch, 360f);
                    if (tumblePitchAtGetUp > 180f) tumblePitchAtGetUp -= 360f;
                }
                break;

            case 2: // getting up
                getUpT += Time.deltaTime / Mathf.Max(0.1f, getUpDuration);
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(getUpT));
                float p = Mathf.Lerp(tumblePitchAtGetUp, 0f, k);
                cameraHolder.localRotation = Quaternion.Euler(pitch * k + p * (1f - k), 0f, 0f);
                cameraHolder.localPosition = Vector3.Lerp(
                    new Vector3(0f, groundLocalY, 0f), normalPos, k);

                if (getUpT >= 1f)
                {
                    tumbleState = 0;
                    slidingTime = 0f;
                }
                break;
        }
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
        if (tumbleState != 0) return; // the tumble owns the camera
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
        float target = IsZooming ? zoomFov
            : normalFov + (IsSprinting && HorizontalSpeed > 1f ? sprintFovBoost : 0f);
        playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, target, zoomSpeed * Time.deltaTime);
    }

    // ---------------------------------------------------------------- crouch

    void HandleCrouch(Keyboard kb)
    {
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
        // Capsule bottom stays planted; check clearance above the head.
        Vector3 bottomSphere = transform.position + controller.center
                             + Vector3.up * (controller.radius - controller.height * 0.5f);
        float clearance = standHeight - controller.radius * 2f + 0.05f;
        return !Physics.SphereCast(bottomSphere, controller.radius * 0.95f, Vector3.up,
            out _, clearance, ~0, QueryTriggerInteraction.Ignore);
    }

    void ApplyHeight(float height)
    {
        // Center pivot like the Player prefab: standing center = (0,0,0).
        // While crouching, the center drops so the feet stay planted.
        float centerY = (height - standHeight) * 0.5f;
        controller.height = height;
        controller.center = new Vector3(0f, centerY, 0f);

        // Eye stays a fixed distance below the capsule top (prefab: 0.7 = 1.0 - 0.3).
        float topY = centerY + height * 0.5f;
        if (cameraHolder != null)
            cameraHolder.localPosition = new Vector3(0f, topY - eyeOffsetFromTop, 0f);

        if (bodyVisual != null)
        {
            // Unity's capsule mesh: height 2, radius 0.5 at scale 1 - the prefab's exact look.
            bodyVisual.localPosition = controller.center;
            bodyVisual.localScale = new Vector3(controller.radius * 2f, height * 0.5f, controller.radius * 2f);
        }
    }

    // -------------------------------------------------------------- movement
    // Direct port of FirstPersonDrifter.FixedUpdate to the new Input System.

    void HandleMovement(Keyboard kb)
    {
        float inputX = (kb.dKey.isPressed || kb.rightArrowKey.isPressed ? 1f : 0f)
                     - (kb.aKey.isPressed || kb.leftArrowKey.isPressed ? 1f : 0f);
        float inputY = (kb.wKey.isPressed || kb.upArrowKey.isPressed ? 1f : 0f)
                     - (kb.sKey.isPressed || kb.downArrowKey.isPressed ? 1f : 0f);

        // No control while tumbling - you're rolling down a dune.
        if (tumbleState != 0) { inputX = 0f; inputY = 0f; }

        // Limit diagonal speed, exactly like the original.
        float inputModifyFactor = (inputX != 0f && inputY != 0f) ? 0.7071f : 1f;

        bool wasGrounded = grounded;

        if (grounded)
        {
            lastGroundedTime = Time.time;

            // --- detect a too-steep slope under our feet (original method) ---
            bool sliding = false;
            RaycastHit hit;
            float rayDistance = controller.height * 0.5f + controller.radius;
            float slideLimit = controller.slopeLimit - 0.1f;
            Vector3 groundNormal = Vector3.up;
            if (Physics.Raycast(transform.position + controller.center, -Vector3.up, out hit, rayDistance))
            {
                groundNormal = hit.normal;
                if (Vector3.Angle(hit.normal, Vector3.up) > slideLimit) sliding = true;
            }
            else if (Physics.Raycast(contactPoint + Vector3.up, -Vector3.up, out hit))
            {
                groundNormal = hit.normal;
                if (Vector3.Angle(hit.normal, Vector3.up) > slideLimit) sliding = true;
            }
            CurrentGroundAngle = Vector3.Angle(groundNormal, Vector3.up);

            // --- speed: crouch < walk < run ---
            IsSprinting = enableRunning && kb.leftShiftKey.isPressed && !IsCrouching;
            speed = IsCrouching ? crouchSpeed : (IsSprinting ? runSpeed : walkSpeed);

            IsSliding = sliding && slideWhenOverSlopeLimit;
            if (IsSliding)
            {
                // Original slide: head straight down the slope, no player control.
                Vector3 hitNormal = groundNormal;
                moveDirection = new Vector3(hitNormal.x, -hitNormal.y, hitNormal.z);
                Vector3.OrthoNormalize(ref hitNormal, ref moveDirection);
                moveDirection *= slideSpeed;
                playerControl = false;
            }
            else
            {
                moveDirection = new Vector3(inputX * inputModifyFactor, -antiBumpFactor, inputY * inputModifyFactor);
                moveDirection = transform.TransformDirection(moveDirection) * speed;
                playerControl = true;

                // Walk speed relative to slope: slower uphill, faster downhill (addition).
                float angle = Vector3.Angle(groundNormal, Vector3.up);
                if (slopeSpeedInfluence > 0f && angle > 1f && angle <= controller.slopeLimit)
                {
                    Vector3 slopeDown = Vector3.ProjectOnPlane(Vector3.down, groundNormal).normalized;
                    Vector3 flatMove = new Vector3(moveDirection.x, 0f, moveDirection.z);
                    if (flatMove.sqrMagnitude > 0.01f)
                    {
                        float downhill = Vector3.Dot(flatMove.normalized, slopeDown);
                        float factor = 1f + downhill * (angle / controller.slopeLimit) * slopeSpeedInfluence;
                        moveDirection.x *= factor;
                        moveDirection.z *= factor;
                    }
                }
            }
        }
        else if (airControl && playerControl)
        {
            // Full air control, exactly like the prefab (airControl = true).
            moveDirection.x = inputX * speed * inputModifyFactor;
            moveDirection.z = inputY * speed * inputModifyFactor;
            Vector3 flat = new Vector3(moveDirection.x, 0f, moveDirection.z);
            flat = transform.TransformDirection(flat);
            moveDirection.x = flat.x;
            moveDirection.z = flat.z;
        }

        // --- jump (with a small coyote-time forgiveness) ---
        bool canJump = !IsSliding && tumbleState == 0 && (grounded || Time.time - lastGroundedTime <= coyoteTime);
        if (kb.spaceKey.wasPressedThisFrame && canJump)
        {
            moveDirection.y = jumpSpeed;
            lastGroundedTime = -99f;
            grounded = false;
            Jumped?.Invoke();
        }

        // --- gravity (prefab model: positive value, subtracted) ---
        moveDirection.y -= gravity * Time.deltaTime;

        float fallSpeed = -moveDirection.y;
        grounded = (controller.Move(moveDirection * Time.deltaTime) & CollisionFlags.Below) != 0;

        // --- ground snap: when walking downhill, stick to the slope instead
        // of floating off it. Only when we just left the ground while moving
        // down (never right after a jump, since then y > 0).
        if (!grounded && wasGrounded && moveDirection.y <= 0f && groundSnapDistance > 0f)
        {
            Vector3 origin = transform.position + controller.center;
            float footGap = controller.height * 0.5f - controller.radius;
            float castDistance = footGap + groundSnapDistance;
            if (Physics.SphereCast(origin, controller.radius * 0.9f, Vector3.down,
                    out RaycastHit snapHit, castDistance, ~0, QueryTriggerInteraction.Ignore))
            {
                float drop = snapHit.distance - footGap + controller.skinWidth;
                if (drop > 0f)
                    grounded = (controller.Move(Vector3.down * drop) & CollisionFlags.Below) != 0;
            }
        }

        if (grounded && !wasGrounded)
            Landed?.Invoke(Mathf.Max(0f, fallSpeed));

        IsGrounded = grounded;
        HorizontalSpeed = new Vector3(controller.velocity.x, 0f, controller.velocity.z).magnitude;
    }

    // Push rigidbodies + remember the contact point, exactly like the original.
    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        contactPoint = hit.point;

        Rigidbody body = hit.collider.attachedRigidbody;
        if (body == null || body.isKinematic) return;

        Vector3 pushDir = new Vector3(hit.moveDirection.x, 0f, hit.moveDirection.z);
        body.AddForce(pushDir * speed * 10f, ForceMode.Force);
    }

    // Like the prefab's CheckIfBelowLevel (resetBelowThisY: -20).
    void CheckBelowLevel()
    {
        if (transform.position.y < resetBelowY)
        {
            moveDirection = Vector3.zero;
            transform.SetPositionAndRotation(startPosition, startRotation);
            Physics.SyncTransforms();
        }
    }
}
