using UnityEngine;
using UnityEngine.InputSystem;

namespace Interactables
{
    public class PlayerInteract : MonoBehaviour
    {
        [Header("References")] 
        public Camera playerCamera;
        
        [Header("Interaction Settings")]
        public float interactDistance = 3f;
        public LayerMask interactLayer;
        
        private void Update()
        {
            var ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);

            if (!Physics.Raycast(ray, out var hit, interactDistance, interactLayer)) return;
            // TODO: Display a UI crosshair

            if (!Keyboard.current.eKey.wasPressedThisFrame) return;
            
            var interactable = hit.collider.GetComponent<InteractableObject>();
            if (interactable != null)
            {
                interactable.OnInteract();
            }
        }
    }
}
