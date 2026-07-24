using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Tools > Drifter > Measure Map.
/// Computes the world-space bounds of the level's renderers (skipping the fog,
/// particles and the player), prints Width x Depth x Height + center + min/max
/// to the Console, and writes map-size.txt to the project folder.
///
/// Tools > Drifter > Fit Sandstorm To Map - sizes the sandstorm to cover the
/// whole map (and stops it from following the player, so it's map-relative).
/// </summary>
public static class MapMeasure
{
    [MenuItem("Tools/Drifter/Measure Map")]
    public static Bounds Measure()
    {
        bool has = false;
        Bounds b = new Bounds();
        var report = new StringBuilder();

        // Prefer an object literally named "map" (case-insensitive) - measure
        // just its renderers, which is the true playable map size.
        GameObject mapGo = null;
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            if (go.name.ToLower() == "map") { mapGo = go; break; }

        Renderer[] renderers = mapGo != null
            ? mapGo.GetComponentsInChildren<Renderer>()
            : Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);

        report.AppendLine(mapGo != null
            ? $"measuring object 'map' ({renderers.Length} renderers)\n"
            : "no object named 'map' found - measuring all level geometry\n");

        foreach (var r in renderers)
        {
            string n = r.gameObject.name.ToLower();
            if (r is ParticleSystemRenderer) continue;
            if (mapGo == null && (n.Contains("fog") || n.Contains("sandstorm")
                || n.Contains("sand particle") || n.Contains("bodyvisual") || n.Contains("drifter"))) continue;
            if (mapGo == null && r.GetComponentInParent<DrifterController>() != null) continue;

            if (!has) { b = r.bounds; has = true; }
            else b.Encapsulate(r.bounds);
            report.AppendLine($"  {r.gameObject.name,-30} size=({r.bounds.size.x:F0} x {r.bounds.size.z:F0})  centerXZ=({r.bounds.center.x:F0}, {r.bounds.center.z:F0})");
        }

        if (!has)
        {
            Debug.LogWarning("MapMeasure: no map renderers found.");
            return b;
        }

        string summary =
            $"===== MAP SIZE =====\n" +
            $"Width  (X): {b.size.x:F1} m\n" +
            $"Depth  (Z): {b.size.z:F1} m\n" +
            $"Height (Y): {b.size.y:F1} m\n" +
            $"Center: ({b.center.x:F1}, {b.center.y:F1}, {b.center.z:F1})\n" +
            $"Min:    ({b.min.x:F1}, {b.min.y:F1}, {b.min.z:F1})\n" +
            $"Max:    ({b.max.x:F1}, {b.max.y:F1}, {b.max.z:F1})\n" +
            $"Diagonal (XZ): {new Vector2(b.size.x, b.size.z).magnitude:F1} m\n" +
            $"--- per renderer ---\n" + report;

        Debug.Log("<b>MapMeasure</b>\n" + summary);
        System.IO.File.WriteAllText(
            System.IO.Path.Combine(Application.dataPath, "..", "map-size.txt"), summary);
        return b;
    }

    [MenuItem("Tools/Drifter/Fit Sandstorm To Map")]
    public static void FitSandstormToMap()
    {
        Bounds b = Measure();
        var storm = Object.FindFirstObjectByType<SandstormController>();
        if (storm == null)
        {
            Debug.LogError("No SandstormController in the scene.");
            return;
        }

        Undo.RecordObject(storm, "Fit Sandstorm To Map");
        Undo.RecordObject(storm.transform, "Fit Sandstorm To Map");

        // Cover the whole map (+20% margin), centered on the map, fixed in place.
        float diameter = Mathf.Max(b.size.x, b.size.z) * 1.2f;
        storm.followPlayerHeight = false;
        storm.stormSize = diameter;
        storm.stormHeight = Mathf.Max(storm.stormHeight, b.size.y * 0.6f);
        storm.transform.position = new Vector3(b.center.x, b.min.y + storm.stormHeight * 0.4f, b.center.z);
        storm.ApplySize();

        // Fixed on the map, not following the player.
        storm.followPlayer = false;

        EditorUtility.SetDirty(storm);
        Debug.Log($"Sandstorm fitted to map: diameter {diameter:F0}m, centered at ({b.center.x:F0}, {b.center.z:F0}). It now covers the whole map instead of following the player. Save the scene (Cmd+S).");
    }
}
