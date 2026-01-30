using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Tf2;
using RosMessageTypes.Geometry;


/// <summary>
/// Subscribes to /tf and extracts the map->base_link transform
/// This works with your ArUco localization system
/// </summary>
public class ArucoLocalizationSubscriber : MonoBehaviour
{
    [Header("TF Settings")]
    [Tooltip("TF topic (usually /tf)")]
    public string tfTopic = "/tf";
    
    [Tooltip("Parent frame (world/map)")]
    public string worldFrame = "map";
    
    [Tooltip("Child frame (robot base)")]
    public string robotFrame = "base_link";

    [Header("Settings")]
    [Tooltip("Smooth pose updates")]
    public bool smoothUpdates = true;
    
    [Range(0f, 0.95f)]
    [Tooltip("Smoothing factor (higher = smoother but more lag)")]
    public float smoothingFactor = 0.7f;
    
    [Header("Status (Read-Only)")]
    public bool receivingData = false;
    public Vector3 currentPosition;
    public Vector3 currentRotation;
    public int updateCount = 0;
    public float lastUpdateTime = 0f;
    
    [Header("Debug")]
    public bool showDebug = true;
    public bool drawGizmos = true;

    private Vector3 targetPosition;
    private Quaternion targetRotation;
    private bool initialized = false;

    void Start()
    {
        ROSConnection.GetOrCreateInstance().Subscribe<TFMessageMsg>(
            tfTopic,
            OnTFReceived
        );
        
        Debug.Log($"[ArUco Localization] Listening to {tfTopic}");
        Debug.Log($"[ArUco Localization] Looking for: {worldFrame} -> {robotFrame}");
    }

    void OnTFReceived(TFMessageMsg msg)
    {
        foreach (var tf in msg.transforms)
        {
            // Check if this transform is map->base_link (or your frame names)
            bool isWorldToRobot = 
                tf.header.frame_id.Contains(worldFrame) && 
                tf.child_frame_id.Contains(robotFrame);
            
            if (isWorldToRobot)
            {
                ProcessTransform(tf);
                receivingData = true;
                updateCount++;
                lastUpdateTime = Time.time;
                return;
            }
        }
    }

    void ProcessTransform(TransformStampedMsg tf)
    {
        // Extract ROS position
        Vector3 rosPos = new Vector3(
            (float)tf.transform.translation.x,
            (float)tf.transform.translation.y,
            (float)tf.transform.translation.z
        );

        // Extract ROS rotation
        Quaternion rosRot = new Quaternion(
            (float)tf.transform.rotation.x,
            (float)tf.transform.rotation.y,
            (float)tf.transform.rotation.z,
            (float)tf.transform.rotation.w
        );

        // Convert to Unity coordinates (ROS underwater: X-forward, Y-left, Z-down)
        targetPosition = new Vector3(
            -rosPos.y,   // ROS Y (left) -> Unity -X
            -rosPos.z,   // ROS Z (down/depth) -> Unity Y (up) - FLIPPED!
            rosPos.x     // ROS X (forward) -> Unity Z
        );

        // Convert rotation (underwater NED convention)
        targetRotation = new Quaternion(
            rosRot.x,
            -rosRot.y,
            rosRot.z,
            -rosRot.w
        );

        // Store for display
        currentPosition = targetPosition;
        currentRotation = targetRotation.eulerAngles;

        // Apply pose
        if (!initialized)
        {
            transform.position = targetPosition;
            transform.rotation = targetRotation;
            initialized = true;

            if (showDebug)
            {
                Debug.Log($"✅ [ArUco Localization] Initial position set: {targetPosition}");
                Debug.Log($"   ROS Position: {rosPos}");
                Debug.Log($"   ROS Rotation (euler): {rosRot.eulerAngles}");
            }
        }
        else if (!smoothUpdates)
        {
            transform.position = targetPosition;
            transform.rotation = targetRotation;
        }

        if (showDebug && updateCount % 100 == 0)
        {
            Debug.Log($"[ArUco Localization] Update #{updateCount} | Pos: {targetPosition.ToString("F2")}");
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

        // Check for timeout (no data for 2 seconds)
        if (receivingData && (Time.time - lastUpdateTime) > 2f)
        {
            receivingData = false;
            if (showDebug)
            {
                Debug.LogWarning("[ArUco Localization] Lost connection - no updates for 2 seconds");
            }
        }
    }

    void OnGUI()
    {
        if (!showDebug)
            return;

        GUI.Box(new Rect(10, 10, 350, 180), "");
        
        GUIStyle titleStyle = new GUIStyle(GUI.skin.label);
        titleStyle.fontSize = 14;
        titleStyle.fontStyle = FontStyle.Bold;
        
        float y = 20;
        GUI.Label(new Rect(20, y, 330, 20), "ARUCO LOCALIZATION", titleStyle); y += 25;
        
        // Status
        string status = receivingData ? "✅ CONNECTED" : "❌ NO DATA";
        GUI.contentColor = receivingData ? Color.green : Color.red;
        GUI.Label(new Rect(20, y, 330, 20), $"Status: {status}"); 
        GUI.contentColor = Color.white;
        y += 20;
        
        GUI.Label(new Rect(20, y, 330, 20), $"Updates: {updateCount}"); y += 20;
        GUI.Label(new Rect(20, y, 330, 20), $"Position: {currentPosition.ToString("F2")}"); y += 20;
        GUI.Label(new Rect(20, y, 330, 20), $"Rotation: {currentRotation.ToString("F1")}"); y += 20;
        
        if (!receivingData && updateCount == 0)
        {
            y += 5;
            GUI.contentColor = Color.yellow;
            GUI.Label(new Rect(20, y, 330, 40), "Waiting for ArUco markers...\nMake sure markers are visible!");
            GUI.contentColor = Color.white;
        }
    }

    void OnDrawGizmos()
    {
        if (!drawGizmos || !initialized)
            return;

        // Draw position sphere
        Gizmos.color = receivingData ? Color.green : Color.red;
        Gizmos.DrawWireSphere(transform.position, 0.2f);

        // Draw coordinate axes
        float axisLen = 0.5f;
        Gizmos.color = Color.red;
        Gizmos.DrawRay(transform.position, transform.rotation * Vector3.right * axisLen);
        Gizmos.color = Color.green;
        Gizmos.DrawRay(transform.position, transform.rotation * Vector3.up * axisLen);
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(transform.position, transform.rotation * Vector3.forward * axisLen);
    }
}
