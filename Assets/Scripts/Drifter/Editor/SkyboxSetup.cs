using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Tools > Drifter > Apply Desert Skybox - warm skybox, no grey horizon band.
/// Tools > Drifter > Apply Desert Sun    - positions and colors the sun so it
/// visibly radiates through the sandstorm (works with the fog's day/night
/// lighting and the skybox sun disk).
/// </summary>
public static class SkyboxSetup
{
    [MenuItem("Tools/Drifter/Apply Desert Skybox")]
    public static void Apply()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Scripts/Drifter/DesertSkybox.mat");
        if (mat == null)
        {
            Debug.LogError("DesertSkybox.mat not found at Assets/Scripts/Drifter/");
            return;
        }
        RenderSettings.skybox = mat;
        DynamicGI.UpdateEnvironment();
        EditorSceneManager.MarkAllScenesDirty();
        Debug.Log("Desert skybox applied. Save the scene (Cmd+S) to keep it.");
    }

    [MenuItem("Tools/Drifter/Apply Desert Sun")]
    public static void ApplySun()
    {
        Light sun = RenderSettings.sun;
        if (sun == null)
        {
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) { sun = l; break; }
        }
        if (sun == null)
        {
            Debug.LogError("No directional light found in the scene.");
            return;
        }

        Undo.RecordObject(sun.transform, "Desert Sun");
        Undo.RecordObject(sun, "Desert Sun");

        // Low-ish warm desert sun: dramatic side light through the storm.
        sun.transform.rotation = Quaternion.Euler(35f, -40f, 0f);
        sun.color = new Color(1f, 0.9f, 0.75f);
        sun.intensity = 1.2f;
        RenderSettings.sun = sun;

        DynamicGI.UpdateEnvironment();
        EditorSceneManager.MarkAllScenesDirty();
        Debug.Log("Desert sun applied - warm light angled through the sandstorm. Save the scene (Cmd+S).");
    }

    [MenuItem("Tools/Drifter/Add Day-Night Cycle")]
    public static void AddDayNight()
    {
        Light sun = RenderSettings.sun;
        if (sun == null)
        {
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) { sun = l; break; }
        }
        if (sun == null)
        {
            Debug.LogError("No directional light found in the scene.");
            return;
        }

        var cycle = sun.GetComponent<DayNightCycle>();
        if (cycle == null) cycle = Undo.AddComponent<DayNightCycle>(sun.gameObject);
        cycle.sun = sun;
        cycle.ApplyTime();

        Selection.activeGameObject = sun.gameObject;
        EditorSceneManager.MarkAllScenesDirty();
        Debug.Log("Day-Night Cycle added to the directional light. Tune Day Length / Time Of Day / gradients in the Inspector, then save (Cmd+S).");
    }
}
