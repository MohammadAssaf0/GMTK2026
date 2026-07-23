using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PauseMenu : MonoBehaviour
{
    public static bool paused = false;

    public float minFOV = 45.0f;
    public float maxFOV = 100.0f;
    public float minSensitivity = 1.0f;
    public float maxSensitivity = 20.0f;

    [Header("UI Elements")]
    public Canvas pauseMenuCanvas;
    public Slider fovSlider;
    public Slider sensitivitySlider;
    public Toggle invertYToggle;
    public Button resumeButton;
    public Button resetButton;
    public Button optionsButton;
    public Button quitButton;
    public Button resetDefaultsButton;
    
    private float defaultFOV;
    private float defaultSensitivity;
    private MouseLook camMouseLook;
    private MouseLook capsuleMouseLook;

    void Start()
    {
        // Set initial states and assign default values
        pauseMenuCanvas.enabled = false;  // Hide pause menu initially

        fovSlider.minValue = minFOV;
        fovSlider.maxValue = maxFOV;
        sensitivitySlider.minValue = minSensitivity;
        sensitivitySlider.maxValue = maxSensitivity;

        camMouseLook = Camera.main.GetComponent<MouseLook>();
        capsuleMouseLook = GameObject.FindWithTag("Player").GetComponent<MouseLook>();

        defaultFOV = Camera.main.fieldOfView;
        defaultSensitivity = capsuleMouseLook.sensitivityX;

        LoadPlayerPrefs();

        // Add listeners for buttons and UI elements
        resumeButton.onClick.AddListener(PauseGame);
        resetButton.onClick.AddListener(ResetLevel);
        optionsButton.onClick.AddListener(() => { pauseMenuCanvas.enabled = true; });
        quitButton.onClick.AddListener(QuitGame);
        resetDefaultsButton.onClick.AddListener(ResetToDefault);

        fovSlider.onValueChanged.AddListener(value => SetFOV(value));
        sensitivitySlider.onValueChanged.AddListener(value => SetSensitivity(value));
        invertYToggle.onValueChanged.AddListener(value => InvertY(value));
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            PauseGame();
        }
    }

    private void PauseGame()
    {
        paused = !paused;
        pauseMenuCanvas.enabled = paused;
        Time.timeScale = paused ? 0 : 1;
    }

    private void ResetLevel()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        PauseGame();
    }

    private void QuitGame()
    {
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }

    private void ResetToDefault()
    {
        fovSlider.value = defaultFOV;
        SetFOV(defaultFOV);

        sensitivitySlider.value = defaultSensitivity;
        SetSensitivity(defaultSensitivity);

        invertYToggle.isOn = false;
        InvertY(false);
    }

    private void SetFOV(float value)
    {
        Camera.main.fieldOfView = value;
        PlayerPrefs.SetFloat("FOV", value);
    }

    private void SetSensitivity(float value)
    {
        camMouseLook.SetSensitivity(value);
        capsuleMouseLook.SetSensitivity(value);
        PlayerPrefs.SetFloat("Sensitivity", value);
    }

    private void InvertY(bool isInverted)
    {
        camMouseLook.invertY = isInverted;
        PlayerPrefs.SetInt("InvertY", isInverted ? 1 : 0);
    }

    private void LoadPlayerPrefs()
    {
        fovSlider.value = PlayerPrefs.GetFloat("FOV", defaultFOV);
        sensitivitySlider.value = PlayerPrefs.GetFloat("Sensitivity", defaultSensitivity);
        invertYToggle.isOn = PlayerPrefs.GetInt("InvertY", 0) == 1;
    }
}