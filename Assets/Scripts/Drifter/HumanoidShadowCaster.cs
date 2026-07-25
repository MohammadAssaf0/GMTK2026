using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Makes the player cast a PERSON-shaped shadow instead of a capsule.
/// Builds a simple humanoid (head, torso, arms, legs) from primitives set to
/// "Shadows Only" - they cast shadows but are invisible in the game.
///
/// Setup: add to the player object (e.g. the Drifter). Assign "Hide Shadow Of"
/// to the capsule body's Renderer so its capsule shadow turns off.
/// Tune Height / Foot Offset Y so the feet sit on the ground.
/// </summary>
public class HumanoidShadowCaster : MonoBehaviour
{
    [Header("Size / placement")]
    [Tooltip("Total height of the shadow figure (meters).")]
    public float height = 1.8f;
    [Tooltip("Move the whole figure up/down so its feet reach the ground. Adjust until the shadow's feet line up.")]
    public float footOffsetY = -0.9f;
    [Tooltip("Overall width multiplier of the figure.")]
    public float widthScale = 1f;

    [Header("Optional")]
    [Tooltip("The capsule body's Renderer - its capsule shadow will be turned off so only the humanoid shadow shows.")]
    public Renderer hideShadowOf;
    [Tooltip("If set, the figure turns to face this transform's yaw (e.g. the camera) so the shadow orients naturally.")]
    public Transform faceYawOf;

    Transform root;

    void Awake()
    {
        if (hideShadowOf != null)
            hideShadowOf.shadowCastingMode = ShadowCastingMode.Off;

        Build();
    }

    void LateUpdate()
    {
        if (root != null && faceYawOf != null)
        {
            Vector3 e = root.eulerAngles;
            root.rotation = Quaternion.Euler(0f, faceYawOf.eulerAngles.y, 0f);
        }
    }

    void Build()
    {
        float h = height;
        var go = new GameObject("HumanoidShadow");
        root = go.transform;
        root.SetParent(transform, false);
        root.localPosition = new Vector3(0f, footOffsetY, 0f);
        root.localRotation = Quaternion.identity;

        // head
        AddPart(PrimitiveType.Sphere,
            new Vector3(0f, 0.90f * h, 0f),
            new Vector3(0.18f * h, 0.20f * h, 0.18f * h));

        // torso
        AddPart(PrimitiveType.Capsule,
            new Vector3(0f, 0.63f * h, 0f),
            new Vector3(0.30f * h, 0.20f * h, 0.22f * h));

        // legs
        AddPart(PrimitiveType.Capsule,
            new Vector3(-0.09f * h, 0.24f * h, 0f),
            new Vector3(0.16f * h, 0.25f * h, 0.16f * h));
        AddPart(PrimitiveType.Capsule,
            new Vector3(0.09f * h, 0.24f * h, 0f),
            new Vector3(0.16f * h, 0.25f * h, 0.16f * h));

        // arms
        AddPart(PrimitiveType.Capsule,
            new Vector3(-0.24f * h, 0.64f * h, 0f),
            new Vector3(0.11f * h, 0.20f * h, 0.11f * h));
        AddPart(PrimitiveType.Capsule,
            new Vector3(0.24f * h, 0.64f * h, 0f),
            new Vector3(0.11f * h, 0.20f * h, 0.11f * h));
    }

    void AddPart(PrimitiveType type, Vector3 localPos, Vector3 localScale)
    {
        var part = GameObject.CreatePrimitive(type);
        part.name = "ShadowPart";

        var col = part.GetComponent<Collider>();
        if (col != null) DestroyImmediate(col);

        var r = part.GetComponent<Renderer>();
        r.shadowCastingMode = ShadowCastingMode.ShadowsOnly; // casts shadow, invisible

        var t = part.transform;
        t.SetParent(root, false);
        localScale.x *= widthScale;
        localScale.z *= widthScale;
        t.localPosition = localPos;
        t.localScale = localScale;
    }
}
