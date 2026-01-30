using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Tf2;
using RosMessageTypes.Geometry;

/// <summary>
/// CONFIGURABLE TF Pose Controller - Test different conversions until it works!
/// Use this to find the correct coordinate transformation for your robot
/// </summary>
public class BlueRovTFPoseController_Configurable : MonoBehaviour
{
    [Header("TF Settings")]
    public string tfTopic = "/tf";
    public string robotFrameId = "base_link";
    public string referenceFrameId = "odom";

    [Header("🔧 ROTATION MODE - Try each one!")]
    [Tooltip("Test different rotation conversions")]
    public RotationMode rotationMode = RotationMode.Mode1_UnderwaterNED;

    [Header("📐 Position Axis Mapping")]
    [Tooltip("Unity X-axis comes from ROS:")]
    public AxisMapping unityX = AxisMapping.NegY;  // Default: ROS -Y
    
    [Tooltip("Unity Y-axis comes from ROS:")]
    public AxisMapping unityY = AxisMapping.NegZ;  // Default: ROS -Z (depth flip)
    
    [Tooltip("Unity Z-axis comes from ROS:")]
    public AxisMapping unityZ = AxisMapping.PosX;  // Default: ROS +X

    [Header("🎚️ Fine Tuning")]
    [Tooltip("Extra rotation to apply (degrees)")]
    public Vector3 rotationOffset = Vector3.zero;
    
    [Tooltip("Smooth movements")]
    public bool smoothPoseUpdates = false;  // Disabled for testing
    
    [Range(0f, 0.95f)]
    public float smoothingFactor = 0.7f;

    [Header("📊 Status (Read-Only)")]
    public bool receivingPoses = false;
    public Vector3 currentPosition;
    public Vector3 currentRotation;
    public int updateCount = 0;

    [Header("🐛 Debug")]
    public bool showDebugInfo = true;
    public bool drawGizmos = true;

    private Vector3 targetPosition;
    private Quaternion targetRotation;
    private bool initialized = false;

    public enum RotationMode
    {
        Mode1_UnderwaterNED,      // Most common for underwater
        Mode2_StandardROS,        // Standard land robot
        Mode3_InvertedNED,        // NED but inverted
        Mode4_NoYFlip,            // Like NED but no Y flip
        Mode5_FullInvert,         // Everything inverted
        Mode6_XZFlip,             // X and Z flipped
        Mode7_Identity,           // Use ROS rotation as-is
        Mode8_NoRotation          // Ignore rotation entirely
    }

    public enum AxisMapping
    {
        PosX, NegX,
        PosY, NegY,
        PosZ, NegZ
    }

    void Start()
    {
        ROSConnection.GetOrCreateInstance().Subscribe<TFMessageMsg>(tfTopic, OnTFReceived);
        Debug.Log("═══════════════════════════════════════");
        Debug.Log("🔧 CONFIGURABLE TF CONTROLLER ACTIVE");
        Debug.Log($"Looking for: {referenceFrameId} -> {robotFrameId}");
        Debug.Log($"Current Rotation Mode: {rotationMode}");
        Debug.Log("Try different modes until robot moves correctly!");
        Debug.Log("═══════════════════════════════════════");
    }

    void OnTFReceived(TFMessageMsg msg)
    {
        foreach (var tf in msg.transforms)
        {
            if (tf.child_frame_id.Contains(robotFrameId))
            {
                ProcessTransform(tf);
                receivingPoses = true;
                updateCount++;
                return;
            }
        }
    }

    void ProcessTransform(TransformStampedMsg tf)
    {
        // Get ROS position
        Vector3 rosPos = new Vector3(
            (float)tf.transform.translation.x,
            (float)tf.transform.translation.y,
            (float)tf.transform.translation.z
        );

        // Get ROS rotation
        Quaternion rosRot = new Quaternion(
            (float)tf.transform.rotation.x,
            (float)tf.transform.rotation.y,
            (float)tf.transform.rotation.z,
            (float)tf.transform.rotation.w
        );

        // Convert position
        targetPosition = new Vector3(
            GetAxis(rosPos, unityX),
            GetAxis(rosPos, unityY),
            GetAxis(rosPos, unityZ)
        );

        // Convert rotation
        targetRotation = ConvertRotation(rosRot) * Quaternion.Euler(rotationOffset);

        // Store for display
        currentPosition = targetPosition;
        currentRotation = targetRotation.eulerAngles;

        // Apply
        if (!initialized)
        {
            transform.position = targetPosition;
            transform.rotation = targetRotation;
            initialized = true;
            
            Debug.Log($"✅ INITIAL POSE SET");
            Debug.Log($"   ROS Pos: {rosPos}");
            Debug.Log($"   Unity Pos: {targetPosition}");
            Debug.Log($"   ROS Rot (euler): {rosRot.eulerAngles}");
            Debug.Log($"   Unity Rot (euler): {targetRotation.eulerAngles}");
        }
        else if (!smoothPoseUpdates)
        {
            transform.position = targetPosition;
            transform.rotation = targetRotation;
        }

        if (showDebugInfo && updateCount % 100 == 0)
        {
            Debug.Log($"Update #{updateCount} | Pos: {targetPosition.ToString("F2")} | Rot: {targetRotation.eulerAngles.ToString("F1")}");
        }
    }

    float GetAxis(Vector3 v, AxisMapping m)
    {
        switch (m)
        {
            case AxisMapping.PosX: return v.x;
            case AxisMapping.NegX: return -v.x;
            case AxisMapping.PosY: return v.y;
            case AxisMapping.NegY: return -v.y;
            case AxisMapping.PosZ: return v.z;
            case AxisMapping.NegZ: return -v.z;
            default: return 0;
        }
    }

    Quaternion ConvertRotation(Quaternion q)
    {
        switch (rotationMode)
        {
            case RotationMode.Mode1_UnderwaterNED:
                // Underwater NED convention
                return new Quaternion(q.x, -q.y, q.z, -q.w);

            case RotationMode.Mode2_StandardROS:
                // Standard ROS->Unity (for land robots)
                return new Quaternion(-q.y, q.z, q.x, -q.w);

            case RotationMode.Mode3_InvertedNED:
                // NED but fully inverted
                return new Quaternion(-q.x, q.y, -q.z, q.w);

            case RotationMode.Mode4_NoYFlip:
                // Like NED but don't flip Y
                return new Quaternion(q.x, q.y, q.z, -q.w);

            case RotationMode.Mode5_FullInvert:
                // Invert everything
                return new Quaternion(-q.x, -q.y, -q.z, -q.w);

            case RotationMode.Mode6_XZFlip:
                // Flip X and Z only
                return new Quaternion(-q.x, q.y, -q.z, -q.w);

            case RotationMode.Mode7_Identity:
                // Use ROS quaternion directly
                return q;

            case RotationMode.Mode8_NoRotation:
                // Ignore rotation, position only
                return Quaternion.identity;

            default:
                return Quaternion.identity;
        }
    }

    void Update()
    {
        if (smoothPoseUpdates && initialized)
        {
            transform.position = Vector3.Lerp(transform.position, targetPosition, 1f - smoothingFactor);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 1f - smoothingFactor);
        }

        // Keyboard shortcuts for testing
        if (Input.GetKeyDown(KeyCode.R))
        {
            int nextMode = ((int)rotationMode + 1) % System.Enum.GetValues(typeof(RotationMode)).Length;
            rotationMode = (RotationMode)nextMode;
            Debug.Log($"🔄 Switched to: {rotationMode}");
        }
    }

    void OnGUI()
    {
        if (!showDebugInfo)
            return;

        GUI.Box(new Rect(10, 10, 350, 220), "");
        
        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 12;
        
        float y = 20;
        GUI.Label(new Rect(20, y, 330, 20), $"🔧 TF CONFIGURABLE CONTROLLER", style); y += 25;
        GUI.Label(new Rect(20, y, 330, 20), $"Receiving: {(receivingPoses ? "✅ YES" : "❌ NO")}"); y += 20;
        GUI.Label(new Rect(20, y, 330, 20), $"Updates: {updateCount}"); y += 20;
        GUI.Label(new Rect(20, y, 330, 20), $"Mode: {rotationMode}"); y += 20;
        GUI.Label(new Rect(20, y, 330, 20), $"Pos: {currentPosition.ToString("F2")}"); y += 20;
        GUI.Label(new Rect(20, y, 330, 20), $"Rot: {currentRotation.ToString("F1")}"); y += 20;
        y += 5;
        GUI.Label(new Rect(20, y, 330, 20), "Press 'R' to cycle rotation modes"); y += 20;
        
        if (GUI.Button(new Rect(20, y, 150, 25), "Next Rotation Mode"))
        {
            int nextMode = ((int)rotationMode + 1) % System.Enum.GetValues(typeof(RotationMode)).Length;
            rotationMode = (RotationMode)nextMode;
            Debug.Log($"🔄 Switched to: {rotationMode}");
        }
    }

    void OnDrawGizmos()
    {
        if (!drawGizmos || !initialized)
            return;

        // Target position sphere
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(targetPosition, 0.15f);

        // Line from current to target
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, targetPosition);

        // Coordinate axes
        float axisLen = 0.4f;
        Gizmos.color = Color.red;
        Gizmos.DrawRay(targetPosition, targetRotation * Vector3.right * axisLen);
        Gizmos.color = Color.green;
        Gizmos.DrawRay(targetPosition, targetRotation * Vector3.up * axisLen);
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(targetPosition, targetRotation * Vector3.forward * axisLen);
    }
}
