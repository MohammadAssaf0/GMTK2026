using System.Collections.Generic;
using UnityEngine;
using Dreamteck.Splines;

// RoadMeshFromSpline
// ------------------
// Builds a solid road mesh (with thickness) that follows a Dreamteck spline and
// hugs the surface below it (raycast at every sample). Assigns your asphalt material.
//
// It auto-builds when Unity recompiles, auto-finds the "terrain" collider, and
// auto-fixes Ray Height if it's 0. You can also right-click the component header
// -> "Build Road Mesh" any time.
[RequireComponent(typeof(SplineComputer))]
public class RoadMeshFromSpline : MonoBehaviour
{
    [Header("Target to stick to")]
    [Tooltip("Drag the object (with a Collider) the road should hug. If empty, it looks for an object named 'terrain'.")]
    public Collider targetCollider;
    public LayerMask surfaceMask = ~0;
    public float rayHeight = 500f;
    public float heightOffset = 0.05f;

    [Header("Road shape")]
    public float width = 6f;
    public float thickness = 0.5f;
    [Range(2, 2000)] public int samples = 250;

    [Header("Look")]
    public Material roadMaterial;
    [Tooltip("Texture tiles per world meter along the road length.")]
    public float tilesPerMeter = 0.2f;

    [Header("Automation")]
    [Tooltip("Rebuild automatically when Unity recompiles / loads this component (edit mode only).")]
    public bool autoBuildOnLoad = true;

    bool CastDown(Vector3 origin, out RaycastHit hit)
    {
        Ray ray = new Ray(origin, Vector3.down);
        if (targetCollider != null)
            return targetCollider.Raycast(ray, out hit, rayHeight * 2f);
        return Physics.Raycast(ray, out hit, rayHeight * 2f, surfaceMask, QueryTriggerInteraction.Ignore);
    }

    [ContextMenu("Build Road Mesh")]
    public void Build()
    {
        SplineComputer spline = GetComponent<SplineComputer>();
        if (spline == null) { Debug.LogError("RoadMeshFromSpline: no SplineComputer on this object."); return; }

        // safety: a zero ray height means nothing gets hit
        if (rayHeight <= 0f) rayHeight = 500f;

        // auto-find the ground collider if none was assigned
        if (targetCollider == null)
        {
            GameObject t = GameObject.Find("terrain");
            if (t != null) targetCollider = t.GetComponent<Collider>();
        }

#if UNITY_EDITOR
        if (roadMaterial == null)
        {
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:Material", new[] { "Assets/art/material/asphalt" });
            if (guids.Length > 0)
                roadMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]));
        }
#endif

        Transform holder = transform.Find("RoadMesh");
        if (holder == null)
        {
            GameObject go = new GameObject("RoadMesh");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>();
            go.AddComponent<MeshRenderer>();
            holder = go.transform;
        }
        MeshFilter mf = holder.GetComponent<MeshFilter>();
        MeshRenderer mr = holder.GetComponent<MeshRenderer>();

        int n = Mathf.Max(2, samples);
        Vector3[] center = new Vector3[n];
        Vector3[] up = new Vector3[n];
        int hits = 0;

        for (int i = 0; i < n; i++)
        {
            double t = (double)i / (n - 1);
            Vector3 p = spline.EvaluatePosition(t);
            RaycastHit hit;
            if (CastDown(new Vector3(p.x, p.y + rayHeight, p.z), out hit))
            {
                center[i] = hit.point + hit.normal * heightOffset;
                up[i] = hit.normal;
                hits++;
            }
            else
            {
                center[i] = p;
                up[i] = Vector3.up;
            }
        }

        if (hits == 0)
        {
            Debug.LogWarning("RoadMeshFromSpline: no raycast hits. Assign Target Collider (needs a Collider) or check Surface Mask.");
            return;
        }

        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();
        float halfW = width * 0.5f;
        float dist = 0f;

        for (int i = 0; i < n; i++)
        {
            Vector3 fwd;
            if (i == 0) fwd = center[1] - center[0];
            else if (i == n - 1) fwd = center[n - 1] - center[n - 2];
            else fwd = center[i + 1] - center[i - 1];
            if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.forward;
            fwd.Normalize();

            Vector3 right = Vector3.Cross(up[i], fwd).normalized;

            Vector3 TL = center[i] - right * halfW;
            Vector3 TR = center[i] + right * halfW;
            Vector3 BL = TL - up[i] * thickness;
            Vector3 BR = TR - up[i] * thickness;

            if (i > 0) dist += Vector3.Distance(center[i], center[i - 1]);
            float v = dist * tilesPerMeter;

            int b = verts.Count;
            verts.Add(holder.InverseTransformPoint(TL)); uvs.Add(new Vector2(0f, v));
            verts.Add(holder.InverseTransformPoint(TR)); uvs.Add(new Vector2(1f, v));
            verts.Add(holder.InverseTransformPoint(BL)); uvs.Add(new Vector2(0f, v));
            verts.Add(holder.InverseTransformPoint(BR)); uvs.Add(new Vector2(1f, v));

            if (i > 0)
            {
                int pa = b - 4, pb = b - 3, pc = b - 2, pd = b - 1;
                int a = b, bb = b + 1, c = b + 2, d = b + 3;
                AddQuad(tris, pa, pb, bb, a); // top
                AddQuad(tris, pb, pd, d, bb); // right side
                AddQuad(tris, pd, pc, c, d);  // bottom
                AddQuad(tris, pc, pa, a, c);  // left side
            }
        }

        Mesh mesh = new Mesh { name = "RoadMesh" };
        mesh.indexFormat = verts.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        mf.sharedMesh = mesh;
        if (roadMaterial != null) mr.sharedMaterial = roadMaterial;

        Debug.Log("RoadMeshFromSpline: built road (" + n + " sections, " + hits + " surface hits, width " + width + ", thickness " + thickness + ").");
    }

    static void AddQuad(List<int> tris, int a, int b, int c, int d)
    {
        // reversed winding so the road's top faces UP (visible from above)
        tris.Add(a); tris.Add(c); tris.Add(b);
        tris.Add(a); tris.Add(d); tris.Add(c);
    }

#if UNITY_EDITOR
    void OnEnable()
    {
        if (autoBuildOnLoad && !Application.isPlaying)
            UnityEditor.EditorApplication.delayCall += DelayedBuild;
    }

    void DelayedBuild()
    {
        if (this == null) return;
        Build();
    }
#endif
}
