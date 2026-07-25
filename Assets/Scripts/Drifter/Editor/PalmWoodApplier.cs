using UnityEngine;
using UnityEditor;

/// <summary>
/// Editor tool: assigns the "wood" material to every renderer of the selected
/// object(s) that is NOT the leaves (moss). Use it on the palm.
///
/// Menu: Tools > Palm > Apply Wood To Non-Leaf Parts
/// </summary>
public static class PalmWoodApplier
{
    [MenuItem("Tools/Palm/Apply Wood To Non-Leaf Parts")]
    static void Apply()
    {
        Material wood = LoadMaterial("wood");
        Material moss = LoadMaterial("moss");

        if (wood == null)
        {
            Debug.LogError("PalmWoodApplier: couldn't find a material named 'wood'.");
            return;
        }

        var targets = Selection.gameObjects;
        if (targets == null || targets.Length == 0)
        {
            Debug.LogWarning("PalmWoodApplier: select the palm object in the Hierarchy first.");
            return;
        }

        int changedRenderers = 0;
        foreach (var go in targets)
        {
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                var mats = r.sharedMaterials;
                bool changed = false;

                for (int i = 0; i < mats.Length; i++)
                {
                    // keep the leaves (moss) exactly as they are
                    if (mats[i] == moss) continue;
                    if (mats[i] != null && mats[i].name.ToLower().Contains("moss")) continue;
                    if (mats[i] == wood) continue; // already wood

                    mats[i] = wood;
                    changed = true;
                }

                if (changed)
                {
                    Undo.RecordObject(r, "Apply wood to palm");
                    r.sharedMaterials = mats;
                    EditorUtility.SetDirty(r);
                    changedRenderers++;
                }
            }
        }

        Debug.Log($"PalmWoodApplier: applied 'wood' to {changedRenderers} renderers (leaves/moss kept).");
    }

    static Material LoadMaterial(string exactName)
    {
        foreach (var guid in AssetDatabase.FindAssets("t:Material " + exactName))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null && m.name.ToLower() == exactName.ToLower())
                return m;
        }
        return null;
    }
}
