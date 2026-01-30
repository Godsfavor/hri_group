using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Tf2;
using System.Collections.Generic;

/// <summary>
/// Helper script to detect available TF frames
/// Use this to find the correct frame ID for your BlueROV2
/// </summary>
public class TFFrameDetector : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("TF topic to listen to")]
    public string tfTopic = "/tf";
    
    [Tooltip("How long to listen for frames (seconds)")]
    public float detectionDuration = 5f;
    
    [Tooltip("Auto-detect on start")]
    public bool autoDetectOnStart = true;

    [Header("Status")]
    [Tooltip("Is detection running?")]
    public bool isDetecting = false;
    
    [Tooltip("List of detected frames")]
    public List<string> detectedChildFrames = new List<string>();
    
    [Tooltip("List of detected parent frames")]
    public List<string> detectedParentFrames = new List<string>();

    private float detectionStartTime;
    private HashSet<string> childFrameSet = new HashSet<string>();
    private HashSet<string> parentFrameSet = new HashSet<string>();
    private Dictionary<string, string> frameRelationships = new Dictionary<string, string>();

    void Start()
    {
        if (autoDetectOnStart)
        {
            StartDetection();
        }
    }

    /// <summary>
    /// Start listening for TF frames
    /// </summary>
    public void StartDetection()
    {
        if (isDetecting)
        {
            Debug.LogWarning("[TF Detector] Detection already running");
            return;
        }

        // Clear previous results
        childFrameSet.Clear();
        parentFrameSet.Clear();
        frameRelationships.Clear();
        detectedChildFrames.Clear();
        detectedParentFrames.Clear();

        // Subscribe to TF
        ROSConnection.GetOrCreateInstance().Subscribe<TFMessageMsg>(tfTopic, OnTFMessageReceived);
        
        isDetecting = true;
        detectionStartTime = Time.time;
        
        Debug.Log($"[TF Detector] Started detection on {tfTopic}");
        Debug.Log($"[TF Detector] Will collect frames for {detectionDuration} seconds...");
    }

    /// <summary>
    /// Stop detection and display results
    /// </summary>
    public void StopDetection()
    {
        if (!isDetecting)
            return;

        isDetecting = false;
        
        // Convert sets to lists for inspector display
        detectedChildFrames = new List<string>(childFrameSet);
        detectedParentFrames = new List<string>(parentFrameSet);

        // Display results
        Debug.Log("========================================");
        Debug.Log("[TF Detector] Detection Complete!");
        Debug.Log($"Found {detectedChildFrames.Count} child frames:");
        
        foreach (var frame in detectedChildFrames)
        {
            string parent = frameRelationships.ContainsKey(frame) ? frameRelationships[frame] : "unknown";
            Debug.Log($"  - {frame} (parent: {parent})");
        }
        
        Debug.Log($"\nFound {detectedParentFrames.Count} parent frames:");
        foreach (var frame in detectedParentFrames)
        {
            Debug.Log($"  - {frame}");
        }
        
        Debug.Log("\n[TF Detector] Recommendations:");
        
        // Try to identify likely robot base frame
        List<string> likelyRobotFrames = new List<string>();
        foreach (var frame in detectedChildFrames)
        {
            if (frame.Contains("base") || 
                frame.Contains("robot") || 
                frame.Contains("bluerov"))
            {
                likelyRobotFrames.Add(frame);
            }
        }
        
        if (likelyRobotFrames.Count > 0)
        {
            Debug.Log("Likely robot frames:");
            foreach (var frame in likelyRobotFrames)
            {
                Debug.Log($"  ✓ {frame}");
            }
            Debug.Log($"\nSuggestion: Use '{likelyRobotFrames[0]}' as Robot Frame ID");
        }
        else
        {
            Debug.Log("⚠ Could not auto-identify robot frame");
            Debug.Log("Check the list above and look for frames containing 'base', 'robot', or 'bluerov'");
        }
        
        // Identify likely reference frames
        List<string> likelyReferenceFrames = new List<string>();
        foreach (var frame in detectedParentFrames)
        {
            if (frame.Contains("odom") || 
                frame.Contains("map") || 
                frame.Contains("world"))
            {
                likelyReferenceFrames.Add(frame);
            }
        }
        
        if (likelyReferenceFrames.Count > 0)
        {
            Debug.Log($"\nSuggestion: Use '{likelyReferenceFrames[0]}' as Reference Frame ID");
        }
        
        Debug.Log("========================================");
    }

    /// <summary>
    /// Called when TF message received
    /// </summary>
    void OnTFMessageReceived(TFMessageMsg tfMessage)
    {
        if (!isDetecting)
            return;

        foreach (var transform in tfMessage.transforms)
        {
            string childFrame = transform.child_frame_id;
            string parentFrame = transform.header.frame_id;

            // Add to sets
            childFrameSet.Add(childFrame);
            parentFrameSet.Add(parentFrame);
            
            // Store relationship
            if (!frameRelationships.ContainsKey(childFrame))
            {
                frameRelationships[childFrame] = parentFrame;
            }
        }
    }

    void Update()
    {
        if (isDetecting && (Time.time - detectionStartTime) >= detectionDuration)
        {
            StopDetection();
        }
    }

    /// <summary>
    /// Display detection UI
    /// </summary>
    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 170, 400, 200));
        
        if (isDetecting)
        {
            GUILayout.Box("TF Frame Detection Running...");
            float timeLeft = detectionDuration - (Time.time - detectionStartTime);
            GUILayout.Label($"Time remaining: {timeLeft:F1}s");
            GUILayout.Label($"Frames found: {childFrameSet.Count}");
            
            if (GUILayout.Button("Stop Detection Now"))
            {
                StopDetection();
            }
        }
        else
        {
            GUILayout.Box("TF Frame Detector");
            
            if (GUILayout.Button("Start Detection"))
            {
                StartDetection();
            }
            
            if (detectedChildFrames.Count > 0)
            {
                GUILayout.Label($"Last scan found {detectedChildFrames.Count} frames");
                GUILayout.Label("Check Console for details");
            }
        }
        
        GUILayout.EndArea();
    }

    /// <summary>
    /// Get the detected frames as a formatted string
    /// </summary>
    public string GetDetectedFramesString()
    {
        string result = "Child Frames:\n";
        foreach (var frame in detectedChildFrames)
        {
            result += $"  - {frame}\n";
        }
        result += "\nParent Frames:\n";
        foreach (var frame in detectedParentFrames)
        {
            result += $"  - {frame}\n";
        }
        return result;
    }
}
