using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Tf2;
using RosMessageTypes.Std;

public class BlueRovHybridNav : MonoBehaviour
{
    [Header("ROS Settings")]
    public string tfTopic = "/tf";
    public string depthTopic = "/bluerov2/global_position/rel_alt";
    public string robotFrameName = "base_link";

    [Header("Fixes")]
    [Tooltip("Check this if the robot spawns upside down or on its side")]
    public bool fixRotation90 = true;
    [Tooltip("Check this if the controls are reversed")]
    public bool invertForward = false;

    [Header("Calibration")]
    public float depthScale = 1.0f;
    public float depthOffset = 0.0f; 

    // Internal state
    private Vector3 targetPos;
    private Quaternion targetRot;
    private float pressureDepth = 0f;
    private bool hasTf = false;

    void Start()
    {
        ROSConnection ros = ROSConnection.GetOrCreateInstance();
        
        // Subscribe to topics - Unity ROS-TCP-Connector doesn't support QoS API
        // The ros-tcp-endpoint bridge should handle QoS conversion
        ros.Subscribe<TFMessageMsg>(tfTopic, OnTFReceived);
        ros.Subscribe<Float64Msg>(depthTopic, OnDepthReceived);
        
        Debug.Log($"[BlueRov Nav] Subscribed to {tfTopic}");
        Debug.Log($"[BlueRov Nav] Subscribed to {depthTopic}");
        Debug.Log("[BlueRov Nav] Note: QoS warnings are expected - ros-tcp-endpoint should bridge them");
        
        // Initialize with current position so we don't jump to 0,0,0
        targetPos = transform.position;
        targetRot = transform.rotation;
    }

    void OnTFReceived(TFMessageMsg msg)
    {
        foreach (var transformMsg in msg.transforms)
        {
            // Check if this TF message talks about our robot
            if (transformMsg.child_frame_id.Contains(robotFrameName))
            {
                // ROS Position -> Unity Position
                // We IGNORE ROS Z (Depth) here, we use pressure for that.
                float rx = (float)transformMsg.transform.translation.x;
                float ry = (float)transformMsg.transform.translation.y;

                targetPos.x = -ry; // Map ROS Y to Unity X
                targetPos.z = rx;  // Map ROS X to Unity Z
                
                if (invertForward) { targetPos.x = -targetPos.x; targetPos.z = -targetPos.z; }

                // ROS Rotation -> Unity Rotation
                targetRot = new Quaternion(
                    -(float)transformMsg.transform.rotation.y,
                    (float)transformMsg.transform.rotation.z,
                    (float)transformMsg.transform.rotation.x,
                    -(float)transformMsg.transform.rotation.w
                );

                if (fixRotation90)
                {
                    // Rotate 90 degrees on X axis to fix "Upside Down" issue
                    targetRot *= Quaternion.Euler(90, 0, 0); 
                    // Depending on the camera mount, you might need (0, 90, 0) or (-90, 0, 0)
                    // Try changing the numbers above if it's still wrong.
                }

                hasTf = true;
            }
        }
    }

    void OnDepthReceived(Float64Msg msg)
    {
        // Update the Y height based on pressure
        pressureDepth = (float)msg.data * depthScale;
    }

    void Update()
    {
        if (hasTf)
        {
            // Combine ArUco X/Z with Pressure Y
            Vector3 finalPos = targetPos;
            finalPos.y = pressureDepth + depthOffset;

            // Smoothly move there
            transform.position = Vector3.Lerp(transform.position, finalPos, Time.deltaTime * 5f);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 5f);
        }
    }
}