using System;
using System.Globalization;
using System.IO;
using UnityEngine;

/// <summary>
/// This class is used to display the FPS on the screen and log it to a file.
/// </summary>
public class FPSPrint : MonoBehaviour
{
    private float deltaTime;
    private int frameCounter = 0;
    private int printInterval = 2;
    private StreamWriter writer;

    void Start()
    {
        writer = new StreamWriter("Assets/DEBUG_IMGS/fps_log.txt", append: true);
        if (writer != null)
        {
            try
            {     
                writer.WriteLine("\nSession Start: " + DateTime.Now + " (Logs FPS every " + printInterval + " frames)");
                writer.Flush();
            }
            catch (Exception e)
            {
                Debug.LogError("Failed to write to file: " + e.Message);
            }
        }
    }

    void Update()
{
    if (writer == null) return;

    deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;
    var fps = 1.0f / deltaTime;

    frameCounter++;
    if (frameCounter >= printInterval)
    {
        try
        {
            writer.WriteLine(DateTime.Now + " FPS: " + fps.ToString("F2"));
            writer.Flush();
        }
        catch (Exception e)
        {
            Debug.LogError("Failed to write to file: " + e.Message);
        }
        frameCounter = 0;
    }
}

    void OnDestroy()
    {
        if (writer != null)
        {
            try
            {
                writer.Close();
                Debug.Log("Closed StreamWriter");  // Add this line
            }
            catch (Exception e)
            {
                Debug.LogError("Failed to close StreamWriter: " + e.Message);
            }
        }
    }
}