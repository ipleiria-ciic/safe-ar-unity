using System;
using UnityEngine;
using UnityEngine.UI;

public static class ImgAnnot
{
    public enum BoundingBoxType
    {
        Corner, // For (x1, y1, x2, y2)
        Center // For (x_center, y_center, w, h)
    }

    public static void DrawBoundingBox(
        Texture2D texture,
        int[] x1y1x2y2,
        Color color,
        int thickness = 3,
        int padding = 3,
        bool invertY = true,
        float scaleX = 1.0f,
        float scaleY = 1.0f
    )
    {
        // Apply scale factors to x and y coordinates
        int x1 = (int)(x1y1x2y2[0] * scaleX);
        int y1 = (int)(x1y1x2y2[1] * scaleY);
        int x2 = (int)(x1y1x2y2[2] * scaleX);
        int y2 = (int)(x1y1x2y2[3] * scaleY);

        x1 = Mathf.Max(x1, padding);
        y1 = Mathf.Max(y1, padding);
        x2 = Mathf.Min(x2, texture.width - padding);
        y2 = Mathf.Min(y2, texture.height - padding);

        if (invertY) // Invert after padding only !!!
        {
            y1 = texture.height - y1;
            y2 = texture.height - y2;
        }

        Debug.Log($"DrawBoundingBox x1: {x1}, y1: {y1}, x2: {x2}, y2: {y2}");

        Color32[] pixels = texture.GetPixels32();
        int width = texture.width;

        // Draw top and bottom edges
        for (int x = x1; x <= x2; x++)
        {
            for (int t = 0; t < thickness; t++)
            {
                pixels[(y1 + t) * width + x] = Color.green; // Top edge
                pixels[(y2 - t) * width + x] = color; // Bottom edge
            }
        }

        for (int y = y2; y <= y1; y++) // inverted y1 and y2
        {
            for (int t = 0; t < thickness; t++)
            {
                pixels[y * width + x1 + t] = color; // Left edge
                pixels[y * width + x2 - t] = color; // Corrected right edge calculation
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();
    }

    public static void DrawCircle(
        Texture2D texture,
        int centerX,
        int centerY,
        int radius,
        Color color
    )
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
}
