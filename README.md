# BlueROV2 Unity Connection - Real Robot

Quick guide to connect Unity to your actual BlueROV2.

## What You Need Running

### 1. ROS Side (On your robot/computer)
Start these in order:

```bash
# First, start the ROS-Unity bridge
ros2 run ros_tcp_endpoint default_server_endpoint

# Then launch your robot stuff:
ros2 launch [your_package] mavros.launch.py          # Connects to the ROV
ros2 launch [your_package] gamepad.launch.py         # Controller reading
ros2 launch [your_package] init_joy.launch.py        # Manual joystick control
ros2 launch [your_package] yolo_detection.launch.py  # Camera + visual servoing
# OR if you just want YOLO without servoing:
ros2 launch [your_package] GF_yolo.launch.py

# For ArUco localization:
ros2 launch [your_package] localization.launch.py
# Note: You might need to cd into the project folder first if paths are weird
```

### 2. Unity Side
Just press Play. That's it.

## What's Actually Happening

The Unity scene has three scripts enabled by default:

### aruco_unity_Pose_subscriber.cs
- Listens to `/tf` topic for robot position from ArUco markers
- Shows where your robot is in 3D space based on marker detection
- Only works when ArUco markers are visible to the camera

### MAVROSIMUSubscriber_Fixed.cs
- Gets orientation data from the IMU sensor
- This ALWAYS works - doesn't need GPS or anything fancy
- Shows which way the robot is tilted/rotated
- Good for testing if your connection is alive

### MAVROSDiagnostic.cs
- Shows connection status in a debug window
- Tells you if data is actually coming through
- Displays message counts so you know things are working

## Debug Windows Explained

When you run Unity, you'll see some overlay windows:

**MAVROS Connection Diagnostic** (main window)
- Shows if you're connected to ROS
- Displays ROS IP and port
- Two status lines:
  - `/bluerov2/imu/data` - Should say "RECEIVING" (this always works)
  - `/bluerov2/local_position/pose` - May say "NO DATA" (needs GPS/positioning)

**ArUco Localization** (smaller window)
- Status: Connected or not
- Position: Where the robot is in 3D space
- Rotation: Which way it's facing
- If it says "Waiting for ArUco markers" - the camera can't see any markers

**IMU Data** (right side)
- Shows raw sensor data
- Orient: Current tilt angles
- Accel: Acceleration forces
- Message count keeps going up if it's working

## Troubleshooting

**"ROS Connection: DISCONNECTED"**
- Check if `ros_tcp_endpoint` is running
- Make sure Unity's ROS Settings has the right IP (usually localhost or 127.0.0.1)

**"CONNECTED BUT NO TOPICS RECEIVED"**
- Your launch files might not be running
- Check `ros2 topic list` to see what's publishing

**IMU works but no position data**
- Normal! Position needs ArUco markers or GPS
- Just use IMU data for orientation, it's good enough for testing

**"Waiting for ArUco markers"**
- Camera can't see the markers on the pool floor
- Move the robot or adjust camera angle
- Make sure markers aren't covered/dirty

## What Topics Are Being Used

- `/tf` - Robot position from ArUco localization
- `/bluerov2/imu/data` - Orientation and motion data
- `/bluerov2/local_position/pose` - GPS/positioning (if you have it)
- `/bluerov2/cmd_vel` - Movement commands (if you're controlling from Unity)
- Camera topics (if running YOLO)


## Quick Connection Test

1. Start `ros_tcp_endpoint`
2. Press Play in Unity
3. Look at the diagnostic window
4. If IMU message count is increasing → You're connected!
5. If position updates are coming → ArUco is working!

If the IMU count is going up, your connection is good.
