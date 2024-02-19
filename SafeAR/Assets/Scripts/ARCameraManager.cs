using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class ARCameraManager : MonoBehaviour
{
    //--- Camera --------------------
    [Header("AR_Camera")]
    [SerializeField] private GameObject arCamera;
    [SerializeField] private GameObject arCameraButtonBack;
    private Camera arCameraComponent;        
    public static ARCameraManager Instance { get; private set; }

    [Header("Map, Player and RenderTexture")]
    [SerializeField] private GameObject map;
    [SerializeField] private GameObject player;
    [SerializeField] private RenderTexture renderTexture;

    // --- Obfuscation --------------
    public Dictionary<int, Obfuscation.Type> obfuscationTypes;
    [Header("SafeARLayer Obfuscation")]
    [SerializeField] private ImageObfuscator imageObfuscator;
    [Header("Debug Plane Output")]
    private Texture2D outputTexture;
    public GameObject toPlaneImage;
    private Texture2D currentFrame;

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

        GameObject xrOrigin = GameObject.Find("XR Origin");  // It must be ACTIVE to get the camera component !!!
        Transform cameraOffset = xrOrigin.transform.Find("Camera Offset");
        Transform mainCameraAR = cameraOffset.Find("Main Camera AR");
        Camera arCameraComponent = mainCameraAR.GetComponent<Camera>();
        renderTexture = RenderTexture.GetTemporary(arCameraComponent.pixelWidth, arCameraComponent.pixelHeight, 24);
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
        
        // --------------------------------------------------------------------------------
        // Obfuscation Definition. Options: Masking, Pixelation, Blurring or None 
        // --------------------------------------------------------------------------------
        obfuscationTypes = new Dictionary<int, Obfuscation.Type>
        {
            {0, Obfuscation.Type.Masking},  // person
            {1, Obfuscation.Type.Masking},  // bicycle
            {53, Obfuscation.Type.Masking}, // pizza
            {67, Obfuscation.Type.Masking}, // cell phone
        };

        StartCoroutine(CaptureAndProcessFrame());
    }

    /// <summary>
    /// Update is called once per frame. Capture the current frame and process it.
    /// Convert the RenderTexture to a Texture2D.
    /// (Debug: Save the Texture2D as a PNG file to the Assets/JPG folder.)
    /// Run the ImageObfuscator on the Texture2D.
    /// </summary>
    void Update()
    {
        StartCoroutine(CaptureAndProcessFrame()); 
		currentFrame = ToTexture2D(renderTexture);
        Debug.Log("currentFrame.width: " + currentFrame.width + ", currentFrame.height: " + currentFrame.height);
        // currentFrame.width: 241, currentFrame.height: 340

        // ---------------------
        // For still Image Input
        // ---------------------
        // var imgPath = Application.dataPath + "/Test_Images/test_img2_full_size.png";
        // var currentFrame = ImgUtils.LoadTextureFromImage(imagePath: imgPath);

        // ------------------------
        // For rotating Image Input
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
        outputTexture = imageObfuscator.Run(currentFrame, obfuscationTypes);

        // Bypass the obfuscator (Test Only)
        //outputTexture = currentFrame;

        // DEBUG Only: Save the output texture as a PNG
        // -----------
        // ImageWriter.WriteTextureToPNG(currentFrame, "Assets/DEBUG_IMGS/currentframe.png");
        // ImageWriter.WriteTextureToPNG(outputTexture, "Assets/DEBUG_IMGS/outputOfuscator.png");


    
        if (outputTexture != null)
        {
            // Render the output texture to a plane
            var renderer = toPlaneImage.GetComponent<Renderer>();
            // Convert the texture to a material and apply it to the plane
            renderer.material.mainTexture = outputTexture;
            }

    }


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
    }

    /// <summary>
    /// Capture the current frame from the AR camera and process it.
    /// </summary>
    IEnumerator CaptureAndProcessFrame()
    {
        var cameraGameObject = GameObject.Find("Main Camera AR");
        if (!cameraGameObject)
        {
            Debug.LogError("'Main Camera AR' not found in the scene.");
            yield break;
        }

        arCameraComponent = cameraGameObject.GetComponent<Camera>();
        if (!arCameraComponent)
        {
            Debug.LogError("Camera component not found on 'Main Camera AR'.");
            yield break;
        }

        while (true)
        {
            yield return new WaitForEndOfFrame();

            // Set the RenderTexture as the target for the camera
            var previousRT = RenderTexture.active;
            RenderTexture.active = renderTexture;
            arCameraComponent.targetTexture = renderTexture;

            // Reset the active RenderTexture and clear the target texture of the camera
            RenderTexture.active = previousRT;
            arCameraComponent.targetTexture = null;

            // Now renderTexture contains the rendered image from the camera
            currentFrame = ToTexture2D(renderTexture);

            outputTexture = imageObfuscator.Run(currentFrame, obfuscationTypes);

            // Wait for a short delay before capturing the next frame
            yield return new WaitForSeconds(0.1f);
        }
    }


    // /// <summary>
    // /// Activate the AR camera.
    // /// </summary>
    // void ARCameraOn()
    // {
    //     arCamera.SetActive(!arCamera.activeSelf);
    //     if (arCamera.activeSelf)
    //     {
    //         // Find the AR camera each time in case the hierarchy changes
    //         GameObject cameraGameObject = GameObject.Find("Main Camera AR");
    //         if (cameraGameObject)
    //         {
    //             arCameraComponent = cameraGameObject.GetComponent<Camera>();
    //             if (arCameraComponent)
    //             {
    //                 //arCameraComponent.depth = 0;
    //                 Debug.Log("AR Camera On. Camera name: " + arCameraComponent.name);
    //                 Debug.Log("RenderTexture set. Width: " + renderTexture.width + ", Height: " + renderTexture.height);
    //             }
    //             else
    //             {
    //                 Debug.LogError("Camera component not found on 'Main Camera AR'.");
    //             }
    //         }
    //         else
    //         {
    //             Debug.LogError("'Main Camera AR' not found in the scene.");
    //         }

    //         //rawImage.texture = renderTexture;

    //         Debug.Log("AR Camera On");
    //         arCameraButtonBack.SetActive(true);
    //         map.SetActive(false);
    //         player.SetActive(false);
    //         //camTexture.Play();
    //     }
    //     else
    //     {
    //         //set rawImage to false
    //         //rawImage.enabled = false;
    //         arCameraButtonBack.SetActive(false);
    //         map.SetActive(true);
    //         player.SetActive(true);
    //         //camTexture.Stop();
    //     }
    //     //SceneManager.LoadScene("ARCamera");
    // }

    void BackToMap()
    {
        arCamera.SetActive(false);
        arCameraButtonBack.SetActive(false);
        map.SetActive(true);
        player.SetActive(true);
        //rawImage.enabled = false;
        //camTexture.Stop();
        //SceneManager.LoadScene("World");
    }

    // void OnDestroy()
    // {
    //     renderTexture?.Release();
    //     if (arCamera != null)
    //     {
    //         arCameraComponent.targetTexture = null;
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
}
