using UnityEngine;

/// <summary>
/// Day/night cycle driving the sun (and through it, the sandstorm fog and
/// the skybox). Everything is tunable in the Inspector:
///   - Day Length     - real minutes per full in-game day
///   - Time Of Day    - scrub it (also in edit mode!) to preview any hour
///   - Sun Color      - gradient across the day (0 = midnight, 0.5 = noon)
///   - Sun Intensity  - curve across the day
///   - Sky Exposure   - skybox brightness curve (runtime)
/// The sandstorm follows automatically because its profile uses the sun
/// (Day Night Cycle is enabled in the fog profile).
/// </summary>
[ExecuteAlways]
public class DayNightCycle : MonoBehaviour
{
    [Header("Time")]
    [Tooltip("Length of a full in-game day, in real-time minutes.")]
    public float dayLengthMinutes = 10f;
    [Range(0f, 24f), Tooltip("Current hour (0-24). Scrub in edit mode to preview; advances in play mode.")]
    public float timeOfDay = 10f;
    [Tooltip("Advance time automatically in play mode.")]
    public bool advanceTime = true;

    [Header("Sun")]
    [Tooltip("The directional light. Auto-found if empty.")]
    public Light sun;
    [Range(-180f, 180f), Tooltip("Compass direction of the sun's path.")]
    public float sunYaw = -40f;
    [Tooltip("Sun color across the day. 0 = midnight, 0.25 = sunrise, 0.5 = noon, 0.75 = sunset.")]
    public Gradient sunColor = DefaultSunColor();
    [Tooltip("Sun intensity across the day.")]
    public AnimationCurve sunIntensity = DefaultSunIntensity();

    [Header("Sky")]
    [Tooltip("Skybox exposure across the day (applied in play mode).")]
    public AnimationCurve skyExposure = DefaultSkyExposure();

    Material skyboxRuntime;

    void OnEnable()
    {
        if (sun == null)
        {
            sun = GetComponent<Light>();
            if (sun == null || sun.type != LightType.Directional)
            {
                foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                    if (l.type == LightType.Directional) { sun = l; break; }
            }
        }

        // Instance the skybox in play mode so exposure changes don't
        // permanently modify the material asset.
        if (Application.isPlaying && RenderSettings.skybox != null)
        {
            skyboxRuntime = new Material(RenderSettings.skybox);
            RenderSettings.skybox = skyboxRuntime;
        }

        ApplyTime();
    }

    void Update()
    {
        if (Application.isPlaying && advanceTime && dayLengthMinutes > 0.01f)
            timeOfDay = Mathf.Repeat(timeOfDay + 24f / (dayLengthMinutes * 60f) * Time.deltaTime, 24f);
        ApplyTime();
    }

    /// <summary>Applies the current timeOfDay to sun rotation, color, intensity and sky.</summary>
    public void ApplyTime()
    {
        if (sun == null) return;
        float t = Mathf.Repeat(timeOfDay, 24f) / 24f;

        // 06:00 sunrise at the horizon, 12:00 overhead, 18:00 sunset.
        sun.transform.rotation = Quaternion.Euler(t * 360f - 90f, sunYaw, 0f);
        sun.color = sunColor.Evaluate(t);
        sun.intensity = sunIntensity.Evaluate(t);

        if (skyboxRuntime != null && skyboxRuntime.HasProperty("_Exposure"))
            skyboxRuntime.SetFloat("_Exposure", skyExposure.Evaluate(t));
    }

    static Gradient DefaultSunColor()
    {
        var g = new Gradient();
        g.SetKeys(new[]
        {
            new GradientColorKey(new Color(0.10f, 0.13f, 0.28f), 0f),    // midnight blue
            new GradientColorKey(new Color(0.10f, 0.13f, 0.28f), 0.22f), // pre-dawn
            new GradientColorKey(new Color(1f, 0.55f, 0.30f), 0.28f),    // sunrise orange
            new GradientColorKey(new Color(1f, 0.92f, 0.78f), 0.40f),    // warm day
            new GradientColorKey(new Color(1f, 0.92f, 0.78f), 0.62f),    // warm day
            new GradientColorKey(new Color(1f, 0.42f, 0.20f), 0.75f),    // sunset red
            new GradientColorKey(new Color(0.10f, 0.13f, 0.28f), 0.82f), // night
        }, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        return g;
    }

    static AnimationCurve DefaultSunIntensity() => new AnimationCurve(
        new Keyframe(0f, 0.03f), new Keyframe(0.23f, 0.03f), new Keyframe(0.32f, 1.0f),
        new Keyframe(0.5f, 1.25f), new Keyframe(0.68f, 1.0f), new Keyframe(0.78f, 0.03f),
        new Keyframe(1f, 0.03f));

    static AnimationCurve DefaultSkyExposure() => new AnimationCurve(
        new Keyframe(0f, 0.25f), new Keyframe(0.25f, 0.4f), new Keyframe(0.38f, 1.3f),
        new Keyframe(0.62f, 1.3f), new Keyframe(0.75f, 0.45f), new Keyframe(1f, 0.25f));
}
