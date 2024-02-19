using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class TouchSimulator : MonoBehaviour
{
    void Update()
    {
        // Simulate touch input as a click.
        if (Input.GetMouseButtonDown(0))
        {
            PointerEventData eventData = new PointerEventData(EventSystem.current);
            eventData.position = Input.mousePosition;
            eventData.button = PointerEventData.InputButton.Left;
            EventSystem.current.RaycastAll(eventData, null);
            ExecuteEvents.Execute(eventData.pointerEnter, eventData, ExecuteEvents.pointerClickHandler);
        }
    }
}
