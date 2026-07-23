using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class SmartwatchManager : MonoBehaviour
{
    [Header("UI References")]
    public GameObject watchUIPanel;
    public TextMeshProUGUI distanceText;
    public TextMeshProUGUI stepCountText;

    [Header("Tracking")]
    public Transform crashSite;
    
    private int _stepCount;
    private bool _isUIActive;
    private DrifterFootsteps _footsteps;
    
    void Awake()
    {
        _footsteps = GetComponent<DrifterFootsteps>();
    }
    
    void OnEnable()
    {
        if (_footsteps != null) _footsteps.StepTaken += AddStep;
    }

    void OnDisable()
    {
        if (_footsteps != null) _footsteps.StepTaken -= AddStep;
    }

    private void Start()
    {
        if (watchUIPanel != null) 
        {
            watchUIPanel.SetActive(false);
        }
        
        UpdateStepUI();
    }

    private void Update()
    {
        if (Keyboard.current.tabKey.wasPressedThisFrame)
        {
            _isUIActive = !_isUIActive;
            if (watchUIPanel) 
            {
                watchUIPanel.SetActive(_isUIActive);
            }
        }

        if (Keyboard.current.rKey.wasPressedThisFrame)
        {
            ResetSteps();
        }

        if (!_isUIActive || !crashSite) return;
        var distance = Vector3.Distance(transform.position, crashSite.position);
            
        distanceText.text = "Dist to Crash: " + Mathf.RoundToInt(distance) + "m";
    }

    private void AddStep()
    {
        _stepCount++;
        UpdateStepUI();
    }
    
    private void ResetSteps()
    {
        _stepCount = 0;
        UpdateStepUI();
    }

    private void UpdateStepUI()
    {
        if (stepCountText)
        {
            stepCountText.text = "Steps: " + _stepCount;
        }
    }
}