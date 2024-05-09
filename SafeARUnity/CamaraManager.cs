using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Stopwatch = System.Diagnostics.Stopwatch;

public class CamaraManager : MonoBehaviour
{
    public RawImage display; // Assign this in the Inspector
    private WebCamTexture webCamTexture;
    private Texture2D obfuscatedTexture;
    public Dictionary<int, Obfuscation.Type> obfuscationTypes;

    [Header("Image Obfuscator")]
    [SerializeField]
    private ImgObfuscator imgObfuscator;

    void Start()
    {
        InitializeCamera();

        obfuscationTypes = new Dictionary<int, Obfuscation.Type>
        {
            { 0, Obfuscation.Type.Masking },     // person
            { 1, Obfuscation.Type.Masking },     // bicycle
            { 53, Obfuscation.Type.Pixelation }, // pizza
            { 67, Obfuscation.Type.Masking },    // cell phone
        };
    }

    void Update()
    {

        // Check if the camera is playing
        if (webCamTexture.isPlaying && webCamTexture.didUpdateThisFrame)
        {
            
            // Debug.Log(webCamTexture == null ? "WebCamTexture is null" : "WebCamTexture is not null");
            // Debug.Log(obfuscationTypes == null ? "obfuscationTypes is null" : "obfuscationTypes is not null");
            // var stopwatch1 = new Stopwatch();
            // stopwatch1.Start();
            
            obfuscatedTexture = imgObfuscator.Run(webCamTexture, obfuscationTypes);
            
            // stopwatch1.Stop();
            // Debug.Log("Total time: " + stopwatch1.ElapsedMilliseconds + "ms");
            // ImageWriter.WriteTexture2DToPNG(obfuscatedTexture, "Assets/DebugOutputs/obfuscated.png");
            display.texture = obfuscatedTexture;
        }
    }

    void InitializeCamera()
    {
        WebCamDevice[] devices = WebCamTexture.devices;
        if (devices.Length > 0)
        {
            // Use the first available camera
            webCamTexture = new WebCamTexture(devices[0].name, 640, 480);
            display.texture = webCamTexture;
            webCamTexture.Play(); // Start the camera
        }
        else
        {
            Debug.LogError("No camera found.");
        }
    }

    void OnDestroy()
    {
        // Stop the camera when the application is closed or the object is destroyed
        if (webCamTexture != null)
        {
            webCamTexture.Stop();
        }

        // Destroy the obfuscated texture
        if (obfuscatedTexture != null)
        {
            Destroy(obfuscatedTexture);
        }
    }
}
