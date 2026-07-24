namespace Survival
{
   using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using System.Collections.Generic;

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
    
    public SmartwatchManager smartwatch; 

    private bool _isDiaryOpen;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        if (diaryModel != null) diaryModel.SetActive(false);
    }

    private void Update()
    {
        if (Keyboard.current.tabKey.wasPressedThisFrame)
        {
            ToggleDiary();
        }

        if (_isDiaryOpen)
        {
            if (Keyboard.current.qKey.wasPressedThisFrame) FlipPage(-1);
            if (Keyboard.current.eKey.wasPressedThisFrame) FlipPage(1);
            
            if (_currentPageIndex == 0) UpdateNavigationPage();
        }
    }

    public void ToggleDiary()
    {
        _isDiaryOpen = !_isDiaryOpen;
        diaryModel.SetActive(_isDiaryOpen);

        if (_isDiaryOpen)
        {
            UpdateResourcePage();
            ShowPage(_currentPageIndex);
            
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
        _currentPageIndex += direction;
        
        if (_currentPageIndex < 0) _currentPageIndex = pages.Length - 1;
        if (_currentPageIndex >= pages.Length) _currentPageIndex = 0;

        ShowPage(_currentPageIndex);
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
    }
}
}