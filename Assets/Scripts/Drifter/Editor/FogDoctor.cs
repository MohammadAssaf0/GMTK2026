using System.Text;
using UnityEditor;
using UnityEngine;
using VolumetricFogAndMist2;

/// <summary>
/// Tools > Drifter > Fix Sandstorm (Fog Doctor).
/// Reimports the VolumetricFog2 shaders and textures (NOT the scripts, so no
/// domain reload wipes the results), verifies everything the fog needs, and
/// refreshes all fog volumes. Writes a full report to Console AND to
/// fogdoctor-report.txt in the project root.
/// </summary>
public static class FogDoctor
{
    static readonly string[] RequiredShaders =
    {
        "Hidden/VolumetricFog2/Turbulence2D",
        "Hidden/VolumetricFog2/Noise2DGen",
        "Hidden/VolumetricFog2/Empty",
        "VolumetricFog2/VolumetricFog2DURP",
    };

    [MenuItem("Tools/Drifter/Fix Sandstorm (Fog Doctor)")]
    public static void Run()
    {
        var report = new StringBuilder();
        bool allGood = true;

        // 1. Reimport ONLY shader/texture folders - no scripts, no recompile.
        string[] assetFolders =
        {
            "Assets/VolumetricFog2/Resources",
            "Assets/VolumetricFog2/Demo/Noise Textures",
            "Assets/VolumetricFog2/Demo/Presets",
        };
        foreach (string folder in assetFolders)
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                AssetDatabase.ImportAsset(folder,
                    ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
                report.AppendLine($"reimported: {folder}");
            }
            else
            {
                report.AppendLine($"FOLDER MISSING: {folder}");
                allGood = false;
            }
        }
        AssetDatabase.Refresh();

        // 2. Shaders present + compiled?
        foreach (string name in RequiredShaders)
        {
            Shader s = Shader.Find(name);
            if (s == null)
            {
                allGood = false;
                report.AppendLine($"shader NOT FOUND: {name}");
                continue;
            }
            bool hasError = false;
            if (ShaderUtil.GetShaderMessageCount(s) > 0)
            {
                foreach (var msg in ShaderUtil.GetShaderMessages(s))
                {
                    if (msg.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error)
                    {
                        hasError = true;
                        report.AppendLine($"shader ERROR in {name} (line {msg.line}): {msg.message}");
                    }
                }
            }
            if (hasError) allGood = false;
            else report.AppendLine($"shader OK: {name}");
        }

        // 3. Scene inventory: manager, fog volumes, profiles, textures.
        var manager = Object.FindFirstObjectByType<VolumetricFogManager>(FindObjectsInactive.Include);
        report.AppendLine("manager in scene: " + (manager != null ? "OK" : "MISSING"));
        if (manager == null) allGood = false;

        var fogs = Object.FindObjectsByType<VolumetricFog>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        report.AppendLine($"fog volumes in scene: {fogs.Length}");
        if (fogs.Length == 0) allGood = false;
        foreach (var fog in fogs)
        {
            var p = fog.profile;
            report.AppendLine($"- '{fog.name}' active={fog.isActiveAndEnabled} pos={fog.transform.position} scale={fog.transform.localScale}");
            if (p == null) { report.AppendLine("  profile: MISSING"); allGood = false; continue; }
            report.AppendLine($"  profile: {p.name} density={p.density} noiseTexture={(p.noiseTexture != null ? p.noiseTexture.name : "NULL!")} detail={(p.detailTexture != null ? p.detailTexture.name : "null")}");
            if (p.noiseTexture == null) allGood = false;

            fog.UpdateMaterialPropertiesNow();
            var mr = fog.GetComponent<MeshRenderer>();
            var mat = mr != null ? mr.sharedMaterial : null;
            report.AppendLine($"  renderer material: {(mat != null ? mat.name : "NULL")} shader: {(mat != null && mat.shader != null ? mat.shader.name : "NULL")}");
            EditorUtility.SetDirty(fog);
        }

        var cam = Camera.main;
        report.AppendLine("main camera: " + (cam != null ? $"OK far={cam.farClipPlane}" : "MISSING (no MainCamera tag)"));

        report.AppendLine(allGood ? "RESULT: ALL CHECKS PASSED" : "RESULT: PROBLEMS FOUND (see above)");

        string text = report.ToString();
        System.IO.File.WriteAllText(
            System.IO.Path.Combine(Application.dataPath, "..", "fogdoctor-report.txt"), text);
        if (allGood) Debug.Log("FogDoctor:\n" + text);
        else Debug.LogWarning("FogDoctor:\n" + text);
        SceneView.RepaintAll();
    }
}
