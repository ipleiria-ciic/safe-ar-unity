using System;
using System.IO;
using System.Collections.Generic;
using Unity.Sentis;
using UnityEngine;
using Color = UnityEngine.Color;
// Profiling
using DebugSD = System.Diagnostics.Debug;
using Stopwatch = System.Diagnostics.Stopwatch;

public static class Obfuscation
{
    public enum Type
    {
        None,
        Blurring,
        Pixelation,
        Masking
    }
}

/// <summary>
/// Obfuscates the image based on the detected objects.
/// </summary>
public class ImageObfuscator : MonoBehaviour
{

    [Header("YOLOv8 Model Asset")]
    public ModelAsset yoloV8Asset;
    private Model Yolov8Model;
    public IWorker worker;

    private static readonly int yoloInputSize = 640;
    public static Unity.Sentis.DeviceType deviceType;
    private Dictionary<int, string> labels;

    // Debug variables
    readonly int numRuns = 10;
    double totalTime = 0;

    ///<summary>
    /// In the Start method, the device type is determined based on the graphics device type.
    /// The best backend type for the device is obtained, and the YOLO model asset is loaded.
    /// Labels are parsed from the YOLO model.
    ///</summary>
    void Start()
    {
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Direct3D11 ||
            SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Metal ||
            SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Vulkan)
        {
            deviceType = Unity.Sentis.DeviceType.GPU;
        }
        else
        {
            deviceType = Unity.Sentis.DeviceType.CPU;
        }

        var backendType = WorkerFactory.GetBestTypeForDevice(deviceType);

        Yolov8Model = ModelLoader.Load(yoloV8Asset);
        worker = WorkerFactory.CreateWorker(backendType, Yolov8Model);

        if (worker == null)
        {
            Debug.LogError("Worker is null after initialization");
        }
        // else
        // {
        //     Debug.Log("Worker initialized");
        // }
        labels = ParseNames(Yolov8Model);
    }

    /// <summary>
    /// Parses the names from the model.
    /// </summary>
    private static Dictionary<int, string> ParseNames(Model model)
    {
        Dictionary<int, string> labels = new();
        // A dictionary with the format "{0: 'person', 1: 'bicycle', 2: 'car', 3: 'motorcycle', .. }"
        char[] removeChars = { '{', '}', ' ' };
        char[] removeCharsValue = { '\'', ' ' };
        var items = model.Metadata["names"].Trim(removeChars).Split(",");
        foreach (var item in items)
        {
            var values = item.Split(":");
            var classId = int.Parse(values[0]);
            var name = values[1].Trim(removeCharsValue);
            labels.Add(classId, name);
        }
        return labels;
    }

    /// <summary>
    /// Resizes texture to specified width and height; discards alpha channel.
    /// </summary>
    private static Texture2D ResizeTexture(Texture2D texture, int width, int height)
    {
        var rt = RenderTexture.GetTemporary(width, height);
        Graphics.Blit(texture, rt);
        var preRt = RenderTexture.active;
        RenderTexture.active = rt;
        var resizedTexture = new Texture2D(width, height, TextureFormat.RGB24, false);
        resizedTexture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        resizedTexture.Apply();
        RenderTexture.active = preRt;
        RenderTexture.ReleaseTemporary(rt);
        return resizedTexture;
    }

    /// <summary>
    /// Calculates the IoU of two detection results (version 2)
    /// (Version 1 is in the ImgUtils.cs file)
    /// </summary>
    private static float Iou(DetectionOutput0 boxA, DetectionOutput0 boxB)
    {
        var xA = Math.Max(boxA.X1, boxB.X1);
        var yA = Math.Max(boxA.Y1, boxB.Y1);
        var xB = Math.Min(boxA.X2, boxB.X2);
        var yB = Math.Min(boxA.Y2, boxB.Y2);

        // Calculate the width and height of the intersection rectangle
        var intersectionWidth = Math.Max(0.0f, xB - xA);
        var intersectionHeight = Math.Max(0.0f, yB - yA);

        // Calculate the area of intersection rectangle
        var intersectionArea = intersectionWidth * intersectionHeight;

        // Calculate the area of union
        var boxAArea = (boxA.X2 - boxA.X1) * (boxA.Y2 - boxA.Y1);
        var boxBArea = (boxB.X2 - boxB.X1) * (boxB.Y2 - boxB.Y1);
        var unionArea = boxAArea + boxBArea - intersectionArea;

        // Return the IoU
        return intersectionArea / unionArea;
    }

    /// <summary>
    /// Processes the mask information.
    /// </summary>
    /// <param name="detects">The list of detection results</param>
    /// <param name="output1">The output tensor containing the mask information</param>
    /// <param name="originalHeight">The original height of the input image</param>
    /// <param name="originalWidth">The original width of the input image</param>
    /// <returns>The list of detection results with mask information</returns>
    private static void ProcessMask(List<DetectionOutput0> detects, Tensor output1, int originalHeight, int originalWidth)
    {
        // Check if detects and output1 are null
        detects = detects ?? throw new ArgumentNullException(nameof(detects), "detects is null");
        output1 = output1 ?? throw new ArgumentNullException(nameof(output1), "output1 is null");

        // Cast to TensorFloat
        var output1Float = output1 as TensorFloat;
        output1.MakeReadable();

        // Tensor dimensions (1, CH, H, W) of output1 (masks information)
        (var CH, var H, var W) = (output1.shape[1], output1.shape[2], output1.shape[3]);

        // output1.Dispose();

        // Calculate the scale factors
        var scaleX = originalWidth / 640f;
        var scaleY = originalHeight / 640f;

        // Calculate the masks matrix
        var counter = 0;
        foreach (var detect in detects)
        {
            counter++;
            var calcMatrix = new float[W, H];

            for (var i = 0; i < CH; i++)
            {
                var maskCoef = detect.Masks[i]; // mask coefficient

                for (var h = 0; h < H; h++)
                {
                    for (var w = 0; w < W; w++)
                    {
                        calcMatrix[w, H - 1 - h] += output1Float[0, i, h, w] * maskCoef;
                    }
                }
            }

            for (var w = 0; w < W; w++)
            {
                for (var h = 0; h < H; h++)
                {
                    calcMatrix[w, H - 1 - h] = ImgUtils.Sigmoid(calcMatrix[w, H - 1 - h]);
                }
            }
            // Expand the mask matrix to the original image dimensions
            var resizedMatrix = ImgUtils.BilinearInterpol(calcMatrix, W, H, originalWidth, originalHeight);

            // DEBUG
            // -----
            // ImageWriter.WriteFloatMatrixToPNG(resizedMatrix, "Assets/DEBUG_IMGS/resizedMatrix.png");

            var newX1 = (int)(detect.X1 * scaleX);
            var newY1 = (int)(detect.Y1 * scaleY);
            var newX2 = (int)(detect.X2 * scaleX);
            var newY2 = (int)(detect.Y2 * scaleY);

            // Crop the mask within the detection box
            var croppedMask = CropMask(maskMatrix: resizedMatrix,
                                           xMin: newX1,
                                           yMin: newY1,
                                           xMax: newX2,
                                           yMax: newY2);


            detect.MaskMatrix = croppedMask;

            // DEBUG
            // -----
            // ImageWriter.WriteBoolMatrixToPNG(croppedMask, $"Assets/DEBUG_IMGS/croppedMask_{counter}.png");

            var cropTexture = BoolMatrixToTexture2D(croppedMask);

            // DEBUG
            // -----
            // ImageWriter.WriteTextureToPNG(cropTexture, $"Assets/DEBUG_IMGS/cropTexture_{counter}.png");

        }
    }

    /// <summary>
    /// Crops the mask within the detection box. Discards mask outside the box.
    /// </summary>
    private static bool[,] CropMask(float[,] maskMatrix, float xMin, float yMin, float xMax, float yMax, bool bypassCrop = false)
    {
        // Invert the y coordinate to consider the top-left axis
        yMin = maskMatrix.GetLength(1) - yMin;
        yMax = maskMatrix.GetLength(1) - yMax;

        var mask = new bool[maskMatrix.GetLength(0), maskMatrix.GetLength(1)];

        if (bypassCrop) // If bypassCrop true, convert mask to boolean matrix
        {
            for (var w = 0; w < maskMatrix.GetLength(0); w++)
            {
                for (var h = 0; h < maskMatrix.GetLength(1); h++)
                {
                    mask[w, h] = maskMatrix[w, h] > 0.5f;
                }
            }
        }
        else // If bypassCrop false, perform the cropping
        {
            var x1 = Math.Max(0, (int)xMin);
            var x2 = Math.Min(maskMatrix.GetLength(0), (int)xMax);
            var y2 = Math.Max(0, (int)yMin);
            var y1 = Math.Min(maskMatrix.GetLength(1), (int)yMax);

            // Debug.Log($"Mask coord. x1: {x1}, x2: {x2}, y1: {y1}, y2: {y2}");

            for (var w = x1; w < x2; w++)
            {
                for (var h = y1; h < y2; h++)
                {
                    if (maskMatrix[w, h] > 0.5f)
                    {
                        mask[w, h] = true;
                    }
                }
            }
        }
        return mask;
    }

    /// <summary>
    /// Obfuscates the image based on the detected objects.
    /// </summary>
    /// <param name="inputTexture">The input image</param>
    /// <param name="classObfuscationTypes">The dictionary of obfuscation types for each class</param>
    /// <param name="pixelSize">The pixel size for pixelation</param>
    /// <returns>The obfuscated image</returns>
    public Texture2D Run(Texture2D inputTexture, Dictionary<int, Obfuscation.Type> classObfuscationTypes, int pixelSize = 16)
    {
        // Initialize the unspecified classes to None
        InitUnspecifiedClassToNone(classObfuscationTypes);

        // Resize the input texture to the YOLOv8 input size
        Texture2D  inputTextureRz = ResizeTexture(inputTexture, yoloInputSize, yoloInputSize);

        // Texture2D inputTxtrRzNrm = TextureNormalize(inputTextureRz);
     
        // Convert the Texture2D to Tensor and make it readable
        TensorFloat inputTensor = TextureConverter.ToTensor(inputTextureRz);
        inputTensor.MakeReadable();

        // Execute the YOLOv8 model
        worker.Execute(inputTensor);

        var output0 = worker.PeekOutput("output0");
        var output1 = worker.PeekOutput("output1");

        // (??) worker.ExecuteAsync(inputTensor).ContinueWith(_ => OnModelExecuted());

        // Parse the outputs of the YOLOv8 model
        List<DetectionOutput0> detections = ParseOutputs(output0, output1);

        // Process the mask information
        ProcessMask(detections, output1, inputTexture.height, inputTexture.width);

        // Obfuscate the image based on the detected objects
        Texture2D outputTexture = ObfuscateImage(inputTexture, detections, classObfuscationTypes, pixelSize);


        // DEBUG: Print the detections
        // ---------------------------
        // Debug.Log($"detections.Count: {detections.Count}");
        // foreach (var detection in detections)
        // {
        //     Debug.Log($"Detection: {detection}, x1: {detection.X1}, y1: {detection.Y1}, x2: {detection.X2}, y2: {detection.Y2}, classId: {detection.ClassID}, score: {detection.Score}, maskMatrix: {detection.MaskMatrix}, Masks: {detection.Masks}");
        // }

        // Dispose of Tensor objects
        inputTensor.Dispose();
        output0.Dispose();
        output1.Dispose();


        return outputTexture;
    }

    
    private static List<DetectionOutput0> ParseOutputs(Tensor output0, Tensor output1, float scoreThres = 0.25f, float iouThres = 0.5f)
    {
        var outputWidth = output0.shape[2];                       // detected objects: 8400
        var classCount = output0.shape[1] - output1.shape[1] - 4; // 80 classes: CoCo dataset

        List<DetectionOutput0> candidateDetects = new();
        List<DetectionOutput0> detects = new();

        for (var i = 0; i < outputWidth; i++)
        {
            var result = new DetectionOutput0(output0, i, classCount);
            if (result.Score >= scoreThres)
            {
                candidateDetects.Add(result);
            }
        }

        // Non-Maximum Suppression (NMS)
        while (candidateDetects.Count > 0)
        {
            var maxScore = float.MinValue;
            DetectionOutput0 maxDetection = null;

            foreach (var detection in candidateDetects)
            {
                if (detection.Score > maxScore)
                {
                    maxScore = detection.Score;
                    maxDetection = detection;
                }
            }

            detects.Add(maxDetection);
            candidateDetects.Remove(maxDetection);

            var deletes = new List<DetectionOutput0>();
            foreach (var detection in candidateDetects)
            {
                var iou = Iou(maxDetection, detection);
                if (iou >= iouThres)
                {
                    deletes.Add(detection);
                }
            }

            foreach (var detection in deletes)
            {
                candidateDetects.Remove(detection);
            }
        }

        return detects;
    }


    /// <summary>
    /// Parses the outputs of the YOLOv8 model, 
    /// performs Non-Maximum Suppression (NMS) to eliminate overlapping bounding boxes, 
    /// and processes the detection mask information.
    /// </summary>
    // private static List<DetectionOutput0> ParseOutputs(Tensor output0, Tensor output1, float scoreThres = 0.25f, float iouThres = 0.5f)
    // {
    //     var outputWidth = output0.shape[2]; // detected objects: 8400
    //     var classCount = output0.shape[1] - output1.shape[1] - 4; // 80 classes: CoCo dataset

    //     List<DetectionOutput0> candidateDetects = new();
    //     List<DetectionOutput0> detects = new();

    //     for (var i = 0; i < outputWidth; i++)
    //     {
    //         var result = new DetectionOutput0(output0, i, classCount);
    //         if (result.Score < scoreThres)
    //         {
    //             continue;
    //         }
    //         candidateDetects.Add(result);
    //     }

    //     // Non-Maximum Suppression (NMS)
    //     while (candidateDetects.Count > 0)
    //     {
    //         var idx = 0;
    //         var maxScore = 0.0f;
    //         for (var i = 0; i < candidateDetects.Count; i++)
    //         {
    //             if (candidateDetects[i].Score > maxScore)
    //             {
    //                 idx = i;
    //                 maxScore = candidateDetects[i].Score;
    //             }
    //         }
    //         // Obtain the detection with the highest score
    //         var cand = candidateDetects[idx];
    //         candidateDetects.RemoveAt(idx);

    //         // Add the detection to the list of detections
    //         detects.Add(cand);
    //         List<int> deletes = new();
    //         for (var i = 0; i < candidateDetects.Count; i++)
    //         {
    //             var iou = Iou(cand, candidateDetects[i]);
    //             //float iou = ImgUtils.Iou(cand, candidateDetects[i]);
    //             if (iou >= iouThres)
    //             {
    //                 deletes.Add(i);
    //             }
    //         }
    //         for (var i = deletes.Count - 1; i >= 0; i--)
    //         {
    //             candidateDetects.RemoveAt(deletes[i]);
    //         }
    //     }

    //     // DEBUG
    //     // -----
    //     // Debug.Log("inputHeight: " + inputHeight);
    //     // Debug.Log("inputWidth: " + inputWidth);
    //     // ProcessMask(detects, output1, 640, 640);

    //     return detects;
    // }

    // /// <summary>
    // /// Represents the result of a detection.
    // /// </summary>
    // public class DetectionOutput0
    // {
    //     public float X1 { get; private set; }
    //     public float Y1 { get; private set; }
    //     public float X2 { get; private set; }
    //     public float Y2 { get; private set; }
    //     public int ClassID { get; private set; }
    //     public float Score { get; private set; }
    //     public bool[,] MaskMatrix { get; set; }
    //     public List<float> Masks = new();

    //     /// <summary>
    //     /// Represents the result of an object detection. 
    //     /// xywh to x1y1x2y2 conversion is done here.
    //     /// </summary>
    //     /// <param name="output0_">The tensor containing the detection results.</param>
    //     /// <param name="idx">The index of the detection result.</param>
    //     /// <param name="classCount">The number of classes in the detection model.</param>
    //     public DetectionOutput0(Tensor output0_, int idx, int classCount)
    //     {
    //         output0_.MakeReadable();
    //         var output0T = output0_ as TensorFloat;

    //         // BBox Convention for YOLOv8: x_min, y_min, w, h (origin: top-left)
    //         var halfWidth = output0T[0, 2, idx] / 2;
    //         var halfHeight = output0T[0, 3, idx] / 2;

    //         (X1, Y1) = (output0T[0, 0, idx] - halfWidth, output0T[0, 1, idx] - halfHeight);
    //         (X2, Y2) = (output0T[0, 0, idx] + halfWidth, output0T[0, 1, idx] + halfHeight);

    //         var highestScoreSoFar = 0f;

    //         for (var classIndex = 0; classIndex < classCount; classIndex++)
    //         {
    //             var currentClassScore = output0T[0, classIndex + 4, idx];
    //             if (currentClassScore < highestScoreSoFar)
    //             {
    //                 continue;
    //             }
    //             ClassID = classIndex;
    //             highestScoreSoFar = currentClassScore;
    //         }
    //         Score = highestScoreSoFar;

    //         for (var i = classCount + 4; i < output0T.shape[1]; i++)
    //         {
    //             Masks.Add(output0T[0, i, idx]); // Store the mask info for later use
    //         }
    //     }
    // }

    public class DetectionOutput0
    {
        public float X1 { get; private set; }
        public float Y1 { get; private set; }
        public float X2 { get; private set; }
        public float Y2 { get; private set; }
        public int ClassID { get; private set; }
        public float Score { get; private set; }
        public bool[,] MaskMatrix { get; set; }
        public List<float> Masks = new List<float>();

        public DetectionOutput0(Tensor output0_, int idx, int classCount)
        {
            var output0T = output0_ as TensorFloat;

            // BBox Convention for YOLOv8: x_min, y_min, w, h (origin: top-left)
            var halfWidth = output0T[0,  2, idx] /  2;
            var halfHeight = output0T[0,  3, idx] /  2;

            (X1, Y1) = (output0T[0,  0, idx] - halfWidth, output0T[0,  1, idx] - halfHeight);
            (X2, Y2) = (output0T[0,  0, idx] + halfWidth, output0T[0,  1, idx] + halfHeight);

            var highestScoreSoFar =  0f;

            for (var classIndex =  0; classIndex < classCount; classIndex++)
            {
                var currentClassScore = output0T[0, classIndex +  4, idx];
                if (currentClassScore < highestScoreSoFar)
                {
                    continue;
                }
                ClassID = classIndex;
                highestScoreSoFar = currentClassScore;
            }
            Score = highestScoreSoFar;

            Masks.Clear(); // Reuse the existing list
            for (var i = classCount +  4; i < output0T.shape[1]; i++)
            {
                Masks.Add(output0T[0, i, idx]); // Store the mask info for later use
            }
        }
    }


    /// <summary>
    /// Draws a bounding box on the texture.
    /// </summary>
    public static void DrawBoundingBox(Texture2D texture, int x1, int y1, int x2, int y2, Color color, int thickness = 3, int padding = 4)
    {
        // Texture2D axis is bottom-left, so we need to invert the Y axis !!!
        y1 = texture.height - y1;
        y2 = texture.height - y2;

        // Coodinates validation and padding
        if (x1 < padding) x1 = padding;
        if (y2 < padding) y2 = padding;
        if (x2 > texture.width - padding) x2 = texture.width - padding;
        if (y1 > texture.height - padding) y1 = texture.height - padding;

        for (var x = x1; x <= x2; x++)
        {
            for (var t = 0; t < thickness; t++)
            {
                texture.SetPixel(x, y1 + t, Color.yellow); // Top edge
                texture.SetPixel(x, y2 - t, color);        // Bottom edge
            }
        }

        for (var y = y2; y <= y1; y++)
        {
            for (var t = 0; t < thickness; t++)
            {
                texture.SetPixel(x1 + t, y, color); // Left edge
                texture.SetPixel(x2 - t, y, color); // Right edge
            }
        }
        texture.Apply();

        // DrawCircle(texture, x2, y2, 5, Color.red);
        // DrawCircle(texture, x1, y1, 5, Color.green);
    }

    public static void DrawCircle(Texture2D texture, int centerX, int centerY, int radius, Color color)
    {
        var d = (5 - radius * 4) / 4;
        var x = 0;
        var y = radius;

        while (x <= y)
        {
            texture.SetPixel(centerX + x, centerY + y, color);
            texture.SetPixel(centerX + x, centerY - y, color);
            texture.SetPixel(centerX - x, centerY + y, color);
            texture.SetPixel(centerX - x, centerY - y, color);
            texture.SetPixel(centerX + y, centerY + x, color);
            texture.SetPixel(centerX + y, centerY - x, color);
            texture.SetPixel(centerX - y, centerY + x, color);
            texture.SetPixel(centerX - y, centerY - x, color);

            if (d < 0)
            {
                d += 2 * x + 1;
            }
            else
            {
                d += 2 * (x - y) + 1;
                y--;
            }
            x++;
        }
        texture.Apply();
    }

    /// <summary>
    /// Initializes the unspecified classes to None.
    /// </summary>
    public static void InitUnspecifiedClassToNone
    (Dictionary<int, Obfuscation.Type> classObfuscationTypes, int totalClasses = 80)
    {
        for (var i = 0; i < totalClasses; i++)
        {
            if (!classObfuscationTypes.ContainsKey(i))
            {
                classObfuscationTypes[i] = Obfuscation.Type.None;
            }
        }
    }

    /// <summary>
    /// Draws the class info on the texture.
    /// </summary>
    public static Texture2D BoolMatrixToTexture2D(bool[,] matrix)
    {
        var height = matrix.GetLength(1);
        var width = matrix.GetLength(0);
        // Debug.Log("Bool2Text height: " + height + ", width: " + width);

        Texture2D texture = new(width, height);

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                texture.SetPixel(x, y, matrix[x, y] ? Color.white : Color.black);
            }
        }
        texture.Apply();
        return texture;
    }

    /// <summary>
    /// Obfuscates the image based on the detected objects. 
    /// </summary>
    /// <param name="inputImage">The input image, original dimensions</param> 
    /// <param name="detections">The list of detection results</param>
    /// <param name="classObfuscationTypes">The dictionary of obfuscation types for each class</param>
    /// <returns>The obfuscated image, the same dimennsions as inputImage</returns>
    private Texture2D ObfuscateImage(Texture2D inputImage, List<DetectionOutput0> detections, Dictionary<int, Obfuscation.Type> classObfuscationTypes, int pixelSize = 4, int blurSize = 5, bool DrawBBoxes = true)
    {
        // Create a copy of the input image
        Texture2D outputImage = new(inputImage.width, inputImage.height);
        outputImage.SetPixels(inputImage.GetPixels());
        outputImage.Apply();

        // Calculate the scale factors
        var scaleX = (float)outputImage.width / yoloInputSize;
        var scaleY = (float)outputImage.height / yoloInputSize;

        foreach (var detect in detections)
        {   
            // Calculate the new coordinates based on the scale factors
            var newX1 = (int)(detect.X1 * scaleX);
            var newY1 = (int)(detect.Y1 * scaleY);
            var newX2 = (int)(detect.X2 * scaleX);
            var newY2 = (int)(detect.Y2 * scaleY);

            var obfuscationType = classObfuscationTypes[detect.ClassID];

            switch (obfuscationType)
            {
                case Obfuscation.Type.Masking:
                    
                    ImgUtils.MaskTexture(texture: outputImage,
                                        mask: detect.MaskMatrix,
                                        maskColor: new Color(1, 0, 0, 1));
                    break;

                case Obfuscation.Type.Pixelation:
                    ImgUtils.PixelateTexture(texture: outputImage,
                                            mask: detect.MaskMatrix,
                                            pixelSize: pixelSize);
                    break;

                case Obfuscation.Type.Blurring:

                    ImgUtils.BlurTexture(texture: ref outputImage,
                                        mask: detect.MaskMatrix,
                                        blurSize: blurSize);
                    break;

                case Obfuscation.Type.None:
                    break;

                default:
                    throw new ArgumentException("Invalid obfuscation type");
            }

            if (DrawBBoxes == true)
            {
                DrawBoundingBox(texture: outputImage,
                                x1: newX1,
                                y1: newY1,
                                x2: newX2,
                                y2: newY2,
                                color: Color.red);

                // TODO: Complete the DrawClassInfo method
                // DrawClassInfo(texture: outputImage,
                //                 x: newX1,
                //                 y: newY2,
                //                 text: labels[detect.ClassID] + " " + detect.Score.ToString("0.00"),
                //                 color: Color.magenta);
            }
        }

        outputImage.Apply();
        return outputImage;
    }

    private void DrawClassInfo(Texture2D texture, int x, int y, string text, Color color)
    {
        // Create a Font object
        var font = Resources.Load<Font>("NotoSans");

        // Create a style with the desired color
        var style = new GUIStyle();
        style.normal.textColor = color;
        style.font = font;

        // Create a temporary RenderTexture
        var renderTexture = RenderTexture.GetTemporary(texture.width, texture.height);
        RenderTexture.active = renderTexture;

        // Clear the RenderTexture
        GL.Clear(true, true, Color.clear);

        // Create a temporary TextMesh
        var go = new GameObject();
        var textMesh = go.AddComponent<TextMesh>();
        textMesh.fontSize = 100;
        textMesh.font = font;
        textMesh.text = text;
        textMesh.color = color;

        // Render the TextMesh to the RenderTexture
        var tempCamera = new GameObject().AddComponent<Camera>();
        tempCamera.targetTexture = renderTexture;
        tempCamera.RenderWithShader(Shader.Find("Hidden/Internal-Colored"), "");

        // Copy the RenderTexture to the Texture2D
        texture.ReadPixels(new Rect(x, y, textMesh.GetComponent<Renderer>().bounds.size.x, textMesh.GetComponent<Renderer>().bounds.size.y), x, y);
        texture.Apply();

        // Clean up
        Destroy(go);
        Destroy(tempCamera.gameObject);

        // Release the temporary RenderTexture
        RenderTexture.ReleaseTemporary(renderTexture);
    }

    /// <summary>
    /// Destroys the worker when the MonoBehaviour is destroyed.
    /// </summary>
    void OnDestroy()
    {
        worker?.Dispose();
        worker = null;
    }
}

    /// <summary>
    /// Contains utility methods for saving images to PNG files.
    /// </summary>
    public static class ImageWriter
    {
        public static void WriteTextureToPNG(Texture2D texture, string path)
        {
            var bytes = texture.EncodeToPNG();
            File.WriteAllBytes(path, bytes);
        }
        public static void WriteFloatMatrixToPNG(float[,] matrix, string path)
        {
            var width = matrix.GetLength(0);
            var height = matrix.GetLength(1);
            var texture = new Texture2D(width, height);
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var value = matrix[x, y];
                    Color color = new(value, value, value, 1.0f);
                    texture.SetPixel(x, y, color);
                }
            }
            texture.Apply();
            WriteTextureToPNG(texture, path);
        }

        public static void WriteTensorMatrixToPNG(float[,,] tensor, string path)
        {
            var width = tensor.GetLength(0);
            var height = tensor.GetLength(1);
            var texture = new Texture2D(width, height);
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var r = tensor[x, y, 0];
                    var g = tensor[x, y, 1];
                    var b = tensor[x, y, 2];
                    var color = new Color(r, g, b, 1.0f);
                    texture.SetPixel(x, y, color);
                }
            }
            texture.Apply();
            WriteTextureToPNG(texture, path);
        }

        public static void WriteBoolMatrixToPNG(bool[,] matrix, string path)
        {
            var width = matrix.GetLength(0);
            var height = matrix.GetLength(1);
            Texture2D texture = new(width, height);
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var value = matrix[x, y] ? 1.0f : 0.0f;
                    Color color = new(value, value, value, 1.0f);
                    texture.SetPixel(x, y, color);
                }
            }
            texture.Apply();
            WriteTextureToPNG(texture, path);
        }
    }


