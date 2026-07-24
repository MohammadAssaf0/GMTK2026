using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Splines;

/// <summary>
/// Tools > Drifter > Build Spline Walls - finds the Spline in the scene, adds
/// a SplineBoundary if needed, and bakes the invisible walls in edit mode so
/// they're saved with the scene (no need to enter Play).
/// </summary>
public static class SplineBoundaryEditor
{
    [MenuItem("Tools/Drifter/Build Spline Walls")]
    public static void Build()
    {
        var container = Object.FindFirstObjectByType<SplineContainer>();
        if (container == null)
        {
            EditorUtility.DisplayDialog("Spline Walls",
                "No Spline (SplineContainer) found in the scene.", "OK");
            return;
        }

        var boundary = container.GetComponent<SplineBoundary>();
        if (boundary == null) boundary = Undo.AddComponent<SplineBoundary>(container.gameObject);

        boundary.Build();
        Selection.activeGameObject = container.gameObject;
        EditorSceneManager.MarkAllScenesDirty();
        Debug.Log("Spline walls built. Save the scene (Cmd+S) to keep them.");
    }
}
