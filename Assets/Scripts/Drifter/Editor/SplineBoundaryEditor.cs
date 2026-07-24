using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Splines;

/// <summary>
/// Tools > Drifter > Build Walls - finds the Spline in the scene, adds a
/// SplineBoundary if needed, and bakes the invisible walls in edit mode so
/// they're saved with the scene (no need to enter Play).
///
/// By default SplineBoundary is in BoxAroundMap mode, so this builds 4 long
/// walls around the map's bounding box (and clears any old spline fence).
/// </summary>
public static class SplineBoundaryEditor
{
    [MenuItem("Tools/Drifter/Build Walls")]
    public static void Build()
    {
        var container = Object.FindFirstObjectByType<SplineContainer>();
        if (container == null)
        {
            EditorUtility.DisplayDialog("Build Walls",
                "No Spline (SplineContainer) found in the scene.", "OK");
            return;
        }

        var boundary = container.GetComponent<SplineBoundary>();
        if (boundary == null) boundary = Undo.AddComponent<SplineBoundary>(container.gameObject);

        boundary.Build();
        Selection.activeGameObject = container.gameObject;
        EditorSceneManager.MarkAllScenesDirty();
        Debug.Log("Walls built. Save the scene (Cmd+S) to keep them.");
    }
}
