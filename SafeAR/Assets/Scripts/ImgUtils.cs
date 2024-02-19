// Purpose: This script contains the utility functions for image processing.
using System;
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

    /// <summary>
    /// Sigmoid function
    /// </summary>
    public static float Sigmoid(float x) => 1.0f / (1 + Mathf.Exp(-x));

    /// <summary>
    /// Bilinear interpolation
    /// </summary>
    /// <param name="calcMatrix">The matrix to be interpolated</param>
    /// <param name="width">The width of the matrix</param>
    /// <param name="height">The height of the matrix</param>
    /// <param name="resizeWidth">Desired width of the resized matrix</param>
    /// <param name="resizeHeight">Desired height of the resized matrix</param>
    public static float[,] BilinearInterpol(float[,] calcMatrix, int width, int height, int resizeWidth, int resizeHeight)
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

    /// <summary>
    /// Calculates the IoU (Intersection over Union) of two detection results.
    /// </summary>
    public static float Iou(ImageObfuscator.DetectionOutput0 boxA, ImageObfuscator.DetectionOutput0 boxB)
    {
        float xA = Mathf.Max(boxA.X1, boxB.X1);
        float yA = Mathf.Max(boxA.Y1, boxB.Y1);
        float xB = Mathf.Min(boxA.X2, boxB.X2);
        float yB = Mathf.Min(boxA.Y2, boxB.Y2);

        // Compute the area of intersection rectangle
        float intersectionArea = Mathf.Max(0, xB - xA) * Mathf.Max(0, yB - yA);

        // If there is no intersection, IoU is 0
        if (intersectionArea == 0)
        {
            return 0.0f;
        }

        // Compute the area of both the prediction and ground-truth rectangles
        float boxAArea = (boxA.X2 - boxA.X1) * (boxA.Y2 - boxA.Y1);
        float boxBArea = (boxB.X2 - boxB.X1) * (boxB.Y2 - boxB.Y1);

        float iou = intersectionArea / (boxAArea + boxBArea - intersectionArea);

        return iou;
    }

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
    /// Converts a Texture2D to a byte array
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
        var tempPixels = new UnityEngine.Color[pixels.Length];
        Array.Copy(pixels, tempPixels, pixels.Length);

        // Iterate over the texture pixels
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                // Check if the current pixel is within the blur area
                if (mask[x, y])
                {
                    float avgR = 0, avgG = 0, avgB = 0;
                    var blurPixelCount = 0;

                    // Calculate the average color within the blur radius
                    for (var i = Mathf.Max(x - blurSize, 0); i <= Mathf.Min(x + blurSize, width - 1); i++)
                    {
                        for (var j = Mathf.Max(y - blurSize, 0); j <= Mathf.Min(y + blurSize, height - 1); j++)
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
                    tempPixels[y * width + x] = new Color(avgR * invBlurPixelCount, avgG * invBlurPixelCount, avgB * invBlurPixelCount);
                }
            }
        }

        // Update the texture with the blurred pixels
        texture.SetPixels(tempPixels);
        texture.Apply();
    }

    /// <summary>
    /// Masks the texture with the given mask
    /// </summary>
    /// <param name="texture">The texture to be masked</param>
    /// <param name="mask">The mask defining the area to be blurred</param>
    /// <returns>The masked texture</returns>
    /// <param name="maskColor">The color of the mask</param>
    /// <returns>The masked texture</returns>
    public static void MaskTexture(Texture2D texture, bool[,] mask, Color maskColor)
    {
        for (var x = 0; x < mask.GetLength(0); x++)
        {
            for (var y = 0; y <mask.GetLength(1); y++)
            {
                if (mask[x, y])
                {
                    texture.SetPixel(x, y, maskColor);
                }
            }
        }
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
                    
                    // Calculate the actual block width and height
                    int blockWidth = Math.Min(pixelSize, width - x);
                    int blockHeight = Math.Min(pixelSize, height - y);
                    
                    Color[] blockColors = new Color[blockWidth * blockHeight];
                    for (int i = 0; i < blockColors.Length; i++)
                    {
                        blockColors[i] = averageColor;
                    }
                    texture.SetPixels(x, y, blockWidth, blockHeight, blockColors);
                }
            }
        }

        texture.Apply();
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
        float totalR = 0, totalG = 0, totalB = 0;
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

}


