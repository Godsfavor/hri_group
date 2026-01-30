using UnityEngine;
using System;

/// <summary>
/// Controls BlueROV2 movement based on ROS Twist messages (cmd_vel)
/// Handles coordinate system conversion from ROS to Unity
/// </summary>
public class BlueRovVeloControl : MonoBehaviour 
{
    [Header("Current Velocity Values")]
    [Tooltip("Linear velocity in X direction (m/s)")]
    public float lvx = 0.0f;
    
    [Tooltip("Linear velocity in Y direction (m/s)")]
    public float lvy = 0.0f;
    
    [Tooltip("Linear velocity in Z direction (m/s)")]
    public float lvz = 0.0f;
    
    [Tooltip("Angular velocity around Z axis (rad/s)")]
    public float avz = 0.0f;
    
    [Header("Status")]
    [Tooltip("Whether movement commands are being processed")]
    public bool movementActive = false;
    
    [Header("Settings")]
    [Tooltip("Multiplier for movement speed (adjust if robot moves too fast/slow)")]
    public float speedMultiplier = 1.0f;
    
    [Tooltip("Multiplier for rotation speed")]
    public float rotationMultiplier = 1.0f;
    
    [Header("References")]
    public Rigidbody rb;

    void Start() 
    {
        // Get Rigidbody component if not assigned
        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }

        if (rb != null)
        {
            // Configure Rigidbody for kinematic control
            rb.isKinematic = true; // We're controlling position directly
            Debug.Log("BlueROV velocity control initialized");
        }
        else
        {
            Debug.LogWarning("No Rigidbody found - using Transform.Translate instead");
        }
    }

    /// <summary>
    /// Applies the current velocity to move the robot
    /// Called every physics frame when movement is active
    /// </summary>
    private void moveVelocityRigidbody() 
    {
        // ROS coordinate system: X-forward, Y-left, Z-up
        // Unity coordinate system: X-right, Y-up, Z-forward
        // 
        // Conversion:
        // ROS X (forward) -> Unity Z (forward)
        // ROS Y (left) -> Unity -X (right)
        // ROS Z (up) -> Unity Y (up)
        
        Vector3 movement = new Vector3(
            -lvy * Time.fixedDeltaTime * speedMultiplier,  // ROS Y -> Unity -X
            lvz * Time.fixedDeltaTime * speedMultiplier,   // ROS Z -> Unity Y
            lvx * Time.fixedDeltaTime * speedMultiplier    // ROS X -> Unity Z
        );
        
        transform.Translate(movement, Space.Self);
        
        // Rotation around vertical axis (yaw)
        // ROS uses right-hand rule, Unity uses left-hand
        // Convert degrees to match Unity's rotation system
        float rotationDegrees = avz * Time.fixedDeltaTime * Mathf.Rad2Deg * rotationMultiplier;
        transform.Rotate(0, rotationDegrees, 0, Space.Self);
    }

    /// <summary>
    /// Called when a new velocity command is received from ROS
    /// Stores the velocity values for application in FixedUpdate
    /// </summary>
    public void moveVelocity(RosMessageTypes.Geometry.TwistMsg velocityMessage) 
    {
        // Extract linear velocities
        this.lvx = (float)velocityMessage.linear.x;
        this.lvy = (float)velocityMessage.linear.y;
        this.lvz = (float)velocityMessage.linear.z;
        
        // Extract angular velocity (yaw)
        this.avz = (float)velocityMessage.angular.z;
        
        // Mark movement as active for this frame
        this.movementActive = true;
    }

    /// <summary>
    /// FixedUpdate is called at a fixed rate (physics timestep)
    /// Use this for consistent physics-based movement
    /// </summary>
    void FixedUpdate() 
    {
        if (movementActive) 
        {
            moveVelocityRigidbody();
        }
        
        // Reset movement flag - will be set again if new command arrives
        this.movementActive = false;
    }

    /// <summary>
    /// Draws velocity vectors in the Scene view for debugging
    /// </summary>
    void OnDrawGizmos()
    {
        if (!Application.isPlaying || !movementActive)
            return;

        // Draw linear velocity vector
        Vector3 linearVel = new Vector3(-lvy, lvz, lvx) * speedMultiplier;
        Gizmos.color = Color.green;
        Gizmos.DrawRay(transform.position, linearVel);
        
        // Draw angular velocity indicator
        if (Mathf.Abs(avz) > 0.01f)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(transform.position, 0.2f);
        }
    }
}

