// using UnityEngine;
// using System;

// public class CameraCapturer : MonoBehaviour
// {
//     public bool IsCaptureEnable = false;
//     Camera _camera;
//     public int resWidth;
//     public int resHeight;
//     public byte[] jpg;


//     void Start()
//     {
//         this._camera = GetComponent<Camera>();
//     }

//     private void FixedUpdate()
//     {
//         if(this.IsCaptureEnable){
//             this.jpg = getJPGFromCurrentCamera();
//             this.IsCaptureEnable = false;
//         }
//     }

//     public byte[] getCapturedJpegImage()
//     {
//         this.IsCaptureEnable = true;
//         while(this.IsCaptureEnable){ }
//         if(this.jpg != null){
//             return this.jpg;
//         }
//         return null;
//     }


//     private byte[] getJPGFromCurrentCamera()
//     {
//         try{
//             RenderTexture rt = new RenderTexture(resWidth, resHeight, 24);
//             _camera.targetTexture = rt;
//             Texture2D screenShot = new Texture2D(resWidth, resHeight, TextureFormat.RGB24, false);
//             _camera.Render();
//             RenderTexture.active = rt;
//             screenShot.ReadPixels(new Rect(0,0,resWidth,resHeight), 0, 0);
//             _camera.targetTexture = null;
//             RenderTexture.active = null;
//             Destroy(rt);
//             byte[] bytes = screenShot.EncodeToJPG();
//             return bytes;
//         }
//         catch(Exception e){
//             return null;
//         }
//     }
// }


using UnityEngine;

public class CameraCapturer : MonoBehaviour
{
    public int resWidth = 640;
    public int resHeight = 480;

    private Camera cam;
    private RenderTexture rt;
    private Texture2D tex;

    void Start()
    {
        cam = GetComponent<Camera>();
        if (resWidth <= 0) resWidth = 640;
        if (resHeight <= 0) resHeight = 480;

        rt  = new RenderTexture(resWidth, resHeight, 24);
        tex = new Texture2D(resWidth, resHeight, TextureFormat.RGB24, false);
    }

    // Returns raw RGB bytes for ImageMsg
    public byte[] GetRGBImage()
    {
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;

        tex.ReadPixels(new Rect(0, 0, resWidth, resHeight), 0, 0);
        tex.Apply();

        // Flip vertically for ROS (bottom-left -> top-left)
        Color32[] pixels = tex.GetPixels32();
        Color32[] flipped = new Color32[pixels.Length];
        for (int y = 0; y < resHeight; y++)
        {
            int srcRow = y * resWidth;
            int dstRow = (resHeight - 1 - y) * resWidth;
            for (int x = 0; x < resWidth; x++)
                flipped[dstRow + x] = pixels[srcRow + x];
        }
        tex.SetPixels32(flipped);
        tex.Apply();

        RenderTexture.active = null;
        cam.targetTexture = null;

        return tex.GetRawTextureData();
    }
}
