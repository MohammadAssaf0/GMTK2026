using UnityEditor;
using UnityEngine;

/// <summary>
/// One-click setup: Tools > Drifter > Create Drifter In Scene.
/// Builds a player identical to Assets/Drifter/Prefabs/Player.prefab
/// (same dimensions, capsule visual with drifter.mat, camera at 0.7,
/// tag "Player") but driven by the new Input System scripts.
/// Attaches the scene's existing Main Camera if there is one.
/// </summary>
public static class DrifterSetup
{
    [MenuItem("Tools/Drifter/Create Drifter In Scene")]
    public static void CreateDrifter()
    {
        // --- body: exactly like the Player prefab ---
        var body = new GameObject("Drifter");
        Undo.RegisterCreatedObjectUndo(body, "Create Drifter");
        body.tag = "Player";

        var cc = body.AddComponent<CharacterController>();
        cc.height = 2f;
        cc.radius = 0.5f;
        cc.center = Vector3.zero;          // center pivot, like the prefab
        cc.slopeLimit = 30f; // calibrated to the dune model's real slopes
        cc.stepOffset = 0.3f;
        cc.skinWidth = 0.08f;
        cc.minMoveDistance = 0f;

        // Center pivot: place it 1.2 above the floor like the prefab.
        var sceneView = SceneView.lastActiveSceneView;
        if (sceneView != null)
        {
            Vector3 pos = sceneView.pivot;
            pos.y += 1.2f;
            body.transform.position = pos;
        }
        else
        {
            body.transform.position = new Vector3(0f, 1.2f, 0f);
        }

        // --- visible capsule body (prefab look: builtin capsule + drifter.mat) ---
        var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        visual.name = "BodyVisual";
        Object.DestroyImmediate(visual.GetComponent<CapsuleCollider>()); // CC handles collision
        visual.transform.SetParent(body.transform, false);
        visual.transform.localPosition = Vector3.zero;   // matches CC center
        visual.transform.localScale = Vector3.one;       // capsule mesh = height 2, radius 0.5

        var drifterMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Drifter/drifter.mat");
        if (drifterMat != null)
            visual.GetComponent<MeshRenderer>().sharedMaterial = drifterMat;

        // --- camera holder at (0, 0.7, 0), exactly like the prefab's camera ---
        var holder = new GameObject("CameraHolder");
        holder.transform.SetParent(body.transform, false);
        holder.transform.localPosition = new Vector3(0f, 0.7f, 0f);

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

        // Prefab camera settings.
        cam.fieldOfView = 60f;
        cam.nearClipPlane = 0.3f;
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
        Debug.Log("Drifter created (Player-prefab twin). WASD move | Shift run | Space jump | Command/Ctrl crouch | Right mouse zoom.");
    }
}
