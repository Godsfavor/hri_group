using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Tf2;
using System.Collections.Generic;
using RosMessageTypes.Geometry;


/// <summary>
/// COMPREHENSIVE ArUco Localization Diagnostic & Fix
/// This will help you find the EXACT coordinate mapping your system needs
/// 
/// USAGE:
/// 1. Attach to your robot GameObject in Unity
/// 2. Press NUMBER KEYS (1-9) to test different coordinate mappings
/// 3. Press SPACE to freeze/unfreeze updates (to analyze what's happening)
/// 4. Press R to reset smoothing
/// 5. Watch the on-screen display to see which mode matches reality
/// </summary>
public class ArucoLocalizationDiagnostic : MonoBehaviour
{
    [Header("ROS Settings")]
    public string tfTopic = "/tf";
    public string worldFrame = "map";
    public string robotFrame = "base_link";

    [Header("🎯 COORDINATE MAPPING - Press 1-9 to Test")]
    public CoordinateMode currentMode = CoordinateMode.Mode1_StandardUnderwater;
    
    [Header("Smoothing")]
    public bool enableSmoothing = true;
    [Range(0f, 0.95f)]
    public float positionSmoothing = 0.3f;  // Much lighter smoothing
    [Range(0f, 0.95f)]
    public float rotationSmoothing = 0.5f;
    
    [Header("Scale (if pool seems too big/small)")]
    public float scaleMultiplier = 1.0f;
    
    [Header("🔍 Debug Controls")]
    public bool showDetailedDebug = true;
    public bool drawGizmos = true;
    public bool freezeUpdates = false;  // Press SPACE to toggle
    
    [Header("📊 Status - READ ONLY")]
    public bool connected = false;
    public int updateCount = 0;
    public Vector3 rawROSPosition;
    public Vector3 rawROSEuler;
    public Vector3 unityPosition;
    public Vector3 unityEuler;
    public float lastUpdateTime;
    
    [Header("📈 Movement Analysis")]
    public Vector3 rosVelocity;
    public Vector3 unityVelocity;
    public float rosSpeed;
    public float unitySpeed;

    private Vector3 targetPosition;
    private Quaternion targetRotation;
    private Vector3 lastRawPosition;
    private Vector3 lastUnityPosition;
    private Queue<string> logMessages = new Queue<string>();
    private const int MAX_LOG_MESSAGES = 5;
    private bool initialized = false;

    public enum CoordinateMode
    {
        Mode1_StandardUnderwater,   // -Y, -Z, X (typical underwater)
        Mode2_YourObservation,      // Y, Z, X (based on your description)
        Mode3_DirectCopy,           // X, Y, Z (no transformation)
        Mode4_SwapXZ,               // Z, Y, X
        Mode5_NegateX,              // -X, Y, Z
        Mode6_NegateY,              // X, -Y, Z
        Mode7_NegateZ,              // X, Y, -Z
        Mode8_FlipYZ,               // X, Z, Y
        Mode9_Custom                // Manual tuning
    }

    [Header("Mode 9: Custom Mapping")]
    public AxisMapping customX = AxisMapping.PosX;
    public AxisMapping customY = AxisMapping.PosY;
    public AxisMapping customZ = AxisMapping.PosZ;
    
    public enum AxisMapping { PosX, NegX, PosY, NegY, PosZ, NegZ }

    void Start()
    {
        ROSConnection.GetOrCreateInstance().Subscribe<TFMessageMsg>(tfTopic, OnTFReceived);
        
        AddLog($"🎯 ArUco Diagnostic Started");
        AddLog($"Listening: {worldFrame} → {robotFrame}");
        AddLog($"Press 1-9 to test coordinate modes");
        AddLog($"Press SPACE to freeze/unfreeze");
        AddLog($"Mode: {currentMode}");
    }

    void OnTFReceived(TFMessageMsg msg)
    {
        if (freezeUpdates) return;

        foreach (var tf in msg.transforms)
        {
            bool isCorrectTransform = 
                tf.header.frame_id.Contains(worldFrame) && 
                tf.child_frame_id.Contains(robotFrame);
            
            if (isCorrectTransform)
            {
                ProcessTransform(tf);
                connected = true;
                updateCount++;
                lastUpdateTime = Time.time;
                return;
            }
        }
    }

    void ProcessTransform(TransformStampedMsg tf)
    {
        // 1. Extract RAW ROS data
        Vector3 rosPos = new Vector3(
            (float)tf.transform.translation.x,
            (float)tf.transform.translation.y,
            (float)tf.transform.translation.z
        );

        Quaternion rosQuat = new Quaternion(
            (float)tf.transform.rotation.x,
            (float)tf.transform.rotation.y,
            (float)tf.transform.rotation.z,
            (float)tf.transform.rotation.w
        );

        // Store raw data
        rawROSPosition = rosPos;
        rawROSEuler = rosQuat.eulerAngles;

        // 2. Calculate velocities for movement analysis
        if (updateCount > 0)
        {
            rosVelocity = (rosPos - lastRawPosition) / Time.deltaTime;
            rosSpeed = rosVelocity.magnitude;
        }
        lastRawPosition = rosPos;

        // 3. Apply coordinate transformation
        targetPosition = TransformPosition(rosPos) * scaleMultiplier;
        targetRotation = TransformRotation(rosQuat);

        // 4. Calculate Unity velocities
        if (initialized)
        {
            unityVelocity = (targetPosition - lastUnityPosition) / Time.deltaTime;
            unitySpeed = unityVelocity.magnitude;
        }
        lastUnityPosition = targetPosition;

        // Store for display
        unityPosition = targetPosition;
        unityEuler = targetRotation.eulerAngles;

        // 5. Apply to transform
        if (!initialized)
        {
            transform.position = targetPosition;
            transform.rotation = targetRotation;
            initialized = true;
            AddLog($"✅ Initial pose set at {targetPosition.ToString("F2")}");
        }
        else if (!enableSmoothing)
        {
            transform.position = targetPosition;
            transform.rotation = targetRotation;
        }
    }

    Vector3 TransformPosition(Vector3 rosPos)
    {
        switch (currentMode)
        {
            case CoordinateMode.Mode1_StandardUnderwater:
                // Standard: ROS underwater (X-forward, Y-left, Z-down)
                return new Vector3(-rosPos.y, -rosPos.z, rosPos.x);

            case CoordinateMode.Mode2_YourObservation:
                // Your observation: "x is my y, y is my z, z is my x"
                return new Vector3(rosPos.y, rosPos.z, rosPos.x);

            case CoordinateMode.Mode3_DirectCopy:
                return new Vector3(rosPos.x, rosPos.y, rosPos.z);

            case CoordinateMode.Mode4_SwapXZ:
                return new Vector3(rosPos.z, rosPos.y, rosPos.x);

            case CoordinateMode.Mode5_NegateX:
                return new Vector3(-rosPos.x, rosPos.y, rosPos.z);

            case CoordinateMode.Mode6_NegateY:
                return new Vector3(rosPos.x, -rosPos.y, rosPos.z);

            case CoordinateMode.Mode7_NegateZ:
                return new Vector3(rosPos.x, rosPos.y, -rosPos.z);

            case CoordinateMode.Mode8_FlipYZ:
                return new Vector3(rosPos.x, rosPos.z, rosPos.y);

            case CoordinateMode.Mode9_Custom:
                return new Vector3(
                    GetAxisValue(rosPos, customX),
                    GetAxisValue(rosPos, customY),
                    GetAxisValue(rosPos, customZ)
                );

            default:
                return targetPosition;
        }
    }

    Quaternion TransformRotation(Quaternion rosQuat)
    {
        // Try standard underwater NED conversion first
        Quaternion unityQuat = new Quaternion(
            rosQuat.x,
            -rosQuat.y,
            rosQuat.z,
            -rosQuat.w
        );

        // If rotation seems wrong, we can add rotation modes too
        return unityQuat;
    }

    float GetAxisValue(Vector3 v, AxisMapping m)
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

    void Update()
    {
        // Apply smoothing
        if (enableSmoothing && initialized && !freezeUpdates)
        {
            transform.position = Vector3.Lerp(
                transform.position,
                targetPosition,
                1f - positionSmoothing
            );
            
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                1f - rotationSmoothing
            );
        }

        // Keyboard controls
        HandleInput();

        // Connection timeout check
        if (connected && (Time.time - lastUpdateTime) > 1f)
        {
            connected = false;
            AddLog("⚠️ Lost connection!");
        }
    }

    void HandleInput()
    {
        // Mode switching
        if (Input.GetKeyDown(KeyCode.Alpha1)) SwitchMode(CoordinateMode.Mode1_StandardUnderwater);
        if (Input.GetKeyDown(KeyCode.Alpha2)) SwitchMode(CoordinateMode.Mode2_YourObservation);
        if (Input.GetKeyDown(KeyCode.Alpha3)) SwitchMode(CoordinateMode.Mode3_DirectCopy);
        if (Input.GetKeyDown(KeyCode.Alpha4)) SwitchMode(CoordinateMode.Mode4_SwapXZ);
        if (Input.GetKeyDown(KeyCode.Alpha5)) SwitchMode(CoordinateMode.Mode5_NegateX);
        if (Input.GetKeyDown(KeyCode.Alpha6)) SwitchMode(CoordinateMode.Mode6_NegateY);
        if (Input.GetKeyDown(KeyCode.Alpha7)) SwitchMode(CoordinateMode.Mode7_NegateZ);
        if (Input.GetKeyDown(KeyCode.Alpha8)) SwitchMode(CoordinateMode.Mode8_FlipYZ);
        if (Input.GetKeyDown(KeyCode.Alpha9)) SwitchMode(CoordinateMode.Mode9_Custom);

        // Freeze toggle
        if (Input.GetKeyDown(KeyCode.Space))
        {
            freezeUpdates = !freezeUpdates;
            AddLog(freezeUpdates ? "⏸️ Updates FROZEN" : "▶️ Updates RESUMED");
        }

        // Reset smoothing
        if (Input.GetKeyDown(KeyCode.R))
        {
            transform.position = targetPosition;
            transform.rotation = targetRotation;
            AddLog("🔄 Smoothing reset");
        }

        // Toggle smoothing
        if (Input.GetKeyDown(KeyCode.S))
        {
            enableSmoothing = !enableSmoothing;
            AddLog($"Smoothing: {(enableSmoothing ? "ON" : "OFF")}");
        }
    }

    void SwitchMode(CoordinateMode newMode)
    {
        currentMode = newMode;
        AddLog($"🔄 Switched to: {newMode}");
        
        // Reset transform immediately to see the change
        if (initialized)
        {
            transform.position = targetPosition;
            transform.rotation = targetRotation;
        }
    }

    void AddLog(string message)
    {
        logMessages.Enqueue(message);
        if (logMessages.Count > MAX_LOG_MESSAGES)
            logMessages.Dequeue();
        
        Debug.Log($"[ArUco Diagnostic] {message}");
    }

    void OnGUI()
    {
        if (!showDetailedDebug) return;

        // Main diagnostic window
        float windowWidth = 500;
        float windowHeight = 480;
        GUI.Box(new Rect(10, 10, windowWidth, windowHeight), "");
        
        GUIStyle titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };
        GUIStyle headerStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold };
        GUIStyle normalStyle = new GUIStyle(GUI.skin.label) { fontSize = 11 };
        
        float x = 20;
        float y = 20;
        float lineHeight = 22;

        // Title
        GUI.Label(new Rect(x, y, windowWidth - 20, 25), "🎯 ARUCO LOCALIZATION DIAGNOSTIC", titleStyle);
        y += 30;

        // Connection Status
        GUI.contentColor = connected ? Color.green : Color.red;
        GUI.Label(new Rect(x, y, windowWidth - 20, lineHeight), 
            $"Status: {(connected ? "✅ CONNECTED" : "❌ NO DATA")} | Updates: {updateCount}", headerStyle);
        GUI.contentColor = Color.white;
        y += lineHeight + 5;

        // Current Mode
        GUI.contentColor = Color.yellow;
        GUI.Label(new Rect(x, y, windowWidth - 20, lineHeight), 
            $"Mode: {currentMode} (Press 1-9 to change)", normalStyle);
        GUI.contentColor = Color.white;
        y += lineHeight;

        GUI.Label(new Rect(x, y, windowWidth - 20, lineHeight), 
            $"Frozen: {(freezeUpdates ? "YES" : "NO")} (SPACE) | Smoothing: {(enableSmoothing ? "ON" : "OFF")} (S)", normalStyle);
        y += lineHeight + 5;

        // Raw ROS Data
        GUI.Label(new Rect(x, y, windowWidth - 20, lineHeight), "📡 RAW ROS DATA:", headerStyle);
        y += lineHeight;
        
        GUI.contentColor = Color.cyan;
        GUI.Label(new Rect(x + 10, y, windowWidth - 30, lineHeight), 
            $"Pos: X={rawROSPosition.x:F2} Y={rawROSPosition.y:F2} Z={rawROSPosition.z:F2}", normalStyle);
        y += lineHeight;
        
        GUI.Label(new Rect(x + 10, y, windowWidth - 30, lineHeight), 
            $"Rot: X={rawROSEuler.x:F0}° Y={rawROSEuler.y:F0}° Z={rawROSEuler.z:F0}°", normalStyle);
        GUI.contentColor = Color.white;
        y += lineHeight + 5;

        // Converted Unity Data
        GUI.Label(new Rect(x, y, windowWidth - 20, lineHeight), "🎮 UNITY DATA:", headerStyle);
        y += lineHeight;
        
        GUI.contentColor = Color.green;
        GUI.Label(new Rect(x + 10, y, windowWidth - 30, lineHeight), 
            $"Pos: X={unityPosition.x:F2} Y={unityPosition.y:F2} Z={unityPosition.z:F2}", normalStyle);
        y += lineHeight;
        
        GUI.Label(new Rect(x + 10, y, windowWidth - 30, lineHeight), 
            $"Rot: X={unityEuler.x:F0}° Y={unityEuler.y:F0}° Z={unityEuler.z:F0}°", normalStyle);
        GUI.contentColor = Color.white;
        y += lineHeight + 5;

        // Movement Analysis
        GUI.Label(new Rect(x, y, windowWidth - 20, lineHeight), "📈 MOVEMENT ANALYSIS:", headerStyle);
        y += lineHeight;
        
        GUI.Label(new Rect(x + 10, y, windowWidth - 30, lineHeight), 
            $"ROS Speed: {rosSpeed:F2} m/s | Unity Speed: {unitySpeed:F2}", normalStyle);
        y += lineHeight;
        
        GUI.Label(new Rect(x + 10, y, windowWidth - 30, lineHeight), 
            $"ROS Vel: {rosVelocity.ToString("F2")}", normalStyle);
        y += lineHeight;
        
        GUI.Label(new Rect(x + 10, y, windowWidth - 30, lineHeight), 
            $"Unity Vel: {unityVelocity.ToString("F2")}", normalStyle);
        y += lineHeight + 5;

        // Instructions
        GUI.Label(new Rect(x, y, windowWidth - 20, lineHeight), "⌨️ KEYBOARD CONTROLS:", headerStyle);
        y += lineHeight;
        
        GUI.contentColor = Color.yellow;
        GUI.Label(new Rect(x + 10, y, windowWidth - 30, lineHeight), "1-9: Switch coordinate mode", normalStyle);
        y += lineHeight;
        GUI.Label(new Rect(x + 10, y, windowWidth - 30, lineHeight), "SPACE: Freeze/Unfreeze updates", normalStyle);
        y += lineHeight;
        GUI.Label(new Rect(x + 10, y, windowWidth - 30, lineHeight), "R: Reset smoothing | S: Toggle smoothing", normalStyle);
        GUI.contentColor = Color.white;
        y += lineHeight + 5;

        // Recent log messages
        GUI.Label(new Rect(x, y, windowWidth - 20, lineHeight), "📋 LOG:", headerStyle);
        y += lineHeight;
        
        foreach (string log in logMessages)
        {
            GUI.Label(new Rect(x + 10, y, windowWidth - 30, lineHeight), log, normalStyle);
            y += lineHeight;
        }

        // Testing instructions box
        DrawTestingInstructions();
    }

    void DrawTestingInstructions()
    {
        float boxX = Screen.width - 420;
        float boxY = 10;
        float boxWidth = 410;
        float boxHeight = 320;
        
        GUI.Box(new Rect(boxX, boxY, boxWidth, boxHeight), "");
        
        GUIStyle titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
        GUIStyle normalStyle = new GUIStyle(GUI.skin.label) { fontSize = 11 };
        
        float x = boxX + 10;
        float y = boxY + 10;
        float lineHeight = 20;

        GUI.Label(new Rect(x, y, boxWidth - 20, 25), "🧪 TESTING PROCEDURE", titleStyle);
        y += 30;

        GUI.contentColor = Color.cyan;
        GUI.Label(new Rect(x, y, boxWidth - 20, lineHeight), "Step 1: MOVE ROBOT FORWARD", normalStyle);
        y += lineHeight;
        GUI.contentColor = Color.white;
        GUI.Label(new Rect(x + 10, y, boxWidth - 30, lineHeight * 2), 
            "Watch which Unity axis changes.\nThat axis = ROS X", normalStyle);
        y += lineHeight * 2 + 5;

        GUI.contentColor = Color.cyan;
        GUI.Label(new Rect(x, y, boxWidth - 20, lineHeight), "Step 2: MOVE ROBOT LEFT", normalStyle);
        y += lineHeight;
        GUI.contentColor = Color.white;
        GUI.Label(new Rect(x + 10, y, boxWidth - 30, lineHeight * 2), 
            "Watch which Unity axis changes.\nThat axis = ROS Y", normalStyle);
        y += lineHeight * 2 + 5;

        GUI.contentColor = Color.cyan;
        GUI.Label(new Rect(x, y, boxWidth - 20, lineHeight), "Step 3: MOVE ROBOT UP", normalStyle);
        y += lineHeight;
        GUI.contentColor = Color.white;
        GUI.Label(new Rect(x + 10, y, boxWidth - 30, lineHeight * 2), 
            "Watch which Unity axis changes.\nThat axis = ROS Z", normalStyle);
        y += lineHeight * 2 + 5;

        GUI.contentColor = Color.cyan;
        GUI.Label(new Rect(x, y, boxWidth - 20, lineHeight), "Step 4: TEST MODES", normalStyle);
        y += lineHeight;
        GUI.contentColor = Color.white;
        GUI.Label(new Rect(x + 10, y, boxWidth - 30, lineHeight * 3), 
            "Press 1-9 to test different mappings.\nFreeze with SPACE to analyze.\nFind the mode where movement matches!", normalStyle);
        y += lineHeight * 3 + 5;

        GUI.contentColor = Color.yellow;
        GUI.Label(new Rect(x, y, boxWidth - 20, lineHeight * 2), 
            "💡 TIP: Start with Mode 1 or Mode 2.\nWatch the velocity arrows in Scene view!", normalStyle);
        GUI.contentColor = Color.white;
    }

    void OnDrawGizmos()
    {
        if (!drawGizmos || !initialized) return;

        Vector3 pos = transform.position;

        // Draw position sphere
        Gizmos.color = connected ? Color.green : Color.red;
        Gizmos.DrawWireSphere(pos, 0.3f);

        // Draw local coordinate axes (Robot frame)
        float axisLength = 1.0f;
        Gizmos.color = Color.red;
        Gizmos.DrawRay(pos, transform.right * axisLength);  // X (Red)
        Gizmos.color = Color.green;
        Gizmos.DrawRay(pos, transform.up * axisLength);     // Y (Green)
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(pos, transform.forward * axisLength); // Z (Blue)

        // Draw velocity vectors
        if (connected && unitySpeed > 0.01f)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(pos, unityVelocity.normalized * 2f);
            
            // Draw a sphere at velocity tip
            Gizmos.DrawSphere(pos + unityVelocity.normalized * 2f, 0.1f);
        }

        // Draw trail (last few positions)
        if (updateCount > 10)
        {
            Gizmos.color = new Color(0, 1, 1, 0.3f);
            Gizmos.DrawLine(lastUnityPosition, pos);
        }
    }
}
