using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Unity.VisualStudio.Editor;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Sentis;
using UnityEngine;
using Color = UnityEngine.Color;
using Debug = UnityEngine.Debug;
using L = Unity.Sentis.Layers;
using Stopwatch = System.Diagnostics.Stopwatch;

// TO DO: REVER este repo: https://github.com/needle-mirror/com.unity.sentis/blob/ccf88aa9c5d0ba9fa70efb5d91bc1e6681b0f6a7/Documentation~/manage-memory.md

/// <summary>
/// Obfuscates the image based on the detected objects.
/// </summary>
public class ImgObfuscator : MonoBehaviour
{
    [Header("YOLOv8 Model Asset")]
    [SerializeField, Range(0, 1)]
    float iouThres = 0.5f;

    [SerializeField, Range(0, 1)]
    float scoreThres = 0.35f;

    [SerializeField, Range(0, 1)]
    float maskThres = 0.5f;

    [SerializeField]
    int maxOutputBoxes = 64;
    public ModelAsset yoloV8Asset;
    public IWorker engine;

    private Model model;
    private static readonly int inputSz = 640;
    private static readonly int prtMskSz = 160;
    private static readonly int prtMsk = 32;
    private static readonly int ttlDtts = 8400;
    private Dictionary<int, string> labels;

    // OPTIMIZATION VARIABLES
    private int frameCounter = 0; // Initialize a frame counter
    private Tensor previousTensor; // Store Frame Data
    private const int DetectionFrameInterval = 3; // Set the interval for detection frames
    private Texture2D previousTexture; // Store the previous texture
    private TensorData previousTensorData = null; // Store the previous tensor data

    // [Header("Debug Text")]
    // public Text debugText;

    Ops ops; // For using the Sentis Ops tensor operations

    public static Unity.Sentis.DeviceType deviceType;
    private static BackendType backendType;
    private bool[,] boolCrpMsk;
    private List<bool[,]> savedMasks = new List<bool[,]>();

    public struct Scale
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float XPrt { get; set; }
        public float YPrt { get; set; }
    }

    public class SavedMask
    {
        public bool[,] Mask { get; set; }
        public int ClassID { get; set; }
    }

    // public class TensorData
    // {
    //     public TensorFloat BoxCoordsAll { get; set; }
    //     public TensorInt NMS { get; set; }
    //     public TensorInt ClassIDs { get; set; }
    //     public TensorFloat Masks { get; set; }
    // }

    public class TensorData
    {
        public TensorFloat BoxCoordsAll { get; set; }
        public TensorInt NMS { get; set; }
        public TensorInt ClassIDs { get; set; }
        public TensorFloat Masks { get; set; }

        // Default constructor
        public TensorData() { }

        // Copy constructor
        public TensorData(TensorData other)
        {
            if (other.BoxCoordsAll != null)
            {
                this.BoxCoordsAll = (TensorFloat)other.BoxCoordsAll.DeepCopy();
            }

            if (other.NMS != null)
            {
                this.NMS = (TensorInt)other.NMS.DeepCopy();
            }

            if (other.ClassIDs != null)
            {
                this.ClassIDs = (TensorInt)other.ClassIDs.DeepCopy();
            }

            if (other.Masks != null)
            {
                this.Masks = (TensorFloat)other.Masks.DeepCopy();
            }
        }
    }

    ///<summary>
    /// In the Start method, the device type is determined based on the graphics
    /// device type. The best backend type for the device is obtained, and the
    /// YOLO model asset is loaded. Labels are parsed from the YOLO model.
    ///</summary>
    void Start()
    {
        deviceType = GetDeviceType();
        backendType = WorkerFactory.GetBestTypeForDevice(deviceType);

        // Force GPU backend
        //var backendType = BackendType.CPU;
        //var backendType = BackendType.GPUPixel; // For WebGL

        ops = WorkerFactory.CreateOps(backendType, null);

        Debug.Log(
            " Start() - backendType: " + backendType + "deviceType: " + deviceType + "ops: " + ops
        );
        LoadSegmentationModel();
    }

    /// <summary>
    /// Loads the YOLOv8 instance segmentation model.
    /// </summary>
    void LoadSegmentationModel()
    {
        model = ModelLoader.Load(yoloV8Asset);
        engine = WorkerFactory.CreateWorker(WorkerFactory.GetBestTypeForDevice(deviceType), model);

        // Force GPU backend
        // engine = WorkerFactory.CreateWorker(BackendType.GPUCompute, model);

        if (engine == null)
            Debug.LogError("Worker is null after initialization");

        labels = ParseLabelNames(model);
        int numClasses = labels.Count;

        // Set Constants
        model.AddConstant(new L.Constant("0", new int[] { 0 }));
        model.AddConstant(new L.Constant("1", new int[] { 1 }));
        model.AddConstant(new L.Constant("4", new int[] { 4 }));
        model.AddConstant(new L.Constant("totalBoxes", new int[] { ttlDtts }));
        model.AddConstant(new L.Constant("masksProtos", new int[] { prtMsk }));
        model.AddConstant(new L.Constant("maskSz", new int[] { prtMskSz }));

        model.AddConstant(new L.Constant("cls_box", new int[] { numClasses + 4 }));
        model.AddConstant(new L.Constant("cls_box_protos", new int[] { numClasses + 4 + prtMsk }));
        model.AddConstant(new L.Constant("maxOutputBoxes", new int[] { maxOutputBoxes }));
        model.AddConstant(new L.Constant("iouThreshold", new float[] { iouThres }));
        model.AddConstant(new L.Constant("scoreThreshold", new float[] { scoreThres }));
        model.AddConstant(new L.Constant("coefsShape", new int[] { ttlDtts, prtMsk }));
        model.AddConstant(
            new L.Constant("masksShape2D", new int[] { prtMsk, prtMskSz * prtMskSz })
        );
        model.AddConstant(new L.Constant("masksShape", new int[] { ttlDtts, prtMskSz, prtMskSz }));

        // Add Layers to process the boxes output
        model.AddLayer(new L.Slice("boxCoords0", "output0", "0", "4", "1"));
        model.AddLayer(new L.Transpose("boxCoords", "boxCoords0", new int[] { 0, 2, 1 }));
        model.AddLayer(new L.Slice("scores0", "output0", "4", "cls_box", "1"));
        model.AddLayer(new L.ReduceMax("scores", new[] { "scores0", "1" }));
        model.AddLayer(new L.ArgMax("classIDs", "scores0", 1));

        model.AddLayer(
            new L.NonMaxSuppression(
                name: "NMS",
                boxes: "boxCoords",
                scores: "scores",
                maxOutputBoxesPerClass: "maxOutputBoxes",
                iouThreshold: "iouThreshold",
                scoreThreshold: "scoreThreshold",
                centerPointBox: L.CenterPointBox.Center
            )
        );

        // Add layers to process the masks output
        model.AddLayer(
            new L.Slice(
                name: "masksCoefs",
                input: "output0",
                starts: "cls_box",
                ends: "cls_box_protos",
                axes: "1"
            )
        ); // (1, 32, 8400)

        // masksCoefs dims: (1, 32, 8400), masksProtos dims: (1, 32, 160, 160)
        // Now, we need to multiply the masks coefficients by the mask prototypes
        // for this, we need to reshape the masksCoefs to (8400, 32) and the masksProtos to (32, 160*160)

        // (1, 32, 8400) to (8400, 32) classObfuscationTypes
        model.AddLayer(new L.Transpose("masksCoefs", "masksCoefs", new int[] { 0, 2, 1 }));
        model.AddLayer(new L.Reshape("masksCoefsRS", "masksCoefs", "coefsShape")); // (8400, 32)
        model.AddLayer(new L.Reshape("masksProtosRS", "output1", "masksShape2D")); // (32, 160*160)
        model.AddLayer(new L.MatMul("masksFlat", "masksCoefsRS", "masksProtosRS")); // (8400, 160*160)
        model.AddLayer(new L.Reshape("masks", "masksFlat", "masksShape")); // (8400, 160*160) to (8400, 160, 160)

        // Layer to multiply the masks coefficients by the mask prototypes (8400, 32) * (32, 160*160) = (8400, 25600)
        model.outputs.Clear();
        model.AddOutput("boxCoords");
        model.AddOutput("classIDs");
        model.AddOutput("NMS");
        model.AddOutput("masks");
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="ipnTxtr"></param>
    /// <param name="classObfuscationTypes"></param>
    public Texture2D Run(Texture ipnTxtr, Dictionary<int, Obfuscation.Type> classObfuscationTypes)
    {
        DebugText.Instance.UpdateTxt("@Run");
        // Increment the frame counter
        frameCounter++;

        // Calculate scale factors
        var scale = CalculateScaleFactors(ipnTxtr);

        // Convert input texture to tensor and create a copy of the input texture
        var inputTensor = TextureToTensor(ipnTxtr as Texture);
        var outTxtr = CopyTexture2D(ipnTxtr);
        InitClassDict(classObfuscationTypes); // Initialize class dictionary

        // Process tensors
        ProcessTensors(inputTensor, outTxtr, classObfuscationTypes, scale);

        // Dispose of the input tensor
        inputTensor.Dispose();

        return outTxtr;
    }

    private Scale CalculateScaleFactors(Texture ipnTxtr)
    {
        return new Scale
        {
            XPrt = ipnTxtr.width / (float)prtMskSz,
            YPrt = ipnTxtr.height / (float)prtMskSz,
            X = ipnTxtr.width / (float)inputSz,
            Y = ipnTxtr.height / (float)inputSz
        };
    }

    /// <summary>
    /// Processes the input tensor and applies obfuscation based on detected classes.
    /// </summary>
    /// <param name="inputTensor">The input tensor with image data.</param>
    /// <param name="outTxtr">The output texture for the obfuscated image.</param>
    /// <param name="classObfuscationTypes">Maps class IDs to obfuscation types.</param>
    /// <param name="scale">Scale factors for the input tensor and output texture.</param>
    /// <remarks>
    /// Checks if the current frame is a detection frame, runs the model, and gets output tensors.
    /// If bounding box coordinates of current and previous frames are similar, processes output tensors with skipping.
    /// Otherwise, processes without skipping and stores current tensor data for the next frame.
    /// If not a detection frame and there is previous tensor data, processes the output tensors of the previous frame.
    /// </remarks>
    // private void ProcessTensors(
    //     Tensor inputTensor,
    //     Texture2D outTxtr,
    //     Dictionary<int, Obfuscation.Type> classObfuscationTypes,
    //     Scale scale
    // )
    // {
    //     if (IsDetectionFrame()) // Check if it's a detection frame
    //     {
    //         if (ModelRunAndGetOutputs(inputTensor, out var tensorData))
    //         {
    //             if (
    //                 previousTensorData != null
    //                 && IsBoxCoordsSimilar(
    //                     tensorData,
    //                     previousTensorData,
    //                     inputTensor.shape[2],
    //                     inputTensor.shape[1]
    //                 )
    //             )
    //             {
    //                 DebugText.Instance.UpdateTxt("@Similar");
    //                 ProcessOutputTensors(
    //                     previousTensorData,
    //                     outTxtr,
    //                     classObfuscationTypes,
    //                     scale,
    //                     true
    //                 );
    //             }
    //             else
    //             {
    //                 DebugText.Instance.UpdateTxt("@NotSimilar");
    //                 ProcessOutputTensors(tensorData, outTxtr, classObfuscationTypes, scale);
    //                 // previousTensorData = tensorData; // Store the current detection results
    //                 previousTensorData = new TensorData(tensorData);
    //             }
    //         }
    //     }
    //     else // If it's skipped frame
    //     {
    //         if (previousTensorData != null)
    //         {
    //             DebugText.Instance.UpdateTxt("@Stride");
    //             ProcessOutputTensors(
    //                 previousTensorData,
    //                 outTxtr,
    //                 classObfuscationTypes,
    //                 scale,
    //                 true
    //             );
    //         }
    //     }
    // }

    /// <summary>
    /// Processes tensors based on the current frame type and updates the output texture accordingly.
    /// </summary>
    /// <param name="inputTensor">The input tensor to be processed.</param>
    /// <param name="outTxtr">The output texture to be updated.</param>
    /// <param name="classObfuscationTypes">A dictionary mapping class IDs to obfuscation types.</param>
    /// <param name="scale">The scale at which to process the tensors.</param>
    /// <remarks>
    /// This method first checks if the current frame is a detection frame.
    /// If it is, it runs the model and compares the current tensor data with the previous tensor data.
    /// If the data is similar, it reuses the previous tensor data; otherwise,
    /// it processes the current tensor data and updates the previous tensor data with a deep copy of the current tensor data.
    /// If the current frame is not a detection frame, it processes the previous tensor data if available.
    /// </remarks>
    private void ProcessTensors(
        Tensor inputTensor,
        Texture2D outTxtr,
        Dictionary<int, Obfuscation.Type> classObfuscationTypes,
        Scale scale
    )
    {
        if (!IsDetectionFrame()) // if it's a skipped frame, use the previous tensor data
        {
            DebugText.Instance.UpdateTxt("@Stride");
            ProcessPreviousTensorData(outTxtr, classObfuscationTypes, scale);
            return;
        }

        if (!ModelRunAndGetOutputs(inputTensor, out var tensorData)) // if no objects are detected
        {
            Debug.Log("No objects detected");
            return;
        }

        if (previousTensorData != null && previousTensorData.ClassIDs != null)
        {
            if (
                IsBoxCoordsSimilar(
                    tensorData,
                    previousTensorData,
                    inputTensor.shape[2],
                    inputTensor.shape[1]
                )
            ) // if the current and previous frames are similar, use the previous tensor data
            {
                DebugText.Instance.UpdateTxt("@Similar");
                ProcessPreviousTensorData(outTxtr, classObfuscationTypes, scale);
            }
        }
        else // if the current and previous frames are not similar, process the current tensor data
        {
            DebugText.Instance.UpdateTxt("@NotSimilar");
            ProcessOutputTensors(tensorData, outTxtr, classObfuscationTypes, scale);
            previousTensorData = new TensorData(tensorData);
        }
    }

    private void ProcessPreviousTensorData(
        Texture2D outTxtr,
        Dictionary<int, Obfuscation.Type> classObfuscationTypes,
        Scale scale
    )
    {
        if (previousTensorData == null)
            return;
        ProcessOutputTensors(previousTensorData, outTxtr, classObfuscationTypes, scale, true);
    }

    private bool IsDetectionFrame()
    {
        DebugText.Instance.UpdateTxt("@IsDetectionFrame");
        return frameCounter % DetectionFrameInterval == 0;
    }

    private static Texture2D CopyTexture2D(Texture ipnTxtr)
    {
        Texture2D outTxtr = new(ipnTxtr.width, ipnTxtr.height, TextureFormat.RGBA32, false);
        Graphics.CopyTexture(ipnTxtr, outTxtr);
        return outTxtr;
    }

    private static Tensor TextureToTensor(Texture ipnTxtr)
    {
        // Convert the input texture to a Tensor
        Tensor inputTensor = TextureConverter.ToTensor(
            ipnTxtr,
            width: inputSz,
            height: inputSz,
            channels: 3
        );
        inputTensor.MakeReadable();
        return inputTensor;
    }

    private bool ModelRunAndGetOutputs(Tensor inputTensor, out TensorData tensorData)
    {
        DebugText.Instance.UpdateTxt("@ModelRunAndGetOutputs");
        engine.Execute(inputTensor);

        tensorData = new TensorData
        {
            BoxCoordsAll = engine.PeekOutput("boxCoords") as TensorFloat,
            NMS = engine.PeekOutput("NMS") as TensorInt,
            ClassIDs = engine.PeekOutput("classIDs") as TensorInt,
            Masks = engine.PeekOutput("masks") as TensorFloat
        };

        // print the organized tensor data
        tensorData.BoxCoordsAll.PrintDataPart(10, "M_BoxCoordsAll");
        tensorData.NMS.PrintDataPart(10, "M_NMS");

        return tensorData.ClassIDs is not null;
    }

    private void ProcessOutputTensors(
        TensorData tData,
        Texture2D outTxtr,
        Dictionary<int, Obfuscation.Type> classObfuscationTypes,
        Scale scale,
        bool skip = false
    )
    {
        DebugText.Instance.UpdateTxt("@ProcessOutputTensors");

        // if (skip)
        // {
        //     foreach (var savedMask in savedMasks)
        //     {
        //         ApplyObfuscation(classObfuscationTypes[savedMask.ClassID], outTxtr, savedMask.Mask);
        //     }
        //     return; // Exit early if skip is true
        // }
        using var boxIDs = ops.Slice(
            tData.NMS,
            new int[] { 2 },
            new int[] { 3 },
            new int[] { 1 },
            new int[] { 1 }
        );
        using var boxIDsFlat =
            boxIDs.ShallowReshape(new TensorShape(boxIDs.shape.length)) as TensorInt;
        using var boxCoords = ops.Gather(tData.BoxCoordsAll, boxIDsFlat, 1) as TensorFloat;
        using var labelIDs = ops.Gather(tData.ClassIDs, boxIDsFlat, 2) as TensorInt;
        using var selectedMasks = ops.Gather(tData.Masks, boxIDsFlat, 0) as TensorFloat;

        boxIDsFlat.PrintDataPart(10, "boxIDsFlat");
        boxCoords.PrintDataPart(10, "boxCoords");
        labelIDs.PrintDataPart(10, "labelIDs");
        selectedMasks.PrintDataPart(10, "selectedMasks");

        // Get Box IDs from NMS, flatten them, and gather the box coordinates, class IDs

        // TensorInt boxIDsFlat =
        //     ops.Slice(tensorData.NMS, new[] { 2 }, new[] { 3 }, new[] { 1 }, new[] { 1 })
        //         .ShallowReshape(tensorData.NMS.shape) as TensorInt;

        // tensorData.NMS.PrintDataPart(10, "NMS");

        // boxIDsFlat.PrintDataPart(10, "boxIDsFlat");
        // TensorFloat boxCoords = ops.Gather(tensorData.BoxCoordsAll, boxIDsFlat, 1) as TensorFloat;
        // boxCoords.PrintDataPart(10, "boxCoords");
        // TensorInt labelIDs = ops.Gather(tensorData.ClassIDs, boxIDsFlat, 2) as TensorInt;
        // TensorFloat selectedMasks = ops.Gather(tensorData.Masks, boxIDsFlat, 0) as TensorFloat;

        // Make labelIDs and boxCoords readable if backendType is GPUCompute
        if (backendType == BackendType.GPUCompute)
        {
            labelIDs.MakeReadable();
            boxCoords.MakeReadable();
        }

        // Print the labelIDs and boxCoords
        labelIDs.PrintDataPart(10, "labelIDs");

        // Dispose of tensors
        tData.Masks.Dispose();
        tData.NMS.Dispose();
        tData.ClassIDs.Dispose();
        tData.BoxCoordsAll.Dispose();
        boxIDsFlat.Dispose();

        Debug.Log("P_boxCoords shape: " + boxCoords.shape[1]);

        for (int i = 0; i < boxCoords.shape[1]; i++)
        {
            var obfuscationType = classObfuscationTypes[labelIDs[i]];
            (float x_center, float y_center, float width, float height) = (
                boxCoords[0, i, 0, 0],
                boxCoords[0, i, 0, 1],
                boxCoords[0, i, 0, 2],
                boxCoords[0, i, 0, 3]
            );

            int[] boxCoordx1y1x2y2 = ImgUtils.CenterToCorner(
                x: (int)x_center,
                y: (int)y_center,
                w: (int)width,
                h: (int)height
            );

            Debug.Log(
                "P_boxCoordx1y1x2y2: "
                    + boxCoordx1y1x2y2[0]
                    + " "
                    + boxCoordx1y1x2y2[1]
                    + " "
                    + boxCoordx1y1x2y2[2]
                    + " "
                    + boxCoordx1y1x2y2[3]
            );

            TensorFloat maskTensor =
                ops.Slice(
                    X: selectedMasks,
                    starts: new int[] { i, 0, 0 },
                    ends: new int[] { i + 1, 160, 160 },
                    axes: new int[] { 0, 1, 2 },
                    steps: new int[] { 1, 1, 1 }
                ) as TensorFloat;

            selectedMasks.Dispose();

            TensorFloat maskTf = ops.Reshape(maskTensor, new TensorShape(160, 160)) as TensorFloat;

            Debug.Log("P_maskTf shape: " + maskTf.shape);

            maskTensor.Dispose();

            // maskTf.MakeReadable();

            TensorFloat maskTfRz = ResizeTF_v3(
                input: maskTf,
                scaleX: scale.XPrt,
                scaleY: scale.YPrt,
                invertY: true
            ); // Shape should be (inpWdth, inpHght)
            // Write every mask to a PNG file with index

            maskTf.Dispose();

            boolCrpMsk = CropAndBinarizeMask(
                maskTensor: maskTfRz,
                x1y1x2y2: boxCoordx1y1x2y2,
                scaleX: scale.X,
                scaleY: scale.Y
            );

            ImageWriter.WriteBoolMatrixToPNG(boolCrpMsk, "Assets/DebugOutputs/MMMask_" + i + ".png");

            maskTfRz.Dispose();

            // save each boolCrpMsk to a list
            // savedMasks.Add(boolCrpMsk);
            
            // savedMasks.Add(new SavedMask { Mask = boolCrpMsk, ClassID = labelIDs[i] });

            ApplyObfuscation(obfuscationType, outTxtr, boolCrpMsk);
        }
    }

    /// <summary>
    /// Checks if the bounding box coordinates of the current and previous data are similar.
    /// </summary>
    /// <param name="currentData">The current TensorData object.</param>
    /// <param name="previousData">The previous TensorData object.</param>
    /// <param name="imageWidth">The width of the image.</param>
    /// <param name="imageHeight">The height of the image.</param>
    /// <returns>
    /// Returns true if the distance between the normalized current and previous coordinates,
    /// as well as their sizes, is greater than a certain threshold (0.01). Otherwise, returns false.
    /// </returns>
    /// <remarks>
    /// The method first checks if the BoxCoordsAll property of both TensorData objects is not null and
    /// if their shapes are equal. If not, it logs an error and returns false.
    /// Then, it makes the BoxCoordsAll of the previous TensorData readable and converts the BoxCoordsAll
    /// of both TensorData objects to arrays.
    /// It then iterates over the coordinates, normalizes them and their sizes, and calculates the distance
    /// between the current and previous normalized coordinates and sizes.
    /// If the distance is greater than the square of the threshold (0.01), it returns true. If not, it continues
    /// with the next set of coordinates. If no set of coordinates has a distance greater than the threshold,
    /// it returns false.
    /// </remarks>
    public bool IsBoxCoordsSimilar(
        TensorData currentData,
        TensorData previousData,
        float imageWidth,
        float imageHeight
    )
    {
        DebugText.Instance.UpdateTxt("@IsBoxCoordsSimilar");
        if (
            currentData.BoxCoordsAll == null
            || previousData.BoxCoordsAll == null
            || currentData.BoxCoordsAll.shape != previousData.BoxCoordsAll.shape
        )
        {
            Debug.LogError("B_Invalid data for comparison.");
            return false;
        }

        currentData.BoxCoordsAll.MakeReadable();

        float[] currentCoords = currentData.BoxCoordsAll.ToReadOnlyArray();
        // Make the previous coordinates readable if backendType is GPUCompute,

        // previousData.BoxCoordsAll.MakeReadable();
        // previousData.BoxCoordsAll.MakeReadable(); // CODIGO FICA PRESO AQUI
        // previousData.BoxCoordsAll.PrintDataPart(10);


        float[] previousCoords = previousData.BoxCoordsAll.ToReadOnlyArray();

        for (int i = 0; i < currentCoords.Length; i += 4)
        {
            Vector2 normalizedCurrent = new Vector2(
                currentCoords[i] / imageWidth,
                currentCoords[i + 1] / imageHeight
            );
            Vector2 normalizedCurrentSize = new Vector2(
                currentCoords[i + 2] / imageWidth,
                currentCoords[i + 3] / imageHeight
            );

            Vector2 normalizedPrevious = new Vector2(
                previousCoords[i] / imageWidth,
                previousCoords[i + 1] / imageHeight
            );
            Vector2 normalizedPreviousSize = new Vector2(
                previousCoords[i + 2] / imageWidth,
                previousCoords[i + 3] / imageHeight
            );

            float distance =
                (normalizedCurrent - normalizedPrevious).sqrMagnitude
                + (normalizedCurrentSize - normalizedPreviousSize).sqrMagnitude;

            if (distance > 0.01f) // square of the threshold
                return true;
        }

        return false;
    }

    // public Texture2D Run(
    //     Texture ipnTxtr,
    //     Dictionary<int, Obfuscation.Type> classObfuscationTypes,
    //     bool DrawBBoxes = false
    // )
    // {
    //     using Tensor inputTensor = TextureConverter.ToTensor(
    //         ipnTxtr,
    //         width: inputSz,
    //         height: inputSz,
    //         channels: 3
    //     );
    //     inputTensor.MakeReadable();

    //     InitClassDict(classObfuscationTypes); // Initialize the unspecified classes to None

    //     (int inpWdth, int inpHght) = (ipnTxtr.width, ipnTxtr.height); // Get the input texture dimensions

    //     // Calculate the scale factors for the mask resizing
    //     (float scaleXPrt, float scaleYPrt) = (inpWdth / (float)prtMskSz, inpHght / (float)prtMskSz);
    //     (float scaleX, float scaleY) = (inpWdth / (float)inputSz, inpHght / (float)inputSz);

    //     // Create a copy of the input Texture to Texture2D
    //     Texture2D outTxtr = new(ipnTxtr.width, ipnTxtr.height, TextureFormat.RGBA32, false);
    //     Graphics.CopyTexture(ipnTxtr, outTxtr);

    //     // Run the model
    //     engine.Execute(inputTensor);

    //     // Get the output tensors
    //     TensorFloat boxCoordsAll = engine.PeekOutput("boxCoords") as TensorFloat;
    //     TensorInt NMS = engine.PeekOutput("NMS") as TensorInt;
    //     TensorInt classIDs = engine.PeekOutput("classIDs") as TensorInt;
    //     TensorFloat masks = engine.PeekOutput("masks") as TensorFloat;

    //     if (classIDs == null)
    //     {
    //         return outTxtr; // Return the input texture if no objects are detected
    //     }

    //     // Get Box IDs from NMS
    //     Tensor boxIDs = ops.Slice(NMS, new[] { 2 }, new[] { 3 }, new[] { 1 }, new[] { 1 });

    //     // Flatten the boxIDs
    //     TensorShape boxIDsShape = boxIDs.shape;
    //     TensorInt boxIDsFlat = boxIDs.ShallowReshape(boxIDsShape) as TensorInt;

    //     // Gather the box coordinates, class IDs
    //     TensorFloat boxCoords = ops.Gather(boxCoordsAll, boxIDsFlat, 1) as TensorFloat;
    //     TensorInt labelIDs = ops.Gather(classIDs, boxIDsFlat, 2) as TensorInt;
    //     TensorFloat selectedMasks = ops.Gather(masks, boxIDsFlat, 0) as TensorFloat;

    //     // For GPU backend
    //     if (backendType == BackendType.GPUCompute)
    //     {
    //         labelIDs.MakeReadable();
    //         boxCoords.MakeReadable();
    //     }

    //     // --- SIMILARITY SCORE ------------------------------------------------

    //     // float similarityScore = SimilarityScore(
    //     //     boxCoords: boxCoords,
    //     //     labelIDs: labelIDs
    //     // );

    //     // --- SIMILARITY SCORE ------------------------------------------------

    //     // Dispose of the tensors
    //     masks.Dispose();
    //     boxIDs.Dispose();
    //     boxIDsFlat.Dispose();
    //     boxCoordsAll.Dispose();
    //     NMS.Dispose();
    //     classIDs.Dispose();

    //     for (int i = 0; i < boxCoords.shape[1]; i++)
    //     {
    //         var obfuscationType = classObfuscationTypes[labelIDs[i]];
    //         bool[,] boolCrpMsk = new bool[0, 0];
    //         (float x_center, float y_center, float width, float height) = (
    //             boxCoords[0, i, 0, 0],
    //             boxCoords[0, i, 0, 1],
    //             boxCoords[0, i, 0, 2],
    //             boxCoords[0, i, 0, 3]
    //         );

    //         int[] boxCoordx1y1x2y2 = ImgUtils.CenterToCorner(
    //             x: (int)x_center,
    //             y: (int)y_center,
    //             w: (int)width,
    //             h: (int)height
    //         );

    //         if (DrawBBoxes == true)
    //         {
    //             ImgAnnot.DrawBoundingBox(
    //                 texture: outTxtr,
    //                 x1y1x2y2: boxCoordx1y1x2y2,
    //                 color: Color.red,
    //                 scaleX: scaleX,
    //                 scaleY: scaleY
    //             );
    //         }

    //         //  ---   Obfuscate the image based on the detected objects   ---
    //         TensorFloat maskTensor =
    //             ops.Slice(
    //                 X: selectedMasks,
    //                 starts: new int[] { i, 0, 0 },
    //                 ends: new int[] { i + 1, 160, 160 },
    //                 axes: new int[] { 0, 1, 2 },
    //                 steps: new int[] { 1, 1, 1 }
    //             ) as TensorFloat;

    //         selectedMasks.Dispose();

    //         TensorFloat maskTf = ops.Reshape(maskTensor, new TensorShape(160, 160)) as TensorFloat;

    //         maskTensor.Dispose();

    //         maskTf.MakeReadable();

    //         TensorFloat maskTfRz = ResizeTF_v3(
    //             input: maskTf,
    //             scaleX: scaleXPrt,
    //             scaleY: scaleYPrt,
    //             invertY: true
    //         ); // Shape should be (inpWdth, inpHght)

    //         maskTf.Dispose();

    //         boolCrpMsk = CropAndBinarizeMask(
    //             maskTensor: maskTfRz,
    //             x1y1x2y2: boxCoordx1y1x2y2,
    //             scaleX: scaleX,
    //             scaleY: scaleY
    //         );

    //         maskTfRz.Dispose();


    //         ApplyObfuscation(obfuscationType, outTxtr, boolCrpMsk);

    //         boolCrpMsk = null;
    //     }

    //     // // Dispose of the tensors
    //     // boxIDs.Dispose();
    //     // boxIDsFlat.Dispose();
    //     // boxCoords.Dispose();
    //     // labelIDs.Dispose();
    //     // selectedMasks.Dispose();
    //     // inputTensor.Dispose();

    //     Texture2D lastOutTxtr = outTxtr;

    //     return outTxtr;
    // }

    /// <summary>
    /// Crops and binarizes a mask tensor based on the given box coordinates and scaling factors.
    /// </summary>
    /// <param name="maskTensor">The input mask tensor.</param>
    /// <param name="x1y1x2y2">The box coordinates [x1, y1, x2, y2].</param>
    /// <param name="scaleX">The scaling factor for the x-axis.</param>
    /// <param name="scaleY">The scaling factor for the y-axis.</param>
    /// <param name="invertY">Flag indicating whether to invert the y-axis.</param>
    /// <returns>A boolean array representing the cropped and binarized mask.</returns>
    private bool[,] CropAndBinarizeMask(
        TensorFloat maskTensor,
        int[] x1y1x2y2,
        float scaleX,
        float scaleY,
        bool invertY = true
    )
    {
        DebugText.Instance.UpdateTxt("@CropAndBinarizeMask");
        // Scale and validate the box coordinates
        int x1 = Mathf.Max((int)(x1y1x2y2[0] * scaleX), 0);
        int y1 = Mathf.Max((int)(x1y1x2y2[1] * scaleY), 0);
        int x2 = Mathf.Min((int)(x1y1x2y2[2] * scaleX), maskTensor.shape[0]);
        int y2 = Mathf.Min((int)(x1y1x2y2[3] * scaleY), maskTensor.shape[1]);

        // binarize the mask
        maskTensor = ops.Sigmoid(maskTensor);
        TensorShape maskShape = maskTensor.shape;
        TensorFloat maskConstant = ops.ConstantOfShape(maskShape, maskThres);
        TensorInt masksBin = ops.GreaterOrEqual(maskTensor, maskConstant);

        // masksBin.MakeReadable();

        // For GPU backend
        // masksBin.MakeReadable();

        // maskTensor.PrintDataPart(1000, msg: "maskTensor");
        // maskConstant.PrintDataPart(1000, msg: "maskConstant");

        if (invertY)
        {
            y1 = maskTensor.shape[1] - y1;
            y2 = maskTensor.shape[1] - y2;
            (y1, y2) = (y2, y1);
        }

        // Create a boolean array to represent the cropped and binarized mask
        bool[,] boolCrpMsk = new bool[maskTensor.shape[0], maskTensor.shape[1]];

        // Iterate over the mask and set the values in the boolCrpMsk
        for (int i = x1; i < x2; i++)
        {
            for (int j = y1; j < y2; j++)
            {
                boolCrpMsk[i, j] = masksBin[i, j] == 1;
            }
        }
        return boolCrpMsk;
    }

    /// <summary>
    /// Applies the obfuscation to the image based on the detected objects.
    /// </summary>
    private static void ApplyObfuscation(
        Obfuscation.Type obfuscationType,
        Texture2D outTxtr,
        bool[,] boolCrpMsk
    )
    {
        DebugText.Instance.UpdateTxt("@ApplyObfuscation");
        switch (obfuscationType)
        {
            case Obfuscation.Type.Masking:
                ImgUtils.MaskTexture(outTxtr, boolCrpMsk, Color.red);
                break;

            case Obfuscation.Type.Pixelation:
                ImgUtils.PixelateTexture(outTxtr, boolCrpMsk, 20);
                break;

            case Obfuscation.Type.Blurring:
                ImgUtils.BlurTexture(ref outTxtr, boolCrpMsk, 7);
                break;

            case Obfuscation.Type.None:
                break;

            default:
                throw new ArgumentException("Invalid obfuscation type");
        }
    }

    /// <summary>
    /// Resizes the input tensor using the Resize operation using nearest.
    /// </summary>
    public static TensorFloat ResizeTF_v2(
        TensorFloat input,
        float scaleX,
        float scaleY,
        bool invertY = false
    )
    {
        int inputHeight = input.shape[0];
        int inputWidth = input.shape[1];
        int outputHeight = (int)(inputHeight * scaleY);
        int outputWidth = (int)(inputWidth * scaleX);

        // Create a new TensorFloat to store the upsampled result
        var output = TensorFloat.Zeros(new TensorShape(outputWidth, outputHeight));

        // Iterate over each position in the output TensorFloat
        for (int y_out = 0; y_out < outputHeight; y_out++)
        {
            for (int x_out = 0; x_out < outputWidth; x_out++)
            {
                int x_nn = (int)Math.Round(x_out / scaleX);
                int y_nn = (int)Math.Round(y_out / scaleY);

                x_nn = Math.Min(Math.Max(x_nn, 0), inputWidth - 1);
                y_nn = Math.Min(Math.Max(y_nn, 0), inputHeight - 1);

                if (invertY)
                {
                    y_nn = inputHeight - 1 - y_nn;
                }
                output[x_out, y_out] = input[y_nn, x_nn];
            }
        }

        return output;
    }

    [BurstCompile(Debug = true)]
    public struct ResizeJob : IJobParallelFor
    {
        [ReadOnly]
        public NativeArray<float> input;
        public NativeArray<float> output;

        public int inputWidth;
        public int inputHeight;
        public int outputWidth;
        public int outputHeight;
        public float scaleX;
        public float scaleY;
        public bool invertY;

        public void Execute(int index)
        {
            if (input == null || output == null)
            {
                Debug.LogError("Input or output array is null.");
                return;
            }

            int y_out = index / outputWidth;
            int x_out = index % outputWidth;

            int x_nn = (int)Math.Round(x_out / scaleX);
            int y_nn = (int)Math.Round(y_out / scaleY);

            if (invertY)
            {
                y_nn = inputHeight - y_nn;
            }

            x_nn = Math.Min(Math.Max(x_nn, 0), inputWidth - 1);
            y_nn = Math.Min(Math.Max(y_nn, 0), inputHeight - 1);

            // Debug.Log("Execute index: " + index);

            // output[index] = input[y_nn * inputWidth + x_nn];

            int outputIndex = y_out * outputWidth + x_out;
            output[outputIndex] = input[y_nn * inputWidth + x_nn];
        }
    }

    public static TensorFloat ResizeTF_v3(
        TensorFloat input,
        float scaleX,
        float scaleY,
        bool invertY = false
    )
    {
        DebugText.Instance.UpdateTxt("@ResizeTF_v3");
        int inputHeight = input.shape[0];
        int inputWidth = input.shape[1];
        int outputHeight = (int)(inputHeight * scaleY);
        int outputWidth = (int)(inputWidth * scaleX);

        // Convert input tensor to NativeArray for efficient access
        float[] inputData = input.ToReadOnlyArray(); // Flat array of shape (160 * 160)
        NativeArray<float> inputNativeArray = new(inputData, Allocator.TempJob);
        NativeArray<float> outputNativeArray = new(outputWidth * outputHeight, Allocator.TempJob);

        // Debug.Log("Resize inputNativeArray size: " + inputNativeArray.Length);
        // Debug.Log("Resize outputNativeArray size: " + outputNativeArray.Length);
        // Debug.Log("Resize input shape: " + input.shape);
        // Debug.Log("Resize output shape: " + outputWidth + "x" + outputHeight);

        // Create a new job
        var job = new ResizeJob
        {
            input = inputNativeArray,
            output = outputNativeArray,
            inputWidth = inputWidth,
            inputHeight = inputHeight,
            outputWidth = outputWidth,
            outputHeight = outputHeight,
            scaleX = scaleX,
            scaleY = scaleY,
            invertY = invertY
        };

        try
        {
            // Schedule the job
            JobHandle handle = job.Schedule(
                arrayLength: outputWidth * outputHeight,
                innerloopBatchCount: 64
            );

            // Wait for the job to complete
            handle.Complete();

            // Convert the output NativeArray to a TensorFloat; TODO: Make this more efficient !!!
            TensorFloat output = TensorFloat.Zeros(new TensorShape(outputWidth, outputHeight));
            for (int y = 0; y < outputHeight; y++)
            {
                for (int x = 0; x < outputWidth; x++)
                {
                    output[x, y] = outputNativeArray[y * outputWidth + x];
                }
            }

            return output;
        }
        finally
        {
            // Dispose of NativeArrays to avoid memory leaks
            inputNativeArray.Dispose();
            outputNativeArray.Dispose();
        }
    }

    /// <summary>
    /// Resizes the input tensor using the Resize operation. (160x160 to output width x height)
    /// </summary>
    public TensorFloat ResizeTF_not_working(TensorFloat input, float scaleX, float scaleY)
    {
        // -- ops.Resize: Does not behave as expected ??? --

        float[] scale = new float[] { 2.25f, 5.0f };
        TensorFloat output = ops.Resize(
            X: input,
            scale: scale,
            interpolationMode: L.InterpolationMode.Nearest,
            coordTransformMode: L.CoordTransformMode.HalfPixel,
            nearestMode: L.NearestMode.RoundPreferFloor
        );

        // -- ops.Resize: Does not behave as expected ??? --

        // Debug.Log("output shape: " + output.shape);
        return output;
    }

    /// <summary>
    /// Initializes the unspecified classes to None. TODO: count the number of classes (model) instead of hardcoding it.
    /// </summary>
    public static void InitClassDict(
        Dictionary<int, Obfuscation.Type> classObfuscationTypes,
        int totalClasses = 80
    )
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
    /// Parses the names from the model.
    /// </summary>
    private static Dictionary<int, string> ParseLabelNames(Model model)
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
    /// Gets the device type based on the graphics device type.
    /// </summary>
    public Unity.Sentis.DeviceType GetDeviceType()
    {
        var graphicsDeviceType = SystemInfo.graphicsDeviceType;

        if (
            graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Direct3D11
            || graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Metal
            || graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Vulkan
        )
        {
            return Unity.Sentis.DeviceType.GPU;
        }
        return Unity.Sentis.DeviceType.CPU;
    }

    /// <summary>
    /// Disposes of the resources when the object is destroyed.
    /// </summary>
    void OnDestroy()
    {
        DebugText.Instance.UpdateTxt("@OnDestroy");
        engine?.Dispose();
        ops?.Dispose();
    }
}

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
