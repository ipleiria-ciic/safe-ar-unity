using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.Sentis;
using UnityEngine;
using UnityEngine.UI;
using Stopwatch = System.Diagnostics.Stopwatch;

public class ARCameraManager : MonoBehaviour
{
    //--- Camera --------------------
    [Header("AR_Camera")]
    [SerializeField]
    private GameObject arCamera;

    [SerializeField]
    private GameObject arCameraButtonBack;
    private Camera arCameraComponent;
    public static ARCameraManager Instance { get; private set; }

    [Header("Map, Player and RenderTexture")]
    [SerializeField]
    private GameObject map;

    [SerializeField]
    private GameObject player;

    [SerializeField]
    private RenderTexture renderTexture;

    // --- Obfuscation --------------
    public Dictionary<int, Obfuscation.Type> obfuscationTypes;

    [Header("SafeARLayer Obfuscation")]
    [SerializeField]
    private ImgObfuscator imgObfuscator;

    [SerializeField]
    private UnityEngine.UI.Image imageUI;

    [Header("Debug Plane Output")]
    private Texture2D outputTexture;
    public GameObject toPlaneImage;


    private Texture2D currentFrame;

    [Header("Screen Renderization")]
    public Material screenMaterial;

    //--- Still Image (DEBUG) -------
    public int frameInterval = 100; // to change the image every 100 frames
    private readonly int frameCount = 0;
    private readonly List<string> imagePaths;
    private int currentImageIndex = 0;

    void Start()
    {
        // ------------------------
        // AR Camera Initialization
        // ------------------------

        // GameObject xrOrigin = GameObject.Find("XR Origin"); // It must be ACTIVE to get the camera component !!!
        // Transform cameraOffset = xrOrigin.transform.Find("Camera Offset");
        // Transform mainCameraAR = cameraOffset.Find("Main Camera AR");
        // arCameraComponent = mainCameraAR.GetComponent<Camera>();

        // ---------------------------
        // Dummy Camera Initialization
        // ---------------------------

        GameObject dummyCamera = GameObject.Find("DummyCam");
        arCameraComponent = dummyCamera.GetComponent<Camera>();

        renderTexture = RenderTexture.GetTemporary(
            arCameraComponent.pixelWidth,
            arCameraComponent.pixelHeight,
            24
        );

        arCameraComponent.targetTexture = renderTexture;

        // ------------------------
        // For rotating Image Input
        // ------------------------

        // // Initialize your list of image paths here
        // imagePaths = new List<string>
        // {
        //     Application.dataPath + "/Test_Images/bic_people.jpeg",
        //     Application.dataPath + "/Test_Images/barbara.jpeg",
        //     Application.dataPath + "/Test_Images/person2.jpg",
        //     Application.dataPath + "/Test_Images/test_img3.png",
        //     Application.dataPath + "/Test_Images/test_img2.png",
        //     Application.dataPath + "/Test_Images/test_img.png",
        //     Application.dataPath + "/Test_Images/test_img.png",
        //     Application.dataPath + "/Test_Images/lebron.jpeg",
        //     Application.dataPath + "/Test_Images/person6.jpg",
        //     Application.dataPath + "/Test_Images/pessoa_juridica.jpg",
        //     Application.dataPath + "/Test_Images/bicy.jpg",
        // };

        // // Load the image and set it as the current frame
        // var imgPath = Application.dataPath + "/Test_Images/test_img2_full_size.png";
        // currentFrame = ImgUtils.LoadTextureFromImage(imagePath: imgPath);

        // ---------------------------------------------------------------------
        // Obfuscation Mapping: dict. with {class_id, Obfuscation.Type} format
        // Options: Masking, Pixelation, Blurring or None
        // We can add pixelSize, blurSize, and maskColor and alpha as parameters
        // ---------------------------------------------------------------------
        obfuscationTypes = new Dictionary<int, Obfuscation.Type>
        {
            { 0, Obfuscation.Type.Masking }, // person
            { 1, Obfuscation.Type.Masking }, // bicycle
            { 53, Obfuscation.Type.Pixelation }, // pizza
            { 67, Obfuscation.Type.Masking }, // cell phone
        };

        // StartCoroutine(CaptureAndProcessFrame());

        // // AR Mode Setup
        // Debug.Log("AR Camera On");
        // arCameraButtonBack.SetActive(true);
        // arCamera.SetActive(true);
        // map.SetActive(false);
        // player.SetActive(false);
    }

    /// <summary>
    /// Update is called once per frame. Capture the current frame and process it.
    /// Convert the RenderTexture to a Texture2D.
    /// (Debug: Save the Texture2D as a PNG file to the Assets/JPG folder.)
    /// Run the ImageObfuscator on the Texture2D.
    /// </summary>
    void Update()
    {
        // currentFrame = CaptureCurrentFrame();

        currentFrame = ToTexture2D(renderTexture);

        // StartCoroutine(CaptureAndProcessFrame());
        // currentFrame = ToTexture2D(renderTexture);
        // Texture2D texture2D =
        //     new(renderTexture.width, renderTexture.height, TextureFormat.RGB24, false);
        // texture2D.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
        // texture2D.Apply();

        // Debug.Log("currentFrame.width: " + currentFrame.width + ", currentFrame.height: " + currentFrame.height);
        // currentFrame.width: 241, currentFrame.height: 340

        // ---------------------
        // For still Image Input
        // ---------------------
        // var imgPath = Application.dataPath + "/Test_Images/test_img2_full_size.png";
        // var currentFrame = ImgUtils.LoadTextureFromImage(imagePath: imgPath);

        // ------------------------
        // For changing Image Input
        // ------------------------
        // if (frameCount % frameInterval == 0)
        // {
        //     currentFrame = LoadNextImage();
        // }
        // frameCount++;

        if (outputTexture != null)
        {
            Destroy(outputTexture);
            outputTexture = null;
        }

        // var stopwatch1 = new Stopwatch();
        // stopwatch1.Start();
        outputTexture = imgObfuscator.Run(currentFrame, obfuscationTypes);

        // stopwatch1.Stop();
        // Debug.Log("P_99) Total OFUSCADOR: " + stopwatch1.ElapsedMilliseconds + " ms");
        // stopwatch1.Reset();

        // Bypass the obfuscator (Test Only)
        //outputTexture = currentFrame;


        // DEBUG Only: Save the output texture as a PNG
        // -----------
        // ImageWriter.WriteTextureToPNG(currentFrame, "Assets/DEBUG_IMGS/currentframe.png");
        // ImageWriter.WriteTextureToPNG(outputTexture, "Assets/DEBUG_IMGS/outputOfuscator.png");

        // Render the output texture to RenderTexture renderTexture
        // Graphics.Blit(outputTexture, renderTexture);

        // if (outputTexture != null)
        // {
        //     // Render the output texture to a plane
        //     var renderer = toPlaneImage.GetComponent<Renderer>();
        //     // Convert the texture to a material and apply it to the plane
        //     renderer.material.mainTexture = outputTexture;
        // }

        if (imageUI != null)
        {
            // Convert the outputTexture to a Sprite and set it to the Image
            var sprite = Sprite.Create(
                outputTexture,
                new Rect(0, 0, outputTexture.width, outputTexture.height),
                new Vector2(0.5f, 0.5f)
            );
            imageUI.sprite = sprite;
        }
        else
        {
            Debug.LogWarning("imageUI is null");
        }
    }

    // void OnRenderImage(RenderTexture source, RenderTexture destination)
    // {
    //     // To render the output texture to the screen
    //     //-------------------------------------------
    //     Debug.Log("OnRenderImage called");
    //     TensorFloat outputTensor = TextureConverter.ToTensor(outputTexture);
    //     TextureConverter.RenderToScreen(outputTensor);
    // }

    // void OnRenderImage(RenderTexture source, RenderTexture destination)
    // {
    //     Debug.Log("OnRenderImage called");
    //     if (outputTexture == null)
    //     {
    //         Debug.LogWarning("OnRenderImage outputTexture is null");
    //     }
    //     else
    //     {
    //         Graphics.Blit(outputTexture, destination);
    //     }
    // }

    /// <summary>
    /// Load the next image from the list of image paths.
    /// </summary>
    private Texture2D LoadNextImage()
    {
        var imgPath = imagePaths[currentImageIndex];
        var currentFrame = ImgUtils.LoadTextureFromImage(imagePath: imgPath);

        // Update the current image index, looping back to 0 if we've reached the end of the list
        currentImageIndex = (currentImageIndex + 1) % imagePaths.Count;

        return currentFrame;
    }

    /// <summary>
    /// Convert a RenderTexture to a Texture2D.
    /// </summary>
    Texture2D ToTexture2D(RenderTexture rTex)
    {
        Texture2D tex = new(rTex.width, rTex.height, TextureFormat.RGB24, false);
        RenderTexture.active = rTex;
        tex.ReadPixels(new Rect(0, 0, rTex.width, rTex.height), 0, 0);
        tex.Apply();
        return tex;

        // Texture2D dest = new(rTex.width, rTex.height, TextureFormat.ARGB32, false);
        // Graphics.CopyTexture(renderTexture, dest);
        // return dest;
    }

    /// <summary>
    /// Capture the current frame from the AR camera and convert it to a Texture2D.
    /// </summary>
    Texture2D CaptureCurrentFrame()
    {
        // New Texture2D to hold the camera's output
        Texture2D tex2D =
            new(renderTexture.width, renderTexture.height, TextureFormat.RGB24, false);
        RenderTexture.active = renderTexture; // Set the active RenderTexture to the one used by the camera

        // Read the pixels from the RenderTexture into the Texture2D
        tex2D.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
        tex2D.Apply();

        RenderTexture.active = null; // Reset the active RenderTexture

        return tex2D;
    }

    public void ARCameraOn()
    {
        Debug.Log("AR Camera On");
        arCameraButtonBack.SetActive(true);
        map.SetActive(false);
        player.SetActive(false);
    }

    // /// <summary>
    // /// Capture the current frame from the AR camera and process it.
    // /// </summary>
    // IEnumerator CaptureAndProcessFrame()
    // {
    //     var cameraGameObject = GameObject.Find("Main Camera AR");
    //     if (!cameraGameObject)
    //     {
    //         Debug.LogError("'Main Camera AR' not found in the scene.");
    //         yield break;
    //     }

    //     arCameraComponent = cameraGameObject.GetComponent<Camera>();
    //     if (!arCameraComponent)
    //     {
    //         Debug.LogError("Camera component not found on 'Main Camera AR'.");
    //         yield break;
    //     }

    //     while (true)
    //     {
    //         yield return new WaitForEndOfFrame();

    //         // Set the RenderTexture as the target for the camera
    //         var previousRT = RenderTexture.active;
    //         RenderTexture.active = renderTexture;
    //         arCameraComponent.targetTexture = renderTexture;

    //         // Reset the active RenderTexture and clear the target texture of the camera
    //         RenderTexture.active = previousRT;
    //         arCameraComponent.targetTexture = null;

    //         // Now renderTexture contains the rendered image from the camera
    //         currentFrame = ToTexture2D(renderTexture);

    //         // outputTexture = imageObfuscator.Run(currentFrame, obfuscationTypes);

    //         // outputTexture = imgObfuscator.Run(currentFrame, obfuscationTypes);

    //         // // Wait for a short delay before capturing the next frame
    //         // yield return new WaitForSeconds(0.01f);
    //     }
    // }


    /// <summary>
    /// Called when the script is disabled or when the script is being destroyed.
    /// This method is used to clean up any resources or subscriptions that
    /// were created by the script.
    /// </summary>
    void OnDisable()
    {
        StopAllCoroutines();
    }

    /// <summary>
    /// Called when the script is being destroyed. This method is used to clean
    /// up resources/subscriptions that were created by the script.
    /// Note: OnDestroy is only called on game objects that have previously been active.
    /// </summary>
    void OnDestroy()
    {
        if (renderTexture != null)
        {
            RenderTexture.ReleaseTemporary(renderTexture);
            renderTexture = null;
        }

        if (currentFrame != null)
        {
            Destroy(currentFrame);
            currentFrame = null;
        }
    }

    // void OnDestroy()
    // {
    //     renderTexture?.Release();
    //     if (arCamera != null)
    //     {
    //         arCameraComponent.targetTexture = null;
    //     }
    // }
}
