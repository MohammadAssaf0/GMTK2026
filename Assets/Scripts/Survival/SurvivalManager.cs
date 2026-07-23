using TMPro;
using UnityEngine;

namespace Survival
{
    public class SurvivalManager : MonoBehaviour
    {
        // Singleton pattern so the SafeZone triggers can easily find this script
        public static SurvivalManager Instance;

        [Header("References")]
        public DayNightCycle dayNightClock;
        
        [Header("Water Settings")]
        public float maxWater = 100f;
        public float currentWater;
        public float daytimeWaterDrainPerSecond = 0.4f;
        public float safeSleepWaterCost = 10f;
        public float desertSleepPenalty = 30f;
        
        [Header("Time & Countdown")]
        public int currentDay = 1;
        public int maxDays = 3;
        public float sunsetHour = 19f;  // 7:00 PM
        public float sunriseHour = 6f;  // 6:00 AM

        [Header("Inventory & Status")]
        public bool hasBatteries;
        public bool hasFlare;
        public bool isPlayerInSafeZone;

        [Header("UI References")]
        public TextMeshProUGUI waterText;
        public TextMeshProUGUI daysLeftText;
        public TextMeshProUGUI notificationText; 
        
        private bool _isGameOver;

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            currentWater = maxWater;
            UpdateUI();
        }

        private void Update()
        {
            if (_isGameOver) return;

            HandleDaytimeWaterDrain();
            WatchTheClock();
        }

        private void HandleDaytimeWaterDrain()
        {
            currentWater -= daytimeWaterDrainPerSecond * Time.deltaTime;
            
            if (currentWater <= 0)
            {
                currentWater = 0;
                TriggerGameOver("You died of thirst in the desert.");
            }
            
            UpdateUI();
        }

        private void WatchTheClock()
        {
            if (dayNightClock.timeOfDay >= sunsetHour)
            {
                SleepThroughNight();
            }
        }

        private void SleepThroughNight()
        {
            currentDay++;
            
            dayNightClock.timeOfDay = sunriseHour;
            dayNightClock.ApplyTime();

            if (isPlayerInSafeZone)
            {
                currentWater -= safeSleepWaterCost;
                ShowNotification("You slept safely. Woke up slightly thirsty.");
            }
            else
            {
                currentWater -= (safeSleepWaterCost + desertSleepPenalty);
                ShowNotification("You slept in the harsh desert. You lost critical water!");
            }

            if (currentWater <= 0)
            {
                currentWater = 0;
                TriggerGameOver("You succumbed to dehydration during the night.");
                return;
            }

            if (currentDay > maxDays)
            {
                CheckWinCondition();
            }

            UpdateUI();
        }

        // Interactions

        public void DrinkWater(float amount)
        {
            currentWater += amount;
            if (currentWater > maxWater) currentWater = maxWater;
            ShowNotification("Drank water.");
            UpdateUI();
        }

        public void PickupBatteries() { hasBatteries = true; ShowNotification("Found Batteries!"); }
        public void PickupFlare() { hasFlare = true; ShowNotification("Found a Flare!"); }

        // ----------------------------------------------

        private void CheckWinCondition()
        {
            _isGameOver = true;
            
            if (hasBatteries && hasFlare && isPlayerInSafeZone)
            {
                TriggerGameOver("Rescue arrived! You survived.");
            }
            else
            {
                TriggerGameOver("Rescue arrived, but you couldn't signal them or weren't at the extraction point. Game Over.");
            }
        }

        private void TriggerGameOver(string message)
        {
            _isGameOver = true;
            Debug.Log("GAME OVER: " + message);
            ShowNotification(message);
        }

        private void ShowNotification(string msg)
        {
            if (notificationText)
            {
                notificationText.text = msg;
                // a simpler way to clear the text would be to use an Invoke or a Coroutine, for now keep it.
            }
        }

        private void UpdateUI()
        {
            if (waterText) waterText.text = "Water: " + Mathf.RoundToInt(currentWater) + "%";
            if (daysLeftText) daysLeftText.text = "Day " + currentDay + " / " + maxDays;
        }
    }
}