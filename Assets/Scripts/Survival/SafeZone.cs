using UnityEngine;

namespace Survival
{
    public class SafeZone : MonoBehaviour
    {
        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            
            SurvivalManager.Instance.isPlayerInSafeZone = true;
            Debug.Log("Entered Safe Zone");
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            
            SurvivalManager.Instance.isPlayerInSafeZone = false;
            Debug.Log("Left Safe Zone");
        }
    }
}