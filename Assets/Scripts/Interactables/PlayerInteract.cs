using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Interactables
{
    public class PlayerInteract : MonoBehaviour
    {
        [Header("References")] 
        public Camera playerCamera;
        
        [Header("UI References")]
        public GameObject defaultCrosshair;
        public GameObject activeCrosshair;
        public TextMeshProUGUI promptText;
        
        [Header("Interaction Settings")]
        public float interactDistance = 3f;
        public LayerMask interactLayer;
        
        private void Start()
        {
            defaultCrosshair.SetActive(true);
            activeCrosshair.SetActive(false);
            if (promptText != null) promptText.text = "";
        }
        
        private void Update()
        {
            var ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);

            if (Physics.Raycast(ray, out var hit, interactDistance, interactLayer))
            {
                defaultCrosshair.SetActive(false);
                activeCrosshair.SetActive(true);

                var interactable = hit.collider.GetComponent<InteractableObject>();
                if (interactable)
                {
                    if (promptText) 
                        promptText.text = "[E] " + interactable.promptText;

                    if (Keyboard.current.eKey.wasPressedThisFrame)
                    {
                        interactable.OnInteract();
                    }
                }
            }
            else
            {
                defaultCrosshair.SetActive(true);
                activeCrosshair.SetActive(false);
                if (promptText) promptText.text = "";
            }
        }
    }
}
