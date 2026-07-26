using Survival;
using UnityEngine;
using UnityEngine.Events;

namespace Interactables
{
    public class InteractableObject : MonoBehaviour
    {
        public string promptText = "Interact";
        public int amountIndex = 1;
        public int interactionCount = 1;
        
        public enum InteractionType
        {
            UseUnityEvent,
            DrinkWater,
            PickupBatteries,
            PickupFlare,
            UnlockMap,
            Sleep
        }
        
        [Header("Prefab Action Settings")]
        public InteractionType actionType = InteractionType.UseUnityEvent;
        
        [Header("Custom Event (Only for Scene Objects)")]
        public UnityEvent onInteractAction; 

        public void OnInteract()
        {
            interactionCount--;
            
            switch (actionType)
            {
                case InteractionType.DrinkWater:
                    SurvivalManager.Instance.GotWater(amountIndex);
                    if (interactionCount == 0) Destroy(gameObject);
                    break;
                
                case InteractionType.PickupBatteries:
                    SurvivalManager.Instance.PickupBatteries();
                    Destroy(gameObject);
                    break;
                
                case InteractionType.PickupFlare:
                    SurvivalManager.Instance.PickupFlare();
                    Destroy(gameObject);
                    break;
                
                case InteractionType.UnlockMap:
                    DiaryManager.Instance.UnlockMapOverlay(amountIndex);
                    break;
                
                case InteractionType.Sleep:
                    SurvivalManager.Instance.SleepThroughNight();
                    break;
                
                case InteractionType.UseUnityEvent:
                    onInteractAction.Invoke();
                    if (interactionCount == 0) Destroy(gameObject);
                    break;
            }
        }
    }
}