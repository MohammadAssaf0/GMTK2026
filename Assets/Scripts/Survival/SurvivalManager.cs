using TMPro;
using UnityEngine;

namespace Survival
{
    public class SurvivalManager : MonoBehaviour
    {
        public static SurvivalManager Instance;

        [Header("References")]
        public DayNightCycle dayNightClock;
        
        [Header("Water Supply")]
        public int waterBottles = 5;
        public int waterCostPerHour = 1;
        public int safeSleepWaterCost = 2;   
        public int desertSleepPenalty = 4;
        
        [Header("Time & Countdown")]
        public int currentDay = 1;
        public int maxDays = 3;
        public float sunsetHour = 19f;  // 7:00 PM
        public float sunriseHour = 6f;  // 6:00 AM
        private int _lastTrackedHour = -1;

        [Header("Inventory & Status")]
        public bool hasBatteries;
        public bool hasFlare;
        public bool isPlayerInSafeZone;

        [Header("UI References")]
        public TextMeshProUGUI waterText;
        public TextMeshProUGUI daysLeftText;
        public TextMeshProUGUI notificationText;

        [HideInInspector] public string diaryLog;
        
        private bool _isGameOver;

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            _lastTrackedHour = Mathf.FloorToInt(dayNightClock.timeOfDay);
            LogEvent(""); // update diary, game start
        }

        private void Update()
        {
            if (_isGameOver) return;

            WatchTheClock();
        }

        private void HourlyWaterDrain()
        {
            if (waterBottles >= waterCostPerHour)
            {
                waterBottles -= waterCostPerHour;
                // Debug.Log($"An hour passed. Drank water. Bottles left: {waterBottles}");
            }
            else if (waterBottles > 0)
            {
                waterBottles = 0;
                LogEvent(""); // update diary, ran out of water
            }
            else
            {
                TriggerGameOver("");
            }
        }

        private void WatchTheClock()
        {
            var currentHour = Mathf.FloorToInt(dayNightClock.timeOfDay);
            
            if (currentHour != _lastTrackedHour)
            {
                if (currentHour >= sunriseHour && currentHour < sunsetHour)
                {
                    HourlyWaterDrain();
                }
                _lastTrackedHour = currentHour;
            }
            
            if (dayNightClock.timeOfDay >= sunsetHour)
            {
                SleepThroughNight();
            }
        }

        private void SleepThroughNight()
        {
            // add night transition here
            
            currentDay++;
            
            dayNightClock.timeOfDay = sunriseHour;
            dayNightClock.ApplyTime();
            _lastTrackedHour = Mathf.FloorToInt(sunriseHour);
            
            LogEvent(""); // update diary, day over

            if (isPlayerInSafeZone)
            {
                DrainWaterForSleep(safeSleepWaterCost);
                LogEvent(""); 
            }
            else
            {
                DrainWaterForSleep(safeSleepWaterCost + desertSleepPenalty);
                LogEvent(""); 
            }
            
            if (CurrentWaterCheckDead()) return;

            if (currentDay > maxDays)
            {
                CheckWinCondition();
            }
        }
        
        private void DrainWaterForSleep(int amount)
        {
            waterBottles -= amount;
            if (waterBottles < 0) waterBottles = 0;
        }

        private bool CurrentWaterCheckDead()
        {
            if (waterBottles <= 0)
            {
                LogEvent(""); // update diary, woke up with no water
            }
            return false;
        }

        // Interactions

        public void GotWater(int amount)
        {
            waterBottles += amount;
            LogEvent(""); // update diary, collected water
        }

        public void PickupBatteries() { hasBatteries = true; LogEvent(""); } // update diary, got batteries
        public void PickupFlare() { hasFlare = true; LogEvent(""); } // update diary, got flare

        // ----------------------------------------------
        
        public void LogEvent(string message)
        {
            diaryLog += "- " + message + "\n";
            // Debug.Log("DIARY: " + message);
        }

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
            // Debug.Log("GAME OVER: " + message);
            LogEvent(""); // update diary, lost
        }
    }
}