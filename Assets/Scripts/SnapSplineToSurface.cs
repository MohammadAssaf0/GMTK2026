using UnityEngine;
using Dreamteck.Splines;

// SnapSplineToSurface
// -------------------
// Projects every point of a Dreamteck SplineComputer straight DOWN onto a
// surface so the spline hugs your mesh / dune / ground.
//
// HOW TO USE:
// 1. Make sure the object you want the spline to sit on has a Collider
//    (e.g. Mesh Collider).
// 2. Drag that exact object's Collider into "Target Collider" below
//    (recommended). If you leave it empty, it uses the Surface Mask instead.
// 3. Draw a rough spline from a top-down view (only X/Z matter).
// 4. Right-click this component header -> "Snap To Surface".
[RequireComponent(typeof(SplineComputer))]
public class SnapSplineToSurface : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("Drag the exact object (with a Collider) you want the spline to snap onto. If set, ONLY this object is used - most reliable.")]
    public Collider targetCollider;

    [Tooltip("Used only when Target Collider is empty: which layers to raycast against.")]
    public LayerMask surfaceMask = ~0;

    [Header("Options")]
    [Tooltip("How high above each point the downward ray starts (meters). Make it larger than your terrain's height range.")]
    public float rayHeight = 500f;

    [Tooltip("Lift the spline this much above the surface so it doesn't clip into the mesh (meters).")]
    public float heightOffset = 0.1f;

    [Tooltip("Also align each point's normal to the surface (recommended).")]
    public bool alignNormals = true;

    bool CastDown(Vector3 origin, out RaycastHit hit)
    {
        Ray ray = new Ray(origin, Vector3.down);
        if (targetCollider != null)
            return targetCollider.Raycast(ray, out hit, rayHeight * 2f);
        return Physics.Raycast(ray, out hit, rayHeight * 2f, surfaceMask, QueryTriggerInteraction.Ignore);
    }

    [ContextMenu("Snap To Surface")]
    public void SnapToSurface()
    {
        SplineComputer spline = GetComponent<SplineComputer>();
        if (spline == null) { Debug.LogError("SnapSplineToSurface: No SplineComputer on this GameObject."); return; }

        SplinePoint[] points = spline.GetPoints();
        if (points == null || points.Length == 0)
        {
            Debug.LogWarning("SnapSplineToSurface: The spline has no points yet. Draw the path first.");
            return;
        }

        int snapped = 0;
        for (int i = 0; i < points.Length; i++)
        {
            Vector3 p = points[i].position;
            RaycastHit hit;
            if (CastDown(new Vector3(p.x, p.y + rayHeight, p.z), out hit))
            {
                points[i].position = hit.point + Vector3.up * heightOffset;
                if (alignNormals) points[i].normal = hit.normal;
                snapped++;
            }
            else
            {
                Debug.LogWarning("SnapSplineToSurface: point " + i + " found no surface below it. Check that Target Collider is set (or the Surface Mask) and that the object has a Collider.");
            }
        }

        spline.SetPoints(points);
        spline.Rebuild();
        Debug.Log("SnapSplineToSurface: snapped " + snapped + " / " + points.Length + " points onto " +
                  (targetCollider != null ? targetCollider.name : "the surface") + ".");
    }
}
