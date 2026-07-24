using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

/// <summary>
/// Builds an invisible boundary so the player can't leave the map.
///
/// Two modes:
///  - BoxAroundMap (default): 4 long walls forming a rectangle around the
///    bounding box of the spline you drew. Cheap (4 colliders) and keeps the
///    scene tiny.
///  - FollowSpline: a fence made of many small segments that hugs the spline
///    exactly (the old behaviour - thousands of colliders).
///
/// Put this on the Spline object (it auto-finds the SplineContainer), then:
///   Tools > Drifter > Build Walls   (bakes them in edit mode, saved with the
///   scene), or just press Play (buildOnStart).
/// </summary>
[RequireComponent(typeof(SplineContainer))]
public class SplineBoundary : MonoBehaviour
{
    public enum BoundaryMode { BoxAroundMap, FollowSpline }

    [Tooltip("BoxAroundMap = 4 long walls around the spline's bounding box (cheap). " +
             "FollowSpline = many small segments hugging the spline exactly (heavy).")]
    public BoundaryMode mode = BoundaryMode.BoxAroundMap;

    [Header("Box mode (4 walls)")]
    [Tooltip("Extra distance outside the spline edges, in meters. Negative pulls the walls inward.")]
    public float boxMargin = 0f;

    [Header("Spline mode (fence)")]
    [Tooltip("Distance between wall segments along the spline (meters). Smaller = smoother, more colliders.")]
    public float segmentLength = 30f;

    [Header("Shared")]
    [Tooltip("How tall each wall is (meters). Taller than any dune the player can climb.")]
    public float wallHeight = 400f;
    [Tooltip("Wall thickness (meters).")]
    public float wallThickness = 4f;
    [Tooltip("Build the walls automatically when the game starts.")]
    public bool buildOnStart = true;

    const string WALLS_ROOT = "SplineWalls";     // legacy name (old fence)
    const string BOX_ROOT = "MapBoundaryWalls";  // new 4-wall box

    void Start()
    {
        if (buildOnStart) Build();
    }

    /// <summary>Regenerates the boundary. Clears any previous walls first.</summary>
    public void Build()
    {
        ClearOld();

        var container = GetComponent<SplineContainer>();
        if (container == null || container.Splines.Count == 0)
        {
            Debug.LogError("SplineBoundary: no SplineContainer / spline found.");
            return;
        }

        if (mode == BoundaryMode.BoxAroundMap) BuildBox(container);
        else BuildFence(container);
    }

    // Remove walls from any previous build (both the old fence and the box).
    void ClearOld()
    {
        var oldFence = transform.Find(WALLS_ROOT);
        if (oldFence != null) DestroyGO(oldFence.gameObject);

        var oldBox = GameObject.Find(BOX_ROOT);
        if (oldBox != null) DestroyGO(oldBox.gameObject);
    }

    void DestroyGO(GameObject go)
    {
        if (Application.isPlaying) Destroy(go);
        else DestroyImmediate(go);
    }

    // ---- 4 long walls around the spline's bounding box ---------------------
    void BuildBox(SplineContainer container)
    {
        // Bounding box (XZ) of every spline point, in world space.
        bool has = false;
        float minX = 0, maxX = 0, minZ = 0, maxZ = 0, sumY = 0;
        int count = 0;

        foreach (var spline in container.Splines)
        {
            float worldLen = spline.GetLength() * SplineScale(container);
            int steps = Mathf.Clamp(Mathf.CeilToInt(worldLen / 5f), 16, 4000);
            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                Vector3 p = container.transform.TransformPoint((Vector3)spline.EvaluatePosition(t));
                if (!has) { minX = maxX = p.x; minZ = maxZ = p.z; has = true; }
                else
                {
                    if (p.x < minX) minX = p.x; if (p.x > maxX) maxX = p.x;
                    if (p.z < minZ) minZ = p.z; if (p.z > maxZ) maxZ = p.z;
                }
                sumY += p.y; count++;
            }
        }

        if (!has)
        {
            Debug.LogError("SplineBoundary: spline had no points to measure.");
            return;
        }

        minX -= boxMargin; maxX += boxMargin;
        minZ -= boxMargin; maxZ += boxMargin;

        float cx = (minX + maxX) * 0.5f;
        float cz = (minZ + maxZ) * 0.5f;
        float y = (count > 0 ? sumY / count : 0f);   // wall vertical center at the spline's height
        float width = maxX - minX;
        float depth = maxZ - minZ;
        float t2 = wallThickness;

        // Walls live at the scene root with an identity transform, so their
        // world size == collider size (no parent scaling surprises).
        var root = new GameObject(BOX_ROOT);
        root.transform.position = Vector3.zero;
        root.transform.rotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;

        // North / South span X (full width, incl. corners), East / West span Z.
        MakeWall(root.transform, "wall_north", new Vector3(cx, y, maxZ), new Vector3(width + t2, wallHeight, t2));
        MakeWall(root.transform, "wall_south", new Vector3(cx, y, minZ), new Vector3(width + t2, wallHeight, t2));
        MakeWall(root.transform, "wall_east",  new Vector3(maxX, y, cz), new Vector3(t2, wallHeight, depth + t2));
        MakeWall(root.transform, "wall_west",  new Vector3(minX, y, cz), new Vector3(t2, wallHeight, depth + t2));

        Debug.Log($"SplineBoundary: built 4 box walls around the map ({width:F0} x {depth:F0} m).");
    }

    void MakeWall(Transform parent, string name, Vector3 worldPos, Vector3 size)
    {
        var seg = new GameObject(name);
        seg.transform.SetParent(parent, true);
        seg.transform.position = worldPos;
        seg.transform.rotation = Quaternion.identity;
        seg.transform.localScale = Vector3.one;
        var box = seg.AddComponent<BoxCollider>();
        box.size = size;
    }

    // ---- Old behaviour: a fence of small segments hugging the spline -------
    void BuildFence(SplineContainer container)
    {
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

        Debug.Log($"SplineBoundary: built {wallCount} fence segments along the spline.");
    }

    // SplineContainer applies the transform; length is in local space, so scale it.
    static float SplineScale(SplineContainer c)
    {
        Vector3 s = c.transform.lossyScale;
        return (Mathf.Abs(s.x) + Mathf.Abs(s.z)) * 0.5f;
    }
}
