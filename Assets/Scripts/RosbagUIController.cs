using UnityEngine;
using UnityEngine.UI;
using TMPro; // If using TextMeshPro, otherwise use regular Text

/// <summary>
/// UI Controller for Rosbag playback
/// Provides buttons and controls for playing, stopping, and configuring rosbag playback
/// </summary>
public class RosbagUIController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Reference to the RosbagController component")]
    public RosbagController rosbagController;

    [Header("UI Elements - Buttons")]
    public Button playButton;
    public Button stopButton;
    public Button restartButton;
    
    [Header("UI Elements - Toggles")]
    public Toggle loopToggle;
    
    [Header("UI Elements - Sliders")]
    public Slider speedSlider;
    public TextMeshProUGUI speedLabel; // Or use: public Text speedLabel;
    
    [Header("UI Elements - Status")]
    public TextMeshProUGUI statusText; // Or use: public Text statusText;
    public Image statusIndicator; // Visual indicator (green = playing, red = stopped)
    
    [Header("Colors")]
    public Color playingColor = Color.green;
    public Color stoppedColor = Color.red;

    void Start()
    {
        // Find RosbagController if not assigned
        if (rosbagController == null)
        {
            rosbagController = FindObjectOfType<RosbagController>();
            if (rosbagController == null)
            {
                Debug.LogError("RosbagController not found! Please assign it in the inspector.");
                return;
            }
        }

        // Set up button listeners
        if (playButton != null)
            playButton.onClick.AddListener(OnPlayButtonClicked);
        
        if (stopButton != null)
            stopButton.onClick.AddListener(OnStopButtonClicked);
        
        if (restartButton != null)
            restartButton.onClick.AddListener(OnRestartButtonClicked);

        // Set up toggle listener
        if (loopToggle != null)
        {
            loopToggle.isOn = rosbagController.loopPlayback;
            loopToggle.onValueChanged.AddListener(OnLoopToggleChanged);
        }

        // Set up slider listener
        if (speedSlider != null)
        {
            speedSlider.minValue = 0.1f;
            speedSlider.maxValue = 3.0f;
            speedSlider.value = rosbagController.playbackRate;
            speedSlider.onValueChanged.AddListener(OnSpeedSliderChanged);
            UpdateSpeedLabel(speedSlider.value);
        }

        // Initial UI update
        UpdateUI();
    }

    void Update()
    {
        // Update UI every frame to reflect current state
        UpdateUI();
    }

    /// <summary>
    /// Play button clicked
    /// </summary>
    void OnPlayButtonClicked()
    {
        Debug.Log("Play button clicked");
        rosbagController.StartPlayback();
    }

    /// <summary>
    /// Stop button clicked
    /// </summary>
    void OnStopButtonClicked()
    {
        Debug.Log("Stop button clicked");
        rosbagController.StopPlayback();
    }

    /// <summary>
    /// Restart button clicked
    /// </summary>
    void OnRestartButtonClicked()
    {
        Debug.Log("Restart button clicked");
        rosbagController.RestartPlayback();
    }

    /// <summary>
    /// Loop toggle changed
    /// </summary>
    void OnLoopToggleChanged(bool value)
    {
        rosbagController.loopPlayback = value;
        Debug.Log($"Loop playback: {value}");
        
        // If currently playing, need to restart for change to take effect
        if (rosbagController.isPlaying)
        {
            Debug.Log("Restarting playback to apply loop setting...");
            rosbagController.RestartPlayback();
        }
    }

    /// <summary>
    /// Speed slider changed
    /// </summary>
    void OnSpeedSliderChanged(float value)
    {
        rosbagController.playbackRate = value;
        UpdateSpeedLabel(value);
        
        // If currently playing, need to restart for change to take effect
        if (rosbagController.isPlaying)
        {
            Debug.Log("Restarting playback to apply speed setting...");
            rosbagController.RestartPlayback();
        }
    }

    /// <summary>
    /// Updates the speed label text
    /// </summary>
    void UpdateSpeedLabel(float speed)
    {
        if (speedLabel != null)
        {
            speedLabel.text = $"Speed: {speed:F1}x";
        }
    }

    /// <summary>
    /// Updates all UI elements based on current state
    /// </summary>
    void UpdateUI()
    {
        bool isPlaying = rosbagController.isPlaying;

        // Update button interactability
        if (playButton != null)
            playButton.interactable = !isPlaying;
        
        if (stopButton != null)
            stopButton.interactable = isPlaying;
        
        if (restartButton != null)
            restartButton.interactable = isPlaying;

        // Update status text
        if (statusText != null)
        {
            if (isPlaying)
            {
                string loopStatus = rosbagController.loopPlayback ? "Looping" : "Once";
                statusText.text = $"Status: Playing ({loopStatus}, {rosbagController.playbackRate:F1}x)\nPID: {rosbagController.processId}";
            }
            else
            {
                statusText.text = "Status: Stopped";
                
                // Show last error if any
                string lastError = rosbagController.GetLastError();
                if (!string.IsNullOrEmpty(lastError) && !lastError.Contains("[INFO]"))
                {
                    statusText.text += $"\n<color=red>Last: {lastError}</color>";
                }
            }
        }

        // Update status indicator color
        if (statusIndicator != null)
        {
            statusIndicator.color = isPlaying ? playingColor : stoppedColor;
        }
    }

    /// <summary>
    /// Validate the rosbag path (useful for debugging)
    /// Can be called from a button
    /// </summary>
    public void ValidatePath()
    {
        bool valid = rosbagController.ValidateRosbagPath();
        
        if (statusText != null)
        {
            if (valid)
            {
                statusText.text = "✓ Rosbag path is valid";
            }
            else
            {
                statusText.text = "✗ Rosbag path is invalid";
            }
        }
    }
}
