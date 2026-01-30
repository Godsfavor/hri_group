using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Sensor;
using UnityEngine.UI;

/// <summary>
/// Subscribes to camera image topic and displays it in Unity UI
/// Works with standard ROS sensor_msgs/Image messages
/// </summary>
public class CameraImageSubscriber : MonoBehaviour
{
    [Header("ROS Settings")]
    [Tooltip("Camera image topic to subscribe to")]
    public string topic = "/bluerov2/camera/image";
    
    [Header("Display Settings")]
    [Tooltip("UI RawImage component to display the camera feed")]
    public RawImage displayImage;
    
    [Tooltip("Flip image horizontally")]
    public bool flipHorizontal = false;
    
    [Tooltip("Flip image vertically")]
    public bool flipVertical = false;

    private Texture2D texture;
    private bool isSubscribed = false;

    void Start()
    {
        // Validate display image
        if (displayImage == null)
        {
            Debug.LogWarning("CameraImageSubscriber: No RawImage assigned! Please assign a UI RawImage component.");
            enabled = false;
            return;
        }

        // Subscribe to camera topic
        ROSConnection.GetOrCreateInstance().Subscribe<ImageMsg>(topic, UpdateImage);
        isSubscribed = true;
        Debug.Log($"Subscribed to camera topic: {topic}");
    }

    /// <summary>
    /// Called when a new camera image is received
    /// </summary>
    void UpdateImage(ImageMsg imageMsg)
    {
        try
        {
            // Create or resize texture if needed
            if (texture == null || 
                texture.width != (int)imageMsg.width || 
                texture.height != (int)imageMsg.height)
            {
                CreateTexture((int)imageMsg.width, (int)imageMsg.height);
            }

            // Convert and load image data based on encoding
            switch (imageMsg.encoding.ToLower())
            {
                case "rgb8":
                    LoadRGB8(imageMsg.data);
                    break;
                    
                case "bgr8":
                    LoadBGR8(imageMsg.data);
                    break;
                    
                case "rgba8":
                    LoadRGBA8(imageMsg.data);
                    break;
                    
                case "bgra8":
                    LoadBGRA8(imageMsg.data);
                    break;
                    
                case "mono8":
                case "8uc1":
                    LoadMono8(imageMsg.data);
                    break;
                    
                default:
                    Debug.LogWarning($"Unsupported image encoding: {imageMsg.encoding}");
                    return;
            }

            // Apply flipping if needed
            if (flipHorizontal || flipVertical)
            {
                FlipTexture();
            }

            texture.Apply();
            displayImage.texture = texture;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error updating camera image: {e.Message}");
        }
    }

    /// <summary>
    /// Creates a new texture with the specified dimensions
    /// </summary>
    void CreateTexture(int width, int height)
    {
        if (texture != null)
        {
            Destroy(texture);
        }

        texture = new Texture2D(width, height, TextureFormat.RGB24, false);
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        
        Debug.Log($"Created texture: {width}x{height}");
    }

    /// <summary>
    /// Loads RGB8 encoded image (most common)
    /// </summary>
    void LoadRGB8(byte[] data)
    {
        texture.LoadRawTextureData(data);
    }

    /// <summary>
    /// Loads BGR8 encoded image (common in OpenCV/ROS)
    /// Converts BGR to RGB
    /// </summary>
    void LoadBGR8(byte[] data)
    {
        byte[] rgb = new byte[data.Length];
        for (int i = 0; i < data.Length; i += 3)
        {
            rgb[i] = data[i + 2];     // B -> R
            rgb[i + 1] = data[i + 1]; // G -> G
            rgb[i + 2] = data[i];     // R -> B
        }
        texture.LoadRawTextureData(rgb);
    }

    /// <summary>
    /// Loads RGBA8 encoded image
    /// </summary>
    void LoadRGBA8(byte[] data)
    {
        // Need to recreate texture with alpha channel
        if (texture.format != TextureFormat.RGBA32)
        {
            CreateTextureWithFormat(texture.width, texture.height, TextureFormat.RGBA32);
        }
        texture.LoadRawTextureData(data);
    }

    /// <summary>
    /// Loads BGRA8 encoded image
    /// Converts BGRA to RGBA
    /// </summary>
    void LoadBGRA8(byte[] data)
    {
        // Need to recreate texture with alpha channel
        if (texture.format != TextureFormat.RGBA32)
        {
            CreateTextureWithFormat(texture.width, texture.height, TextureFormat.RGBA32);
        }

        byte[] rgba = new byte[data.Length];
        for (int i = 0; i < data.Length; i += 4)
        {
            rgba[i] = data[i + 2];     // B -> R
            rgba[i + 1] = data[i + 1]; // G -> G
            rgba[i + 2] = data[i];     // R -> B
            rgba[i + 3] = data[i + 3]; // A -> A
        }
        texture.LoadRawTextureData(rgba);
    }

    /// <summary>
    /// Loads Mono8 (grayscale) encoded image
    /// Converts to RGB by duplicating gray value
    /// </summary>
    void LoadMono8(byte[] data)
    {
        byte[] rgb = new byte[data.Length * 3];
        for (int i = 0; i < data.Length; i++)
        {
            rgb[i * 3] = data[i];     // R
            rgb[i * 3 + 1] = data[i]; // G
            rgb[i * 3 + 2] = data[i]; // B
        }
        texture.LoadRawTextureData(rgb);
    }

    /// <summary>
    /// Creates texture with specific format
    /// </summary>
    void CreateTextureWithFormat(int width, int height, TextureFormat format)
    {
        if (texture != null)
        {
            Destroy(texture);
        }

        texture = new Texture2D(width, height, format, false);
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
    }

    /// <summary>
    /// Flips the texture horizontally and/or vertically
    /// </summary>
    void FlipTexture()
    {
        Color[] pixels = texture.GetPixels();
        Color[] flipped = new Color[pixels.Length];
        int width = texture.width;
        int height = texture.height;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int srcX = flipHorizontal ? (width - 1 - x) : x;
                int srcY = flipVertical ? (height - 1 - y) : y;
                flipped[y * width + x] = pixels[srcY * width + srcX];
            }
        }

        texture.SetPixels(flipped);
    }

    void OnDisable()
    {
        if (texture != null)
        {
            Destroy(texture);
            texture = null;
        }
    }

    void OnDestroy()
    {
        if (texture != null)
        {
            Destroy(texture);
        }
    }
}

