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

    [Header("Size")]
    [Tooltip("Storm footprint diameter in meters. Applied to the fog volume live.")]
    public float stormSize = 150f;
    [Tooltip("Fog layer thickness in meters.")]
    public float stormHeight = 20f;

    [Header("Storm")]
    [Range(0f, 2f), Tooltip("Master intensity. 0 = storm off, 1 = normal, 2 = extreme.")]
    public float intensity = 1f;
    [Tooltip("Fog wind drift (direction & speed). Keep it slow - real storms churn more than they scroll.")]
    public Vector3 windDrift = new Vector3(0.12f, 0f, 0.04f);
    [Range(0f, 1f), Tooltip("Swirling of the fog noise. High = organic churn instead of uniform scrolling.")]
    public float fogTurbulence = 0.8f;

    [Header("Follow")]
    [Tooltip("Follow the player at all. Turn OFF to keep the storm fixed on the map (map-relative), e.g. via Tools > Drifter > Fit Sandstorm To Map.")]
    public bool followPlayer = true;
    [Tooltip("Keep the storm centered on the player's height too, so the player is always wrapped in dense fog.")]
    public bool followPlayerHeight = true;
    [Tooltip("Offset from the player. Negative Y puts the player in the thick lower part of the fog layer.")]
    public Vector3 followOffset = new Vector3(0f, -8f, 0f);
    [Tooltip("How quickly the storm re-centers on the player. Low = smooth drift (stable fog), high = rigid tracking (fog pattern drags visibly). 0 = instant.")]
    public float followSmoothing = 1.5f;

    [Header("Distant Fog (horizon blend)")]
    [Tooltip("Infinite fog beyond the storm volume, so far dunes melt into the storm instead of staying sharp.")]
    public bool distantFogEnabled = true;
    [Tooltip("Distance (meters) where the horizon fog starts. Keep it inside the storm radius for a seamless blend.")]
    public float distantFogStart = 50f;
    [Range(0f, 2f), Tooltip("How quickly things disappear with distance.")]
    public float distantFogDensity = 0.6f;
    [Tooltip("Color of the horizon haze. Match it to the storm's sand tone.")]
    public Color distantFogColor = new Color(0.8f, 0.65f, 0.47f, 1f);
    [Range(0f, 2f), Tooltip("How much the haze varies with altitude. Keep LOW (~0.05) - high values create a dense 'cloud floor' that looks like a new cloud when descending into valleys.")]
    public float distantFogHeightDensity = 0.05f;

    [Header("Gusts")]
    public bool gusts = true;
    [Tooltip("How fast gusts come and go.")]
    public float gustSpeed = 0.15f;
    [Range(0f, 1f), Tooltip("How much gusts change the storm (density, sound, particles).")]
    public float gustStrength = 0.45f;

    [Header("Debug")]
    [Tooltip("Shows a live overlay in-game and writes sandstorm-debug.log in the project folder. Flags teleports and moments the player exits the fog layer. Turn OFF for builds.")]
    public bool debugMode = true;

    [Header("Wind Audio")]
    public bool windAudio = true;
    [Range(0f, 1f)] public float windVolume = 0.25f;
    [Tooltip("Seconds for the wind to fade in when the game starts.")]
    public float windFadeInTime = 3f;

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
            runtimeProfile.windDirection = windDrift;
            runtimeProfile.turbulence = fogTurbulence;
            baseDensity = runtimeProfile.density;
        }

        ApplyOrganicParticles();

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

        ApplySize();
        ApplyDistantFog();
    }

    void OnValidate()
    {
        stormSize = Mathf.Max(10f, stormSize);
        stormHeight = Mathf.Max(4f, stormHeight);
        distantFogStart = Mathf.Max(5f, distantFogStart);
        if (fog == null) fog = GetComponentInChildren<VolumetricFog>();
        if (sandParticles == null) sandParticles = GetComponentInChildren<ParticleSystem>();
        ApplySize();
        ApplyOrganicParticles();
        ApplyDistantFog();
    }

    /// <summary>
    /// Pushes the Distant Fog settings into the fog profile - the instanced
    /// copy in play mode, or the shared profile asset in edit mode (so you
    /// can tune it live in the Inspector either way).
    /// </summary>
    public void ApplyDistantFog()
    {
        var p = Application.isPlaying ? runtimeProfile : (fog != null ? fog.profile : null);
        if (p == null) return;
        p.distantFog = distantFogEnabled;
        p.distantFogStartDistance = distantFogStart;
        p.distantFogDistanceDensity = distantFogDensity;
        p.distantFogColor = distantFogColor;
        p.distantFogHeightDensity = distantFogHeightDensity;
        if (fog != null) fog.UpdateMaterialProperties();
    }

    /// <summary>
    /// Makes the sand particles feel organic: chaotic swirl via the Noise
    /// module, varied lifetimes/sizes, softer streaks - instead of uniform
    /// straight lines. Runs on the existing scene particles, so no rebuild
    /// of the Sandstorm object is needed.
    /// </summary>
    void ApplyOrganicParticles()
    {
        if (sandParticles == null) return;

        var main = sandParticles.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.4f, 3.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.16f);

        var emission = sandParticles.emission;
        emission.rateOverTime = 260f;

        // Chaotic swirl - this is what kills the "conveyor belt" look.
        var noise = sandParticles.noise;
        noise.enabled = true;
        noise.strength = new ParticleSystem.MinMaxCurve(0.8f);
        noise.frequency = 0.22f;
        noise.scrollSpeed = 0.35f;
        noise.damping = true;
        noise.quality = ParticleSystemNoiseQuality.Medium;

        var vel = sandParticles.velocityOverLifetime;
        vel.enabled = true;
        vel.x = new ParticleSystem.MinMaxCurve(6f, 13f);
        vel.y = new ParticleSystem.MinMaxCurve(-1.2f, 0.8f);
        vel.z = new ParticleSystem.MinMaxCurve(1.5f, 4.5f);

        var renderer = sandParticles.GetComponent<ParticleSystemRenderer>();
        if (renderer != null && renderer.renderMode == ParticleSystemRenderMode.Stretch)
            renderer.lengthScale = 3f; // shorter streaks read as grit, not laser lines
    }

    /// <summary>Resizes the fog volume and particle area to stormSize x stormHeight.</summary>
    public void ApplySize()
    {
        if (fog != null)
        {
            fog.transform.localScale = new Vector3(stormSize, stormHeight, stormSize);
            fog.transform.localPosition = new Vector3(0f, stormHeight * 0.5f, 0f);
        }
        if (sandParticles != null)
        {
            var shape = sandParticles.shape;
            shape.scale = new Vector3(
                Mathf.Min(60f, stormSize * 0.4f),
                Mathf.Min(12f, stormHeight * 0.6f),
                Mathf.Min(60f, stormSize * 0.4f));
        }
    }

    void Update()
    {
        // --- gust curve: two perlin octaves so it never feels metronomic ---
        float gust = 1f;
        if (gusts)
        {
            float slow = Mathf.PerlinNoise(Time.time * gustSpeed, 0.37f) - 0.5f;
            float fast = Mathf.PerlinNoise(Time.time * gustSpeed * 3.7f, 7.13f) - 0.5f;
            gust = 1f + (slow * 1.6f + fast * 0.4f) * gustStrength;
        }
        smoothedGust = Mathf.Lerp(smoothedGust, gust, 2f * Time.deltaTime);

        // --- fog density breathes with the gusts ---
        if (runtimeProfile != null && fog != null)
        {
            runtimeProfile.density = baseDensity * intensity * smoothedGust;
            runtimeProfile.windDirection = windDrift * smoothedGust;
            fog.UpdateMaterialProperties();
        }

        // --- particles push harder in gusts ---
        if (sandParticles != null)
        {
            var vel = sandParticles.velocityOverLifetime;
            vel.speedModifierMultiplier = baseParticleSpeed * smoothedGust * Mathf.Max(0.2f, intensity);
        }

        DebugTick();

        // --- wind howls with the gusts (with a gentle fade-in at start) ---
        if (windAudioSource != null)
        {
            float fadeIn = windFadeInTime > 0f
                ? Mathf.SmoothStep(0f, 1f, Time.timeSinceLevelLoad / windFadeInTime)
                : 1f;
            windAudioSource.volume = windVolume * fadeIn * Mathf.Clamp01(intensity)
                                   * Mathf.Clamp01(0.55f + 0.45f * smoothedGust);
            windAudioSource.pitch = 0.96f + 0.08f * (smoothedGust - 1f + gustStrength);
        }
    }

    void LateUpdate()
    {
        // Keep the whole storm centered on the player so the drifter is
        // always wrapped in the fog, whatever height they're at.
        // Smoothed: a rigid per-frame snap drags the fog pattern with every
        // step and makes the ground seen through it look warped.
        if (player == null || !followPlayer) return;  // map-relative when off
        Vector3 target = player.position + followOffset;
        if (!followPlayerHeight) target.y = transform.position.y;
        Vector3 pos = followSmoothing > 0f
            ? Vector3.Lerp(transform.position, target, followSmoothing * Time.deltaTime)
            : target;

        if (followPlayerHeight)
        {
            // Hard guarantee: even in fast falls or slides down dunes, the
            // player can never slip out of the fog layer vertically. The
            // smoothing may lag, but this clamp keeps the slab around him.
            float margin = Mathf.Min(4f, stormHeight * 0.2f);
            float minRootY = player.position.y - (stormHeight - margin); // keep fog above
            float maxRootY = player.position.y - margin;                 // keep fog below
            pos.y = Mathf.Clamp(pos.y, minRootY, maxRootY);
        }

        transform.position = pos;
    }

    // ------------------------------------------------------------------ debug

    float debugTimer;
    float debugConsoleTimer;
    Vector3 debugLastPlayerPos;
    string debugLogPath;
    string debugLine;
    int debugEventCount;

    void DebugTick()
    {
        if (!debugMode || player == null) return;

        // Teleport detection (e.g. fall-reset loops).
        float jump = Vector3.Distance(player.position, debugLastPlayerPos);
        bool teleported = debugLastPlayerPos != Vector3.zero && jump > 15f;
        debugLastPlayerPos = player.position;

        float slabBottom = transform.position.y;
        float slabTop = slabBottom + stormHeight;
        bool inSlab = player.position.y > slabBottom && player.position.y < slabTop;
        float density = runtimeProfile != null ? runtimeProfile.density : -1f;

        var drifterCtrl = player.GetComponent<DrifterController>();
        string moveInfo = drifterCtrl != null
            ? $"moveSpeed={drifterCtrl.HorizontalSpeed:F1}m/s slope={drifterCtrl.CurrentGroundAngle:F0}deg sliding={drifterCtrl.IsSliding} tumble={drifterCtrl.IsTumbling} sprint={drifterCtrl.IsSprinting} crouch={drifterCtrl.IsCrouching}  "
            : "";

        debugLine =
            $"t={Time.timeSinceLevelLoad:F1}s  player=({player.position.x:F0}, {player.position.y:F1}, {player.position.z:F0})  " +
            $"{moveInfo}fogLayer=[{slabBottom:F1} .. {slabTop:F1}]  playerInFog={inSlab}\n" +
            $"gust={smoothedGust:F2}  density={density:F3} (base {baseDensity:F2} x intensity {intensity:F2})  " +
            $"size={stormSize:F0}x{stormHeight:F0}  fogVolume={(fog != null ? "OK" : "MISSING!")}  " +
            $"profile={(runtimeProfile != null ? "instanced" : "NOT INSTANCED")}  events={debugEventCount}";

        bool anomaly = teleported || !inSlab;
        string flags = (teleported ? $"  [TELEPORT! jumped {jump:F0}m]" : "")
                     + (!inSlab ? "  [PLAYER OUTSIDE FOG LAYER!]" : "");

        // --- full movement trace to file: every 0.25s, plus every anomaly ---
        debugTimer += Time.deltaTime;
        if (debugTimer >= 0.25f || anomaly)
        {
            debugTimer = 0f;
            if (anomaly) debugEventCount++;
            if (debugLogPath == null)
            {
                debugLogPath = System.IO.Path.Combine(Application.dataPath, "..", "sandstorm-debug.log");
                System.IO.File.AppendAllText(debugLogPath,
                    $"\n===== session start {System.DateTime.Now:HH:mm:ss} =====\n");
            }
            System.IO.File.AppendAllText(debugLogPath,
                debugLine.Replace("\n", " | ") + flags + "\n");
        }

        // --- Console: heartbeat every 2s, anomalies immediately as warnings ---
        debugConsoleTimer += Time.deltaTime;
        if (anomaly)
        {
            Debug.LogWarning("[Sandstorm]" + flags + "\n" + debugLine);
            debugConsoleTimer = 0f;
        }
        else if (debugConsoleTimer >= 2f)
        {
            debugConsoleTimer = 0f;
            Debug.Log("[Sandstorm] " + debugLine);
        }
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

        // Cascaded one-pole low-pass filters give a smooth, dark "whoosh"
        // instead of harsh static. The airy layer is a gentle band-pass.
        float r1 = 0f, r2 = 0f, r3 = 0f;   // rumble chain (~120 Hz)
        float a1 = 0f, a2 = 0f;            // airy band (~300-1500 Hz)
        for (int i = 0; i < raw.Length; i++)
        {
            float white = (float)(rng.NextDouble() * 2.0 - 1.0);

            r1 += 0.02f * (white - r1);
            r2 += 0.02f * (r1 - r2);
            r3 += 0.02f * (r2 - r3);       // deep, soft rumble

            a1 += 0.18f * (white - a1);
            a2 += 0.045f * (a1 - a2);
            float airy = a1 - a2;          // band-passed "air" hiss

            // Slow swells, loop-periodic (whole number of cycles per loop).
            float t = i / (float)sampleRate;
            float phase = t / duration * 2f * Mathf.PI;
            float swell = 0.7f + 0.2f * Mathf.Sin(phase * 2f) + 0.1f * Mathf.Sin(phase * 5f + 1.3f);

            raw[i] = (r3 * 6f + airy * 0.5f) * swell;
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

        // Normalize to a safe peak so nothing clips or distorts.
        float peak = 0f;
        for (int i = 0; i < samples; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        if (peak > 0f)
        {
            float gain = 0.7f / peak;
            for (int i = 0; i < samples; i++) data[i] *= gain;
        }

        var clip = AudioClip.Create("SynthWindLoop", samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
