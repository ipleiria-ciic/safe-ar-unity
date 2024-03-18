using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Gyroscope = UnityEngine.InputSystem.Gyroscope;

public class SensorGraphs : MonoBehaviour
{
    public LineRenderer lineRenderer1;
    public LineRenderer lineRenderer2;
    public Text xAxisLabel1;
    public Text xAxisLabel2;
    public Text yAxisLabel1;
    public Text yAxisLabel2;
    public int maxPoints = 500;
    private List<Vector3> points1 = new List<Vector3>();
    private List<Vector3> points2 = new List<Vector3>();

    void Update()
    {
        // Update sensor data
        Vector3 angularVelocity = Gyroscope.current.angularVelocity.ReadValue();
        Vector3 acceleration = Accelerometer.current.acceleration.ReadValue();
        AddPoint(lineRenderer1, angularVelocity);
        AddPoint(lineRenderer2, acceleration);

        // Update axis labels with formatted values
        xAxisLabel1.text = $"X: {angularVelocity.x:F1}";
        xAxisLabel2.text = $"X: {acceleration.x:F1}";
        yAxisLabel1.text = $"Y: {angularVelocity.y:F1}";
        yAxisLabel2.text = $"Y: {acceleration.y:F1}";
    }

    void AddPoint(LineRenderer lineRenderer, Vector3 point)
    {
        List<Vector3> points = lineRenderer == lineRenderer1 ? points1 : points2;
        if (points.Count >= maxPoints)
        {
            points.RemoveAt(0);
        }
        points.Add(point);

        lineRenderer.positionCount = points.Count;
        lineRenderer.SetPositions(points.ToArray());
    }
}


// To associate the script elements with the Unity Editor, follow these steps:

// Create a Canvas: If you haven't already, create a UI Canvas in your scene. This will serve as the container for your graph and its labels.
// Add Line Renderers: For each sensor you want to visualize, add a Line Renderer component to a GameObject within the Canvas. You can position these GameObjects in a grid or any other layout that suits your needs.
// Add Text Elements for Labels: For each axis, add Text UI elements to display the axis labels and values. You can position these labels at the ends of your axes.
// Attach the Script to a GameObject: Drag the script asset (SensorGraphs.cs) to a GameObject in the hierarchy panel or to the inspector of the GameObject that is currently selected. This will attach the script as a component to the GameObject, making it active and ready to use 1.
// Assign References in the Inspector: Once the script is attached to a GameObject, you'll see the script's public variables in the Inspector panel of that GameObject. Here, you need to assign references to the LineRenderer components for lineRenderer1 and lineRenderer2, as well as the Text components for xAxisLabel1, xAxisLabel2, yAxisLabel1, and yAxisLabel2. To do this, drag each corresponding UI element from the hierarchy panel to the corresponding field in the Inspector 1.
// Configure Additional Settings: In the Inspector, you can also configure additional settings for your script, such as the maxPoints variable, which determines how many points are displayed on the graph.