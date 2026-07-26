using UnityEngine.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using System.Collections;
using System.Collections.Generic;

namespace Survival
{

    public class DiaryManager : MonoBehaviour
    {
        public static DiaryManager Instance;

        [Header("UI Panels")]
        public GameObject diaryModel;
        public GameObject[] pages;
        private int _currentPageIndex = 0;

        [Header("Log Page")]
        public TextMeshProUGUI logTextDisplay;
        private List<string> _currentDayLogs = new();

        [Header("Resource Page")]
        public TextMeshProUGUI resourceTextDisplay;

        [Header("Navigation Page (Map/Watch)")]
        public TextMeshProUGUI distanceTextDisplay;
        public TextMeshProUGUI stepsTextDisplay;

        [Header("Map Page")]
        public Image mapImageDisplay;
        public GameObject[] mapOverlays;
        private int currentMapPhase = 0;

        public SmartwatchManager smartwatch;

        // ---------------------------------------------------------------------
        // Page-flip animation (optional). If diaryAnimator is left empty the
        // diary behaves exactly as before (instant page swap). When assigned,
        // flipping plays the CINEMA_4D_Main clip, swaps the visible spread at
        // the mid-point of the turn, and (optionally) shows text ON the turning
        // page so the writing flips together with the paper.
        // ---------------------------------------------------------------------
        [Header("Flip Animation (optional)")]
        [Tooltip("Animator on the book model. Its controller needs a state (named below) that holds the flip clip.")]
        public Animator diaryAnimator;
        [Tooltip("Animator state for flipping FORWARD (E / next page). Plays the clip start->end.")]
        public string flipStateName = "Flip";
        [Tooltip("Animator state for flipping BACKWARD (Q / previous page). Same clip, Speed = -1, played end->start.")]
        public string flipBackStateName = "FlipBack";
        [Tooltip("Length of the flip clip in seconds (CINEMA_4D_Main = 3s).")]
        public float flipDuration = 0.5f;
        [Range(0f, 1f)]
        [Tooltip("At what fraction of the flip the visible spread is swapped (0.5 = when the page is edge-on).")]
        public float contentSwapAt = 0.5f;
        [Tooltip("TMP on the FRONT of the turning page (parent it under the flip bone). Shows the outgoing page.")]
        public TextMeshProUGUI flipFrontText;
        [Tooltip("TMP on the BACK of the turning page. Shows the incoming page.")]
        public TextMeshProUGUI flipBackText;

        private bool _isFlipping;

        public static event System.Action<bool> DiaryStateChanged;

        private bool _isDiaryOpen;

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            if (diaryModel != null) diaryModel.SetActive(false);

            // We drive the flip pose ourselves (see FlipRoutine), so stop the
            // Animator from auto-advancing the clip.
            if (diaryAnimator != null) diaryAnimator.speed = 0f;

            foreach (var overlay in mapOverlays)
            {
                if (overlay != null) overlay.SetActive(false);
            }
        }

        private void Update()
        {
            if (Keyboard.current.tabKey.wasPressedThisFrame)
            {
                ToggleDiary();
            }

            if (_isDiaryOpen)
            {
                if (Keyboard.current.eKey.wasPressedThisFrame)
                {
                    if (_currentPageIndex == 0 && logTextDisplay && logTextDisplay.pageToDisplay < logTextDisplay.textInfo.pageCount)
                    {
                        logTextDisplay.pageToDisplay++;
                    }
                    else
                    {
                        FlipPage(1);
                    }
                }

                if (Keyboard.current.qKey.wasPressedThisFrame)
                {
                    if (_currentPageIndex == 0 && logTextDisplay && logTextDisplay.pageToDisplay > 1)
                    {
                        logTextDisplay.pageToDisplay--;
                    }
                    else
                    {
                        FlipPage(-1);
                    }
                }
                
                if (_currentPageIndex == 1) UpdateNavigationPage(); 
            }
        }

        public void ToggleDiary()
        {
            _isDiaryOpen = !_isDiaryOpen;
            diaryModel.SetActive(_isDiaryOpen);

            DiaryStateChanged?.Invoke(_isDiaryOpen);

            if (_isDiaryOpen)
            {
                UpdateResourcePage();
                ShowPage(_currentPageIndex);

                // Rest the book on a flat, settled pose (we control the flip manually).
                if (diaryAnimator != null)
                {
                    diaryAnimator.speed = 0f;
                    diaryAnimator.Play(flipStateName, 0, 1f);
                }

                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        // --- Page Navigation Logic ---

        private void FlipPage(int direction)
        {
            if (_isFlipping) return;

            var target = _currentPageIndex + direction;
            if (target < 0 || target >= pages.Length) return;

            // No animator wired -> keep the original instant behaviour.
            if (diaryAnimator == null || flipDuration <= 0f)
            {
                _currentPageIndex = target;
                ShowPage(_currentPageIndex);
                return;
            }

            StartCoroutine(FlipRoutine(target, direction));
        }

        private IEnumerator FlipRoutine(int target, int direction)
        {
            _isFlipping = true;

            // Put the outgoing spread on the front of the turning page and the
            // incoming spread on its back, so the text rides the flip.
            if (flipFrontText) flipFrontText.text = GetPageText(_currentPageIndex);
            if (flipBackText)  flipBackText.text  = GetPageText(target);

            SetAnimFloat("FlipDir", direction);

            // Manually scrub the SAME flip clip: forward 0->1, backward 1->0.
            // Both directions therefore play a real turn (backward = the mirror-
            // looking turn from the other side), with no reliance on state speed.
            diaryAnimator.speed = 0f;
            float swapAt = Mathf.Clamp01(contentSwapAt);
            float elapsed = 0f;
            bool swapped = false;

            while (elapsed < flipDuration)
            {
                elapsed += Time.deltaTime;
                float f = Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, flipDuration));
                float nt = direction >= 0 ? f : 1f - f;
                diaryAnimator.Play(flipStateName, 0, nt);

                // Swap the visible spread while the page is edge-on (hidden).
                if (!swapped && f >= swapAt)
                {
                    swapped = true;
                    _currentPageIndex = target;
                    ShowPage(_currentPageIndex);
                }
                yield return null;
            }

            // Settle flat on the end/start pose.
            diaryAnimator.Play(flipStateName, 0, direction >= 0 ? 1f : 0f);
            _isFlipping = false;
        }

        // Best-effort text for a logical page: grabs the first TMP under the
        // page panel. Customize this if a page's "written" text lives elsewhere.
        private string GetPageText(int index)
        {
            if (pages == null || index < 0 || index >= pages.Length || pages[index] == null)
                return string.Empty;
            var tmp = pages[index].GetComponentInChildren<TextMeshProUGUI>(true);
            return tmp ? tmp.text : string.Empty;
        }

        private void SetAnimFloat(string param, float value)
        {
            if (diaryAnimator == null) return;
            foreach (var p in diaryAnimator.parameters)
                if (p.type == AnimatorControllerParameterType.Float && p.name == param)
                {
                    diaryAnimator.SetFloat(param, value);
                    return;
                }
        }

        private void ShowPage(int index)
        {
            for (var i = 0; i < pages.Length; i++)
            {
                pages[i].SetActive(i == index);
            }
        }

        // --- Data Updaters ---

        private void UpdateResourcePage()
        {
            if (!resourceTextDisplay) return;

            var res = "--- SUPPLIES ---\n\n";
            res += "Water Bottles: " + SurvivalManager.Instance.waterBottles + "\n\n";
            res += "Batteries: " + (SurvivalManager.Instance.hasBatteries ? "Found" : "Missing") + "\n";
            res += "Signal Flare: " + (SurvivalManager.Instance.hasFlare ? "Found" : "Missing") + "\n";

            resourceTextDisplay.text = res;
        }

        private void UpdateNavigationPage()
        {
            if (!smartwatch || !distanceTextDisplay || !stepsTextDisplay) return;

            var distance = Vector3.Distance(smartwatch.transform.position, smartwatch.crashSite.position);
            distanceTextDisplay.text = "Distance to Crash: " + Mathf.RoundToInt(distance) + "m";
            stepsTextDisplay.text = "Steps Taken: " + smartwatch.GetStepCount();
        }

        public void UnlockMapOverlay(int mapIndex)
        {
            if (mapIndex < 0 || mapIndex >= mapOverlays.Length) return;
            if (mapOverlays[mapIndex].activeSelf) return;

            mapOverlays[mapIndex].SetActive(true);
            LogEvent("Found a new map piece. I sketched the path into my diary.");
        }

        // --- Log Logic ---

        public void LogEvent(string message)
        {
            _currentDayLogs.Add("- " + message);
            RefreshLogDisplay();
        }

        public void StartNewDay(int dayNumber)
        {
            _currentDayLogs.Clear();
            LogEvent($"--- Day {dayNumber} ---");
        }

        private void RefreshLogDisplay()
        {
            if (!logTextDisplay) return;

            logTextDisplay.text = string.Join("\n\n", _currentDayLogs);
            logTextDisplay.ForceMeshUpdate();
            logTextDisplay.pageToDisplay = 1;
        }
    }
}
