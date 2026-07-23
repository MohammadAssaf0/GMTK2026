using UnityEngine;
using VolumetricFogAndMist2;

/// <summary>
/// Sandstorm that surrounds the player: keeps the whole storm rig (volumetric
/// fog + sand particles + wind audio) centered on the player, and animates
/// gusts - fog density, wind sound volume/pitch and particle speed all swell
/// and fade together using the same Perlin-noise gust curve.
///
/// Created automatically by Tools > Drifter > Create Sandstorm Around Player.
/// </summary>
public class SandstormController : MonoBehaviour
{
    [Header("References (auto-found if empty)")]
    public Transform player;
    public VolumetricFog fog;
    public ParticleSystem sandParticles;
    public AudioSource windAudioSource;

    [Header("Storm")]
    [Range(0f, 2f), Tooltip("Master intensity. 0 = storm off, 1 = normal, 2 = extreme.")]
    public float intensity = 1f;
    [Tooltip("Fog wind direction & speed (also drives particle drift).")]
    public Vector3 windDirection = new Vector3(0.5f, 0f, 0.15f);
    [Range(0f, 1f), Tooltip("Swirling of the fog noise.")]
    public float turbulence = 0.35f;

    [Header("Gusts")]
    public bool gusts = true;
    [Tooltip("How fast gusts come and go.")]
    public float gustSpeed = 0.15f;
    [Range(0f, 1f), Tooltip("How much gusts change the storm (density, sound, particles).")]
    public float gustStrength = 0.45f;

    [Header("Wind Audio")]
    public bool windAudio = true;
    [Range(0f, 1f)] public float windVolume = 0.35f;

    VolumetricFogProfile runtimeProfile;   // instanced copy - safe to animate
    float baseDensity = 0.5f;
    float baseParticleSpeed;
    float smoothedGust = 1f;

    void Start()
    {
        if (player == null)
        {
            var tagged = GameObject.FindGameObjectWithTag("Player");
            if (tagged != null) player = tagged.transform;
        }
        if (fog == null) fog = GetComponentInChildren<VolumetricFog>();
        if (sandParticles == null) sandParticles = GetComponentInChildren<ParticleSystem>();

        if (fog != null)
        {
            fog.enableFollow = false;            // this controller moves the whole rig
            runtimeProfile = fog.settings;       // instanced copy (like material vs sharedMaterial)
            runtimeProfile.windDirection = windDirection;
            runtimeProfile.turbulence = turbulence;
            baseDensity = runtimeProfile.density;
        }

        if (sandParticles != null)
        {
            var vel = sandParticles.velocityOverLifetime;
            baseParticleSpeed = vel.speedModifierMultiplier;
        }

        if (windAudio)
        {
            if (windAudioSource == null) windAudioSource = gameObject.AddComponent<AudioSource>();
            windAudioSource.clip = SynthesizeWindLoop();
            windAudioSource.loop = true;
            windAudioSource.spatialBlend = 0f;
            windAudioSource.volume = 0f;
            windAudioSource.Play();
        }
    }

    void Update()
    {
        // --- gust curve: slow perlin noise around 1.0 ---
        float gust = 1f;
        if (gusts)
            gust = 1f + (Mathf.PerlinNoise(Time.time * gustSpeed, 0.37f) - 0.5f) * 2f * gustStrength;
        smoothedGust = Mathf.Lerp(smoothedGust, gust, 2f * Time.deltaTime);

        // --- fog density breathes with the gusts ---
        if (runtimeProfile != null && fog != null)
        {
            runtimeProfile.density = baseDensity * intensity * smoothedGust;
            runtimeProfile.windDirection = windDirection * smoothedGust;
            fog.UpdateMaterialProperties();
        }

        // --- particles push harder in gusts ---
        if (sandParticles != null)
        {
            var vel = sandParticles.velocityOverLifetime;
            vel.speedModifierMultiplier = baseParticleSpeed * smoothedGust * Mathf.Max(0.2f, intensity);
        }

        // --- wind howls with the gusts ---
        if (windAudioSource != null)
        {
            windAudioSource.volume = windVolume * Mathf.Clamp01(intensity) * Mathf.Clamp01(0.55f + 0.45f * smoothedGust);
            windAudioSource.pitch = 0.85f + 0.3f * (smoothedGust - 1f + gustStrength);
        }
    }

    void LateUpdate()
    {
        // Keep the whole storm centered on the player (XZ only - the storm
        // stays at its own height so the fog layer hugs the ground).
        if (player == null) return;
        Vector3 pos = transform.position;
        pos.x = player.position.x;
        pos.z = player.position.z;
        transform.position = pos;
    }

    // Seamlessly-looping procedural wind: layered filtered noise with a slow
    // internal swell, crossfaded at the loop point.
    static AudioClip SynthesizeWindLoop()
    {
        const int sampleRate = 44100;
        const float duration = 6f;
        int samples = (int)(duration * sampleRate);
        int fade = (int)(0.5f * sampleRate);

        // Generate a bit more than needed; the extra tail is crossfaded into
        // the head so the loop point is seamless.
        float[] raw = new float[samples + fade];
        System.Random rng = new System.Random(777);
        float lp1 = 0f, lp2 = 0f;
        for (int i = 0; i < raw.Length; i++)
        {
            float white = (float)(rng.NextDouble() * 2.0 - 1.0);
            lp1 += 0.035f * (white - lp1);   // deep rumble
            lp2 += 0.16f * (white - lp2);    // airy hiss
            float t = i / (float)sampleRate;
            float swell = 0.75f + 0.25f * Mathf.Sin(t / duration * 2f * Mathf.PI * 2f); // 2 swells per loop
            raw[i] = (lp1 * 3.5f + lp2 * 0.6f) * swell * 0.6f;
        }

        float[] data = new float[samples];
        System.Array.Copy(raw, data, samples);
        for (int i = 0; i < fade; i++)
        {
            float t = i / (float)fade;
            // Head fades from the extra tail (which continues the last sample)
            // into the real head - so wrap-around is continuous.
            data[i] = raw[samples + i] * (1f - t) + raw[i] * t;
        }

        var clip = AudioClip.Create("SynthWindLoop", samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
