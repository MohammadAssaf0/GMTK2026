using System.Collections;
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
        
        [Header("Transition Settings")]
        public CanvasGroup fadeScreen;
        public float fadeDuration = 1.5f;
        private bool _isSleeping = false;
        
        [Header("Endgame UI")]
        public TextMeshProUGUI blackScreenText;
        public GameObject restartButton;
        
        private bool _isGameOver;

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            _lastTrackedHour = Mathf.FloorToInt(dayNightClock.timeOfDay);
            DiaryManager.Instance.LogEvent("I survived the plane crash, good thing I was alone flaying it." +
                                           "The rescue on the radio told me they will arrive in 3 days." +
                                           "until then I need to survive this heat. but I'll need a replacement " +
                                           "for the batteries and some kind of fire or flare to signal my location");
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
                DiaryManager.Instance.LogEvent("I dont have any water lief. I need to find more ASAP");
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
            
            fadeScreen.alpha = 1f;
            
            currentDay++;
            dayNightClock.timeOfDay = sunriseHour;
            dayNightClock.ApplyTime(); 
            _lastTrackedHour = Mathf.FloorToInt(sunriseHour);
            blackScreenText.text = "Day " + currentDay;

            DiaryManager.Instance.StartNewDay(currentDay);

            if (isPlayerInSafeZone)
            {
                DrainWaterForSleep(safeSleepWaterCost);
                DiaryManager.Instance.LogEvent("I slept not comfortably but safely in a shelter. a day closer to the rescue");
            }
            else
            {
                DrainWaterForSleep(safeSleepWaterCost + desertSleepPenalty);
                DiaryManager.Instance.LogEvent("I slept out in the harsh desert. Apparently a rat chewed into some" +
                                               " of the water bottles and I lost them.");
            }

            var dead = CurrentWaterCheckDead();
            
            if (currentDay > maxDays)
            {
                EvaluateEndgame();
                yield break;
            }
            
            yield return new WaitForSeconds(fadeDuration);
            
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
                DiaryManager.Instance.LogEvent("I woke up thirsty, I have no water left." +
                                               " If I don't find any soon I might not live to get rescued");
            }
            return false;
        }

        // Interactions

        public void GotWater(int amount)
        {
            waterBottles += amount;
            DiaryManager.Instance.LogEvent("I found some more water, always good.");
        }

        public void PickupBatteries() { hasBatteries = true; DiaryManager.Instance.LogEvent("finally found some batteries"); }
        public void PickupFlare() { hasFlare = true; DiaryManager.Instance.LogEvent("got a flare that I needed"); }

        // ----------------------------------------------

        private void EvaluateEndgame()
        {
            _isGameOver = true;

            if (!isPlayerInSafeZone)
            {
                ShowEndgameScreen("Day 4 arrived, but you slept out in the open desert," +
                                  " a scorpion stung you in your final hours. bummer!");
            }
            else if (!hasFlare)
            {
                var text = "The rescue plane flew directly overhead, but you had no flare to signal them. They kept flying.";
                if (!hasBatteries) text += " And your batteries didn't last long enough to communicate your location properly...";
                ShowEndgameScreen(text);
            }
            else if (!hasBatteries)
            {
                ShowEndgameScreen("You had the flare, but the igniter was dead. Without batteries " +
                                  "you couldn't communicate your location. They thought you were dead.");
            }
            else
            {
                ShowEndgameScreen("You told them your approximate location. You fired the flare" +
                                  " into the morning sky. The rescue plane banked and landed. You survived the " +
                                  "most tragic 3 days of your life.");
            }
        }

        private void TriggerGameOver(string message)
        {
            _isGameOver = true;
            
            if (fadeScreen) fadeScreen.alpha = 1f;

            ShowEndgameScreen(message);
        }
        
        private void ShowEndgameScreen(string finalMessage)
        {
            if (blackScreenText)
            {
                blackScreenText.text = finalMessage;
                blackScreenText.gameObject.SetActive(true);
            }
        
            if (restartButton) restartButton.SetActive(true);

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        
            Time.timeScale = 0f;
        }

        public void RestartGame()
        {
            Time.timeScale = 1f;
            UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
        }
    }
}