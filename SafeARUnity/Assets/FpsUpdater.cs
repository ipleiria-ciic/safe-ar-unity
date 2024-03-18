using TMPro;
using UnityEngine;

public class FpsUpdater : MonoBehaviour
{
    float fps;
    float updateTimer = 0.2f; // Update every 200ms

    [SerializeField]
    TextMeshProUGUI fpsText;

    private void UpdateFPSDisplay()
    {
        updateTimer -= Time.deltaTime;
        if (updateTimer <= 0f)
        {
            fps = 1f / Time.unscaledDeltaTime;
            fpsText.text = "FPS: " + fps.ToString("F1");
            updateTimer = 0.2f;
        }
    }

    void Update()
    {
        UpdateFPSDisplay();
    }
}
