using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class SmartwatchManager : MonoBehaviour
{
    [Header("Tracking")]
    public Transform crashSite;
    
    private int _stepCount;
    private bool _isUIActive;
    private DrifterFootsteps _footsteps;
    
    private void Awake()
    {
        _footsteps = GetComponent<DrifterFootsteps>();
    }
    
    private void OnEnable()
    {
        if (_footsteps != null) _footsteps.StepTaken += AddStep;
    }

    private void OnDisable()
    {
        if (_footsteps != null) _footsteps.StepTaken -= AddStep;
    }

    private void AddStep()
    {
        _stepCount++;
    }
    
    public void ResetSteps()
    {
        _stepCount = 0;
    }
    
    public int GetStepCount()
    {
        return _stepCount;
    }
}