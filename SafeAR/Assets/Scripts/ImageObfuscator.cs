// using System;
// using System.Collections.Generic;
// using System.IO;
// using System.Runtime.InteropServices;
// using System.Threading.Tasks;
// using MathNet.Numerics.LinearAlgebra;
// using Unity.Sentis;
// using UnityEngine;
// using UnityEngine.UI;
// using Color = UnityEngine.Color;
// // Profiling
// using DebugSD = System.Diagnostics.Debug;
// using Stopwatch = System.Diagnostics.Stopwatch;

// public static class Obfuscation
// {
//     public enum Type
//     {
//         None,
//         Blurring,
//         Pixelation,
//         Masking
//     }
// }

// /// <summary>
// /// Obfuscates the image based on the detected objects.
// /// </summary>
// public class ImageObfuscator : MonoBehaviour
// {
//     [Header("YOLOv8 Model Asset")]
//     public ModelAsset yoloV8Asset;
//     private Model yolov8Model;
//     public IWorker worker;

//     private static readonly int yoloInputSize = 640;
//     public static Unity.Sentis.DeviceType deviceType;
//     private Dictionary<int, string> labels;

//     // Debug variables
//     readonly int numRuns = 10;
//     double totalTime = 0;

//     public Text debugText;

//     ///<summary>
//     /// In the Start method, the device type is determined based on the graphics
//     /// device type. The best backend type for the device is obtained, and the
//     /// YOLO model asset is loaded. Labels are parsed from the YOLO model.
//     ///</summary>
//     void Start()
//     {
//         if (
//             SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Direct3D11
//             || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Metal
//             || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Vulkan
//         )
//         {
//             deviceType = Unity.Sentis.DeviceType.GPU;
//         }
//         else
//         {
//             deviceType = Unity.Sentis.DeviceType.CPU;
//         }

//         var backendType = WorkerFactory.GetBestTypeForDevice(deviceType);

//         yolov8Model = ModelLoader.Load(yoloV8Asset);
//         worker = WorkerFactory.CreateWorker(backendType, yolov8Model);

//         if (worker == null)
//         {
//             Debug.LogError("Worker is null after initialization");
//         }
//         labels = ParseNames(yolov8Model);
//     }

//     /// <summary>
//     /// Obfuscates the image based on the detected objects.
//     /// </summary>
//     /// <param name="inputTexture">The input image</param>
//     /// <param name="classObfuscationTypes">The dictionary of obfuscation types for each class</param>
//     /// <param name="pixelSize">The pixel size for pixelation</param>
//     /// <returns>The obfuscated image</returns>
//     public Texture2D Run(
//         Texture2D inputTexture,
//         Dictionary<int, Obfuscation.Type> classObfuscationTypes,
//         int pixelSize = 16
//     )
//     {
//         // Initialize the unspecified classes to None
//         InitUnspecifiedClassToNone(classObfuscationTypes);

//         // Resize the input texture to the YOLOv8 input size
//         Texture2D inputTextureRz = ResizeTexture(inputTexture, yoloInputSize, yoloInputSize);

//         // Texture2D inputTxtrRzNrm = TextureNormalize(inputTextureRz);

//         // Convert the Texture2D to Tensor and make it readable
//         TensorFloat inputTensor = TextureConverter.ToTensor(inputTextureRz);
//         inputTensor.MakeReadable();

//         // Execute the YOLOv8 model
//         worker.Execute(inputTensor);

//         var output0 = worker.PeekOutput("output0");
//         var output1 = worker.PeekOutput("output1");

//         // (??) worker.ExecuteAsync(inputTensor).ContinueWith(_ => OnModelExecuted());

//         // Parse the outputs of the YOLOv8 model
//         List<DetectionOutput0> detections = ParseOutputs(output0, output1);

//         // Process the mask information
//         ProcessMask(detections, output1, inputTexture.height, inputTexture.width);

//         // Obfuscate the image based on the detected objects
//         Texture2D outputTexture = ObfuscateImage(
//             inputTexture,
//             detections,
//             classObfuscationTypes,
//             pixelSize
//         );

//         // DEBUG: Print the detections
//         // ---------------------------
//         // Debug.Log($"detections.Count: {detections.Count}");
//         // foreach (var detection in detections)
//         // {
//         //     Debug.Log($"Detection: {detection}, x1: {detection.X1}, y1: {detection.Y1}, x2: {detection.X2}, y2: {detection.Y2}, classId: {detection.ClassID}, score: {detection.Score}, maskMatrix: {detection.MaskMatrix}, Masks: {detection.Masks}");
//         // }

//         // Dispose of Tensor objects
//         inputTensor.Dispose();
//         output0.Dispose();
//         output1.Dispose();

//         return outputTexture;
//     }

//     /// <summary>
//     /// Parses the names from the model.
//     /// </summary>
//     private static Dictionary<int, string> ParseNames(Model model)
//     {
//         Dictionary<int, string> labels = new();
//         // A dictionary with the format "{0: 'person', 1: 'bicycle', 2: 'car', 3: 'motorcycle', .. }"
//         char[] removeChars = { '{', '}', ' ' };
//         char[] removeCharsValue = { '\'', ' ' };
//         var items = model.Metadata["names"].Trim(removeChars).Split(",");
//         foreach (var item in items)
//         {
//             var values = item.Split(":");
//             var classId = int.Parse(values[0]);
//             var name = values[1].Trim(removeCharsValue);
//             labels.Add(classId, name);
//         }
//         return labels;
//     }

//     /// <summary>
//     /// Resizes texture to specified width and height; discards alpha channel.
//     /// </summary>
//     private static Texture2D ResizeTexture(Texture2D texture, int width, int height)
//     {
//         var rt = RenderTexture.GetTemporary(width, height);
//         Graphics.Blit(texture, rt);
//         var preRt = RenderTexture.active;
//         RenderTexture.active = rt;
//         var resizedTexture = new Texture2D(width, height, TextureFormat.RGB24, false);
//         resizedTexture.ReadPixels(new UnityEngine.Rect(0, 0, width, height), 0, 0);
//         resizedTexture.Apply();
//         RenderTexture.active = preRt;
//         RenderTexture.ReleaseTemporary(rt);
//         return resizedTexture;
//     }

//     /// <summary>
//     /// Calculates the IoU of two detection results (version 2)
//     /// (Version 1 is in the ImgUtils.cs file)
//     /// </summary>
//     private static float Iou(DetectionOutput0 boxA, DetectionOutput0 boxB)
//     {
//         var xA = Math.Max(boxA.X1, boxB.X1);
//         var yA = Math.Max(boxA.Y1, boxB.Y1);
//         var xB = Math.Min(boxA.X2, boxB.X2);
//         var yB = Math.Min(boxA.Y2, boxB.Y2);

//         // Calculate the width and height of the intersection rectangle
//         var intersectionWidth = Math.Max(0.0f, xB - xA);
//         var intersectionHeight = Math.Max(0.0f, yB - yA);

//         // Calculate the area of intersection rectangle
//         var intersectionArea = intersectionWidth * intersectionHeight;

//         // Calculate the area of union
//         var boxAArea = (boxA.X2 - boxA.X1) * (boxA.Y2 - boxA.Y1);
//         var boxBArea = (boxB.X2 - boxB.X1) * (boxB.Y2 - boxB.Y1);
//         var unionArea = boxAArea + boxBArea - intersectionArea;

//         // Return the IoU
//         return intersectionArea / unionArea;
//     }

//     /// <summary>
//     /// Processes the mask information.
//     /// </summary>
//     /// <param name="detects">The list of detection results</param>
//     /// <param name="output1">The output tensor containing the mask information</param>
//     /// <param name="originalHeight">The original height of the input image</param>
//     /// <param name="originalWidth">The original width of the input image</param>
//     /// <returns>The list of detection results with mask information</returns>
//     private static void ProcessMask(
//         List<DetectionOutput0> detects,
//         Tensor output1,
//         int originalHeight,
//         int originalWidth
//     )
//     {
//         var stopwatch = new Stopwatch();
//         stopwatch.Start();
//         // Check if detects and output1 are null
//         detects = detects ?? throw new ArgumentNullException(nameof(detects), "detects is null");
//         output1 = output1 ?? throw new ArgumentNullException(nameof(output1), "output1 is null");
//         stopwatch.Stop();
//         Debug.Log($"Null checks: {stopwatch.ElapsedMilliseconds} ms");
//         stopwatch.Reset();

//         stopwatch.Start();
//         // Cast to TensorFloat
//         var output1Float = output1 as TensorFloat;
//         output1.MakeReadable();
//         stopwatch.Stop();
//         Debug.Log($"Casting and making readable: {stopwatch.ElapsedMilliseconds} ms");
//         stopwatch.Reset();

//         stopwatch.Start();
//         // Tensor dimensions (1, CH, H, W) of output1 (masks information)
//         (var CH, var H, var W) = (output1.shape[1], output1.shape[2], output1.shape[3]);
//         stopwatch.Stop();
//         Debug.Log($"Tensor dimensions: {stopwatch.ElapsedMilliseconds} ms");
//         stopwatch.Reset();

//         // output1.Dispose();


//         stopwatch.Start();
//         // Calculate the scale factors
//         var scaleX = originalWidth / 640f;
//         var scaleY = originalHeight / 640f;
//         stopwatch.Stop();
//         Debug.Log($"Scale factors: {stopwatch.ElapsedMilliseconds} ms");
//         stopwatch.Reset();

//         // Calculate the masks matrix
//         var counter = 0;
//         foreach (var detect in detects)
//         {
//             stopwatch.Start();
//             counter++;
//             var calcMatrix = new float[W, H];
//             for (var i = 0; i < CH; i++)
//             {
//                 var maskCoef = detect.MasksCoef[i]; // mask coefficient
//                 for (var h = 0; h < H; h++)
//                 {
//                     for (var w = 0; w < W; w++)
//                     {
//                         calcMatrix[w, H - 1 - h] += output1Float[0, i, h, w] * maskCoef;
//                     }
//                 }
//             }

//             // Verion 2: Parallel.For
//             // var calcMatrix = new float[W, H];
//             // Parallel.For(
//             //     0,
//             //     CH,
//             //     i =>
//             //     {
//             //         var maskCoef = detect.MasksCoef[i]; // mask coefficient
//             //         for (var h = 0; h < H; h++)
//             //         {
//             //             for (var w = 0; w < W; w++)
//             //             {
//             //                 calcMatrix[w, H - 1 - h] += output1Float[0, i, h, w] * maskCoef;
//             //             }
//             //         }
//             //     }
//             // );


//             // Version 3
//             // // Create a zero matrix
//             // double[,] calcMatrix = new double[W, H];

//             // for (var i = 0; i < CH; i++)
//             // {
//             //     var maskCoef = detect.MasksCoef[i]; // mask coefficient

//             //     // Create a matrix from the tensor slice
//             //     double[,] tensorSlice = new double[H, W];
//             //     for (var h = 0; h < H; h++)
//             //     {
//             //         for (var w = 0; w < W; w++)
//             //         {
//             //             tensorSlice[h, w] = output1Float[0, i, h, w];
//             //         }
//             //     }

//             //     // Multiply the tensor slice by the mask coefficient and add it to calcMatrix
//             //     for (var h = 0; h < H; h++)
//             //     {
//             //         for (var w = 0; w < W; w++)
//             //         {
//             //             calcMatrix[h, w] += tensorSlice[h, w] * maskCoef;
//             //         }
//             //     }
//             // }

//             // Version 4: MathNet.Numerics.LinearAlgebra



//             stopwatch.Stop();
//             Debug.Log($"Calculation of mask matrix: {stopwatch.ElapsedMilliseconds} ms");
//             stopwatch.Reset();

//             stopwatch.Start();
//             // Apply the sigmoid function to the mask matrix
//             for (var w = 0; w < W; w++)
//             {
//                 for (var h = 0; h < H; h++)
//                 {
//                     calcMatrix[w, H - 1 - h] = ImgUtils.Sigmoid(calcMatrix[w, H - 1 - h]);
//                 }
//             }
//             stopwatch.Stop();
//             Debug.Log($"Sigmoid function: {stopwatch.ElapsedMilliseconds} ms");
//             stopwatch.Reset();

//             stopwatch.Start();
//             // Expand the mask matrix to the original image dimensions
//             var resizedMatrix = ImgUtils.BilinearInterpol(
//                 calcMatrix,
//                 W,
//                 H,
//                 originalWidth,
//                 originalHeight
//             );
//             stopwatch.Stop();
//             Debug.Log($"Resizing mask matrix: {stopwatch.ElapsedMilliseconds} ms");
//             stopwatch.Reset();

//             // DEBUG
//             // -----
//             // ImageWriter.WriteFloatMatrixToPNG(resizedMatrix, "Assets/DEBUG_IMGS/resizedMatrix.png");

//             stopwatch.Start();
//             // Calculate the new coordinates based on the scale factors
//             var newX1 = (int)(detect.X1 * scaleX);
//             var newY1 = (int)(detect.Y1 * scaleY);
//             var newX2 = (int)(detect.X2 * scaleX);
//             var newY2 = (int)(detect.Y2 * scaleY);

//             // Crop the mask within the detection box
//             var croppedMask = ImgUtils.CropMask(
//                 maskMatrix: resizedMatrix,
//                 xMin: newX1,
//                 yMin: newY1,
//                 xMax: newX2,
//                 yMax: newY2
//             );
//             stopwatch.Stop();
//             Debug.Log($"Cropping mask: {stopwatch.ElapsedMilliseconds} ms");
//             stopwatch.Reset();

//             stopwatch.Start();
//             detect.MaskMatrix = croppedMask;
//             stopwatch.Stop();
//             Debug.Log($"Assigning mask matrix: {stopwatch.ElapsedMilliseconds} ms");
//             stopwatch.Reset();

//             // DEBUG
//             // -----
//             // ImageWriter.WriteBoolMatrixToPNG(croppedMask, $"Assets/DEBUG_IMGS/croppedMask_{counter}.png");

//             stopwatch.Start();
//             var cropTexture = BoolMatrixToTexture2D(croppedMask);
//             stopwatch.Stop();
//             Debug.Log($"Converting mask to texture: {stopwatch.ElapsedMilliseconds} ms");
//             stopwatch.Reset();
//             // DEBUG
//             // -----
//             // ImageWriter.WriteTextureToPNG(cropTexture, $"Assets/DEBUG_IMGS/cropTexture_{counter}.png");
//         }
//     }

//     /// <summary>
//     /// Parses the outputs of the YOLOv8 model,
//     /// performs Non-Maximum Suppression (NMS) to eliminate overlapping bounding boxes,
//     /// and processes the detection mask information.
//     /// </summary>
//     private static List<DetectionOutput0> ParseOutputs(
//         Tensor output0,
//         Tensor output1,
//         float scoreThres = 0.25f,
//         float iouThres = 0.5f
//     )
//     {
//         var outputWidth = output0.shape[2]; // detected objects: 8400
//         var classCount = output0.shape[1] - output1.shape[1] - 4; // 80 classes: CoCo dataset

//         List<DetectionOutput0> candidateDetects = new();
//         List<DetectionOutput0> detects = new();

//         for (var i = 0; i < outputWidth; i++)
//         {
//             var result = new DetectionOutput0(output0, i, classCount);
//             if (result.Score < scoreThres)
//             {
//                 continue;
//             }
//             candidateDetects.Add(result);
//         }

//         // Non-Maximum Suppression (NMS)
//         while (candidateDetects.Count > 0)
//         {
//             var idx = 0;
//             var maxScore = 0.0f;
//             for (var i = 0; i < candidateDetects.Count; i++)
//             {
//                 if (candidateDetects[i].Score > maxScore)
//                 {
//                     idx = i;
//                     maxScore = candidateDetects[i].Score;
//                 }
//             }
//             // Obtain the detection with the highest score
//             var cand = candidateDetects[idx];
//             candidateDetects.RemoveAt(idx);

//             // Add the detection to the list of detections
//             detects.Add(cand);
//             List<int> deletes = new();
//             for (var i = 0; i < candidateDetects.Count; i++)
//             {
//                 var iou = Iou(cand, candidateDetects[i]);
//                 //float iou = ImgUtils.Iou(cand, candidateDetects[i]);
//                 if (iou >= iouThres)
//                 {
//                     deletes.Add(i);
//                 }
//             }
//             for (var i = deletes.Count - 1; i >= 0; i--)
//             {
//                 candidateDetects.RemoveAt(deletes[i]);
//             }
//         }


//         // Debug text to screen
//         // --------------------
//         // var text = "";
//         // for (var i = 0; i < detects.Count; i++)
//         // {
//         //     // append ClassID and Score to the detection
//         //     text +=
//         //         $"Detect. {i + 1}: ClassID = {detects[i].ClassID}, Score = {detects[i].Score:F2}\n";
//         // }

//         // DEBUG
//         // -----
//         // Debug.Log("inputHeight: " + inputHeight);
//         // Debug.Log("inputWidth: " + inputWidth);
//         // ProcessMask(detects, output1, 640, 640);

//         return detects;
//     }

//     /// <summary>
//     /// Represents the result of a detection.
//     /// </summary>
//     // public class DetectionOutput0
//     // {
//     //     public float X1 { get; private set; }
//     //     public float Y1 { get; private set; }
//     //     public float X2 { get; private set; }
//     //     public float Y2 { get; private set; }
//     //     public int ClassID { get; private set; }
//     //     public float Score { get; private set; }
//     //     public bool[,] MaskMatrix { get; set; }
//     //     public List<float> Masks = new();
//     //     /// <summary>
//     //     /// Represents the result of an object detection.
//     //     /// xywh to x1y1x2y2 conversion is done here.
//     //     /// </summary>
//     //     /// <param name="output0_">The tensor containing the detection results.</param>
//     //     /// <param name="idx">The index of the detection result.</param>
//     //     /// <param name="classCount">The number of classes in the detection model.</param>
//     //     public DetectionOutput0(Tensor output0_, int idx, int classCount)
//     //     {
//     //         output0_.MakeReadable();
//     //         var output0T = output0_ as TensorFloat;

//     //         // BBox Convention for YOLOv8: x_min, y_min, w, h (origin: top-left)
//     //         var halfWidth = output0T[0, 2, idx] / 2;
//     //         var halfHeight = output0T[0, 3, idx] / 2;

//     //         (X1, Y1) = (output0T[0, 0, idx] - halfWidth, output0T[0, 1, idx] - halfHeight);
//     //         (X2, Y2) = (output0T[0, 0, idx] + halfWidth, output0T[0, 1, idx] + halfHeight);

//     //         var highestScoreSoFar = 0f;

//     //         for (var classIndex = 0; classIndex < classCount; classIndex++)
//     //         {
//     //             var currentClassScore = output0T[0, classIndex + 4, idx];
//     //             if (currentClassScore < highestScoreSoFar)
//     //             {
//     //                 continue;
//     //             }
//     //             ClassID = classIndex;
//     //             highestScoreSoFar = currentClassScore;
//     //         }
//     //         Score = highestScoreSoFar;

//     //         for (var i = classCount + 4; i < output0T.shape[1]; i++)
//     //         {
//     //             Masks.Add(output0T[0, i, idx]); // Store the mask info for later use
//     //         }
//     //     }
//     // }

//     public class DetectionOutput0
//     {
//         public float X1 { get; private set; }
//         public float Y1 { get; private set; }
//         public float X2 { get; private set; }
//         public float Y2 { get; private set; }
//         public int ClassID { get; private set; }
//         public float Score { get; private set; }
//         public bool[,] MaskMatrix { get; set; }
//         public List<float> MasksCoef = new List<float>();

//         public DetectionOutput0(Tensor output0_, int idx, int classCount)
//         {
//             var output0T = output0_ as TensorFloat;

//             // BBox Convention for YOLOv8: x_min, y_min, w, h (origin: top-left)
//             var halfWidth = output0T[0, 2, idx] / 2;
//             var halfHeight = output0T[0, 3, idx] / 2;

//             (X1, Y1) = (output0T[0, 0, idx] - halfWidth, output0T[0, 1, idx] - halfHeight);
//             (X2, Y2) = (output0T[0, 0, idx] + halfWidth, output0T[0, 1, idx] + halfHeight);

//             var highestScoreSoFar = 0f;

//             for (var classIndex = 0; classIndex < classCount; classIndex++)
//             {
//                 var currentClassScore = output0T[0, classIndex + 4, idx];
//                 if (currentClassScore < highestScoreSoFar)
//                 {
//                     continue;
//                 }
//                 ClassID = classIndex;
//                 highestScoreSoFar = currentClassScore;
//             }
//             Score = highestScoreSoFar;

//             MasksCoef.Clear(); // Reuse the existing list
//             for (var i = classCount + 4; i < output0T.shape[1]; i++)
//             {
//                 MasksCoef.Add(output0T[0, i, idx]); // Store the mask info for later use
//             }
//         }
//     }

//     /// <summary>
//     /// Initializes the unspecified classes to None.
//     /// </summary>
//     public static void InitUnspecifiedClassToNone(
//         Dictionary<int, Obfuscation.Type> classObfuscationTypes,
//         int totalClasses = 80
//     )
//     {
//         for (var i = 0; i < totalClasses; i++)
//         {
//             if (!classObfuscationTypes.ContainsKey(i))
//             {
//                 classObfuscationTypes[i] = Obfuscation.Type.None;
//             }
//         }
//     }

//     /// <summary>
//     /// Draws the class info on the texture.
//     /// </summary>
//     public static Texture2D BoolMatrixToTexture2D(bool[,] matrix)
//     {
//         var height = matrix.GetLength(1);
//         var width = matrix.GetLength(0);
//         // Debug.Log("Bool2Text height: " + height + ", width: " + width);

//         Texture2D texture = new(width, height);

//         for (var y = 0; y < height; y++)
//         {
//             for (var x = 0; x < width; x++)
//             {
//                 texture.SetPixel(x, y, matrix[x, y] ? Color.white : Color.black);
//             }
//         }
//         texture.Apply();
//         return texture;
//     }

//     /// <summary>
//     /// Obfuscates the image based on the detected objects.
//     /// </summary>
//     /// <param name="inputImage">The input image, original dimensions</param>
//     /// <param name="detections">The list of detection results</param>
//     /// <param name="classObfuscationTypes">The dictionary of obfuscation types for each class</param>
//     /// <returns>The obfuscated image, the same dimennsions as inputImage</returns>
//     private Texture2D ObfuscateImage(
//         Texture2D inputImage,
//         List<DetectionOutput0> detections,
//         Dictionary<int, Obfuscation.Type> classObfuscationTypes,
//         int pixelSize = 4,
//         int blurSize = 5,
//         bool DrawBBoxes = false
//     )
//     {
//         // Create a copy of the input image
//         Texture2D outputImage = new(inputImage.width, inputImage.height);
//         outputImage.SetPixels(inputImage.GetPixels());
//         outputImage.Apply();

//         // Calculate the scale factors
//         var scaleX = (float)outputImage.width / yoloInputSize;
//         var scaleY = (float)outputImage.height / yoloInputSize;

//         foreach (var detect in detections)
//         {
//             // Calculate the new coordinates based on the scale factors
//             var newX1 = (int)(detect.X1 * scaleX);
//             var newY1 = (int)(detect.Y1 * scaleY);
//             var newX2 = (int)(detect.X2 * scaleX);
//             var newY2 = (int)(detect.Y2 * scaleY);

//             var obfuscationType = classObfuscationTypes[detect.ClassID];

//             switch (obfuscationType)
//             {
//                 case Obfuscation.Type.Masking:

//                     ImgUtils.MaskTexture(
//                         texture: outputImage,
//                         mask: detect.MaskMatrix,
//                         maskColor: new Color(1, 0, 0, 1)
//                     );
//                     break;

//                 case Obfuscation.Type.Pixelation:
//                     ImgUtils.PixelateTexture(
//                         texture: outputImage,
//                         mask: detect.MaskMatrix,
//                         pixelSize: pixelSize
//                     );
//                     break;

//                 case Obfuscation.Type.Blurring:

//                     ImgUtils.BlurTexture(
//                         texture: ref outputImage,
//                         mask: detect.MaskMatrix,
//                         blurSize: blurSize
//                     );
//                     break;

//                 case Obfuscation.Type.None:
//                     break;

//                 default:
//                     throw new ArgumentException("Invalid obfuscation type");
//             }

//             if (DrawBBoxes == true)
//             {
//                 ImgAnnot.DrawBoundingBox(
//                     texture: outputImagefcrop,
//                     x1: newX1,
//                     y1: newY1,
//                     x2: newX2,
//                     y2: newY2,
//                     color: Color.red
//                 );
//             }
//         }
//         outputImage.Apply();
//         return outputImage;
//     }

//     /// <summary>
//     /// Destroys the worker when the MonoBehaviour is destroyed.
//     /// </summary>
//     void OnDestroy()
//     {
//         worker?.Dispose();
//         worker = null;
//     }
// }
