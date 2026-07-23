using UnityEngine;

/// <summary>
/// Footsteps for the Drifter - VISUAL head-bob identical to the Player
/// prefab's HeadBob (same formula and values: baseBobSpeed 6, baseBobAmount
/// 0.02, runMultiplier 3, smooth 8), plus AUDIO footsteps synced to the bob
/// cycle, with rate and volume following walk / run / crouch.
///
/// Works out of the box: if no audio clips are assigned it synthesizes a
/// soft procedural step sound at startup. Drop your own clips into
/// <see cref="footstepClips"/> whenever you have them.
/// </summary>
[RequireComponent(typeof(DrifterController))]
public class DrifterFootsteps : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Transform that gets the head-bob. Use the Camera itself (child of CameraHolder). Auto-found if empty.")]
    public Transform bobTarget;
    public AudioSource audioSource;

    [Header("Head Bob (Player prefab values)")]
    public bool headBobEnabled = true;
    [Tooltip("Base bob frequency (radians/sec). Prefab value: 6.")]
    public float baseBobSpeed = 6f;
    [Tooltip("Base bob height in meters. Prefab value: 0.02.")]
    public float baseBobAmount = 0.02f;
    [Tooltip("How much speed increases bob frequency. Prefab value: 3.")]
    public float runMultiplier = 3f;
    [Tooltip("Interpolation speed. Prefab value: 8.")]
    public float smooth = 8f;

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
    float timer;             // bob phase in radians, same as HeadBob
    float currentY;
    int lastStepIndex;
    AudioClip synthStep;     // procedural fallback
    AudioClip synthLand;

    const float TwoPi = Mathf.PI * 2f;

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
        float speed = drifter.HorizontalSpeed;
        bool moving = drifter.IsGrounded && !drifter.IsSliding && speed > 0.1f;

        if (moving)
        {
            // --- exact HeadBob formula from the Player prefab ---
            float bobSpeed = baseBobSpeed + speed * runMultiplier * 0.2f;
            float bobAmount = baseBobAmount + speed * 0.02f;

            timer += Time.deltaTime * bobSpeed;
            float targetY = Mathf.Sin(timer) * bobAmount;
            currentY = Mathf.Lerp(currentY, targetY, Time.deltaTime * smooth);

            // --- audio: one footstep per bob cycle (at the trough) ---
            int stepIndex = Mathf.FloorToInt((timer + Mathf.PI * 0.5f) / TwoPi);
            if (stepIndex != lastStepIndex)
            {
                lastStepIndex = stepIndex;
                PlayFootstep();
            }
        }
        else
        {
            // Reset phase and return smoothly, exactly like HeadBob.
            timer = 0f;
            lastStepIndex = 0;
            currentY = Mathf.Lerp(currentY, 0f, Time.deltaTime * smooth);
        }

        if (headBobEnabled && bobTarget != null)
        {
            Vector3 localPos = bobTarget.localPosition;
            localPos.y = currentY;
            bobTarget.localPosition = localPos;
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
        // Ignore the initial "settling" landing right after the scene loads.
        if (Time.timeSinceLevelLoad < 0.75f) return;

        AudioClip clip = landClip != null ? landClip : synthLand;
        float volume = Mathf.Lerp(0.3f, 1f, Mathf.InverseLerp(2f, 12f, fallSpeed)) * footstepVolume;
        audioSource.pitch = 1f + Random.Range(-pitchJitter, pitchJitter) * 0.5f;
        audioSource.PlayOneShot(clip, volume);
        timer = 0f;
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
