using UnityEngine;

/// <summary>
/// Footsteps for the Drifter - both AUDIO (footstep sounds whose rate and
/// volume follow walking / sprinting / crouching) and VISUAL (head-bob on
/// the camera, synced so each "step" of the bob triggers a sound).
///
/// Works out of the box: if no audio clips are assigned it synthesizes a
/// soft procedural step sound at startup, so you hear footsteps immediately.
/// Drop your own clips into <see cref="footstepClips"/> whenever you have them.
/// </summary>
[RequireComponent(typeof(DrifterController))]
public class DrifterFootsteps : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Transform that gets the head-bob. Use the Camera itself (child of CameraHolder). Auto-found if empty.")]
    public Transform bobTarget;
    public AudioSource audioSource;

    [Header("Head Bob (visual)")]
    public bool headBobEnabled = true;
    [Tooltip("Steps per second while walking.")]
    public float stepFrequency = 1.8f;
    public float sprintFrequencyMultiplier = 1.4f;
    public float crouchFrequencyMultiplier = 0.75f;
    [Tooltip("Vertical bob amplitude in meters.")]
    public float bobHeight = 0.05f;
    [Tooltip("Horizontal sway amplitude in meters.")]
    public float bobSway = 0.03f;
    [Tooltip("Slight roll of the camera while walking, in degrees.")]
    public float bobRoll = 0.4f;
    public float bobSmoothing = 8f;

    [Header("Audio")]
    public AudioClip[] footstepClips;
    public AudioClip jumpClip;
    public AudioClip landClip;
    [Range(0f, 1f)] public float footstepVolume = 0.55f;
    [Range(0f, 1f)] public float crouchVolumeMultiplier = 0.4f;
    [Range(0f, 1f)] public float sprintVolumeMultiplier = 1.0f;
    [Tooltip("Random pitch variation so steps don't sound identical.")]
    [Range(0f, 0.5f)] public float pitchJitter = 0.12f;

    DrifterController drifter;
    float bobPhase;          // grows by 1.0 per step
    int lastStepIndex;
    Vector3 bobOffset;
    float rollOffset;
    AudioClip synthStep;     // procedural fallback
    AudioClip synthLand;

    void Awake()
    {
        drifter = GetComponent<DrifterController>();

        if (bobTarget == null && drifter.playerCamera != null)
            bobTarget = drifter.playerCamera.transform;

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        }
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f; // 2D - it's our own feet

        // Procedural fallback so footsteps work with zero setup.
        synthStep = SynthesizeStep(0.09f, 420f, 0.8f);
        synthLand = SynthesizeStep(0.16f, 300f, 1.0f);
    }

    void OnEnable()
    {
        drifter.Jumped += OnJumped;
        drifter.Landed += OnLanded;
    }

    void OnDisable()
    {
        drifter.Jumped -= OnJumped;
        drifter.Landed -= OnLanded;
    }

    void Update()
    {
        bool moving = drifter.IsGrounded && !drifter.IsSliding && drifter.HorizontalSpeed > 0.2f;

        if (moving)
        {
            float freq = stepFrequency;
            if (drifter.IsSprinting) freq *= sprintFrequencyMultiplier;
            if (drifter.IsCrouching) freq *= crouchFrequencyMultiplier;

            // Scale bob rate a bit with actual speed so slow strafing = slow steps.
            float speedFactor = Mathf.Clamp(drifter.HorizontalSpeed / 4.5f, 0.5f, 1.6f);
            bobPhase += Time.deltaTime * freq * speedFactor;

            // --- audio: one footstep sound each time the phase completes a step ---
            int stepIndex = Mathf.FloorToInt(bobPhase);
            if (stepIndex != lastStepIndex)
            {
                lastStepIndex = stepIndex;
                PlayFootstep();
            }

            // --- visual: classic figure-8 head bob ---
            if (headBobEnabled && bobTarget != null)
            {
                float amp = drifter.IsCrouching ? 0.6f : (drifter.IsSprinting ? 1.3f : 1f);
                float t = bobPhase * Mathf.PI; // half period per step
                Vector3 target = new Vector3(
                    Mathf.Sin(t) * bobSway * amp,
                    -Mathf.Abs(Mathf.Sin(t)) * bobHeight * amp,
                    0f);
                bobOffset = Vector3.Lerp(bobOffset, target, bobSmoothing * Time.deltaTime);
                rollOffset = Mathf.Lerp(rollOffset, Mathf.Sin(t) * bobRoll * amp, bobSmoothing * Time.deltaTime);
            }
        }
        else if (bobTarget != null)
        {
            bobOffset = Vector3.Lerp(bobOffset, Vector3.zero, bobSmoothing * Time.deltaTime);
            rollOffset = Mathf.Lerp(rollOffset, 0f, bobSmoothing * Time.deltaTime);
        }

        if (bobTarget != null)
        {
            bobTarget.localPosition = bobOffset;
            bobTarget.localRotation = Quaternion.Euler(0f, 0f, rollOffset);
        }
    }

    // ----------------------------------------------------------------- audio

    void PlayFootstep()
    {
        AudioClip clip = PickClip();
        if (clip == null) return;

        float volume = footstepVolume;
        if (drifter.IsCrouching) volume *= crouchVolumeMultiplier;
        else if (drifter.IsSprinting) volume *= sprintVolumeMultiplier;

        audioSource.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
        audioSource.PlayOneShot(clip, volume);
    }

    AudioClip PickClip()
    {
        if (footstepClips != null && footstepClips.Length > 0)
        {
            var clip = footstepClips[Random.Range(0, footstepClips.Length)];
            if (clip != null) return clip;
        }
        return synthStep;
    }

    void OnJumped()
    {
        if (jumpClip != null)
        {
            audioSource.pitch = 1f;
            audioSource.PlayOneShot(jumpClip, footstepVolume);
        }
    }

    void OnLanded(float fallSpeed)
    {
        AudioClip clip = landClip != null ? landClip : synthLand;
        float volume = Mathf.Lerp(0.3f, 1f, Mathf.InverseLerp(2f, 12f, fallSpeed)) * footstepVolume;
        audioSource.pitch = 1f + Random.Range(-pitchJitter, pitchJitter) * 0.5f;
        audioSource.PlayOneShot(clip, volume);
        bobPhase = 0f;
        lastStepIndex = 0;
    }

    // Simple procedural "thud": filtered noise burst with exponential decay.
    static AudioClip SynthesizeStep(float duration, float toneHz, float noiseAmount)
    {
        const int sampleRate = 44100;
        int samples = Mathf.CeilToInt(duration * sampleRate);
        float[] data = new float[samples];

        System.Random rng = new System.Random(12345 + (int)toneHz);
        float lowpassed = 0f;
        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)sampleRate;
            float envelope = Mathf.Exp(-t * 38f);
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            lowpassed += 0.18f * (noise - lowpassed); // one-pole low-pass
            float tone = Mathf.Sin(2f * Mathf.PI * toneHz * t) * Mathf.Exp(-t * 55f);
            data[i] = (lowpassed * noiseAmount + tone * 0.5f) * envelope * 0.8f;
        }

        var clip = AudioClip.Create("SynthFootstep", samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
