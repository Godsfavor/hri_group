using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Sensor;
using RosMessageTypes.Geometry;

/// <summary>
/// Diagnostic script to check MAVROS connection
/// Shows which topics are actually receiving data
/// </summary>
public class MAVROSDiagnostic : MonoBehaviour
{
    [Header("Status")]
    public bool rosConnected = false;
    public bool imuReceiving = false;
    public bool poseReceiving = false;
    
    [Header("Counters")]
    public int imuCount = 0;
    public int poseCount = 0;
    
    [Header("Last Data")]
    public string lastIMUTime = "None";
    public string lastPoseTime = "None";
    
    [Header("Debug Info")]
    public string rosIPAddress = "";
    public int rosPort = 0;

    private float lastCheckTime;

    void Start()
    {
        ROSConnection ros = ROSConnection.GetOrCreateInstance();
        rosConnected = ros.HasConnectionThread;
        
        // Get connection info
        rosIPAddress = ros.RosIPAddress;
        rosPort = ros.RosPort;
        
        Debug.Log("╔════════════════════════════════════════╗");
        Debug.Log("🔍 MAVROS DIAGNOSTIC STARTED");
        Debug.Log($"ROS IP: {rosIPAddress}");
        Debug.Log($"ROS Port: {rosPort}");
        Debug.Log($"Connected: {rosConnected}");
        Debug.Log("╚════════════════════════════════════════╝");
        
        // Subscribe to topics - Unity will handle the QoS internally
        // The ros-tcp-endpoint bridge should handle the conversion
        ros.Subscribe<ImuMsg>("/bluerov2/imu/data", OnIMUReceived);
        ros.Subscribe<PoseStampedMsg>("/bluerov2/local_position/pose", OnPoseReceived);
        
        Debug.Log("[DIAGNOSTIC] Subscribed to /bluerov2/imu/data");
        Debug.Log("[DIAGNOSTIC] Subscribed to /bluerov2/local_position/pose");
        Debug.Log("[DIAGNOSTIC] Note: QoS mismatch warnings are expected - ros-tcp-endpoint should bridge them");
        
        lastCheckTime = Time.time;
    }

    void OnIMUReceived(ImuMsg msg)
    {
        imuReceiving = true;
        imuCount++;
        lastIMUTime = System.DateTime.Now.ToString("HH:mm:ss");
        
        if (imuCount == 1)
        {
            Debug.Log("✅ IMU DATA RECEIVED!");
            Debug.Log($"   Orientation: {msg.orientation.x}, {msg.orientation.y}, {msg.orientation.z}, {msg.orientation.w}");
        }
        
        if (imuCount % 50 == 0)
        {
            Debug.Log($"[DIAGNOSTIC] IMU messages received: {imuCount}");
        }
    }

    void OnPoseReceived(PoseStampedMsg msg)
    {
        poseReceiving = true;
        poseCount++;
        lastPoseTime = System.DateTime.Now.ToString("HH:mm:ss");
        
        if (poseCount == 1)
        {
            Debug.Log("✅ POSE DATA RECEIVED!");
            Debug.Log($"   Position: {msg.pose.position.x}, {msg.pose.position.y}, {msg.pose.position.z}");
        }
    }

    void Update()
    {
        // Update connection status
        ROSConnection ros = ROSConnection.GetOrCreateInstance();
        rosConnected = ros.HasConnectionThread;
        
        // Check every 2 seconds
        if (Time.time - lastCheckTime > 2f)
        {
            lastCheckTime = Time.time;
        }
    }

    void OnGUI()
    {
        // Large diagnostic window
        GUI.Box(new Rect(10, 10, 450, 280), "");
        
        GUIStyle titleStyle = new GUIStyle(GUI.skin.label);
        titleStyle.fontSize = 14;
        titleStyle.fontStyle = FontStyle.Bold;
        
        GUIStyle normalStyle = new GUIStyle(GUI.skin.label);
        normalStyle.fontSize = 12;
        
        float y = 20;
        
        GUI.Label(new Rect(20, y, 430, 25), "MAVROS CONNECTION DIAGNOSTIC", titleStyle);
        y += 30;
        
        // Connection status
        string connStatus = rosConnected ? "CONNECTED" : "DISCONNECTED";
        GUI.contentColor = rosConnected ? Color.green : Color.red;
        GUI.Label(new Rect(20, y, 430, 20), $"ROS Connection: {connStatus}", normalStyle);
        GUI.contentColor = Color.white;
        y += 25;
        
        GUI.Label(new Rect(20, y, 430, 20), $"ROS IP: {rosIPAddress}:{rosPort}", normalStyle);
        y += 25;
        
        // Separator
        GUI.Label(new Rect(20, y, 430, 20), "----------------------------------------", normalStyle);
        y += 25;
        
        // IMU Status
        string imuStatus = imuReceiving ? "RECEIVING" : "NO DATA";
        GUI.contentColor = imuReceiving ? Color.green : Color.red;
        GUI.Label(new Rect(20, y, 430, 20), $"/bluerov2/imu/data: {imuStatus}", normalStyle);
        GUI.contentColor = Color.white;
        y += 20;
        GUI.Label(new Rect(20, y, 430, 20), $"  Count: {imuCount} | Last: {lastIMUTime}", normalStyle);
        y += 25;
        
        // Pose Status
        string poseStatus = poseReceiving ? "RECEIVING" : "NO DATA";
        GUI.contentColor = poseReceiving ? Color.green : Color.red;
        GUI.Label(new Rect(20, y, 430, 20), $"/bluerov2/local_position/pose: {poseStatus}", normalStyle);
        GUI.contentColor = Color.white;
        y += 20;
        GUI.Label(new Rect(20, y, 430, 20), $"  Count: {poseCount} | Last: {lastPoseTime}", normalStyle);
        y += 25;
        
        // Diagnosis
        GUI.Label(new Rect(20, y, 430, 20), "----------------------------------------", normalStyle);
        y += 25;
        
        if (!rosConnected)
        {
            GUI.contentColor = Color.red;
            GUI.Label(new Rect(20, y, 430, 40), "NOT CONNECTED TO ROS-TCP-ENDPOINT", normalStyle);
            GUI.contentColor = Color.white;
        }
        else if (!imuReceiving && !poseReceiving)
        {
            GUI.contentColor = Color.yellow;
            GUI.Label(new Rect(20, y, 430, 20), "CONNECTED BUT NO TOPICS RECEIVED", normalStyle);
            GUI.contentColor = Color.white;
            y += 25;
            GUI.Label(new Rect(20, y, 430, 40), "Check ros-tcp-endpoint is using QoS override", normalStyle);
        }
        else if (imuReceiving && !poseReceiving)
        {
            GUI.contentColor = Color.yellow;
            GUI.Label(new Rect(20, y, 430, 20), "IMU OK - POSE NEEDS GPS/POSITIONING", normalStyle);
            GUI.contentColor = Color.white;
        }
        else if (imuReceiving && poseReceiving)
        {
            GUI.contentColor = Color.green;
            GUI.Label(new Rect(20, y, 430, 20), "ALL SYSTEMS WORKING!", normalStyle);
            GUI.contentColor = Color.white;
        }
    }

    void OnDestroy()
    {
        Debug.Log("╔════════════════════════════════════════╗");
        Debug.Log("MAVROS DIAGNOSTIC FINAL REPORT:");
        Debug.Log($"IMU Messages: {imuCount}");
        Debug.Log($"Pose Messages: {poseCount}");
        Debug.Log("╚════════════════════════════════════════╝");
    }
}