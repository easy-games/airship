using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using System;

public class AirshipLongPress : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler {
    [Tooltip("Time before the long press triggered")]
    public float holdTime = 0.2f;

    private float clickStartTime = 0f;

    public UnityEvent onClick = new UnityEvent();

    public UnityEvent onLongPress = new UnityEvent();

    public void OnPointerDown(PointerEventData eventData) {
        onClick.Invoke();
        this.clickStartTime = Time.time;
        // InvokeRepeating("OnLongPress", holdTime, intervalTime);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        //Debug.Log("Stop Long Pressing");
        CancelInvoke("OnLongPress");
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        //Debug.Log("Stop Long Pressing");
        CancelInvoke("OnLongPress");
    }

    private void OnLongPress()
    {
        try
        {
            //Debug.Log("Long Press is ongoing");
            onLongPress.Invoke();
        }
        catch (Exception ex)
        {
            Debug.Log(ex.ToString());
        }
    }
}