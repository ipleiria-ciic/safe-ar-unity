using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class DebugText : MonoBehaviour
{
    [SerializeField]
    TextMeshProUGUI debugText;

    // Singleton instance
    public static DebugText Instance { get; private set; }

    void Awake()
    {
        // Set the instance to this script
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // Method to update the debug text
    public void UpdateTxt(string newText)
    {
        debugText.text = newText;
    }
}