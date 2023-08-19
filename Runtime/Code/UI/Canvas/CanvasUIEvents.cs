using System;
using System.Collections.Generic;
using FishNet;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CanvasUIEvents : MonoBehaviour {

    /** Event interceptor instance. */
    public CanvasUIEventInterceptor interceptor;

    /** Last hovered GameObject. */
    private GameObject _lastHovered = null;

    private HashSet<int> _registeredEvents = new();

    public void RegisterEvents(GameObject gameObject)
    {
        if (_registeredEvents.Contains(gameObject.GetInstanceID()))
        {
            return;
        }

        _registeredEvents.Add(gameObject.GetInstanceID());
        
        if (!gameObject.TryGetComponent<EventTrigger>(out EventTrigger eventTrigger))
        {
            eventTrigger = gameObject.AddComponent<EventTrigger>();
        }

        // Pointer enter
        EventTrigger.Entry pointerEnter = new EventTrigger.Entry();
        pointerEnter.eventID = EventTriggerType.PointerEnter;
        pointerEnter.callback.AddListener((data) =>
        {
            PointerEnterHook(data);
        });
        eventTrigger.triggers.Add(pointerEnter);
        
        // Pointer down
        EventTrigger.Entry pointerDown = new EventTrigger.Entry();
        pointerDown.eventID = EventTriggerType.PointerDown;
        pointerDown.callback.AddListener((data) =>
        {
            PointerDownHook(data);
        });
        eventTrigger.triggers.Add(pointerDown);
        
        // Click
        if (gameObject.TryGetComponent<Button>(out var button))
        {
            button.onClick.AddListener(() =>
            {
                ClickHook(button.gameObject);
            });
        }

        // Slider value changed
        if (gameObject.TryGetComponent<Slider>(out Slider slider))
        {
            slider.onValueChanged.AddListener((value) =>
            {
                ValueChangedHook(value);
            });
        }

        var childText = gameObject.GetComponentInChildren<TMP_Text>();
        if (childText)
        {
            childText.raycastTarget = false;
        }
    }

    /**
     * `PointerDown` handler.
     * Captured event is routed to the interceptor and dispatched to TS.
     */
    public void PointerDownHook(BaseEventData data = null) {
        if (data == null) return;
        this.SetInterceptor();
        if (data is PointerEventData pointerData) {
            var target = pointerData.pointerPressRaycast.gameObject;
            var button = (int)pointerData.button;
            interceptor.FirePointerEvent(target.GetInstanceID(), 0, button);
        }
    }

    /**
     * `PointerUp` handler.
     * Captured event is routed to the interceptor and dispatched to TS.
     */
    public void PointerUpHook(BaseEventData data = null) {
        if (data == null) return;
        this.SetInterceptor();
        if (data is PointerEventData pointerData) {
            var target = pointerData.pointerPressRaycast.gameObject;
            var button = (int)pointerData.button;
            interceptor.FirePointerEvent(target.GetInstanceID(), 1, button);
        }
    }
    
    /**
     * `PointerEnter` handler.
     * Captured event is routed to the interceptor and dispatched to TS.
     */
    public void PointerEnterHook(BaseEventData data = null) {
        if (data == null) return;
        this.SetInterceptor();
        if (data is PointerEventData pointerData) {
            var target = pointerData.pointerEnter.gameObject;
            this._lastHovered = target;
            interceptor.FireHoverEvent(target.GetInstanceID(), 0);
        }
    }
    
    /**
     * `PointerExit` handler.
     * Captured event is routed to the interceptor and dispatched to TS.
     */
    public void PointerExitHook(BaseEventData data = null) {
        if (data == null) return;
        this.SetInterceptor();
        if (data is PointerEventData pointerData) {
            if (this._lastHovered != null) {
                var target = this._lastHovered;
                interceptor.FireHoverEvent(target.GetInstanceID(), 1);
            }
        }
    }
    
    public void SubmitHook(BaseEventData data = null) {
        if (data == null) return;
        this.SetInterceptor();
        
        interceptor.FireSubmitEvent(data.selectedObject.GetInstanceID());
    }
    
    public void SelectHook(BaseEventData data = null) {
        if (data == null) return;
        this.SetInterceptor();
        
        interceptor.FireSelectEvent(data.selectedObject.GetInstanceID());
    }

    public void DeselectHook(BaseEventData data = null) {
        if (data == null) return;
        this.SetInterceptor();
        
        interceptor.FireDeselectEvent(data.selectedObject.GetInstanceID());
    }

    public void ClickHook(GameObject gameObject)
    {
        this.SetInterceptor();
        this.interceptor.FireClickEvent(gameObject.GetInstanceID());
    }

    /** Sets global interceptor reference. */
    private void SetInterceptor() {
        // if (interceptor == null)
        // {
        //     var eventInterceptor = FindObjectOfType<CanvasUIEventInterceptor>();
        //     if (eventInterceptor)
        //     {
        //         this.interceptor = eventInterceptor;
        //     }
        // }
    }

    public void ValueChangedHook(float value)
    {
        this.SetInterceptor();
        if (EventSystem.current.currentSelectedGameObject == null)
        {
            return;
        }
        interceptor.FireValueChangeEvent(EventSystem.current.currentSelectedGameObject.GetInstanceID(), value);
    }
}
