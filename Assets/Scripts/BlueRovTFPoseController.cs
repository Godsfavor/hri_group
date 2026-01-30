using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Tf2;
using RosMessageTypes.Geometry;

/// <summary>
/// Direct pose mirroring from TF transforms
/// This REPLACES velocity-based control for accurate visualization
/// The robot's position is set directly from /tf topic, not computed from velocities
/// 
/// IMPORTANT: Disable BlueRovCmdVeloRosSub when using this!
/// You want EITHER velocity control OR pose mirroring, not both.
/// </summary>
public class BlueRovTFPoseController : MonoBehaviour
{
    [Header("TF Settings")]
    [Tooltip("The TF topic to subscribe to (usually /tf)")]
    public string tfTopic = "/tf";
    
    [Tooltip("The frame ID for the BlueROV2 base (e.g., 'base_link', 'bluerov2/base_link')")]
    public string robotFrameId = "base_link";
    
    [Tooltip("The reference frame (usually 'odom', 'map', or 'world')")]
    public string referenceFrameId = "odom";

    [Header("Pose Update Settings")]
    [Tooltip("Smooth the pose updates for visual quality")]
    public bool smoothPoseUpdates = true;
    
    [Tooltip("Smoothing factor (0 = instant, higher = smoother but more lag)")]
    [Range(0f, 0.95f)]
    public float smoothingFactor = 0.7f;

    [Header("Status")]
    [Tooltip("Is the robot receiving pose updates?")]
    public bool receivingPoses = false;
    
    [Tooltip("Last received position")]
    public Vector3 lastReceivedPosition;
    
    [Tooltip("Last received rotation (euler angles)")]
    public Vector3 lastReceivedRotation;
    
    [Tooltip("Number of pose updates received")]
    public int updateCount = 0;

    [Header("Debug")]
    [Tooltip("Show debug information in console")]
    public bool debugMode = false;
    
    [Tooltip("Draw gizmos showing target pose")]
    public bool drawGizmos = true;

    // Private variables
    private Vector3 targetPosition;
    private Quaternion targetRotation;
    private bool hasReceivedFirstPose = false;
    private float lastUpdateTime = 0f;

    void Start()
    {
        // Subscribe to TF topic
        ROSConnection.GetOrCreateInstance().Subscribe<TFMessageMsg>(tfTopic, OnTFMessageReceived);
        Debug.Log($"[TF Pose Controller] Subscribed to {tfTopic}");
        Debug.Log($"[TF Pose Controller] Looking for transform: {referenceFrameId} -> {robotFrameId}");
        Debug.Log($"[TF Pose Controller] This will SET robot pose directly from recorded TF data");
    }

    /// <summary>
    /// Called when TF message is received
    /// Extracts the robot's transform and updates the Unity GameObject
    /// </summary>
    void OnTFMessageReceived(TFMessageMsg tfMessage)
    {
        foreach (var transform in tfMessage.transforms)
        {
            // Check if this is the transform we're looking for
            bool isRobotFrame = transform.child_frame_id == robotFrameId || 
                               transform.child_frame_id.EndsWith("/" + robotFrameId) ||
                               transform.child_frame_id.Contains(robotFrameId);
            
            bool isCorrectParent = transform.header.frame_id == referenceFrameId ||
                                  transform.header.frame_id.EndsWith("/" + referenceFrameId) ||
                                  transform.header.frame_id.Contains(referenceFrameId);

            if (isRobotFrame)
            {
                if (debugMode && updateCount < 5)
                {
                    Debug.Log($"[TF Pose] Found transform: {transform.header.frame_id} -> {transform.child_frame_id}");
                }

                // Extract and convert the pose
                UpdatePoseFromTransform(transform);
                receivingPoses = true;
                updateCount++;
                lastUpdateTime = Time.time;
                
                return; // Found our transform, no need to check others
            }
        }
    }

    /// <summary>
    /// Converts ROS transform to Unity pose and updates the robot
    /// </summary>
    void UpdatePoseFromTransform(TransformStampedMsg transform)
    {
        // Extract ROS position
        Vector3 rosPosition = new Vector3(
            (float)transform.transform.translation.x,
            (float)transform.transform.translation.y,
            (float)transform.transform.translation.z
        );

        // Convert ROS position to Unity position
        // UNDERWATER ROBOTICS: ROS uses Z-down (depth increases downward)
        // Unity: X-right, Y-up, Z-forward
        // Conversion: ROS(X-forward, Y-left, Z-down) -> Unity(X-right, Y-up, Z-forward)
        Vector3 unityPosition = new Vector3(
            -rosPosition.y,   // ROS Y (left) -> Unity -X (left)
            -rosPosition.z,   // ROS Z (down/depth) -> Unity Y (up/height) - NEGATED!
            rosPosition.x     // ROS X (forward) -> Unity Z (forward)
        );

        // Extract ROS rotation (quaternion)
        Quaternion rosRotation = new Quaternion(
            (float)transform.transform.rotation.x,
            (float)transform.transform.rotation.y,
            (float)transform.transform.rotation.z,
            (float)transform.transform.rotation.w
        );

        // Convert ROS rotation to Unity rotation
        Quaternion unityRotation = new Quaternion(
            -rosRotation.y,
            -rosRotation.z,
            rosRotation.x,
            -rosRotation.w
        );

        // Store target pose
        targetPosition = unityPosition;
        targetRotation = unityRotation;
        
        // Store for display
        lastReceivedPosition = unityPosition;
        lastReceivedRotation = unityRotation.eulerAngles;

        // Apply pose update
        if (!hasReceivedFirstPose)
        {
            // First pose: jump directly to position (no smoothing)
            this.transform.position = targetPosition;
            this.transform.rotation = targetRotation;
            hasReceivedFirstPose = true;
            
            Debug.Log($"[TF Pose] Initial pose set: Position={targetPosition}, Rotation={targetRotation.eulerAngles}");
        }
        else if (!smoothPoseUpdates)
        {
            // No smoothing: direct update
            this.transform.position = targetPosition;
            this.transform.rotation = targetRotation;
        }
        // If smoothing is enabled, Update() will handle interpolation

        if (debugMode && updateCount < 10)
        {
            Debug.Log($"[TF Pose] Update #{updateCount}: Pos={unityPosition}, Rot={unityRotation.eulerAngles}");
        }
    }

    /// <summary>
    /// Update is called once per frame
    /// Handles smooth interpolation if enabled
    /// </summary>
    void Update()
    {
        if (smoothPoseUpdates && hasReceivedFirstPose)
        {
            // Smoothly interpolate to target pose
            this.transform.position = Vector3.Lerp(
                this.transform.position,
                targetPosition,
                1f - smoothingFactor
            );

            this.transform.rotation = Quaternion.Slerp(
                this.transform.rotation,
                targetRotation,
                1f - smoothingFactor
            );
        }

        // Check if we're still receiving updates
        if (receivingPoses && (Time.time - lastUpdateTime) > 1.0f)
        {
            receivingPoses = false;
            if (debugMode)
            {
                Debug.LogWarning("[TF Pose] No pose updates received in last 1 second");
            }
        }
    }

    /// <summary>
    /// Reset the pose controller
    /// </summary>
    public void ResetPose()
    {
        hasReceivedFirstPose = false;
        receivingPoses = false;
        updateCount = 0;
        Debug.Log("[TF Pose] Controller reset");
    }

    /// <summary>
    /// Manually set the robot frame ID (useful for runtime changes)
    /// </summary>
    public void SetRobotFrameId(string frameId)
    {
        robotFrameId = frameId;
        ResetPose();
        Debug.Log($"[TF Pose] Robot frame ID set to: {frameId}");
    }

    /// <summary>
    /// Draw debug gizmos in Scene view
    /// </summary>
    void OnDrawGizmos()
    {
        if (!drawGizmos || !hasReceivedFirstPose)
            return;

        // Draw target position as a wire sphere
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(targetPosition, 0.1f);

        // Draw line from current to target position
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, targetPosition);

        // Draw coordinate axes at target pose
        DrawAxisGizmo(targetPosition, targetRotation, 0.2f);
    }

    /// <summary>
    /// Helper to draw coordinate axes
    /// </summary>
    void DrawAxisGizmo(Vector3 position, Quaternion rotation, float scale)
    {
        // X axis (red)
        Gizmos.color = Color.red;
        Gizmos.DrawRay(position, rotation * Vector3.right * scale);

        // Y axis (green)
        Gizmos.color = Color.green;
        Gizmos.DrawRay(position, rotation * Vector3.up * scale);

        // Z axis (blue)
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(position, rotation * Vector3.forward * scale);
    }

    /// <summary>
    /// Display runtime information
    /// </summary>
    void OnGUI()
    {
        if (!debugMode)
            return;

        GUILayout.BeginArea(new Rect(10, 10, 300, 150));
        GUILayout.Box("TF Pose Controller Debug");
        GUILayout.Label($"Receiving: {receivingPoses}");
        GUILayout.Label($"Updates: {updateCount}");
        GUILayout.Label($"Frame: {robotFrameId}");
        GUILayout.Label($"Pos: {lastReceivedPosition}");
        GUILayout.Label($"Rot: {lastReceivedRotation}");
        GUILayout.EndArea();
    }
}
