using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Tf2;
using RosMessageTypes.Geometry;
using System.Collections.Generic;

/// <summary>
/// Subscribes to /tf topic and visualizes transform frames as colored axes
/// Similar to what you see in RViz with the TF display
/// </summary>
public class TFSubscriber : MonoBehaviour
{
    [Header("ROS Settings")]
    [Tooltip("The TF topic to subscribe to")]
    public string tfTopic = "/tf";
    
    [Header("Visualization Settings")]
    [Tooltip("Optional: Assign a prefab with colored axes. Leave empty to auto-generate.")]
    public GameObject axesPrefab;
    
    [Tooltip("Length of each axis in meters")]
    public float axisLength = 0.1f;
    
    [Tooltip("Thickness of each axis")]
    public float axisThickness = 0.01f;

    // Dictionary to store frame GameObjects by name
    private Dictionary<string, GameObject> frameObjects = new Dictionary<string, GameObject>();

    void Start()
    {
        // Subscribe to the TF topic
        ROSConnection.GetOrCreateInstance().Subscribe<TFMessageMsg>(tfTopic, UpdateTF);
        Debug.Log($"Subscribed to {tfTopic}");
    }

    /// <summary>
    /// Called when a new TF message is received
    /// </summary>
    void UpdateTF(TFMessageMsg tfMessage)
    {
        foreach (var transform in tfMessage.transforms)
        {
            string frameName = transform.child_frame_id;
            
            // Create frame visualization if it doesn't exist
            if (!frameObjects.ContainsKey(frameName))
            {
                GameObject frameObj = CreateFrameObject(frameName);
                frameObjects[frameName] = frameObj;
                Debug.Log($"Created frame visualization for: {frameName}");
            }

            // Update the frame's position and rotation
            UpdateFrameTransform(frameObjects[frameName], transform);
        }
    }

    /// <summary>
    /// Creates a GameObject to represent a coordinate frame
    /// </summary>
    GameObject CreateFrameObject(string name)
    {
        GameObject obj;
        
        if (axesPrefab != null)
        {
            // Use provided prefab
            obj = Instantiate(axesPrefab, Vector3.zero, Quaternion.identity);
        }
        else
        {
            // Create simple RGB axes (like RViz)
            obj = new GameObject();
            CreateRGBAxes(obj);
        }
        
        obj.name = $"TF_{name}";
        obj.transform.parent = transform; // Parent to this manager object
        return obj;
    }

    /// <summary>
    /// Creates RGB axes similar to RViz visualization
    /// Red = X, Green = Y, Blue = Z
    /// </summary>
    void CreateRGBAxes(GameObject parent)
    {
        // X-axis (Red) - Points in the X direction
        CreateAxis(parent, "X_Axis", Vector3.right, Color.red);
        
        // Y-axis (Green) - Points in the Y direction
        CreateAxis(parent, "Y_Axis", Vector3.up, Color.green);
        
        // Z-axis (Blue) - Points in the Z direction
        CreateAxis(parent, "Z_Axis", Vector3.forward, Color.blue);
    }

    /// <summary>
    /// Helper to create a single colored axis
    /// </summary>
    void CreateAxis(GameObject parent, string name, Vector3 direction, Color color)
    {
        GameObject axis = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        axis.name = name;
        axis.transform.parent = parent.transform;
        
        // Position at half length in the direction
        axis.transform.localPosition = direction * (axisLength / 2);
        
        // Rotate cylinder to point in the correct direction
        if (direction == Vector3.right)
            axis.transform.localRotation = Quaternion.Euler(0, 0, 90);
        else if (direction == Vector3.forward)
            axis.transform.localRotation = Quaternion.Euler(90, 0, 0);
        // else Vector3.up is default (no rotation needed)
        
        // Scale: thin cylinder of appropriate length
        axis.transform.localScale = new Vector3(axisThickness, axisLength / 2, axisThickness);
        
        // Set color
        Renderer renderer = axis.GetComponent<Renderer>();
        renderer.material = new Material(Shader.Find("Standard"));
        renderer.material.color = color;
        
        // Remove collider (we don't need physics)
        Destroy(axis.GetComponent<Collider>());
    }

    /// <summary>
    /// Updates a frame's position and rotation based on ROS transform
    /// Handles coordinate system conversion from ROS to Unity
    /// </summary>
    void UpdateFrameTransform(GameObject frameObj, TransformStampedMsg transform)
    {
        // ROS coordinate system (UNDERWATER): X-forward, Y-left, Z-down (depth)
        // Unity coordinate system: X-right, Y-up, Z-forward
        
        Vector3 rosPosition = new Vector3(
            (float)transform.transform.translation.x,
            (float)transform.transform.translation.y,
            (float)transform.transform.translation.z
        );

        // Convert ROS position to Unity position
        // Underwater robotics uses Z-down, so we negate Z to get Y-up in Unity
        Vector3 unityPosition = new Vector3(
            -rosPosition.y,   // ROS Y (left) -> Unity -X (left)
            -rosPosition.z,   // ROS Z (down/depth) -> Unity Y (up/height) - NEGATED!
            rosPosition.x     // ROS X (forward) -> Unity Z (forward)
        );

        frameObj.transform.localPosition = unityPosition;

        // Convert quaternion orientation
        Quaternion rosRotation = new Quaternion(
            (float)transform.transform.rotation.x,
            (float)transform.transform.rotation.y,
            (float)transform.transform.rotation.z,
            (float)transform.transform.rotation.w
        );

        // Convert ROS rotation to Unity rotation
        Quaternion unityRotation = new Quaternion(
            -rosRotation.y,  
            rosRotation.z,   
            rosRotation.x,   
            -rosRotation.w
        );

        frameObj.transform.localRotation = unityRotation;
    }

    /// <summary>
    /// Optional: Clear all frames when disabled
    /// </summary>
    void OnDisable()
    {
        foreach (var kvp in frameObjects)
        {
            if (kvp.Value != null)
            {
                Destroy(kvp.Value);
            }
        }
        frameObjects.Clear();
    }
}
