using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
// using OpenCvSharp;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Sentis;
using UnityEngine;
using Color = UnityEngine.Color;
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

    // [Header("Debug Text")]
    // public Text debugText;

    Ops ops; // For using the Sentis Ops tensor operations

    public static Unity.Sentis.DeviceType deviceType;

    ///<summary>
    /// In the Start method, the device type is determined based on the graphics
    /// device type. The best backend type for the device is obtained, and the
    /// YOLO model asset is loaded. Labels are parsed from the YOLO model.
    ///</summary>
    void Start()
    {
        deviceType = GetDeviceType();
        var backendType = WorkerFactory.GetBestTypeForDevice(deviceType);

        // Force GPU backend
        // var backendType = BackendType.GPUCompute;

        ops = WorkerFactory.CreateOps(backendType, null);

        Debug.Log(
            "~ Start Method ~  backendType: "
                + backendType
                + "deviceType: "
                + deviceType
                + "ops: "
                + ops
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

    public Texture2D Run(
        Texture2D ipnTxtr,
        Dictionary<int, Obfuscation.Type> classObfuscationTypes,
        bool DrawBBoxes = false
    )
    {
        // Create a stopwatch for measuring the time
        // var stopwatch1 = new Stopwatch();
        // stopwatch1.Start();

        // Initialize the unspecified classes to None
        InitClassDict(classObfuscationTypes);

        // Get the input texture dimensions
        (int inpWdth, int inpHght) = (ipnTxtr.width, ipnTxtr.height);

        // Calculate the scale factors for the mask resizing
        (float scaleXPrt, float scaleYPrt) = (inpWdth / (float)prtMskSz, inpHght / (float)prtMskSz);
        (float scaleX, float scaleY) = (inpWdth / (float)inputSz, inpHght / (float)inputSz);

        // stopwatch1.Stop();
        // Debug.Log("P_1) Dict and vars init.: " + stopwatch1.ElapsedMilliseconds + " ms");
        // stopwatch1.Reset();

        // var stopwatch2 = new Stopwatch();
        // stopwatch2.Start();

        // Create a copy of the input image
        Texture2D outTxtr = new(ipnTxtr.width, ipnTxtr.height);
        outTxtr.SetPixels32(ipnTxtr.GetPixels32());
        outTxtr.Apply();

        // stopwatch2.Stop();
        // Debug.Log("P_2) Texture2D copy of input.: " + stopwatch2.ElapsedMilliseconds + " ms");
        // stopwatch2.Reset();

        // var stopwatch3 = new Stopwatch();
        // stopwatch3.Start();
        // Resize the input texture to the model input size

        // Texture2D ipnTxtrRz = ImgUtils.ResizeTexture(ipnTxtr, inputSz, inputSz);
        using Tensor inputTensor = TextureConverter.ToTensor(
            ipnTxtr,
            width: inputSz,
            height: inputSz,
            channels: -1
        );
        // inputTensor.MakeReadable();

        // stopwatch3.Stop();
        // Debug.Log(
        //     "P_3) Resize Text. Convert to Tensor, MakeReadable: "
        //         + stopwatch3.ElapsedMilliseconds
        //         + " ms"
        // );
        // stopwatch3.Reset();

        // var stopwatch4 = new Stopwatch();
        // stopwatch4.Start();

        // Run the model
        engine.Execute(inputTensor);

        // stopwatch4.Stop();
        // Debug.Log("P_4) Run the model: " + stopwatch4.ElapsedMilliseconds + " ms");
        // stopwatch4.Reset();

        // var stopwatch5 = new Stopwatch();
        // stopwatch5.Start();

        // Get the output tensors
        TensorFloat boxCoordsAll = engine.PeekOutput("boxCoords") as TensorFloat;
        TensorInt NMS = engine.PeekOutput("NMS") as TensorInt;
        TensorInt classIDs = engine.PeekOutput("classIDs") as TensorInt;
        TensorFloat masks = engine.PeekOutput("masks") as TensorFloat;

        // stopwatch5.Stop();
        // Debug.Log("P_5) PeekOutput model outputs: " + stopwatch5.ElapsedMilliseconds + " ms");
        // stopwatch5.Reset();

        // var stopwatch6 = new Stopwatch();
        // stopwatch6.Start();

        // Get Box IDs from NMS
        Tensor boxIDs = ops.Slice(NMS, new[] { 2 }, new[] { 3 }, new[] { 1 }, new[] { 1 });

        // stopwatch6.Stop();
        // Debug.Log("P_6) Slice ops boxIDs: " + stopwatch6.ElapsedMilliseconds + " ms");
        // stopwatch6.Reset();

        // var stopwatch7 = new Stopwatch();
        // stopwatch7.Start();

        // Flatten the boxIDs
        TensorShape boxIDsShape = boxIDs.shape;
        TensorInt boxIDsFlat = boxIDs.ShallowReshape(boxIDsShape) as TensorInt;

        // stopwatch7.Stop();
        // Debug.Log("P_7) Reshape boxIDs: " + stopwatch7.ElapsedMilliseconds + " ms");
        // stopwatch7.Reset();

        // For GPU backend
        // labelIDs.MakeReadable();
        // boxCoords.MakeReadable();

        // var stopwatch8 = new Stopwatch();
        // stopwatch8.Start();

        // Gather the box coordinates, class IDs
        TensorFloat boxCoords = ops.Gather(boxCoordsAll, boxIDsFlat, 1) as TensorFloat;
        TensorInt labelIDs = ops.Gather(classIDs, boxIDsFlat, 2) as TensorInt;
        TensorFloat selectedMasks = ops.Gather(masks, boxIDsFlat, 0) as TensorFloat;
        // Debug.Log("selectedMasks shape: " + selectedMasks.shape);

        // stopwatch8.Stop();
        // Debug.Log("P_8) Gather ops: " + stopwatch8.ElapsedMilliseconds + " ms");
        // stopwatch8.Reset();

        // var stopwatch9 = new Stopwatch();
        // stopwatch9.Start();

        // Dispose of the tensors
        masks.Dispose();
        boxIDs.Dispose();
        boxIDsFlat.Dispose();
        boxCoordsAll.Dispose();

        // stopwatch9.Stop();
        // Debug.Log("P_9) Dispose: " + stopwatch9.ElapsedMilliseconds + " ms");
        // stopwatch9.Reset();

        for (int i = 0; i < boxCoords.shape[1]; i++)
        {
            // var stopwatch9_5 = new Stopwatch();
            // stopwatch9_5.Start();

            var obfuscationType = classObfuscationTypes[labelIDs[i]];
            bool[,] boolCrpMsk = new bool[0, 0];
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

            if (DrawBBoxes == true)
            {
                ImgAnnot.DrawBoundingBox(
                    texture: outTxtr,
                    x1y1x2y2: boxCoordx1y1x2y2,
                    color: Color.red,
                    scaleX: scaleX,
                    scaleY: scaleY
                );

                // stopwatch9_5.Stop();
                // Debug.Log("P_9_5) BoxCoord extract: " + stopwatch9_5.ElapsedMilliseconds + " ms");
                // stopwatch9_5.Reset();
            }

            // var stopwatch10 = new Stopwatch();
            // stopwatch10.Start();

            //  ---   Obfuscate the image based on the detected objects   ---
            TensorFloat maskTensor =
                ops.Slice(
                    X: selectedMasks,
                    starts: new int[] { i, 0, 0 },
                    ends: new int[] { i + 1, 160, 160 },
                    axes: new int[] { 0, 1, 2 },
                    steps: new int[] { 1, 1, 1 }
                ) as TensorFloat;

            // stopwatch10.Stop();
            // Debug.Log("P_10) masks Slice: " + stopwatch10.ElapsedMilliseconds + " ms");
            // stopwatch10.Reset();

            // var stopwatch11 = new Stopwatch();
            // stopwatch11.Start();

            TensorFloat maskTf = ops.Reshape(maskTensor, new TensorShape(160, 160)) as TensorFloat;

            // stopwatch11.Stop();
            // Debug.Log("P_11) masks Reshape: " + stopwatch11.ElapsedMilliseconds + " ms");
            // stopwatch11.Reset();

            // ImageWriter.WriteTensorFloatToPNG(maskTf, "Assets/DEBUG_IMGS/maskTf_" + i + ".png");
            // For GPU backend
            // maskTf.MakeReadable();

            // var stopwatch12 = new Stopwatch();
            // stopwatch12.Start();

            TensorFloat maskTfRz = ResizeTF_v3(
                input: maskTf,
                scaleX: scaleXPrt,
                scaleY: scaleYPrt,
                invertY: true
            ); // Shape should be (inpWdth, inpHght)

            // stopwatch12.Stop();
            // Debug.Log("P_12) ResizeTF_v2 : " + stopwatch12.ElapsedMilliseconds + " ms");
            // stopwatch12.Reset();

            // ImageWriter.WriteTensorFloatToPNG(
            //     maskTfRz,
            //     "Assets/DEBUG_IMGS/maskTfRzv2_" + i + ".png"
            // );
            // For GPU backend
            // maskTfRz.MakeReadable();

            // var stopwatch13 = new Stopwatch();
            // stopwatch13.Start();

            boolCrpMsk = CropAndBinarizeMask(
                maskTensor: maskTfRz,
                x1y1x2y2: boxCoordx1y1x2y2,
                scaleX: scaleX,
                scaleY: scaleY
            );

            // stopwatch13.Stop();
            // Debug.Log("P_13) CropAndBinarizeMask : " + stopwatch13.ElapsedMilliseconds + " ms");
            // stopwatch13.Reset();

            // ImageWriter.WriteBoolMatrixToPNG(
            //     boolCrpMsk,
            //     "Assets/DEBUG_IMGS/boolCrpMsk_" + i + ".png"
            // );

            // var stopwatch14 = new Stopwatch();
            // stopwatch14.Start();

            ApplyObfuscation(obfuscationType, outTxtr, boolCrpMsk);

            // stopwatch14.Stop();
            // Debug.Log("P_14) ApplyObfuscation : " + stopwatch14.ElapsedMilliseconds + " ms");
            // stopwatch14.Reset();
        }

        // // Dispose of the tensors
        // boxIDs.Dispose();
        // boxIDsFlat.Dispose();
        // boxCoords.Dispose();
        // labelIDs.Dispose();
        // selectedMasks.Dispose();
        // inputTensor.Dispose();

        return outTxtr;
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
    private bool[,] CropAndBinarizeMask(
        TensorFloat maskTensor,
        int[] x1y1x2y2,
        float scaleX,
        float scaleY,
        bool invertY = true
    )
    {
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
        switch (obfuscationType)
        {
            case Obfuscation.Type.Masking:
                ImgUtils.MaskTexture(outTxtr, boolCrpMsk, Color.red);
                break;

            case Obfuscation.Type.Pixelation:
                ImgUtils.PixelateTexture(outTxtr, boolCrpMsk, 6);
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

    [BurstCompile]
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
            int y_out = index / outputWidth;
            int x_out = index % outputWidth;

            int x_nn = (int)Math.Round(x_out / scaleX);
            int y_nn = (int)Math.Round(y_out / scaleY);

            if (invertY)
            {
                y_nn = inputHeight - 1 - y_nn;
            }

            x_nn = Math.Min(Math.Max(x_nn, 0), inputWidth - 1);
            y_nn = Math.Min(Math.Max(y_nn, 0), inputHeight - 1);

            // output[index] = input[y_nn * inputWidth + x_nn];

            int outputIndex = y_out * outputWidth + x_out;
            output[outputIndex] = input[y_nn * inputWidth + x_nn];
        }
    }

    private static int count = 0;

    public static TensorFloat ResizeTF_v3(
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

        // Convert input tensor to NativeArray for efficient access
        float[] inputData = input.ToReadOnlyArray(); // Flat array of shape (160 * 160)
        NativeArray<float> inputNativeArray = new(inputData, Allocator.TempJob);
        NativeArray<float> outputNativeArray = new(outputWidth * outputHeight, Allocator.TempJob);

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

        // Dispose of NativeArrays to avoid memory leaks
        inputNativeArray.Dispose();
        outputNativeArray.Dispose();

        return output;
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
