using UnityEditor;
using UnityEngine;

/// <summary>
/// One-click setup: Tools > Drifter > Create Drifter In Scene.
/// Builds the player hierarchy (body + camera holder + audio) and attaches
/// the scene's existing Main Camera to the drifter's head, keeping all of
/// its settings (URP data, post-processing, etc.). Only if no Main Camera
/// exists is a new one created.
/// </summary>
public static class DrifterSetup
{
    [MenuItem("Tools/Drifter/Create Drifter In Scene")]
    public static void CreateDrifter()
    {
        // --- body ---
        var body = new GameObject("Drifter");
        Undo.RegisterCreatedObjectUndo(body, "Create Drifter");

        var cc = body.AddComponent<CharacterController>();
        cc.height = 1.8f;
        cc.radius = 0.35f;
        cc.center = new Vector3(0f, 0.9f, 0f);
        cc.slopeLimit = 50f;
        cc.stepOffset = 0.35f;
        cc.skinWidth = 0.06f;

        // Place the body in front of the scene view before parenting the camera.
        var sceneView = SceneView.lastActiveSceneView;
        if (sceneView != null)
        {
            Vector3 pos = sceneView.pivot;
            pos.y += 1f;
            body.transform.position = pos;
        }
        else
        {
            body.transform.position = new Vector3(0f, 1f, 0f);
        }

        // --- visible capsule body ---
        var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        visual.name = "BodyVisual";
        // The CharacterController handles collision - remove the mesh collider.
        Object.DestroyImmediate(visual.GetComponent<CapsuleCollider>());
        visual.transform.SetParent(body.transform, false);
        visual.transform.localPosition = new Vector3(0f, 0.9f, 0f);
        visual.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f); // matches radius 0.35, height 1.8

        // --- camera holder (pitches up/down, moves when crouching) ---
        var holder = new GameObject("CameraHolder");
        holder.transform.SetParent(body.transform, false);
        holder.transform.localPosition = new Vector3(0f, 1.62f, 0f);

        // --- find the scene's Main Camera (even if it was disabled) ---
        Camera cam = null;
        foreach (var c in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!c.CompareTag("MainCamera")) continue;
            cam = c;
            if (c.gameObject.activeInHierarchy) break; // prefer an active one
        }

        GameObject camGo;
        if (cam != null)
        {
            // Attach the existing Main Camera to the drifter's head.
            camGo = cam.gameObject;
            Undo.RecordObject(camGo.transform, "Attach Main Camera");
            Undo.SetTransformParent(camGo.transform, holder.transform, "Attach Main Camera");
            camGo.transform.localPosition = Vector3.zero;
            camGo.transform.localRotation = Quaternion.identity;
            camGo.transform.localScale = Vector3.one;
            camGo.SetActive(true);
        }
        else
        {
            camGo = new GameObject("DrifterCamera");
            camGo.transform.SetParent(holder.transform, false);
            cam = camGo.AddComponent<Camera>();
            camGo.tag = "MainCamera";
        }

        cam.fieldOfView = 60f;
        cam.nearClipPlane = 0.05f;
        cam.enabled = true;

        var camListener = camGo.GetComponent<AudioListener>();
        if (camListener == null) camListener = camGo.AddComponent<AudioListener>();
        camListener.enabled = true;

        // Disable any OTHER cameras / audio listeners so there's no conflict.
        foreach (var other in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
        {
            if (other == cam) continue;
            Undo.RecordObject(other.gameObject, "Disable other camera");
            other.gameObject.SetActive(false);
        }
        foreach (var listener in Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
        {
            if (listener == camListener) continue;
            Undo.RecordObject(listener, "Disable other listener");
            listener.enabled = false;
        }

        // --- components ---
        var controller = body.AddComponent<DrifterController>();
        controller.cameraHolder = holder.transform;
        controller.playerCamera = cam;
        controller.bodyVisual = visual.transform;

        var audioSource = body.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;

        var footsteps = body.AddComponent<DrifterFootsteps>();
        footsteps.bobTarget = camGo.transform;
        footsteps.audioSource = audioSource;

        Selection.activeGameObject = body;
        EditorGUIUtility.PingObject(body);
        Debug.Log("Drifter created with the scene's Main Camera attached. WASD move | Shift sprint | Space jump | Command/Ctrl crouch | Right mouse zoom.");
    }
}
