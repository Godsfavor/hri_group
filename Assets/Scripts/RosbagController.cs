using UnityEngine;
using System.Diagnostics;
using System.IO;
using System;

/// <summary>
/// Controls ROS bag playback from Unity
/// Allows starting, stopping, and looping rosbag playback via UI buttons
/// </summary>
public class RosbagController : MonoBehaviour
{
    [Header("Rosbag Settings")]
    [Tooltip("Full path to your rosbag directory")]
    public string rosbagPath = "/home/usuario/Downloads/ui_localization_bags/";
    
    [Tooltip("Playback speed multiplier (1.0 = normal speed, 0.5 = half speed, 2.0 = double speed)")]
    public float playbackRate = 1.0f;
    
    [Tooltip("Loop the rosbag playback continuously")]
    public bool loopPlayback = true;
    
    [Tooltip("Start playback automatically when Unity starts")]
    public bool autoPlay = false;

    [Header("ROS Environment")]
    [Tooltip("Path to ROS setup.bash file")]
    public string rosSetupPath = "/opt/ros/humble/setup.bash";
    
    [Tooltip("Path to your workspace setup.bash (if using custom workspace)")]
    public string workspaceSetupPath = "";

    [Header("Status")]
    [Tooltip("Is rosbag currently playing?")]
    public bool isPlaying = false;
    
    [Tooltip("Current playback process ID")]
    public int processId = -1;

    // Private variables
    private Process rosbagProcess;
    private string lastError = "";

    void Start()
    {
        // Validate rosbag path
        if (!Directory.Exists(rosbagPath))
        {
            UnityEngine.Debug.LogError($"Rosbag directory not found: {rosbagPath}");
            return;
        }

        if (autoPlay)
        {
            StartPlayback();
        }
    }

    /// <summary>
    /// Starts rosbag playback
    /// </summary>
    public void StartPlayback()
    {
        if (isPlaying)
        {
            UnityEngine.Debug.LogWarning("Rosbag is already playing!");
            return;
        }

        try
        {
            // Build the ros2 bag play command
            string arguments = BuildPlaybackCommand();
            
            // Create process start info
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = $"-c \"{arguments}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            // Start the process
            rosbagProcess = new Process { StartInfo = startInfo };
            
            // Set up event handlers for output
            rosbagProcess.OutputDataReceived += OnOutputReceived;
            rosbagProcess.ErrorDataReceived += OnErrorReceived;
            
            rosbagProcess.Start();
            rosbagProcess.BeginOutputReadLine();
            rosbagProcess.BeginErrorReadLine();

            processId = rosbagProcess.Id;
            isPlaying = true;

            UnityEngine.Debug.Log($"Started rosbag playback (PID: {processId})");
            UnityEngine.Debug.Log($"Command: {arguments}");
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError($"Failed to start rosbag playback: {ex.Message}");
            lastError = ex.Message;
        }
    }

    /// <summary>
    /// Stops rosbag playback
    /// </summary>
    public void StopPlayback()
    {
        if (!isPlaying || rosbagProcess == null)
        {
            UnityEngine.Debug.LogWarning("Rosbag is not playing!");
            return;
        }

        try
        {
            // Kill the process
            if (!rosbagProcess.HasExited)
            {
                rosbagProcess.Kill();
                rosbagProcess.WaitForExit(2000); // Wait up to 2 seconds
            }

            rosbagProcess.Dispose();
            rosbagProcess = null;
            isPlaying = false;
            processId = -1;

            UnityEngine.Debug.Log("Stopped rosbag playback");
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError($"Failed to stop rosbag playback: {ex.Message}");
            lastError = ex.Message;
        }
    }

    /// <summary>
    /// Toggles playback on/off
    /// </summary>
    public void TogglePlayback()
    {
        if (isPlaying)
        {
            StopPlayback();
        }
        else
        {
            StartPlayback();
        }
    }

    /// <summary>
    /// Restarts playback (stop then start)
    /// </summary>
    public void RestartPlayback()
    {
        StopPlayback();
        // Small delay to ensure cleanup
        Invoke("StartPlayback", 0.5f);
    }

    /// <summary>
    /// Builds the complete ros2 bag play command
    /// </summary>
    private string BuildPlaybackCommand()
    {
        string command = "";

        // Source ROS setup
        if (!string.IsNullOrEmpty(rosSetupPath) && File.Exists(rosSetupPath))
        {
            command += $"source {rosSetupPath}; ";
        }

        // Source workspace setup if provided
        if (!string.IsNullOrEmpty(workspaceSetupPath) && File.Exists(workspaceSetupPath))
        {
            command += $"source {workspaceSetupPath}; ";
        }

        // Change to rosbag directory
        command += $"cd {rosbagPath}; ";

        // Build ros2 bag play command
        command += "ros2 bag play .";

        // Add playback rate
        if (playbackRate != 1.0f)
        {
            command += $" --rate {playbackRate}";
        }

        // Add loop option
        if (loopPlayback)
        {
            command += " --loop";
        }

        return command;
    }

    /// <summary>
    /// Handles output from the rosbag process
    /// </summary>
    private void OnOutputReceived(object sender, DataReceivedEventArgs e)
    {
        if (!string.IsNullOrEmpty(e.Data))
        {
            UnityEngine.Debug.Log($"[Rosbag] {e.Data}");
        }
    }

    /// <summary>
    /// Handles errors from the rosbag process
    /// </summary>
    private void OnErrorReceived(object sender, DataReceivedEventArgs e)
    {
        if (!string.IsNullOrEmpty(e.Data))
        {
            // Some "errors" are actually just info messages from ros2
            if (e.Data.Contains("[INFO]"))
            {
                UnityEngine.Debug.Log($"[Rosbag] {e.Data}");
            }
            else
            {
                UnityEngine.Debug.LogWarning($"[Rosbag] {e.Data}");
            }
            lastError = e.Data;
        }
    }

    /// <summary>
    /// Get the last error message
    /// </summary>
    public string GetLastError()
    {
        return lastError;
    }

    /// <summary>
    /// Clean up when Unity stops
    /// </summary>
    void OnApplicationQuit()
    {
        if (isPlaying)
        {
            StopPlayback();
        }
    }

    void OnDestroy()
    {
        if (isPlaying)
        {
            StopPlayback();
        }
    }

    /// <summary>
    /// For debugging - check if rosbag path exists
    /// </summary>
    public bool ValidateRosbagPath()
    {
        bool exists = Directory.Exists(rosbagPath);
        
        if (exists)
        {
            // Check for .db3 file
            string[] db3Files = Directory.GetFiles(rosbagPath, "*.db3");
            if (db3Files.Length > 0)
            {
                UnityEngine.Debug.Log($"✓ Found {db3Files.Length} rosbag database file(s)");
                return true;
            }
            else
            {
                UnityEngine.Debug.LogWarning($"⚠ Directory exists but no .db3 files found in {rosbagPath}");
                return false;
            }
        }
        else
        {
            UnityEngine.Debug.LogError($"✗ Rosbag directory not found: {rosbagPath}");
            return false;
        }
    }
}
