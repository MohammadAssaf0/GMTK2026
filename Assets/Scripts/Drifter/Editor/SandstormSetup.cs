using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using VolumetricFogAndMist2;

/// <summary>
/// One-click setup: Tools > Drifter > Create Sandstorm Around Player.
/// Builds a complete sandstorm rig centered on the player:
///   - Volumetric Fog Manager (created if missing)
///   - Fog volume using a sand-tinted copy of the "Sand Storm 1" preset
///   - Sand particle streaks blowing through the air
///   - SandstormController (follows player, gusts, procedural wind audio)
/// </summary>
public static class SandstormSetup
{
    const string PresetPath = "Assets/VolumetricFog2/Demo/Presets/Sand Storm 1.asset";
    const string ProfilePath = "Assets/Scripts/Drifter/Sandstorm Profile.asset";

    [MenuItem("Tools/Drifter/Create Sandstorm Around Player")]
    public static void CreateSandstorm()
    {
        // --- find the player ---
        Transform player = null;
        var tagged = GameObject.FindGameObjectWithTag("Player");
        if (tagged != null) player = tagged.transform;
        if (player == null)
        {
            var drifter = Object.FindFirstObjectByType<DrifterController>();
            if (drifter != null) player = drifter.transform;
        }
        if (player == null)
        {
            EditorUtility.DisplayDialog("Sandstorm",
                "No player found. Create the Drifter first (Tools > Drifter > Create Drifter In Scene).", "OK");
            return;
        }

        // --- ensure the Volumetric Fog Manager exists ---
        var manager = VolumetricFogManager.instance;
        if (manager == null)
        {
            Debug.LogError("Could not create the Volumetric Fog Manager. Is VolumetricFog2 imported?");
            return;
        }

        // --- sand-tinted profile (copy of the Sand Storm 1 preset) ---
        var profile = AssetDatabase.LoadAssetAtPath<VolumetricFogProfile>(ProfilePath);
        if (profile == null)
        {
            var preset = AssetDatabase.LoadAssetAtPath<VolumetricFogProfile>(PresetPath);
            if (preset == null)
            {
                Debug.LogError("Sand Storm preset not found at: " + PresetPath);
                return;
            }
            profile = Object.Instantiate(preset);
            profile.name = "Sandstorm Profile";
            profile.albedo = new Color(0.79f, 0.63f, 0.43f, 1f);        // sandy tint
            profile.specularColor = new Color(1f, 0.85f, 0.6f, 1f);
            profile.density = 0.5f;
            profile.turbulence = 0.35f;
            profile.windDirection = new Vector3(0.5f, 0f, 0.15f);
            profile.customHeight = true;
            profile.height = 25f;
            AssetDatabase.CreateAsset(profile, ProfilePath);
            AssetDatabase.SaveAssets();
        }

        // --- storm root ---
        var root = new GameObject("Sandstorm");
        Undo.RegisterCreatedObjectUndo(root, "Create Sandstorm");
        Vector3 rootPos = player.position;
        rootPos.y = 0f;
        root.transform.position = rootPos;

        // --- fog volume ---
        GameObject fogGo = VolumetricFogManager.CreateFogVolume("Sandstorm Fog");
        fogGo.transform.SetParent(root.transform, false);
        fogGo.transform.localPosition = new Vector3(0f, 10f, 0f);
        fogGo.transform.localScale = new Vector3(150f, 20f, 150f);
        var fog = fogGo.GetComponent<VolumetricFog>();
        fog.profile = profile;

        // --- sand particle streaks ---
        var particles = CreateSandParticles(root.transform);

        // --- controller (follow + gusts + wind audio) ---
        var controller = root.AddComponent<SandstormController>();
        controller.player = player;
        controller.fog = fog;
        controller.sandParticles = particles;

        Selection.activeGameObject = root;
        EditorGUIUtility.PingObject(root);
        Debug.Log("Sandstorm created around the player. Tune it via the SandstormController component (intensity, gusts, wind).");
    }

    static ParticleSystem CreateSandParticles(Transform parent)
    {
        var go = new GameObject("SandParticles");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, 6f, 0f);

        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.12f);
        main.startColor = new Color(0.82f, 0.68f, 0.47f, 0.45f);
        main.maxParticles = 2000;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = 450f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(60f, 12f, 60f);

        // Wind push - the SandstormController scales this with gusts.
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(12f, 20f);
        vel.y = new ParticleSystem.MinMaxCurve(-1.5f, 0.5f);
        vel.z = new ParticleSystem.MinMaxCurve(3f, 7f);
        vel.speedModifier = 1f;

        // Fade in/out so particles don't pop.
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f),
                    new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
        col.color = grad;

        // Stretched billboards = wind streaks.
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.lengthScale = 5f;
        renderer.material = GetOrCreateSandMaterial();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        ps.Play();
        return ps;
    }

    static Material GetOrCreateSandMaterial()
    {
        const string matPath = "Assets/Scripts/Drifter/SandParticle.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat != null) return mat;

        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        mat = new Material(shader);

        // Configure as soft transparent particles (URP).
        mat.SetFloat("_Surface", 1f);   // transparent
        mat.SetFloat("_Blend", 0f);     // alpha blend
        mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = 3000;

        var softCircle = AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd");
        if (softCircle != null) mat.SetTexture("_BaseMap", softCircle);
        mat.SetColor("_BaseColor", Color.white);

        AssetDatabase.CreateAsset(mat, matPath);
        AssetDatabase.SaveAssets();
        return mat;
    }
}
