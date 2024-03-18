using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Gyroscope = UnityEngine.InputSystem.Gyroscope;

public class SensorDataReader : MonoBehaviour
{
    private Text sensorDataText;

    void Start()
    {
        sensorDataText = GetComponent<Text>();

        // Enable sensors
        InputSystem.EnableDevice(UnityEngine.InputSystem.Gyroscope.current);
        InputSystem.EnableDevice(Accelerometer.current);
        InputSystem.EnableDevice(GravitySensor.current);
        // TODO: AttitudeSensor
    }

    void Update()
    {
        // Read sensor data
        Vector3 angularVelocity = Gyroscope.current.angularVelocity.ReadValue();
        Vector3 acceleration = Accelerometer.current.acceleration.ReadValue();
        Vector3 gravity = GravitySensor.current.gravity.ReadValue();

        // Display sensor data
        sensorDataText.text =
            $"Angular Velocity: {angularVelocity}\n"
            + $"Acceleration: {acceleration}\n"
            + $"Gravity: {gravity}";
    }

    void OnDisable()
    {
        // Disable sensors
        InputSystem.DisableDevice(Gyroscope.current);
        InputSystem.DisableDevice(Accelerometer.current);
        InputSystem.DisableDevice(GravitySensor.current);
    }
}
