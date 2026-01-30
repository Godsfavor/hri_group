using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Geometry;
using System.Collections.Generic;

/// <summary>
/// Subscribes to ArUco marker poses and visualizes them in Unity
/// This will show all detected ArUco markers as small cubes/markers
/// </summary>
public class ArucoPoseArraySubscriber : MonoBehaviour
{
    [Header("ROS Settings")]
    [Tooltip("Topic publishing ArUco marker poses")]
    public string topic = "/bluerov2/aruco_pool/poses";
    
    [Tooltip("If true, will try to use fixed_marker topics automatically (disable if not in rosbag)")]
    public bool useFixedMarkers = false;  // ← CHANGED to false since fixed_ topics don't exist
    
    [Header("Visualization Settings")]
    [Tooltip("Optional: Prefab to use for markers (e.g., a cube with ArUco texture)")]
    public GameObject markerPrefab;
    
    [Tooltip("Size of auto-generated marker cubes")]
    public float markerSize = 0.05f;
    
    [Tooltip("Color of auto-generated markers")]
    public Color markerColor = Color.yellow;
    
    [Tooltip("Show marker labels")]
    public bool showLabels = true;

    private List<GameObject> markerObjects = new List<GameObject>();

    void Start()
    {
        // If useFixedMarkers is enabled, try to use fixed_marker topics
        string subscribeTopic = topic;
        
        if (useFixedMarkers)
        {
            // Check if topic already has "fixed_" prefix
            if (!topic.Contains("fixed_"))
            {
                // Try to insert "fixed_" before the last part of the topic name
                // e.g., /bluerov2/aruco_pool/poses -> /bluerov2/aruco_pool/fixed_poses
                // or just prepend if simpler naming
                int lastSlash = topic.LastIndexOf('/');
                if (lastSlash != -1)
                {
                    subscribeTopic = topic.Substring(0, lastSlash + 1) + "fixed_" + topic.Substring(lastSlash + 1);
                    Debug.Log($"Using fixed markers topic: {subscribeTopic}");
                }
            }
        }
        
        ROSConnection.GetOrCreateInstance().Subscribe<PoseArrayMsg>(subscribeTopic, UpdateMarkers);
        Debug.Log($"ArUco Subscriber: Listening to {subscribeTopic}");
        Debug.Log($"NOTE: Make sure you're using markers on the pool FLOOR, not surface!");
    }

    /// <summary>
    /// Called when new ArUco marker poses are received
    /// </summary>
    void UpdateMarkers(PoseArrayMsg poseArray)
    {
        // Clear old markers
        ClearMarkers();

        // Create new markers for each pose in the array
        for (int i = 0; i < poseArray.poses.Length; i++)
        {
            CreateMarker(poseArray.poses[i], i);
        }
    }

    /// <summary>
    /// Creates a marker visualization for a single ArUco marker pose
    /// </summary>
    void CreateMarker(PoseMsg pose, int index)
    {
        GameObject marker;
        
        if (markerPrefab != null)
        {
            // Use provided prefab
            marker = Instantiate(markerPrefab, transform);
        }
        else
        {
            // Create a simple colored cube
            marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.transform.parent = transform;
            marker.transform.localScale = Vector3.one * markerSize;
            
            // Set color
            Renderer renderer = marker.GetComponent<Renderer>();
            renderer.material = new Material(Shader.Find("Standard"));
            renderer.material.color = markerColor;
            
            // Remove collider
            Destroy(marker.GetComponent<Collider>());
        }

        marker.name = $"ArUco_Marker_{index}";

        // Convert ROS position to Unity position
        Vector3 rosPos = new Vector3(
            (float)pose.position.x,
            (float)pose.position.y,
            (float)pose.position.z
        );

        // ROS to Unity coordinate conversion
        // UNDERWATER ROBOTICS: ROS uses Z-down (depth), Unity uses Y-up (height)
        Vector3 unityPos = new Vector3(
            -rosPos.y,   // ROS Y (left) -> Unity -X (left)
            -rosPos.z,   // ROS Z (down/depth) -> Unity Y (up/height) - NEGATED!
            rosPos.x     // ROS X (forward) -> Unity Z (forward)
        );

        marker.transform.localPosition = unityPos;

        // Convert ROS rotation to Unity rotation
        Quaternion rosRot = new Quaternion(
            (float)pose.orientation.x,
            (float)pose.orientation.y,
            (float)pose.orientation.z,
            (float)pose.orientation.w
        );

        Quaternion unityRot = new Quaternion(
            -rosRot.y,
            rosRot.z,
            rosRot.x,
            -rosRot.w
        );

        marker.transform.localRotation = unityRot;

        // Optionally add a label
        if (showLabels)
        {
            AddLabel(marker, $"Marker {index}");
        }

        markerObjects.Add(marker);
    }

    /// <summary>
    /// Adds a 3D text label to a marker
    /// </summary>
    void AddLabel(GameObject parent, string text)
    {
        GameObject labelObj = new GameObject("Label");
        labelObj.transform.parent = parent.transform;
        labelObj.transform.localPosition = Vector3.up * (markerSize * 1.5f);
        
        // Add TextMesh component
        TextMesh textMesh = labelObj.AddComponent<TextMesh>();
        textMesh.text = text;
        textMesh.fontSize = 40;
        textMesh.characterSize = 0.01f;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.color = Color.white;
        
        // Make it face the camera (billboard effect)
        labelObj.AddComponent<Billboard>();
    }

    /// <summary>
    /// Clears all existing marker visualizations
    /// </summary>
    void ClearMarkers()
    {
        foreach (var obj in markerObjects)
        {
            if (obj != null)
            {
                Destroy(obj);
            }
        }
        markerObjects.Clear();
    }

    void OnDisable()
    {
        ClearMarkers();
    }
}

/// <summary>
/// Simple billboard component to make labels always face the camera
/// </summary>
public class Billboard : MonoBehaviour
{
    void LateUpdate()
    {
        if (Camera.main != null)
        {
            transform.rotation = Camera.main.transform.rotation;
        }
    }
}
