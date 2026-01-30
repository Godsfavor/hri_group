using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Sensor;

/// <summary>
/// Subscribes to MAVROS IMU data for real-time orientation
/// This ALWAYS works - doesn't need GPS or positioning!
/// USE THIS for testing connection - it works immediately!
/// </summary>
public class MAVROSIMUSubscriber : MonoBehaviour
{
    [Header("MAVROS Settings")]
    [Tooltip("MAVROS IMU topic - always available!")]
    public string imuTopic = "/bluerov2/imu/data";
    
    [Header("Visualization")]
    [Tooltip("Show orientation on this GameObject")]
    public GameObject orientationObject;
    
    [Tooltip("Show acceleration vector")]
    public bool showAccelerationGizmo = true;
    
    [Tooltip("Acceleration scale for visualization")]
    public float accelerationScale = 0.5f;
    
    [Header("Status")]
    public Vector3 currentOrientation;
    public Vector3 currentAngularVelocity;
    public Vector3 currentAcceleration;
    public bool receivingData = false;
    public int messageCount = 0;
    
    [Header("Debug")]
    public bool showDebug = true;

    private Vector3 currentAccel;

    void Start()
    {
        if (orientationObject == null)
            orientationObject = this.gameObject;

        ROSConnection.GetOrCreateInstance().Subscribe<ImuMsg>(
            imuTopic,
            OnIMUReceived
        );
        
        Debug.Log($"[MAVROS IMU] Subscribed to {imuTopic}");
        Debug.Log("[MAVROS IMU] This topic ALWAYS works - use for testing!");
    }

    void OnIMUReceived(ImuMsg msg)
    {
        receivingData = true;
        messageCount++;
        
        // Extract orientation quaternion
        Quaternion rosOrientation = new Quaternion(
            (float)msg.orientation.x,
            (float)msg.orientation.y,
            (float)msg.orientation.z,
            (float)msg.orientation.w
        );

        // Convert to Unity orientation (underwater NED)
        Quaternion unityOrientation = new Quaternion(
            rosOrientation.x,
            -rosOrientation.y,
            rosOrientation.z,
            -rosOrientation.w
        );

        // Apply to GameObject
        if (orientationObject != null)
        {
            orientationObject.transform.rotation = unityOrientation;
        }

        // Store euler angles
        currentOrientation = unityOrientation.eulerAngles;

        // Extract angular velocity
        currentAngularVelocity = new Vector3(
            (float)msg.angular_velocity.x,
            (float)msg.angular_velocity.y,
            (float)msg.angular_velocity.z
        );

        // Extract linear acceleration
        currentAcceleration = new Vector3(
            (float)msg.linear_acceleration.x,
            (float)msg.linear_acceleration.y,
            (float)msg.linear_acceleration.z
        );

        currentAccel = currentAcceleration;

        if (messageCount == 1)
        {
            Debug.Log("[MAVROS IMU] First message received!");
            Debug.Log($"   Orientation: {currentOrientation}");
        }
        
        if (showDebug && messageCount % 100 == 0)
        {
            Debug.Log($"[MAVROS IMU] Messages: {messageCount} | Orient: {currentOrientation.ToString("F1")}");
        }
    }

    void OnDrawGizmos()
    {
        if (!showAccelerationGizmo || !receivingData)
            return;

        // Draw acceleration vector
        Gizmos.color = Color.red;
        Gizmos.DrawRay(transform.position, currentAccel * accelerationScale);

        // Draw angular velocity
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(transform.position, currentAngularVelocity * 0.2f);
    }

    void OnGUI()
    {
        if (!showDebug)
            return;

        GUI.Box(new Rect(370, 10, 300, 140), "");
        
        float y = 20;
        GUI.Label(new Rect(380, y, 280, 20), "IMU DATA"); y += 25;
        GUI.Label(new Rect(380, y, 280, 20), $"Status: {(receivingData ? "RECEIVING" : "NO DATA")}"); y += 20;
        GUI.Label(new Rect(380, y, 280, 20), $"Messages: {messageCount}"); y += 20;
        GUI.Label(new Rect(380, y, 280, 20), $"Orient: {currentOrientation.ToString("F0")}"); y += 20;
        GUI.Label(new Rect(380, y, 280, 20), $"Accel: {currentAcceleration.ToString("F2")}"); y += 20;
    }
}
