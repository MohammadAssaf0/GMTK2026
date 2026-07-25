using UnityEngine;

/// <summary>
/// Plays sand/gravel "boom" impact sounds when the Drifter tumbles down a slope.
/// The tumble does DrifterController.tumbleRolls somersaults, so this fires one
/// heavy sandy boom PER roll (e.g. 3 booms for 3 rolls), volume scaled by speed.
/// Hard (non-tumble) landings also get a scaled impact; soft landings are left
/// to DrifterFootsteps so sounds don't double up.
///
/// Zero-setup: if no clips are assigned it synthesizes a sandy boom.
/// </summary>
[RequireComponent(typeof(DrifterController))]
public class DrifterFallImpacts : MonoBehaviour
{
    [Header("Audio")]
    [Tooltip("Sand/gravel impact sounds. If empty, a boom is synthesized.")]
    public AudioClip[] impactClips;
    public AudioSource audioSource;
    [Range(0f, 1f)] public float volume = 1f;
    [Range(0f, 0.5f)] public float pitchJitter = 0.14f;

    [Header("Tumble")]
    [Tooltip("Fire this many booms per tumble (auto-matches the controller's roll count).")]
    public bool oneBoomPerRoll = true;
    [Tooltip("Used if 'One Boom Per Roll' is off: seconds between booms while tumbling.")]
    public float manualInterval = 0.5f;
    [Tooltip("Speed (m/s) that counts as a full-volume impact.")]
    public float maxImpactSpeed = 14f;

    [Header("Hard landing")]
    [Tooltip("Fall speed (m/s) below which a landing is 'soft' and left to the footstep sound.")]
    public float minLandFallSpeed = 5f;

    DrifterController drifter;
    CharacterController cc;
    AudioClip synthBoom;

    bool wasTumbling;
    int tumbleHits;
    int maxTumbleHits;
    float rollPeriod;
    float nextImpactTime;

    void Awake()
    {
        drifter = GetComponent<DrifterController>();
        cc = GetComponent<CharacterController>();

        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;

        // one boom per somersault
        rollPeriod = (drifter.tumbleSpinSpeed > 1f) ? (360f / drifter.tumbleSpinSpeed) : 0.9f;
        maxTumbleHits = Mathf.Max(1, drifter.tumbleRolls);

        synthBoom = SynthesizeSandBoom();
    }

    void OnEnable()  { drifter.Landed += OnLanded; }
    void OnDisable() { drifter.Landed -= OnLanded; }

    void Update()
    {
        if (drifter.IsTumbling)
        {
            if (!wasTumbling)
            {
                wasTumbling = true;
                tumbleHits = 0;
                nextImpactTime = Time.time; // first boom right away
            }

            float interval = oneBoomPerRoll ? rollPeriod : Mathf.Max(0.05f, manualInterval);
            int cap = oneBoomPerRoll ? maxTumbleHits : int.MaxValue;

            if (tumbleHits < cap && Time.time >= nextImpactTime)
            {
                tumbleHits++;
                nextImpactTime = Time.time + interval;
                float speed = cc != null ? cc.velocity.magnitude : 8f;
                // booms stay punchy: floor at 0.6
                PlayImpact(Mathf.Lerp(0.6f, 1f, Mathf.Clamp01(speed / maxImpactSpeed)));
            }
        }
        else
        {
            wasTumbling = false;
        }
    }

    void OnLanded(float fallSpeed)
    {
        if (Time.timeSinceLevelLoad < 0.75f) return;
        if (drifter.IsTumbling) return;
        if (fallSpeed < minLandFallSpeed) return;
        PlayImpact(Mathf.InverseLerp(minLandFallSpeed, maxImpactSpeed, fallSpeed));
    }

    void PlayImpact(float t)
    {
        AudioClip clip = synthBoom;
        if (impactClips != null && impactClips.Length > 0)
        {
            var c = impactClips[Random.Range(0, impactClips.Length)];
            if (c != null) clip = c;
        }
        if (clip == null) return;

        audioSource.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
        audioSource.PlayOneShot(clip, Mathf.Clamp01(t) * volume);
    }

    // Sandy/gravel boom: low sine "boom" + a burst of filtered noise (the sand crunch).
    static AudioClip SynthesizeSandBoom()
    {
        const int sampleRate = 44100;
        float duration = 0.30f;
        int samples = Mathf.CeilToInt(duration * sampleRate);
        float[] data = new float[samples];

        System.Random rng = new System.Random(4477);
        float lp = 0f;
        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)sampleRate;

            // low boom (pitch drops slightly for a "thud")
            float boomFreq = Mathf.Lerp(70f, 45f, Mathf.Clamp01(t / duration));
            float boom = Mathf.Sin(2f * Mathf.PI * boomFreq * t) * Mathf.Exp(-t * 20f);

            // sand/gravel crunch = band-ish filtered noise, quick decay
            float n = (float)(rng.NextDouble() * 2.0 - 1.0);
            lp += 0.25f * (n - lp);                // low-pass toward a gritty rumble
            float grit = (n - lp) * Mathf.Exp(-t * 16f); // high part = grainy crunch

            data[i] = (boom * 0.8f + grit * 0.6f) * 0.9f;
        }

        var clip = AudioClip.Create("SandBoom", samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
