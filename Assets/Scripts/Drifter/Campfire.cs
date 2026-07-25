using UnityEngine;

/// <summary>
/// Procedural campfire: rising flame particles + sparks (additive glow),
/// a flickering point light, and optional log primitives.
/// Add to a GameObject placed where the fire should be (e.g. inside the kippah,
/// on the floor). Builds automatically on play; right-click the component and
/// choose "Build Campfire (preview)" to see it in the editor.
/// </summary>
public class Campfire : MonoBehaviour
{
    [Header("Size")]
    public float scale = 1f;
    public float baseRadius = 0.35f;

    [Header("Colors")]
    public Color flameHot = new Color(1f, 0.85f, 0.30f);
    public Color flameMid = new Color(1f, 0.42f, 0.10f);
    public Color spark    = new Color(1f, 0.75f, 0.35f);

    [Header("Light")]
    public bool flickeringLight = true;
    public float lightIntensity = 3.2f;
    public float lightRange = 14f;

    [Header("Extras")]
    public bool makeLogs = true;

    Light fireLight;
    Material addMat;

    void Awake()
    {
        if (Application.isPlaying) Build();
    }

    void Update()
    {
        if (flickeringLight && fireLight != null)
        {
            float n = Mathf.PerlinNoise(Time.time * 11f, 0.5f);
            float n2 = Mathf.PerlinNoise(Time.time * 4.3f, 3.1f);
            fireLight.intensity = lightIntensity * (0.6f + n * 0.5f + n2 * 0.2f);
        }
    }

    [ContextMenu("Build Campfire (preview)")]
    public void BuildPreview() { Build(); }

    [ContextMenu("Clear")]
    public void Clear()
    {
        var fx = transform.Find("CampfireFX");
        if (fx != null) DestroyGO(fx.gameObject);
    }

    void Build()
    {
        Clear();

        var root = new GameObject("CampfireFX");
        root.transform.SetParent(transform, false);

        addMat = MakeAdditiveMaterial();

        // ---- flames ----
        var flame = CreatePS("Flames", root.transform);
        {
            var main = flame.main;
            main.startLifetime = 0.9f;
            main.startSpeed = 1.4f * scale;
            main.startSize = 0.5f * scale;
            main.startColor = flameMid;
            main.gravityModifier = -0.15f; // rise
            main.maxParticles = 300;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var em = flame.emission; em.rateOverTime = 55f;
            var sh = flame.shape;
            sh.shapeType = ParticleSystemShapeType.Cone;
            sh.angle = 12f; sh.radius = baseRadius * scale;

            var col = flame.colorOverLifetime; col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(flameHot, 0f), new GradientColorKey(flameMid, 0.5f), new GradientColorKey(new Color(0.4f,0.05f,0f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0.7f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;

            var sol = flame.sizeOverLifetime; sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.1f));

            var noise = flame.noise; noise.enabled = true; noise.strength = 0.35f; noise.frequency = 1.2f;
        }

        // ---- sparks / embers ----
        var sparks = CreatePS("Sparks", root.transform);
        {
            var main = sparks.main;
            main.startLifetime = 1.6f;
            main.startSpeed = 2.2f * scale;
            main.startSize = 0.06f * scale;
            main.startColor = spark;
            main.gravityModifier = -0.05f;
            main.maxParticles = 120;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var em = sparks.emission; em.rateOverTime = 14f;
            var sh = sparks.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 20f; sh.radius = baseRadius * 0.6f * scale;

            var col = sparks.colorOverLifetime; col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(spark, 0f), new GradientColorKey(flameMid, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;

            var noise = sparks.noise; noise.enabled = true; noise.strength = 0.5f; noise.frequency = 0.8f;
        }

        // ---- flickering light ----
        var lightGO = new GameObject("FireLight");
        lightGO.transform.SetParent(root.transform, false);
        lightGO.transform.localPosition = new Vector3(0f, 0.6f * scale, 0f);
        fireLight = lightGO.AddComponent<Light>();
        fireLight.type = LightType.Point;
        fireLight.color = new Color(1f, 0.55f, 0.2f);
        fireLight.range = lightRange * scale;
        fireLight.intensity = lightIntensity;
        fireLight.shadows = LightShadows.Soft;

        // ---- logs ----
        if (makeLogs)
        {
            int n = 4;
            for (int i = 0; i < n; i++)
            {
                float a = (i / (float)n) * Mathf.PI;
                var log = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                log.name = "Log";
                var c = log.GetComponent<Collider>(); if (c != null) DestroyGO(c);
                log.transform.SetParent(root.transform, false);
                log.transform.localScale = new Vector3(0.08f, 0.32f, 0.08f) * scale;
                log.transform.localPosition = new Vector3(Mathf.Cos(a) * 0.18f, 0.04f, Mathf.Sin(a) * 0.18f) * scale;
                log.transform.localRotation = Quaternion.Euler(78f, a * Mathf.Rad2Deg, 0f);
                var mr = log.GetComponent<MeshRenderer>();
                mr.sharedMaterial = MakeLogMaterial();
            }
        }
    }

    ParticleSystem CreatePS(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop();
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.material = addMat;
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ps.Play();
        return ps;
    }

    Material MakeAdditiveMaterial()
    {
        Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        var m = new Material(sh);
        var tex = MakeRadialTexture();
        m.SetTexture("_BaseMap", tex);
        m.SetTexture("_MainTex", tex);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
        if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);           // transparent
        if (m.HasProperty("_Blend"))   m.SetFloat("_Blend", 2f);             // additive
        if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        if (m.HasProperty("_ZWrite"))  m.SetFloat("_ZWrite", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = 3200;
        return m;
    }

    Material MakeLogMaterial()
    {
        Shader sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null) sh = Shader.Find("Standard");
        var m = new Material(sh);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", new Color(0.15f, 0.09f, 0.05f));
        if (m.HasProperty("_Color")) m.SetColor("_Color", new Color(0.15f, 0.09f, 0.05f));
        return m;
    }

    static Texture2D MakeRadialTexture()
    {
        int s = 64;
        var t = new Texture2D(s, s, TextureFormat.RGBA32, false);
        t.wrapMode = TextureWrapMode.Clamp;
        Vector2 c = new Vector2(s * 0.5f, s * 0.5f);
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), c) / (s * 0.5f);
                float a = Mathf.Clamp01(1f - d);
                a = a * a; // soft falloff
                t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        t.Apply();
        return t;
    }

    void DestroyGO(Object o)
    {
        if (Application.isPlaying) Destroy(o);
        else DestroyImmediate(o);
    }
}
