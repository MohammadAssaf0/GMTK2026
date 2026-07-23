using UnityEngine;
using UnityEngine.Events;

namespace Interactables
{
    public class InteractableObject : MonoBehaviour
    {
        public string promptText = "Interact";
        
        public UnityEvent onInteractAction; 

        public void OnInteract()
        {
            Debug.Log("Player interacted with: " + gameObject.name);
            onInteractAction.Invoke();
        }
    }
}