// using System;
// using System.Globalization;
// using System.IO;
// using UnityEngine;

// /// <summary>
// /// This class is used to display the FPS on the screen and log it to a file.
// /// </summary>
// public class FPSPrint : MonoBehaviour
// {
//     private float deltaTime;
//     private int frameCounter = 0;
//     private int printInterval = 2;
//     private StreamWriter writer;

//     void Start()
//     {
//         writer = new StreamWriter("Assets/DEBUG_IMGS/fps_log.txt", append: true);
//         if (writer != null)
//         {
//             try
//             {     
//                 writer.WriteLine("\nSession Start: " + DateTime.Now + " (Logs FPS every " + printInterval + " frames)");
//                 writer.Flush();
//             }
//             catch (Exception e)
//             {
//                 Debug.LogError("Failed to write to file: " + e.Message);
//             }
//         }
//     }

//     void Update()
//     {
//         if (writer == null) return;

//         deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;
//         var fps = 1.0f / deltaTime;

//         frameCounter++;
//         if (frameCounter >= printInterval)
//         {
//             try
//             {
//                 writer.WriteLine(DateTime.Now + " FPS: " + fps.ToString("F2"));
//                 writer.Flush();
//             }
//             catch (Exception e)
//             {
//                 Debug.LogError("Failed to write to file: " + e.Message);
//             }
//             frameCounter = 0;
//         }
//     }

//     void OnDestroy()
//     {
//         if (writer != null)
//         {
//             try
//             {
//                 writer.Close();
//                 Debug.Log("Closed StreamWriter");  // Add this line
//             }
//             catch (Exception e)
//             {
//                 Debug.LogError("Failed to close StreamWriter: " + e.Message);
//             }
//         }
//     }
// }

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
 
public class FPSPrint : MonoBehaviour
{
    private TextMeshProUGUI Text;
    public TextMeshProUGUI FPS_Text;
 
    private Dictionary<int, string> CachedNumberStrings = new();
    private int[] _frameRateSamples;
    private int _cacheNumbersAmount = 300;
    private int _averageFromAmount = 30;
    private int _averageCounter = 0;
    private int _currentAveraged;
 
    void Awake()
    {
        // Cache strings and create array
        {
            for (int i = 0; i < _cacheNumbersAmount; i++)
            {
                CachedNumberStrings[i] = i.ToString();
            }
            _frameRateSamples = new int[_averageFromAmount];
        }
    }
    void Update()
    {
        // Sample
        {
            var currentFrame = (int)Math.Round(1f / Time.smoothDeltaTime); // If your game modifies Time.timeScale, use unscaledDeltaTime and smooth manually (or not).
            _frameRateSamples[_averageCounter] = currentFrame;
        }
 
        // Average
        {
            var average = 0f;
 
            foreach (var frameRate in _frameRateSamples)
            {
                average += frameRate;
            }
 
            _currentAveraged = (int)Math.Round(average / _averageFromAmount);
            _averageCounter = (_averageCounter + 1) % _averageFromAmount;
        }
 
        // Assign to Private Text value
        {
            Text.text = _currentAveraged < _cacheNumbersAmount && _currentAveraged > 0
                ? CachedNumberStrings[_currentAveraged]
                : _currentAveraged < 0
                    ? "< 0"
                    : _currentAveraged > _cacheNumbersAmount
                        ? $"> {_cacheNumbersAmount}"
                        : "-1";
        }
        // Assign to UI with additional text indicator
        {
            FPS_Text.text = "FPS: " + Text.text;
        }
 
    }
}