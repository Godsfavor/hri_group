using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Nav;
using RosMessageTypes.Geometry;

public class BlueRovOdomMirrorRosSub : MonoBehaviour
{
    [Header("ROS Topic (from your bag)")]
    public string topic = "/bluerov2/global_position/local";

    [Header("Options")]
    public bool useFirstMessageAsOrigin = true;   // makes motion start near (0,0,0) in Unity
    public bool mirrorRotationYawOnly = true;     // simplest + usually enough
    public float positionSmoothing = 0.0f;        // 0 = no smoothing

    private bool _haveOrigin = false;
    private Vector3 _originUnityPos;

    void Start()
    {
        ROSConnection.GetOrCreateInstance().Subscribe<OdometryMsg>(topic, OnOdom);
    }

    void OnOdom(OdometryMsg msg)
    {
        // --- POSITION ---
        PointMsg p = msg.pose.pose.position;

        // Mapping consistent with your existing cmd_vel mapping:
        // Unity (x,y,z) = (-ROS.x, ROS.z, ROS.y)
        Vector3 unityPos = new Vector3(
            -(float)p.x,
             (float)p.z,
             (float)p.y
        );

        if (useFirstMessageAsOrigin && !_haveOrigin)
        {
            _originUnityPos = unityPos;
            _haveOrigin = true;
        }

        if (useFirstMessageAsOrigin)
            unityPos -= _originUnityPos;

        if (positionSmoothing > 0f)
            transform.position = Vector3.Lerp(transform.position, unityPos, positionSmoothing);
        else
            transform.position = unityPos;

        // --- ROTATION ---
        QuaternionMsg q = msg.pose.pose.orientation;

        if (mirrorRotationYawOnly)
        {
            // Compute ROS yaw (rotation around ROS Z/up)
            float yawRad = YawFromQuaternion((float)q.x, (float)q.y, (float)q.z, (float)q.w);

            // Apply to Unity Y/up. Sign might need flipping depending on your frames.
            transform.rotation = Quaternion.Euler(0f, -yawRad * Mathf.Rad2Deg, 0f);
        }
        // If you later want full 3D rotation mirroring (roll/pitch/yaw), we can add it carefully.
    }

    // Standard yaw extraction from quaternion (right-handed, ROS convention)
    private float YawFromQuaternion(float x, float y, float z, float w)
    {
        // yaw (z-axis rotation)
        float siny_cosp = 2f * (w * z + x * y);
        float cosy_cosp = 1f - 2f * (y * y + z * z);
        return Mathf.Atan2(siny_cosp, cosy_cosp);
    }
}



