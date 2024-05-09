using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Sentis;
using UnityEditor.AssetImporters;
using UnityEngine;
using UnityEngine.UI;
using Color = UnityEngine.Color;
using Debug = UnityEngine.Debug;
using L = Unity.Sentis.Layers;

//using Stopwatch = System.Diagnostics.Stopwatch;

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
    private float maskThres = 0.5f;

    [SerializeField]
    int maxOutputBoxes = 64;
    public ModelAsset yoloV8Asset;
    private IWorker m_Engine;

    private Model m_Model;
    private static readonly int s_InputSz = 640;
    private static readonly int s_PrtMskSz = 160;
    private static readonly int s_PrtMsk = 32;
    private static readonly int s_TtlDtts = 8400;
    private Dictionary<int, string> m_Labels;

    // OPTIMIZATION VARIABLES
    private int m_FrameCounter; // Initialize a frame counter
    private Tensor m_PreviousTensor; // Store Frame Data
    private const int DetectionFrameInterval = 4; // Set the interval for detection frames
    private Texture2D m_PreviousTexture; // Store the previous texture
    private TensorData m_PreviousTensorData = null; // Store the previous tensor data
    private static Texture2D _outTexture;

    //private TensorData m_CurrentTensorData = null; // Store the current tensor data

    // [Header("Debug Text")]
    // public Text debugText;

    private Ops m_Ops; // For using the Sentis Ops tensor operations

    private static Unity.Sentis.DeviceType _deviceType;
    private static BackendType _backendType;
    private bool[,] m_BoolCrpMsk;

    private readonly List<MaskAndClassID> m_SavedMasksAndClassIDs = new List<MaskAndClassID>();

    private struct Scale
    {
        public float x { get; set; }
        public float y { get; set; }
        public float xPrt { get; set; }
        public float yPrt { get; set; }
    }

    private class MaskAndClassID
    {
        public bool[,] mask { get; set; }
        public int classID { get; set; }
    }

    public class TensorData
    {
        public TensorFloat boxCoordsAll { get; set; }
        public TensorInt nms { get; set; }
        public TensorInt classIDs { get; set; }
        public TensorFloat masks { get; set; }

        // Default constructor
        public TensorData() { }

        // Copy constructor
        public TensorData(TensorData other)
        {
            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            try
            {
                if (other.boxCoordsAll is { allocator: not null })
                {
                    other.boxCoordsAll.MakeReadable();
                    TensorShape shape = other.boxCoordsAll.shape;
                    float[] data = other.boxCoordsAll.ToReadOnlyArray();
                    this.boxCoordsAll = new TensorFloat(shape, data);
                }

                if (other.nms is { allocator: not null })
                {
                    other.nms.MakeReadable();
                    TensorShape shape = other.nms.shape;
                    int[] data = other.nms.ToReadOnlyArray();
                    this.nms = new TensorInt(shape, data);
                }

                if (other.classIDs is { allocator: not null })
                {
                    other.classIDs.MakeReadable();
                    TensorShape shape = other.classIDs.shape;
                    int[] data = other.classIDs.ToReadOnlyArray();
                    this.classIDs = new TensorInt(shape, data);
                }

                if (other.masks is { allocator: not null })
                {
                    other.masks.MakeReadable();
                    TensorShape shape = other.masks.shape;
                    float[] data = other.masks.ToReadOnlyArray();
                    this.masks = new TensorFloat(shape, data);
                }
            }
            catch (InvalidOperationException ex)
            {
                // Handle the exception as needed, for example, log the error message
                Debug.LogError($"Error copying TensorData: {ex.Message}");
            }
        }
    }

    void Start()
    {
        _deviceType = GetDeviceType();
        _backendType = WorkerFactory.GetBestTypeForDevice(_deviceType);

        // Force GPU backend
        //var backendType = BackendType.CPU;
        //var backendType = BackendType.GPUPixel; // For WebGL

        m_Ops = WorkerFactory.CreateOps(_backendType, null);

        Debug.Log(
            " Start() : backendType: "
                + _backendType
                + "deviceType: "
                + _deviceType
                + "ops: "
                + m_Ops
        );
        LoadSegmentationModel();
    }

    void LoadSegmentationModel()
    {
        m_Model = ModelLoader.Load(yoloV8Asset);
        m_Engine = WorkerFactory.CreateWorker(
            WorkerFactory.GetBestTypeForDevice(_deviceType),
            m_Model
        );

        // Force GPU backend
        // engine = WorkerFactory.CreateWorker(BackendType.GPUCompute, model);

        if (m_Engine == null)
            Debug.LogError("Worker is null after initialization");

        m_Labels = ParseLabelNames(m_Model);
        int numClasses = m_Labels.Count;

        // Set Constants
        m_Model.AddConstant(new L.Constant("0", new int[] { 0 }));
        m_Model.AddConstant(new L.Constant("1", new int[] { 1 }));
        m_Model.AddConstant(new L.Constant("4", new int[] { 4 }));
        m_Model.AddConstant(new L.Constant("totalBoxes", new int[] { s_TtlDtts }));
        m_Model.AddConstant(new L.Constant("masksProtos", new int[] { s_PrtMsk }));
        m_Model.AddConstant(new L.Constant("maskSz", new int[] { s_PrtMskSz }));

        m_Model.AddConstant(new L.Constant("cls_box", new int[] { numClasses + 4 }));
        m_Model.AddConstant(
            new L.Constant("cls_box_protos", new int[] { numClasses + 4 + s_PrtMsk })
        );
        m_Model.AddConstant(new L.Constant("maxOutputBoxes", new int[] { maxOutputBoxes }));
        m_Model.AddConstant(new L.Constant("iouThreshold", new float[] { iouThres }));
        m_Model.AddConstant(new L.Constant("scoreThreshold", new float[] { scoreThres }));
        m_Model.AddConstant(new L.Constant("coefsShape", new int[] { s_TtlDtts, s_PrtMsk }));
        m_Model.AddConstant(
            new L.Constant("masksShape2D", new int[] { s_PrtMsk, s_PrtMskSz * s_PrtMskSz })
        );
        m_Model.AddConstant(
            new L.Constant("masksShape", new int[] { s_TtlDtts, s_PrtMskSz, s_PrtMskSz })
        );

        // Add layers to process the boxes output
        m_Model.AddLayer(new L.Slice("boxCoords0", "output0", "0", "4", "1"));
        m_Model.AddLayer(new L.Transpose("boxCoords", "boxCoords0", new int[] { 0, 2, 1 }));
        m_Model.AddLayer(new L.Slice("scores0", "output0", "4", "cls_box", "1"));
        m_Model.AddLayer(new L.ReduceMax("scores", new[] { "scores0", "1" }));
        m_Model.AddLayer(new L.ArgMax("classIDs", "scores0", 1));

        m_Model.AddLayer(
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
        m_Model.AddLayer(
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
        m_Model.AddLayer(new L.Transpose("masksCoefs", "masksCoefs", new int[] { 0, 2, 1 }));
        m_Model.AddLayer(new L.Reshape("masksCoefsRS", "masksCoefs", "coefsShape")); // (8400, 32)
        m_Model.AddLayer(new L.Reshape("masksProtosRS", "output1", "masksShape2D")); // (32, 160*160)
        m_Model.AddLayer(new L.MatMul("masksFlat", "masksCoefsRS", "masksProtosRS")); // (8400, 160*160)
        m_Model.AddLayer(new L.Reshape("masks", "masksFlat", "masksShape")); // (8400, 160*160) to (8400, 160, 160)

        // Layer to multiply the masks coefficients by the mask prototypes (8400, 32) * (32, 160*160) = (8400, 25600)
        m_Model.outputs.Clear();
        m_Model.AddOutput("boxCoords");
        m_Model.AddOutput("classIDs");
        m_Model.AddOutput("NMS");
        m_Model.AddOutput("masks");
    }

    /// <summary>
    /// Runs the model and gets the outputs.
    /// </summary>
    /// <param name="inpTexture"></param>
    /// <param name="classObfuscationTypes"></param>
    public Texture2D Run(
        Texture inpTexture,
        Dictionary<int, Obfuscation.Type> classObfuscationTypes
    )
    {
        // DebugText.Instance.UpdateTxt("@Run");

        // Increment the frame counter
        m_FrameCounter++;

        // Calculate scale factors
        Scale scale = CalculateScaleFactors(inpTexture);

        // Convert input texture to tensor and create a copy of the input texture
        Tensor inputTensor = TextureToTensor(inpTexture);
        _outTexture = CopyTexture2D(inpTexture);
        InitClassDict(classObfuscationTypes); // Initialize class dictionary

        // Process tensors
        //ProcessTensors(inputTensor, outTexture, classObfuscationTypes, scale);
        ProcessTensors(inputTensor, classObfuscationTypes, scale);

        // Dispose of the input tensor
        inputTensor.Dispose();

        return _outTexture;
    }

    private static Scale CalculateScaleFactors(Texture inpTexture)
    {
        return new Scale
        {
            xPrt = inpTexture.width / (float)s_PrtMskSz,
            yPrt = inpTexture.height / (float)s_PrtMskSz,
            x = inpTexture.width / (float)s_InputSz,
            y = inpTexture.height / (float)s_InputSz
        };
    }

    /// <summary>
    /// Processes tensors based on the current frame type and updates the output texture accordingly.
    /// </summary>
    /// <param name="inputTensor">The input tensor to be processed.</param>
    /// <param name="outTexture">The output texture to be updated.</param>
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
        Dictionary<int, Obfuscation.Type> classObfuscationTypes,
        Scale scale
    )
    {
        if (!IsDetectionFrame()) // if it's a skipped frame, use the previous tensor data
        {
            //DebugText.Instance.UpdateTxt("@Stride");
            //Debug.Log("C_Stride");
            ProcessPreviousTensorData(
                m_PreviousTensorData,
                classObfuscationTypes,
                scale
            );
            return;
        }

        if (!ModelRunAndGetOutputs(inputTensor, out var currentData))
        {
            //Debug.Log("C_No objects detected");
            m_PreviousTensorData = null;
            return;
        }

        if (
            IsBoxCoordsSimilar(
                currentData,
                m_PreviousTensorData,
                inputTensor.shape[2],
                inputTensor.shape[1]
            )
        ) // if the current and previous frames are similar, use the previous tensor data
        {
            ProcessPreviousTensorData(
                m_PreviousTensorData,
                classObfuscationTypes,
                scale
            );
        }
        else // if the current and previous frames are not similar, process the current tensor data
        {
            if (currentData.boxCoordsAll == null)
            {
                m_PreviousTensorData = null;
                Debug.LogError("C_currentTensorData BoxCoordsAll is null");
                return;
            }

            // Process the current data and then update the previous data
            ProcessOutputTensors(currentData, classObfuscationTypes, scale);
            m_PreviousTensorData = new TensorData(currentData);
        }
    }

    private void ProcessPreviousTensorData(
        TensorData previousTensorData,
        Dictionary<int, Obfuscation.Type> classObfuscationTypes,
        Scale scale
    )
    {
        if (previousTensorData == null)
        {
            Debug.Log("P_previousTensorData is null");
            return;
        }
        ProcessOutputTensors(previousTensorData, classObfuscationTypes, scale, true);
    }

    private bool IsDetectionFrame()
    {
        // DebugText.Instance.UpdateTxt("@IsDetectionFrame");
        return m_FrameCounter % DetectionFrameInterval == 0;
    }

    private static Texture2D CopyTexture2D(Texture inpTexture)
    {
        Texture2D outTexture =
            new(inpTexture.width, inpTexture.height, TextureFormat.RGBA32, false);
        Graphics.CopyTexture(inpTexture, outTexture);
        return outTexture;
    }

    private static Tensor TextureToTensor(Texture inpTexture)
    {
        // Convert the input texture to a Tensor
        Tensor inputTensor = TextureConverter.ToTensor(
            inpTexture,
            width: s_InputSz,
            height: s_InputSz,
            channels: 3
        );
        inputTensor.MakeReadable();
        return inputTensor;
    }

    private bool ModelRunAndGetOutputs(Tensor inputTensor, out TensorData tensorData)
    {
        // DebugText.Instance.UpdateTxt("@ModelRunAndGetOutputs");
        m_Engine.Execute(inputTensor);

        tensorData = new TensorData
        {
            boxCoordsAll = m_Engine.PeekOutput("boxCoords") as TensorFloat,
            nms = m_Engine.PeekOutput("NMS") as TensorInt,
            classIDs = m_Engine.PeekOutput("classIDs") as TensorInt,
            masks = m_Engine.PeekOutput("masks") as TensorFloat
        };

        // print the organized tensor data
        //tensorData.boxCoordsAll.PrintDataPart(10, "M_BoxCoordsAll");
        //tensorData.nms.PrintDataPart(10, "M_NMS");

        return tensorData.classIDs is not null;
    }

    private void ProcessOutputTensors(
        TensorData tData,
        Dictionary<int, Obfuscation.Type> classObfuscationTypes,
        Scale scale,
        bool skip = false
    )
    {
        // DebugText.Instance.UpdateTxt("@ProcessOutputTensors");

        if (skip)
        {
            foreach (MaskAndClassID maskAndClassID in m_SavedMasksAndClassIDs)
            {
                //LOg number of masks
                Debug.Log($"P_SavedMasksAndClassIDs: {m_SavedMasksAndClassIDs.Count}");
                ApplyObfuscation(
                    classObfuscationTypes[maskAndClassID.classID],
                    maskAndClassID.mask);

            }
            return;
        }

        //Debug.Log($"P_tData.NMS shape: {tData.nms.shape}");
        Tensor boxIDs = m_Ops.Slice(
            tData.nms,
            new int[] { 2 },
            new int[] { 3 },
            new int[] { 1 },
            new int[] { 1 }
        );
        TensorInt boxIDsFlat =
            boxIDs.ShallowReshape(new TensorShape(boxIDs.shape.length)) as TensorInt;
        TensorFloat boxCoords = m_Ops.Gather(tData.boxCoordsAll, boxIDsFlat, 1) as TensorFloat;
        TensorInt labelIDs = m_Ops.Gather(tData.classIDs, boxIDsFlat, 2) as TensorInt;
        TensorFloat selectedMasks = m_Ops.Gather(tData.masks, boxIDsFlat, 0) as TensorFloat;
        
        if (selectedMasks.shape.rank != 3)
        {
            selectedMasks?.Dispose();
            return;
        }

        //boxIDsFlat.PrintDataPart(10, "O_boxIDsFlat");
        //boxCoords.PrintDataPart(10, "O_boxCoords");
        //labelIDs.PrintDataPart(10, "O_labelIDs");
        //selectedMasks.PrintDataPart(10, "O_selectedMasks");

        //Debug.Log("boxIDsFlat shape: " + boxIDsFlat.shape);

        // Make labelIDs and boxCoords readable if backendType is GPUCompute
        if (_backendType == BackendType.GPUCompute)
        {
            labelIDs?.MakeReadable();
            boxCoords?.MakeReadable();
        }

        // Print the labelIDs and boxCoords
        //labelIDs.PrintDataPart(10, "labelIDs");

        // Dispose of tensors
        tData.masks.Dispose();
        tData.nms.Dispose();
        tData.classIDs.Dispose();
        tData.boxCoordsAll.Dispose();
        boxIDsFlat?.Dispose();

        //Debug.Log($"P_boxCoords shape: {boxCoords.shape[1]}");
        m_SavedMasksAndClassIDs.Clear();
        
        //boxCoords.shape[1]
        Debug.Log($"P_boxCoords shape: {boxCoords.shape[1]}");

        // Process each detected object
        for (int i = 0; i < boxCoords.shape[1]; i++)
        {
            var obfuscationType = classObfuscationTypes[labelIDs[i]];
            Debug.Log($"P_labelID: {labelIDs[i]}");

            (float xCenter, float yCenter, float width, float height) = (
                boxCoords[0, i, 0, 0],
                boxCoords[0, i, 0, 1],
                boxCoords[0, i, 0, 2],
                boxCoords[0, i, 0, 3]);

            int[] boxCord1Y1X2Y2 = ImgUtils.CenterToCorner(
                x: (int)xCenter,
                y: (int)yCenter,
                w: (int)width,
                h: (int)height
            );

            /*Debug.Log(
                $"P_boxCord1y1x2y2: {boxCord1Y1X2Y2[0]} {boxCord1Y1X2Y2[1]} {boxCord1Y1X2Y2[2]} {boxCord1Y1X2Y2[3]}"
            );*/

            // Print shape of selectedMasks
            Debug.Log($"selectedMasks shape: {selectedMasks.shape}");
            
            // if selectedMasks rank != 3, dispose and return
            
            
            TensorFloat masksTensor =
                m_Ops.Slice(
                    X: selectedMasks,
                    starts: new int[] { i, 0, 0 },
                    ends: new int[] { i + 1, 160, 160 },
                    axes: new int[] { 0, 1, 2 },
                    steps: new int[] { 1, 1, 1 }
                ) as TensorFloat;
            
            //Debug.Log($"P_masksTensor shape: {masksTensor.shape}");

            TensorFloat maskTf =
                m_Ops.Reshape(masksTensor, new TensorShape(160, 160)) as TensorFloat;

            //Debug.Log($"P_maskTf shape: {maskTf.shape}");

            masksTensor?.Dispose();

            // maskTf.MakeReadable();

            TensorFloat maskTfRz = ResizeTF_v3(
                input: maskTf,
                scaleX: scale.xPrt,
                scaleY: scale.yPrt,
                invertY: true
            ); // Shape should be (inpWdth, inpHght)
            // Write every mask to a PNG file with index

            maskTf?.Dispose();

            bool[,] boolCrpMsk = CropAndBinarizeMask(
                maskTensor: maskTfRz,
                x1Y1X2Y2: boxCord1Y1X2Y2,
                scaleX: scale.x,
                scaleY: scale.y
            );

            /*ImageWriter.WriteBoolMatrixToPNG(
                boolCrpMsk,
                $"Assets/DebugOutputs/MMMask_{i}.png"
            );*/

            maskTfRz.Dispose();

            // Save the mask and class ID
            
            var maskAndClassID = new MaskAndClassID { mask = boolCrpMsk, classID = labelIDs[i] };
            m_SavedMasksAndClassIDs.Add(maskAndClassID);

            //ApplyObfuscation(obfuscationType, outTexture, boolCrpMsk);
        }
        selectedMasks?.Dispose();
        
        foreach (MaskAndClassID maskAndClassID in m_SavedMasksAndClassIDs)
        {
            ApplyObfuscation(
                classObfuscationTypes[maskAndClassID.classID],
                maskAndClassID.mask
            );
            
            ImageWriter.WriteBoolMatrixToPNG(
                maskAndClassID.mask,
                $"Assets/DebugOutputs/BinMask_{maskAndClassID.classID}.png"
            );
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
    static bool IsBoxCoordsSimilar(
        TensorData currentData,
        [CanBeNull] TensorData previousData,
        float imageWidth,
        float imageHeight
    )
    {
        // Check if both currentData and previousData are null
        if (currentData == null && previousData == null)
        {
            return true; // both are null, so similar
        }

        // Check if either currentData or previousData is null
        if (currentData == null || previousData == null)
        {
            return false; // one of them is null, so not similar
        }

        // Check if the shapes of boxCoordsAll are different
        if (
            currentData.boxCoordsAll != null
            && previousData.boxCoordsAll != null
            && currentData.boxCoordsAll.shape != previousData.boxCoordsAll.shape
        )
        {
            return false; // different shapes means different number of boxes, so not similar
        }

        // Ensure currentData.boxCoordsAll is readable
        currentData.boxCoordsAll?.MakeReadable();
        currentData.boxCoordsAll?.CompleteAllPendingOperations();
        float[] currentCoords = currentData.boxCoordsAll?.ToReadOnlyArray();

        // Ensure previousData.boxCoordsAll is readable
        previousData.boxCoordsAll?.MakeReadable();
        previousData.boxCoordsAll?.CompleteAllPendingOperations();
        float[] previousCoords = previousData.boxCoordsAll?.ToReadOnlyArray();
        if (currentCoords == null || previousCoords == null)
        {
            return false;
        }

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

            Debug.Log("IsBoxCoordsSimilar, Distance: " + distance);

            if (distance > 0.01f) // square of the threshold
                return true;
        }

        return false;
    }

    /// <summary>
    /// Crops and binarizes a mask tensor based on the given box coordinates and scaling factors.
    /// </summary>
    /// <param name="maskTensor">The input mask tensor.</param>
    /// <param name="x1y1x2y2">The box coordinates [x1, y1, x2, y2].</param>
    /// <param name="scaleX">The scaling factor for the x-axis.</param>
    /// <param name="scaleY">The scaling factor for the y-axis.</param>
    /// <param name="invertY">Flag indicating whether to invert the y-axis.</param>
    /// <returns>A boolean array representing the cropped and binarized mask.</returns>
    bool[,] CropAndBinarizeMask(
        TensorFloat maskTensor,
        int[] x1Y1X2Y2,
        float scaleX,
        float scaleY,
        bool invertY = true
    )
    {
        // DebugText.Instance.UpdateTxt("@CropAndBinarizeMask");
        // Scale and validate the box coordinates
        int x1 = Mathf.Max((int)(x1Y1X2Y2[0] * scaleX), 0);
        int y1 = Mathf.Max((int)(x1Y1X2Y2[1] * scaleY), 0);
        int x2 = Mathf.Min((int)(x1Y1X2Y2[2] * scaleX), maskTensor.shape[0]);
        int y2 = Mathf.Min((int)(x1Y1X2Y2[3] * scaleY), maskTensor.shape[1]);

        // binarize the mask
        maskTensor = m_Ops.Sigmoid(maskTensor);
        TensorShape maskShape = maskTensor.shape;
        TensorFloat maskConstant = m_Ops.ConstantOfShape(maskShape, maskThres);
        TensorInt masksBin = m_Ops.GreaterOrEqual(maskTensor, maskConstant);

        // For GPU backend
        // masksBin.MakeReadable();

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

    static void ApplyObfuscation(
        Obfuscation.Type obfuscationType,
        bool[,] boolCrpMsk
    )
    {
        // DebugText.Instance.UpdateTxt("@ApplyObfuscation");
        switch (obfuscationType)
        {
            case Obfuscation.Type.Masking:
                ImgUtils.MaskTexture(_outTexture, boolCrpMsk, Color.red);
                
                
                break;

            case Obfuscation.Type.Pixelation:
                ImgUtils.PixelateTexture(_outTexture, boolCrpMsk, 20);
                break;

            case Obfuscation.Type.Blurring:
                ImgUtils.BlurTexture(ref _outTexture, boolCrpMsk, 7);
                break;

            case Obfuscation.Type.None:
                break;

            default:
                throw new ArgumentException("Invalid obfuscation type");
        }
    }

    [BurstCompile]
    struct ResizeJob : IJobParallelFor
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
            int yOut = index / outputWidth;
            int xOut = index % outputWidth;

            int xNn = (int)Math.Round(xOut / scaleX);
            int yNn = (int)Math.Round(yOut / scaleY);

            if (invertY)
            {
                yNn = inputHeight - yNn;
            }

            xNn = Math.Min(Math.Max(xNn, 0), inputWidth - 1);
            yNn = Math.Min(Math.Max(yNn, 0), inputHeight - 1);

            int outputIndex = yOut * outputWidth + xOut;
            output[outputIndex] = input[yNn * inputWidth + xNn];
        }
    }

    private static TensorFloat ResizeTF_v3(
        TensorFloat input,
        float scaleX,
        float scaleY,
        bool invertY = false
    )
    {
        // DebugText.Instance.UpdateTxt("@ResizeTF_v3");
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
    /// Initializes the unspecified classes to None. TODO: count the number of classes (model) instead of hardcoding it.
    /// </summary>
    void InitClassDict(
        Dictionary<int, Obfuscation.Type> classObfuscationTypes,
        int totalClasses = 80
    )
    {
        for (var i = 0; i < totalClasses; i++)
        {
            classObfuscationTypes.TryAdd(i, Obfuscation.Type.None);
        }
    }

    static Dictionary<int, string> ParseLabelNames(Model model)
    {
        Dictionary<int, string> labels = new();
        // A dictionary with the format "{0: 'person', 1: 'bicycle', 2: 'car', 3: 'motorcycle', .. }"
        char[] removeChars = { '{', '}', ' ' };
        char[] removeCharsValue = { '\'', ' ' };
        string[] items = model.Metadata["names"].Trim(removeChars).Split(",");
        foreach (string item in items)
        {
            string[] values = item.Split(":");
            int classId = int.Parse(values[0]);
            string trim = values[1].Trim(removeCharsValue);
            labels.Add(classId, trim);
        }
        return labels;
    }

    private static Unity.Sentis.DeviceType GetDeviceType()
    {
        var graphicsDeviceType = SystemInfo.graphicsDeviceType;

        return
            graphicsDeviceType
                is UnityEngine.Rendering.GraphicsDeviceType.Direct3D11
                    or UnityEngine.Rendering.GraphicsDeviceType.Metal
                    or UnityEngine.Rendering.GraphicsDeviceType.Vulkan
            ? Unity.Sentis.DeviceType.GPU
            : Unity.Sentis.DeviceType.CPU;
    }

    /// <summary>
    /// Disposes of the resources when the object is destroyed.
    /// </summary>
    void OnDestroy()
    {
        // DebugText.Instance.UpdateTxt("@OnDestroy");
        m_Engine?.Dispose();
        m_Ops?.Dispose();
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
