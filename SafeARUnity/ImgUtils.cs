// Purpose: This script contains the utility functions for image processing.
using System;
using Unity.Sentis;
using UnityEngine;
using Color = UnityEngine.Color;

// using Emgu.CV;
// using Emgu.CV.Structure;

public static class ImgUtils
{
    /// <summary>
    /// Linear interpolation
    /// </summary>
    public static float InterpolLin(float m1, float m2, float value) => m1 + (m2 - m1) * value;

    public static double InterpolLin(double m1, double m2, double value) => m1 + (m2 - m1) * value;

    /// <summary>
    /// Sigmoid function for float
    /// </summary>
    public static float Sigmoid(float x) => 1.0f / (1 + Mathf.Exp(-x));

    public static double Sigmoid(double x) => 1.0 / (1 + Math.Exp(-x));

    /// <summary>
    /// Bilinear interpolation
    /// </summary>
    /// <param name="calcMatrix">The matrix to be interpolated</param>
    /// <param name="width">The width of the matrix</param>
    /// <param name="height">The height of the matrix</param>
    /// <param name="resizeWidth">Desired width of the resized matrix</param>
    /// <param name="resizeHeight">Desired height of the resized matrix</param>
    public static float[,] BilinearInterpol(
        float[,] calcMatrix,
        int width,
        int height,
        int resizeWidth,
        int resizeHeight
    )
    {
        var maskMatrix = new float[resizeWidth, resizeHeight];
        var ratioX = 1.0f / ((float)resizeWidth / (width - 1));
        var ratioY = 1.0f / ((float)resizeHeight / (height - 1));

        for (var y = 0; y < resizeHeight; y++)
        {
            var yFloor = (int)Mathf.Floor(y * ratioY);
            var yLerp = y * ratioY - yFloor;

            for (var x = 0; x < resizeWidth; x++)
            {
                var xFloor = (int)Mathf.Floor(x * ratioX);
                var xLerp = x * ratioX - xFloor;

                var topLeft = calcMatrix[xFloor, yFloor];
                var topRight = calcMatrix[xFloor + 1, yFloor];
                var bottomLeft = calcMatrix[xFloor, yFloor + 1];
                var bottomRight = calcMatrix[xFloor + 1, yFloor + 1];

                var top = InterpolLin(topLeft, topRight, xLerp);
                var bottom = InterpolLin(bottomLeft, bottomRight, xLerp);

                maskMatrix[x, y] = InterpolLin(top, bottom, yLerp);
            }
        }

        return maskMatrix;
    }

    public static double[,] BilinearInterpol(
        double[,] calcMatrix,
        int width,
        int height,
        int resizeWidth,
        int resizeHeight
    )
    {
        var maskMatrix = new double[resizeWidth, resizeHeight];
        var ratioX = 1.0 / ((double)resizeWidth / (width - 1));
        var ratioY = 1.0 / ((double)resizeHeight / (height - 1));

        for (var y = 0; y < resizeHeight; y++)
        {
            var yFloor = (int)Math.Floor(y * ratioY);
            var yLerp = y * ratioY - yFloor;

            for (var x = 0; x < resizeWidth; x++)
            {
                var xFloor = (int)Math.Floor(x * ratioX);
                var xLerp = x * ratioX - xFloor;

                var topLeft = calcMatrix[xFloor, yFloor];
                var topRight = calcMatrix[xFloor + 1, yFloor];
                var bottomLeft = calcMatrix[xFloor, yFloor + 1];
                var bottomRight = calcMatrix[xFloor + 1, yFloor + 1];

                var top = InterpolLin(topLeft, topRight, xLerp);
                var bottom = InterpolLin(bottomLeft, bottomRight, xLerp);

                maskMatrix[x, y] = InterpolLin(top, bottom, yLerp);
            }
        }

        return maskMatrix;
    }

    /// <summary>
    /// Resizes texture to specified width and height; discards alpha channel.
    /// </summary>
    public static Texture2D ResizeTexture(Texture2D texture, int width, int height)
    {
        var rt = RenderTexture.GetTemporary(width, height);
        Graphics.Blit(texture, rt);
        var preRt = RenderTexture.active;
        RenderTexture.active = rt;
        var resizedTexture = new Texture2D(width, height, TextureFormat.RGB24, false);
        resizedTexture.ReadPixels(new UnityEngine.Rect(0, 0, width, height), 0, 0);
        resizedTexture.Apply();
        RenderTexture.active = preRt;
        RenderTexture.ReleaseTemporary(rt);
        return resizedTexture;
    }

    /// <summary>
    /// Calculates the IoU (Intersection over Union) of two detection results.
    /// </summary>
    // public static float Iou(
    //     ImageObfuscator.DetectionOutput0 boxA,
    //     ImageObfuscator.DetectionOutput0 boxB
    // )
    // {
    //     float xA = Mathf.Max(boxA.X1, boxB.X1);
    //     float yA = Mathf.Max(boxA.Y1, boxB.Y1);
    //     float xB = Mathf.Min(boxA.X2, boxB.X2);
    //     float yB = Mathf.Min(boxA.Y2, boxB.Y2);

    //     // Compute the area of intersection rectangle
    //     float intersectionArea = Mathf.Max(0, xB - xA) * Mathf.Max(0, yB - yA);

    //     // If there is no intersection, IoU is 0
    //     if (intersectionArea == 0)
    //     {
    //         return 0.0f;
    //     }

    //     // Compute the area of both the prediction and ground-truth rectangles
    //     float boxAArea = (boxA.X2 - boxA.X1) * (boxA.Y2 - boxA.Y1);
    //     float boxBArea = (boxB.X2 - boxB.X1) * (boxB.Y2 - boxB.Y1);

    //     float iou = intersectionArea / (boxAArea + boxBArea - intersectionArea);

    //     return iou;
    // }

    /// <summary>
    /// Normalizes texture is first normalized (/255f), then the mean (R=0.485,
    /// G=0.456, B=0.406) is subtracted and divided by the standard
    /// deviation (R=0.229, G=0.224, B=0.225).
    /// </summary>
    /// <param name="inputTexture">The input texture</param>
    /// <returns>The normalized texture</returns>
    public static Texture2D TextureNormalize(Texture2D inputTexture)
    {
        var pixels = inputTexture.GetPixels32();
        var processedPixels = new Color32[pixels.Length];

        for (var i = 0; i < pixels.Length; i++)
        {
            var r = pixels[i].r / 255f;
            var g = pixels[i].g / 255f;
            var b = pixels[i].b / 255f;

            // Source: Ultralytics YOLOv8
            r = (r - 0.485f) / 0.229f;
            g = (g - 0.456f) / 0.224f;
            b = (b - 0.406f) / 0.225f;

            processedPixels[i] = new Color32(
                (byte)Mathf.Clamp(r * 255, 0, 255),
                (byte)Mathf.Clamp(g * 255, 0, 255),
                (byte)Mathf.Clamp(b * 255, 0, 255),
                pixels[i].a
            );
        }

        Texture2D outputTexture = new(inputTexture.width, inputTexture.height);
        outputTexture.SetPixels32(processedPixels);
        outputTexture.Apply();

        return outputTexture;
    }

    ///</summary>
    /// Lod the image file into a byte array
    /// </summary>
    public static Texture2D LoadTextureFromImage(string imagePath)
    {
        // Load the image file into a byte array
        var imageBytes = System.IO.File.ReadAllBytes(imagePath);

        Texture2D tex = new(2, 2); // Create new "empty" texture (placeholder)
        if (tex.LoadImage(imageBytes))
        {
            return tex;
        }
        else
        {
            Debug.LogError("Failed to load image from " + imagePath);
            return null;
        }
    }

    /// <summary>
    /// Blurs the texture with the given mask.
    /// Mean or box blur: averages the color values of each pixel with
    /// the values of its neighboring pixels within a defined radiu
    /// </summary>
    /// <param name="texture">The texture to be converted</param>
    /// <param name="mask">The mask defining the area to be blurred</param>
    /// <param name="blurSize">The size of the blur area</param>
    /// <returns>The byte array of the texture</returns>
    public static void BlurTexture(ref Texture2D texture, bool[,] mask, int blurSize)
    {
        var pixels = texture.GetPixels();
        var width = texture.width;
        var height = texture.height;

        // Create a temporary array to hold the averaged colors
        var tempPixels = new Color[pixels.Length];
        Array.Copy(pixels, tempPixels, pixels.Length);

        // Iterate over the texture pixels
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                // Check if the current pixel is within the blur area
                if (mask[x, y])
                {
                    float avgR = 0,
                        avgG = 0,
                        avgB = 0;
                    var blurPixelCount = 0;

                    // Calculate the average color within the blur radius
                    for (
                        var i = Mathf.Max(x - blurSize, 0);
                        i <= Mathf.Min(x + blurSize, width - 1);
                        i++
                    )
                    {
                        for (
                            var j = Mathf.Max(y - blurSize, 0);
                            j <= Mathf.Min(y + blurSize, height - 1);
                            j++
                        )
                        {
                            if (mask[i, j])
                            {
                                avgR += pixels[j * width + i].r;
                                avgG += pixels[j * width + i].g;
                                avgB += pixels[j * width + i].b;
                                blurPixelCount++;
                            }
                        }
                    }

                    // Apply the average color to the current pixel
                    var invBlurPixelCount = 1f / blurPixelCount;
                    tempPixels[y * width + x] = new Color(
                        avgR * invBlurPixelCount,
                        avgG * invBlurPixelCount,
                        avgB * invBlurPixelCount
                    );
                }
            }
        }

        // Update the texture with the blurred pixels
        texture.SetPixels(tempPixels);
        texture.Apply();
    }

    /// <summary>
    /// Converts TensorInt to Texture2D with black and white colors
    /// </summary>
    public static Texture2D TensorIntToTexture(TensorInt maskInt)
    {
        var width = maskInt.shape[0];
        var height = maskInt.shape[1];
        var texture = new Texture2D(width, height);

        var pixels = new Color[width * height];
        for (var x = 0; x < width; x++)
        {
            for (var y = 0; y < height; y++)
            {
                if (maskInt[x, y] == 1)
                {
                    pixels[y * width + x] = Color.white;
                }
                else
                {
                    pixels[y * width + x] = Color.black;
                }
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }

    /// <summary>
    /// Crops the mask within the detection box. Discards mask outside the box.
    /// </summary>
    public static bool[,] CropMask(
        float[,] maskMatrix,
        float xMin,
        float yMin,
        float xMax,
        float yMax
    )
    {
        // Invert the y coordinate to consider the top-left axis
        yMin = maskMatrix.GetLength(1) - yMin;
        yMax = maskMatrix.GetLength(1) - yMax;

        var mask = new bool[maskMatrix.GetLength(0), maskMatrix.GetLength(1)];
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

        return mask;
    }

    public static bool[,] CropMask(
        double[,] maskMatrix,
        float xMin,
        float yMin,
        float xMax,
        float yMax
    )
    {
        // Invert the y coordinate to consider the top-left axis
        yMin = maskMatrix.GetLength(1) - yMin;
        yMax = maskMatrix.GetLength(1) - yMax;

        var mask = new bool[maskMatrix.GetLength(0), maskMatrix.GetLength(1)];
        var x1 = Math.Max(0, (int)xMin);
        var x2 = Math.Min(maskMatrix.GetLength(0), (int)xMax);
        var y2 = Math.Max(0, (int)yMin);
        var y1 = Math.Min(maskMatrix.GetLength(1), (int)yMax);

        // Debug.Log($"Mask coord. x1: {x1}, x2: {x2}, y1: {y1}, y2: {y2}");

        for (var w = x1; w < x2; w++)
        {
            for (var h = y1; h < y2; h++)
            {
                if (maskMatrix[w, h] > 0.5)
                {
                    mask[w, h] = true;
                }
            }
        }
        return mask;
    }

    /// <summary>
    /// Crops the mask within the detection box. Discards mask outside the box.
    /// </summary>
    public static bool[,] CropAndBinarizeMask(
        TensorFloat maskTensor,
        float xMin,
        float yMin,
        float xMax,
        float yMax
    )
    {
        var x1 = Math.Max(0, (int)xMin);
        var x2 = Math.Min(maskTensor.shape[0], (int)xMax);
        var y2 = Math.Max(0, (int)yMin);
        var y1 = Math.Min(maskTensor.shape[1], (int)yMax);

        var mask = new bool[maskTensor.shape[0], maskTensor.shape[1]];

        for (var x = x1; x < x2; x++)
        {
            for (var y = y1; y < y2; y++)
            {
                if (maskTensor[x, y] > 0.5f)
                {
                    mask[x, y] = true;
                }
            }
        }

        return mask;
    }

    /// <summary>
    /// Masks the texture with the given mask
    /// </summary>
    /// <param name="texture">The texture to be masked</param>
    /// <param name="mask">The mask defining the area to be blurred</param>
    /// <param name="maskColor">The color of the mask</param>
    public static void MaskTexture(Texture2D texture, bool[,] mask, Color maskColor)
    {
        // Convert the mask color to Color32
        Color32 maskColor32 = maskColor;

        for (var x = 0; x < mask.GetLength(0); x++)
        {
            for (var y = 0; y < mask.GetLength(1); y++)
            {
                if (mask[x, y])
                {
                    texture.SetPixels32(x, y, 1, 1, new Color32[] { maskColor32 });
                }
            }
        }

        texture.Apply();
    }

    // /// <summary>
    // /// Pixelates the texture with the given mask
    // /// </summary>
    // public static void PixelateTexture(Texture2D texture, bool[,] mask, int pixelSize){
    //     for (var x = 0; x < mask.GetLength(0); x += pixelSize)
    //     {
    //         for (var y = 0; y < mask.GetLength(1); y += pixelSize)
    //         {
    //             var averageColor = CalculateAverageColor(texture, x, y, pixelSize);
    //             FillBlock(texture, x, y, pixelSize, averageColor);
    //         }
    //     }
    // }


    /// <summary>
    /// Pixelates the texture with the given mask
    /// </summary>
    /// <param name="texture">The texture to be pixelated</param>
    /// <param name="mask">The mask defining the area to be pixelated</param>
    /// <param name="pixelSize">The size of the pixelation area</param>
    public static void PixelateTexture(Texture2D texture, bool[,] mask, int pixelSize)
    {
        int width = texture.width;
        int height = texture.height;

        for (int x = 0; x < width; x += pixelSize)
        {
            for (int y = 0; y < height; y += pixelSize)
            {
                // Check if any pixel in the block is masked
                bool isMasked = false;
                for (int i = 0; i < pixelSize && !isMasked; i++)
                {
                    for (int j = 0; j < pixelSize && !isMasked; j++)
                    {
                        if (x + i < width && y + j < height && mask[x + i, y + j])
                        {
                            isMasked = true;
                        }
                    }
                }

                // If the block is masked, calculate the average color and fill the block
                if (isMasked)
                {
                    Color averageColor = CalculateAverageColor(texture, x, y, pixelSize);

                    // Convert the average color to Color32
                    Color32 averageColor32 = averageColor;

                    // Calculate the actual block width and height
                    int blockWidth = Math.Min(pixelSize, width - x);
                    int blockHeight = Math.Min(pixelSize, height - y);

                    Color32[] blockColors = new Color32[blockWidth * blockHeight];
                    for (int i = 0; i < blockColors.Length; i++)
                    {
                        blockColors[i] = averageColor32;
                    }
                    texture.SetPixels32(x, y, blockWidth, blockHeight, blockColors);
                }
            }
        }

        texture.Apply();
    }

    /// <summary>
    /// Converts RenderTexture to Texture2D
    /// </summary>
    public static Texture2D RenderToTexture2D(RenderTexture rTex)
    {
        Texture2D tex = new(rTex.width, rTex.height, TextureFormat.RGB24, false);
        RenderTexture.active = rTex;
        tex.ReadPixels(new Rect(0, 0, rTex.width, rTex.height), 0, 0);
        tex.Apply();
        return tex;
    }

    /// <summary>
    /// Fills a block of pixels with a given color
    /// </summary>
    static void FillBlock(Texture2D texture, int x, int y, int pixelSize, Color color)
    {
        for (var i = x; i < x + pixelSize && i < texture.width; i++)
        {
            for (var j = y; j < y + pixelSize && j < texture.height; j++)
            {
                texture.SetPixel(i, j, color);
            }
        }
    }

    /// <summary>
    ///  Calculates average color in a give set on pixels.
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="pixelSize"></param>
    /// <param name="texture"></param>
    /// <returns>Average Color</returns>
    /// TODO: Calcular apena a(s) diagonal(is) para tornar mais rápido ?
    static Color CalculateAverageColor(Texture2D texture, int x, int y, int pixelSize)
    {
        float totalR = 0,
            totalG = 0,
            totalB = 0;
        var totalPixels = 0;

        for (var i = x; i < x + pixelSize && i < texture.width; i++)
        {
            for (var j = y; j < y + pixelSize && j < texture.height; j++)
            {
                var pixelColor = texture.GetPixel(i, j);
                totalR += pixelColor.r;
                totalG += pixelColor.g;
                totalB += pixelColor.b;
                totalPixels++;
            }
        }

        if (totalPixels == 0)
            return Color.black;

        return new Color(totalR / totalPixels, totalG / totalPixels, totalB / totalPixels);
    }

    
    /// method to convert center coordinates to corner coordinates
    public static int[] CenterToCorner(float x, float y, float w, float h)
    {
        // Convert any negative value to 0 using Math.Max
        x = Math.Max(0, x);
        y = Math.Max(0, y);
        w = Math.Max(0, w);
        h = Math.Max(0, h);

        // Calculate corner coordinates and ensure they are non-negative
        int x1 = Math.Max(0, (int)(x - w / 2));
        int y1 = Math.Max(0, (int)(y - h / 2));
        int x2 = Math.Max(0, (int)(x + w / 2));
        int y2 = Math.Max(0, (int)(y + h / 2));

        return new int[] { x1, y1, x2, y2 };
    }


    public static int[] CornerToCenter(float x1, float y1, float x2, float y2)
    {
        int x = (int)((x1 + x2) / 2);
        int y = (int)((y1 + y2) / 2);
        int w = (int)(x2 - x1);
        int h = (int)(y2 - y1);

        return new int[] { x, y, w, h };
    }

    public static bool[,] BoolMaskFromBoxCoord(
        int width,
        int height,
        int[] x1y1x2y2,
        bool invertY = true,
        float scaleX = 1.0f,
        float scaleY = 1.0f
    )
    {
        int x1 = (int)(x1y1x2y2[0] * scaleX);
        int y1 = (int)(x1y1x2y2[1] * scaleY);
        int x2 = (int)(x1y1x2y2[2] * scaleX);
        int y2 = (int)(x1y1x2y2[3] * scaleY);

        if (invertY)
        {
            y1 = height - y1;
            y2 = height - y2;
        }

        Debug.Log($"BoolMaskFromBoxCoord: x1: {x1}, y1: {y1}, x2: {x2}, y2: {y2}");

        x1 = Mathf.Max(x1, 0);
        y1 = Mathf.Max(y1, 0);
        x2 = Mathf.Min(x2, width);
        y2 = Mathf.Min(y2, height);
        // get bigger y1 and y2
        if (y1 > y2)
            (y2, y1) = (y1, y2);
        bool[,] mask = new bool[width, height];
        for (int x = x1; x < x2; x++)
        {
            for (int y = y1; y < y2; y++)
            {
                mask[x, y] = true;
            }
        }

        return mask;
    }
}

// ----------------------------- Work in Progress ------------------------------

// public void ProcessMasks(){
//             // Sigmoid the masks
//     selectedMasks = ops.Sigmoid(selectedMasks); // TODO: Add this op. to model graphint
//     // Reshape the masks from (numBoxes, 1, 160, 160) to (numBoxe        /// s, 160, 160)
//     TensorShape maskShape = new(sele        /// ctedMasks.s        /// hape[0], prtMskSz, prtMskSz);
//     TensorFloat reshapedMasks = (TensorFloat)selectedMasks.ShallowReshape(maskShape);
//     // ResizedMasks shape:

//     // ---------------------------------------------------------------------
//     // DEBUG
//     // -----

//     Debug.Log("reshapedMasks SIG shape: " + reshapedMasks.shape);

//     for (int i = 0; i < reshapedMasks.shape[0]; i++)
//     {
//         TensorFloat mask =
//             ops.Slice(
//                 X: reshapedMasks,
//                 starts: new int[] { i, 0, 0 },
//                 ends: new int[] { i + 1, 160, 160 },
//                 axes: new int[] { 0, 1, 2 },
//                 steps: new int[] { 1, 1, 1 }
//             ) as TensorFloat;

//         // Squeeze first dimension: (1, 160, 160) -> (160, 160)
//         TensorShape newShape = new(prtMskSz, prtMskSz);
//         mask = ops.Reshape(mask, newShape) as TensorFloat;

//         // Debug.Log("reshapedMask shape: " + mask.shape);
//         ImageWriter.WriteTensorFloatToPNG(mask, "Assets/DEBUG_IMGS/reshapedMask_" + i + ".png");
//     }

//     TensorFloat resizedMasks = ops.Resize(
//         X: reshapedMasks,
//         scale: new float[] { 1, (float)inputSz / prtMskSz, (float)inputSz / prtMskSz },
//         interpolationMode: L.InterpolationMode.Nearest,
//         nearestMode: L.NearestMode.RoundPreferFloor,
//         coordTransformMode: L.CoordTransformMode.HalfPixel
//     );

//     Debug.Log("resizedMasks shape: " + resizedMasks.shape);

//     // Binarize the masks (maskThres = 0.5)
//     TensorFloat thresTensor = ops.ConstantOfShape(new TensorShape(inputSz, inputSz), maskThres);
//     TensorInt masksBin = ops.GreaterOrEqual(resizedMasks, thresTensor);

//     Debug.Log("masksBin shape: " + masksBin.shape);
// // TensorFloat Resize(TensorFloat X, ReadOnlySpan<float> scale, InterpolationMode interpolationMode, NearestMode nearestMode = NearestMode.RoundPreferFloor, CoordTransformMode coordTransformMode = CoordTransformMode.HalfPixel)

// // Convert TensorInt to TensorFloat
// TensorFloat masksBinFloat = ops.Cast(masksBin, DataType.Float);

// TensorInt RsMaskBin = ops.Resize(
//     TensorFloat.FromInt(masksBin),
//     new float[] { scaleXPrt, scaleYPrt },
//     L.InterpolationMode.Nearest,
//     L.NearestMode.RoundPreferFloor,
//     L.CoordTransformMode.HalfPixel
// );

// // PAREI AQUI
// // TODO:
// // 1. Crop the masks with the boxCoords
// if (masksBin.shape[0] > 0)
// {
//     TensorShape masksBinShape = masksBin.shape;
//     bool[][,] croppedMasks = new bool[masksBinShape[0]][,];

//     for (int i = 0; i < masksBin.shape[0]; i++)
//     {
//         float x_center = boxCoords[0, i, 0];
//         float y_center = boxCoords[0, i, 1];
//         float width = boxCoords[0, i, 2];
//         float height = boxCoords[0, i, 3];

//         int x1 = (int)(x_center - width / 2);
//         int y1 = (int)(y_center - height / 2);
//         int x2 = (int)(x_center + width / 2);
//         int y2 = (int)(y_center + height / 2);

//         // maskBin(i, :, :) to mask (640, 640)

//         TensorFloat mask =
//             ops.Slice(
//                 X: masksBin,
//                 starts: new int[] { i, 0, 0 },
//                 ends: new int[] { i + 1, 160, 160 },
//                 axes: new int[] { 0, 1, 2 },
//                 steps: new int[] { 1, 1, 1 }
//             ) as TensorFloat;

//         Debug.Log("chegou!");
//         Debug.Log("mask: " + mask);
//         Debug.Log("masksBin shape: " + masksBin.shape);
//         Debug.Log("PAssei!");

//         mask = ops.Reshape(mask, new TensorShape(inputSz, inputSz)) as TensorFloat;

//         bool[,] croppedMask = ImgUtils.CropMask_TF(mask, x1, y1, x2, y2);

//         ImageWriter.WriteBoolMatrixToPNG(
//             croppedMask,
//             "Assets/DEBUG_IMGS/croppedMask_" + i + ".png"
//         );

//         croppedMasks[i] = croppedMask;
//     }
// }
// else
// {
//     Debug.Log("No masks detected");
// }
// }
