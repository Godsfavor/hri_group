using UnityEngine;
using Unity.Robotics.ROSTCPConnector;

/// <summary>
/// Subscribes to the cmd_vel (command velocity) topic
/// This makes the BlueROV2 model move according to velocity commands in the rosbag
/// </summary>
public class BlueRovCmdVeloRosSub : MonoBehaviour
{
    [Header("ROS Settings")]
    [Tooltip("The cmd_vel topic - must match your rosbag topic name")]
    public string topic = "/bluerov2/cmd_vel";  // Updated to match your rosbag!
    
    [Header("References")]
    [Tooltip("Reference to the velocity controller component")]
    public BlueRovVeloControl blueRovVeloControl;

    void Start()
    {
        // Get the velocity control component if not assigned
        if (blueRovVeloControl == null)
        {
            blueRovVeloControl = GetComponent<BlueRovVeloControl>();
        }

        if (blueRovVeloControl == null)
        {
            Debug.LogError("BlueRovVeloControl component not found! Please attach it to this GameObject.");
            enabled = false;
            return;
        }

        // Subscribe to the cmd_vel topic
        ROSConnection.GetOrCreateInstance().Subscribe<RosMessageTypes.Geometry.TwistMsg>(
            topic, 
            VelocityChange
        );
        
        Debug.Log($"Subscribed to {topic} - BlueROV2 will move according to rosbag commands");
    }

    /// <summary>
    /// Called whenever a new velocity command is received from ROS
    /// </summary>
    void VelocityChange(RosMessageTypes.Geometry.TwistMsg velocityMessage)
    {
        // Pass the velocity command to the controller
        blueRovVeloControl.moveVelocity(velocityMessage);
        
        // Optional: Log velocity for debugging
        // Uncomment the line below if you want to see velocity values in the console
        // Debug.Log($"Velocity - Linear: ({velocityMessage.linear.x:F2}, {velocityMessage.linear.y:F2}, {velocityMessage.linear.z:F2}), " +
        //           $"Angular: ({velocityMessage.angular.x:F2}, {velocityMessage.angular.y:F2}, {velocityMessage.angular.z:F2})");
    }
}

