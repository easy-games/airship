using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using System;

public class AirshipLongPress : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler, IDragHandler, IBeginDragHandler {
    [Tooltip("Time before the long press triggered")]
    public float holdTime = 0.22f;

    private float pressStartTime = 0f;
    private bool pressing = false;
    private Vector2 pressPosition;
    private IDragHandler dragHandlerImplementation;

    public event Action OnClick;
    public event Action<object> OnLongPress;

    public void OnPointerDown(PointerEventData eventData) {
        this.OnClick?.Invoke();
        this.pressStartTime = Time.time;
        this.pressing = true;
        this.pressPosition = eventData.pressPosition;
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
            this.OnLongPress?.Invoke(this.pressPosition);
        }
    }

    public void OnDrag(PointerEventData eventData) {
        this.pressing = false;
    }

    public void OnBeginDrag(PointerEventData eventData) {
        this.pressing = false;
    }
}