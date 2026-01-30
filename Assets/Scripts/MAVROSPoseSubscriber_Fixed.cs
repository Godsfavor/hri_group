using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Geometry;

/// <summary>
/// Subscribes to MAVROS pose topic for real-time robot tracking
/// Use this for LIVE robot control (not rosbag playback)
/// NOTE: Requires GPS or positioning system to work!
/// </summary>
public class MAVROSPoseSubscriber : MonoBehaviour
{
    [Header("MAVROS Settings")]
    [Tooltip("MAVROS pose topic from real robot")]
    public string poseTopic = "/mavros/local_position/pose";
    
    [Header("Settings")]
    [Tooltip("Smooth pose updates")]
    public bool smoothUpdates = true;
    
    [Range(0f, 0.95f)]
    [Tooltip("Smoothing factor")]
    public float smoothingFactor = 0.7f;
    
    [Header("Status")]
    public bool receivingData = false;
    public Vector3 currentPosition;
    public Vector3 currentRotation;
    public int updateCount = 0;
    
    [Header("Debug")]
    public bool showDebug = true;

    private Vector3 targetPosition;
    private Quaternion targetRotation;
    private bool initialized = false;

    void Start()
    {
        ROSConnection.GetOrCreateInstance().Subscribe<PoseStampedMsg>(
            poseTopic,
            OnPoseReceived
        );
        
        Debug.Log($"[MAVROS Pose] Subscribed to {poseTopic}");
        Debug.Log("[MAVROS Pose] NOTE: This topic requires GPS or positioning system!");
        Debug.Log("[MAVROS Pose] If no data, use MAVROSIMUSubscriber instead!");
    }

    void OnPoseReceived(PoseStampedMsg msg)
    {
        // Extract ROS position
        Vector3 rosPos = new Vector3(
            (float)msg.pose.position.x,
            (float)msg.pose.position.y,
            (float)msg.pose.position.z
        );

        // Extract ROS rotation
        Quaternion rosRot = new Quaternion(
            (float)msg.pose.orientation.x,
            (float)msg.pose.orientation.y,
            (float)msg.pose.orientation.z,
            (float)msg.pose.orientation.w
        );

        // Convert to Unity coordinates (underwater NED)
        targetPosition = new Vector3(
            -rosPos.y,   // ROS Y (left) -> Unity -X
            -rosPos.z,   // ROS Z (down/depth) -> Unity Y (up) - FLIPPED!
            rosPos.x     // ROS X (forward) -> Unity Z
        );

        // Convert rotation (underwater NED)
        targetRotation = new Quaternion(
            rosRot.x,
            -rosRot.y,
            rosRot.z,
            -rosRot.w
        );

        // Store for display
        currentPosition = targetPosition;
        currentRotation = targetRotation.eulerAngles;
        receivingData = true;
        updateCount++;

        // Apply pose
        if (!initialized)
        {
            transform.position = targetPosition;
            transform.rotation = targetRotation;
            initialized = true;

            if (showDebug)
            {
                Debug.Log($"[MAVROS Pose] Connected! Initial position: {targetPosition}");
            }
        }
        else if (!smoothUpdates)
        {
            transform.position = targetPosition;
            transform.rotation = targetRotation;
        }

        if (showDebug && updateCount % 100 == 0)
        {
            Debug.Log($"[MAVROS Pose] Update #{updateCount} | Pos: {targetPosition.ToString("F2")}");
        }
    }

    void Update()
    {
        if (smoothUpdates && initialized)
        {
            transform.position = Vector3.Lerp(
                transform.position,
                targetPosition,
                1f - smoothingFactor
            );
            
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                1f - smoothingFactor
            );
        }
    }

    void OnGUI()
    {
        if (!showDebug)
            return;

        GUI.Box(new Rect(10, 10, 350, 150), "");
        
        float y = 20;
        GUI.Label(new Rect(20, y, 330, 20), "LIVE MAVROS POSE"); y += 25;
        GUI.Label(new Rect(20, y, 330, 20), $"Status: {(receivingData ? "CONNECTED" : "NO DATA")}"); y += 20;
        GUI.Label(new Rect(20, y, 330, 20), $"Updates: {updateCount}"); y += 20;
        GUI.Label(new Rect(20, y, 330, 20), $"Position: {currentPosition.ToString("F2")}"); y += 20;
        GUI.Label(new Rect(20, y, 330, 20), $"Rotation: {currentRotation.ToString("F1")}"); y += 20;
    }
}
