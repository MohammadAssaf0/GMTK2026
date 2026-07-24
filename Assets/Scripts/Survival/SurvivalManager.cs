using System.Collections;
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
        
        [Header("Transition Settings")]
        public CanvasGroup fadeScreen;
        public float fadeDuration = 1.5f;
        private bool _isSleeping = false;
        
        private bool _isGameOver;

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            _lastTrackedHour = Mathf.FloorToInt(dayNightClock.timeOfDay);
            DiaryManager.Instance.LogEvent(""); // update diary, game start
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
                DiaryManager.Instance.LogEvent(""); // update diary, ran out of water
            }
            else
            {
                TriggerGameOver("");
            }
        }
        
        public void SleepThroughNight()
        {
            if (_isSleeping) return; 
            StartCoroutine(SleepRoutine());
        }

        private IEnumerator SleepRoutine()
        {
            _isSleeping = true;

            var timer = 0f;
            while (timer < fadeDuration)
            {
                timer += Time.deltaTime;
                fadeScreen.alpha = Mathf.Lerp(0f, 1f, timer / fadeDuration);
                yield return null;
            }
            
            currentDay++;
            dayNightClock.timeOfDay = sunriseHour;
            dayNightClock.ApplyTime(); 
            _lastTrackedHour = Mathf.FloorToInt(sunriseHour); 

            DiaryManager.Instance.StartNewDay(currentDay);

            if (isPlayerInSafeZone)
            {
                DrainWaterForSleep(safeSleepWaterCost);
                DiaryManager.Instance.LogEvent("Slept safely in a shelter.");
            }
            else
            {
                DrainWaterForSleep(safeSleepWaterCost + desertSleepPenalty);
                DiaryManager.Instance.LogEvent("Slept out in the harsh desert. Woke up severely dehydrated.");
            }

            var dead = CurrentWaterCheckDead();
            if (!dead && currentDay > maxDays)
            {
                CheckWinCondition();
            }
            
            timer = 0f;
            while (timer < fadeDuration)
            {
                timer += Time.deltaTime;
                fadeScreen.alpha = Mathf.Lerp(1f, 0f, timer / fadeDuration);
                yield return null;
            }

            _isSleeping = false;
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
        
        private void DrainWaterForSleep(int amount)
        {
            waterBottles -= amount;
            if (waterBottles < 0) waterBottles = 0;
        }

        private bool CurrentWaterCheckDead()
        {
            if (waterBottles <= 0)
            {
                DiaryManager.Instance.LogEvent(""); // update diary, woke up with no water
            }
            return false;
        }

        // Interactions

        public void GotWater(int amount)
        {
            waterBottles += amount;
            DiaryManager.Instance.LogEvent(""); // update diary, collected water
        }

        public void PickupBatteries() { hasBatteries = true; DiaryManager.Instance.LogEvent(""); } // update diary, got batteries
        public void PickupFlare() { hasFlare = true; DiaryManager.Instance.LogEvent(""); } // update diary, got flare

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
            DiaryManager.Instance.LogEvent(""); // update diary, lost
        }
    }
}