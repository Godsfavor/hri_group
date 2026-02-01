# BlueROV2 Unity Visualization Project

## Project Summary

This project connects a BlueROV2 underwater robot to Unity for real-time 3D visualization and monitoring. The robot is controlled with a physical joystick, and all movement and sensor data can be viewed live in the Unity interface.

GitHub Repository: https://github.com/Godsfavor/hri_group.git

## What It Does

The system bridges ROS2 (running on the robot) with Unity (for visualization) using the ROS-TCP-Connector. You fly the robot around with a joystick, and Unity shows you exactly where it is and what it's doing in real-time.

### Key Features

- **Real-time 3D position tracking** using ArUco marker localization
- **Live orientation data** from the robot's IMU sensor
- **Visual feedback** with on-screen diagnostic panels
- **Joystick control** with Unity visualization
- **Automatic coordinate conversion** from ROS underwater frame to Unity's coordinate system

## The User Interface

When you run the project, you'll see three diagnostic windows overlaid on the Unity scene:

### 1. MAVROS Connection Diagnostic (Main Window - Left Side)
This is your main status panel. It shows:
- **ROS Connection status** - Green "CONNECTED" or red "DISCONNECTED"
- **ROS IP and port** - Where Unity is talking to
- **IMU data status** - Shows if sensor data is coming through (should always say "RECEIVING")
- **Pose data status** - GPS/positioning data if available
- **Message counts** - Numbers that keep going up mean data is flowing
- **Timestamps** - Last time each type of data was received
- **Diagnostic messages** - Tells you what's wrong if something isn't working

### 2. ArUco Localization Window (Left Side, Below Main)
This tracks the robot's position based on camera marker detection:
- **Connection status** - Connected or waiting for markers
- **Update counter** - How many position updates received
- **Position (X, Y, Z)** - Where the robot is in 3D space
- **Rotation (X, Y, Z)** - Which way the robot is facing
- **Warning messages** - If markers aren't visible

### 3. IMU Data Window (Right Side)
Shows raw sensor data from the robot's motion sensor:
- **Status** - Receiving or no data
- **Message count** - Total IMU updates received
- **Orientation** - Tilt angles (roll, pitch, yaw)
- **Acceleration** - Motion forces in X, Y, Z

All three windows update in real-time as the robot moves around.

## How the Movement Works

1. **You control the robot** - Use the joystick/gamepad to fly the BlueROV2
2. **ROS handles the control** - The `init_joy` launch file reads joystick inputs and sends movement commands
3. **Position tracking happens** - ArUco markers on the pool floor are detected by the robot's camera
4. **Unity receives the data** - The ROS-TCP bridge sends position/orientation to Unity
5. **You see it visualized** - The 3D robot model moves in Unity to match the real robot

It's basically like watching a live GPS map of your robot, but in full 3D.

## Main Scripts

The project uses three main C# scripts that are always enabled:

**aruco_unity_Pose_subscriber.cs**
- Subscribes to the `/tf` topic for position data
- Gets the robot's location from ArUco marker detection
- Updates the 3D model position in Unity
- Shows the "ARUCO LOCALIZATION" window

**MAVROSIMUSubscriber_Fixed.cs**
- Subscribes to `/bluerov2/imu/data` for orientation
- Shows which way the robot is tilted/rotated
- This always works, even without markers
- Shows the "IMU DATA" window

**MAVROSDiagnostic.cs**
- Monitors the ROS connection health
- Checks if data is actually flowing
- Shows connection troubleshooting info
- Shows the "MAVROS CONNECTION DIAGNOSTIC" window

## Technical Details

**Coordinate System Conversion**
The trickiest part was converting between ROS's underwater coordinate system (X-forward, Y-left, Z-down) and Unity's system (X-right, Y-up, Z-forward). The scripts handle this automatically so the robot moves correctly in 3D space.

**Topics Used**
- `/tf` - Position from ArUco localization
- `/bluerov2/imu/data` - Orientation and motion sensor
- `/bluerov2/local_position/pose` - GPS-based positioning (if available)
- `/bluerov2/cmd_vel` - Movement commands from joystick

## What Makes This Useful

Instead of staring at a bunch of terminal windows with scrolling numbers, you get a clean 3D view of what your robot is actually doing. The diagnostic panels tell you immediately if something's wrong with the connection or sensors. It's way easier to understand "the robot is at position (2.5, 0.3, 1.8)" when you can actually see it in 3D space.

Plus, the whole thing updates in real-time - no lag, no playback. What you see is what's happening right now.

## Running It

1. Start the ROS-TCP endpoint
2. Launch your robot nodes (MAVROS, gamepad, localization, etc.)
3. Press Play in Unity
4. Start flying with the joystick
5. Watch the Unity interface show everything in real-time

It's a live 3D monitor for your underwater robot.
