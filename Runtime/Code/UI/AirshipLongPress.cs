using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using System;

public class AirshipLongPress : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler {
    [Tooltip("Time before the long press triggered")]
    public float holdTime = 0.2f;

    private float pressStartTime = 0f;
    private bool pressing = false;

    public event Action OnClick;
    public event Action OnLongPress;

    public void OnPointerDown(PointerEventData eventData) {
        this.OnClick?.Invoke();
        this.pressStartTime = Time.time;
        this.pressing = true;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        //Debug.Log("Stop Long Pressing");/**/
        this.pressing = false;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        //Debug.Log("Stop Long Pressing");
        this.pressing = false;
    }

    public void Update() {
        if (this.pressing && Time.time - this.pressStartTime >= this.holdTime) {
            this.pressing = false;
            this.OnLongPress?.Invoke();
        }
    }
}