using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Sensor;

public class CameraRosPublisher : MonoBehaviour
{
    public string topic = "/bluerov2/camera/image";
    public int publishHz = 10;

    private ROSConnection ros;
    private CameraCapturer capturer;
    private float timer = 0f;

    void Start()
    {
        ros = ROSConnection.GetOrCreateInstance();
        capturer = GetComponent<CameraCapturer>();

        ros.RegisterPublisher<ImageMsg>(topic); // msg type Image
    }

    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= 1f / publishHz)
        {
            timer = 0f;
            PublishImage();
        }
    }

    void PublishImage()
    {
        byte[] rgb = capturer.GetRGBImage();
        int width = capturer.resWidth;
        int height = capturer.resHeight;

        var msg = new ImageMsg
        {
            height = (uint)height,
            width = (uint)width,
            encoding = "rgb8",
            is_bigendian = 0,
            step = (uint)(width * 3), 
            data = rgb
        };

        ros.Publish(topic, msg);
    }
}
