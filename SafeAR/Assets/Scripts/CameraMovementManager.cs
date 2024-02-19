namespace Mapbox.Examples
{
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEngine.EventSystems;
    using Mapbox.Unity.Map;
    using UnityEngine.UIElements;

    public class CameraMovementManager : MonoBehaviour
    {
        [SerializeField]
        AbstractMap _map;

        [SerializeField]
        float _rotationSpeed = 5.0f;
        [SerializeField] private float _rotationSpeedMobile = 1f;

        [SerializeField]
        float _followSpeed = 5.0f;
        [SerializeField]float _followSpeedMobile = 1f;

        [SerializeField]
        float _zoomSpeed = 50f;
        [SerializeField] float _zoomSpeedMobile = 1f;

        [SerializeField]
        Camera _referenceCamera;

        [SerializeField]
        Transform _cameraTarget; // Reference to the player's transform

        private Vector3 _offset;
        private Vector3 initialPosition = new Vector3(-0.5f,58, -39);
        private float initialRotationX;

        private void Awake()
        {
            if (_referenceCamera == null)
            {
                _referenceCamera = GetComponent<Camera>();
                if (_referenceCamera == null)
                {
                    throw new System.Exception("You must have a reference camera assigned!");
                }
            }

            if (_map == null)
            {
                _map = FindObjectOfType<AbstractMap>();
                if (_map == null)
                {
                    throw new System.Exception("You must have a reference map assigned!");
                }
            }
            
        }

        private void Start()
        {
            _offset = initialPosition - _cameraTarget.position;
            initialRotationX = transform.eulerAngles.x;
        }

        private bool isSimulator = true;
        private void HandleInput()
        {
            if (isSimulator)
            {
                HandleTouchInput();
            } else {
                HandleMouseAndKeyboardInput();
            }
        }

        private float maxRotationAngle = 2.0f;
        private void HandleTouchInput()
        {
            if (Input.touchCount == 1 && !EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId))
            {
                //Debug.Log("Touch moved");
                Touch touch = Input.GetTouch(0);
                if (touch.phase == TouchPhase.Moved)
                {
                    float horizontalInput = Mathf.Clamp(touch.deltaPosition.x, -maxRotationAngle, maxRotationAngle) * _rotationSpeedMobile;

                    _offset = Quaternion.Euler(0, horizontalInput, 0) * _offset;

                    //Handle rotation on Y axis and keep the initial X rotation
                    Vector3 newPosition = _cameraTarget.position + _offset;
                    transform.position = Vector3.Lerp(transform.position, newPosition, Time.deltaTime * _followSpeed);
                    transform.LookAt(_cameraTarget);
                    transform.eulerAngles = new Vector3(initialRotationX, transform.eulerAngles.y, transform.eulerAngles.z);

                    
                }
            }
            // Add code to handle pinch-to-zoom for smartphones when touchCount is 2
            if (Input.touchCount == 2)
            {
                // Handle pinch-to-zoom
                Vector2 touch1 = Input.GetTouch(0).position;
                Vector2 touch2 = Input.GetTouch(1).position;
                Vector2 touch1Prev = touch1 - Input.GetTouch(0).deltaPosition;
                Vector2 touch2Prev = touch2 - Input.GetTouch(1).deltaPosition;

                float prevMagnitude = (touch1Prev - touch2Prev).magnitude;
                float currentMagnitude = (touch1 - touch2).magnitude;
                float deltaMagnitude = currentMagnitude - prevMagnitude;

                float zoomFactor = deltaMagnitude * _zoomSpeedMobile;
                Vector3 move = transform.forward * zoomFactor;
                _offset += move;
            }

            Vector3 newPosition1 = _cameraTarget.position + _offset;
            transform.position = newPosition1;
        }

        private void HandleMouseAndKeyboardInput()
        {
            if (Input.GetMouseButton(0) && !EventSystem.current.IsPointerOverGameObject())
            {
                float horizontalInput = Input.GetAxis("Mouse X") * _rotationSpeed;
                _offset = Quaternion.Euler(0, horizontalInput, 0) * _offset;

                //Handle rotation on Y axis and keep the initial X rotation
                Vector3 newPosition = _cameraTarget.position + _offset;
                transform.position = Vector3.Lerp(transform.position, newPosition, Time.deltaTime * _followSpeed);
                transform.LookAt(_cameraTarget);
                transform.eulerAngles = new Vector3(initialRotationX, transform.eulerAngles.y, transform.eulerAngles.z);
            }
            else
            {
                if (EventSystem.current.IsPointerOverGameObject())
                {
                    return;
                }

                float zoomFactor = Input.GetAxis("Mouse ScrollWheel") * _zoomSpeed;
                Vector3 move = transform.forward * zoomFactor;
                _offset += move;

            }

            Vector3 newPosition1 = _cameraTarget.position + _offset;
            //newPosition = initialPosition + (newPosition - initialPosition).normalized * _offset.magnitude;
            transform.position = newPosition1;
        }

        #region HandleMouseAndKeyboardInput old
        //if (Input.GetMouseButton(0) && !EventSystem.current.IsPointerOverGameObject())
        //{
        //    float horizontalInput = Input.GetAxis("Mouse X") * _rotationSpeed;

        //    // Calculate the pivot point around the player's position
        //    Vector3 pivotPoint = _cameraTarget.position;

        //    // Calculate the new camera position
        //    Vector3 offset = transform.position - pivotPoint;
        //    Quaternion rotation = Quaternion.Euler(0, horizontalInput, 0);
        //    offset = rotation * offset;
        //    Vector3 newPosition = pivotPoint + offset;

        //    transform.position = newPosition;
        //    // Calculate a new Quaternion for LookAt with the desired X rotation

        //}
        //else
        //{
        //    if (EventSystem.current.IsPointerOverGameObject())
        //    {
        //        return;
        //    }

        //    float scrollInput = Input.GetAxis("Mouse ScrollWheel");

        //    if (scrollInput != 0)
        //    {
        //        // Handle scroll input
        //        float zoomFactor = scrollInput * _zoomSpeed;
        //        Vector3 move = transform.forward * zoomFactor;
        //        _offset += move;

        //        // Handle zoom in without changing the Y position
        //        Vector3 newPosition = _cameraTarget.position + _offset; // Keep the same Y position
        //        transform.position = newPosition;

        //        // Update the camera's rotation to look at the target
        //    }
        //}

        //transform.LookAt(_cameraTarget);

        //Vector3 eulerAngles = transform.eulerAngles;
        //eulerAngles.x = initialRotationX;
        //transform.eulerAngles = eulerAngles;
        #endregion

        private void LateUpdate()
        {
            HandleInput();
        }
    }
}
