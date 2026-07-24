using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

/// <summary>
/// Builds invisible walls along a Spline, so the player can't leave the map.
/// Put this on the Spline object (it auto-finds the SplineContainer), press
/// Play, and a tall collider fence is generated along the whole spline.
///
/// Also bakeable from the editor: Tools > Drifter > Build Spline Walls
/// (so the walls exist without entering Play mode and get saved in the scene).
/// </summary>
[RequireComponent(typeof(SplineContainer))]
public class SplineBoundary : MonoBehaviour
{
    [Tooltip("Distance between wall segments along the spline (meters). Smaller = smoother wall, more colliders.")]
    public float segmentLength = 30f;
    [Tooltip("How tall each wall is (meters). Make it taller than any dune the player can climb.")]
    public float wallHeight = 400f;
    [Tooltip("Wall thickness (meters).")]
    public float wallThickness = 4f;
    [Tooltip("Build the walls automatically when the game starts.")]
    public bool buildOnStart = true;

    const string WALLS_ROOT = "SplineWalls";

    void Start()
    {
        if (buildOnStart) Build();
    }

    /// <summary>Regenerates the invisible wall fence along the spline.</summary>
    public void Build()
    {
        var container = GetComponent<SplineContainer>();
        if (container == null || container.Splines.Count == 0)
        {
            Debug.LogError("SplineBoundary: no SplineContainer / spline found.");
            return;
        }

        // Clear old walls.
        var old = transform.Find(WALLS_ROOT);
        if (old != null)
        {
            if (Application.isPlaying) Destroy(old.gameObject);
            else DestroyImmediate(old.gameObject);
        }

        var root = new GameObject(WALLS_ROOT);
        root.transform.SetParent(transform, false);

        int wallCount = 0;
        foreach (var spline in container.Splines)
        {
            float length = spline.GetLength() * SplineScale(container);
            if (length < 1f) continue;
            int steps = Mathf.Max(2, Mathf.CeilToInt(length / Mathf.Max(1f, segmentLength)));

            List<Vector3> pts = new List<Vector3>();
            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                float3 local = spline.EvaluatePosition(t);
                pts.Add(container.transform.TransformPoint((Vector3)local));
            }

            for (int i = 0; i < pts.Count - 1; i++)
            {
                Vector3 a = pts[i], b = pts[i + 1];
                Vector3 mid = (a + b) * 0.5f;
                Vector3 dir = b - a;
                float len = dir.magnitude;
                if (len < 0.01f) continue;

                var seg = new GameObject("wall_" + wallCount);
                seg.transform.SetParent(root.transform, false);
                seg.transform.position = mid;
                seg.transform.rotation = Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.z).normalized, Vector3.up);

                var box = seg.AddComponent<BoxCollider>();
                box.size = new Vector3(wallThickness, wallHeight, len + wallThickness);
                wallCount++;
            }
        }

        Debug.Log($"SplineBoundary: built {wallCount} invisible wall segments along the spline.");
    }

    // SplineContainer applies the transform; length is in local space, so scale it.
    static float SplineScale(SplineContainer c)
    {
        Vector3 s = c.transform.lossyScale;
        return (Mathf.Abs(s.x) + Mathf.Abs(s.z)) * 0.5f;
    }
}
