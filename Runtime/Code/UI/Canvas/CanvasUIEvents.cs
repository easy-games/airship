using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CanvasUIEvents : MonoBehaviour {

    /** Event interceptor instance. */
    public CanvasUIEventInterceptor interceptor;

    /** Last hovered GameObject. */
    private GameObject _lastHovered = null;

    private HashSet<int> _registeredEvents = new();

    public void RegisterEvents(GameObject gameObject) {
        if (_registeredEvents.Contains(gameObject.GetInstanceID()))
        {
            return;
        }

        _registeredEvents.Add(gameObject.GetInstanceID());
        
        if (!gameObject.TryGetComponent<EventTrigger>(out EventTrigger eventTrigger)) {
            eventTrigger = gameObject.AddComponent<EventTrigger>();
        }

        if (!gameObject.TryGetComponent<DestroyWatcher>(out var destroyWatcher)) {
            destroyWatcher = gameObject.AddComponent<DestroyWatcher>();
        }

        destroyWatcher.disabledEvent += () => {
            interceptor.FireDeselectEvent(gameObject.GetInstanceID());
        };

        destroyWatcher.destroyedEvent += () => {
            interceptor.FireDeselectEvent(gameObject.GetInstanceID());
        };

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

        // Submit
        EventTrigger.Entry submit = new EventTrigger.Entry();
        submit.eventID = EventTriggerType.Submit;
        submit.callback.AddListener((data) => {
            SubmitHook(data);
        });
        eventTrigger.triggers.Add(submit);

        // Select
        EventTrigger.Entry select = new EventTrigger.Entry();
        select.eventID = EventTriggerType.Select;
        select.callback.AddListener((data) => {
            SelectHook(data);
        });
        eventTrigger.triggers.Add(select);

        // Deselect
        EventTrigger.Entry deselect = new EventTrigger.Entry();
        deselect.eventID = EventTriggerType.Deselect;
        deselect.callback.AddListener((data) => {
            DeselectHook(data);
        });
        eventTrigger.triggers.Add(deselect);

        // Begin Drag
        EventTrigger.Entry beginDrag = new EventTrigger.Entry();
        beginDrag.eventID = EventTriggerType.BeginDrag;
        beginDrag.callback.AddListener((d) => {
            PointerEventData data = (PointerEventData)d;
            this.SetInterceptor();
            interceptor.FireBeginDragEvent(gameObject.GetInstanceID());
        });
        eventTrigger.triggers.Add(beginDrag);

        // End Drag
        EventTrigger.Entry endDrag = new EventTrigger.Entry();
        endDrag.eventID = EventTriggerType.EndDrag;
        endDrag.callback.AddListener((d) => {
            // PointerEventData data = (PointerEventData)d;
            this.SetInterceptor();
            interceptor.FireEndDragEvent(gameObject.GetInstanceID());
        });
        eventTrigger.triggers.Add(endDrag);

        // Drop
        EventTrigger.Entry drop = new EventTrigger.Entry();
        drop.eventID = EventTriggerType.Drop;
        drop.callback.AddListener((d) => {
            PointerEventData data = (PointerEventData)d;
            // this.SetInterceptor();
            interceptor.FireDropEvent(gameObject.GetInstanceID());
        });
        eventTrigger.triggers.Add(drop);

        // Drag
        EventTrigger.Entry drag = new EventTrigger.Entry();
        drag.eventID = EventTriggerType.Drag;
        drag.callback.AddListener((d) => {
            // PointerEventData data = (PointerEventData)d;
            this.SetInterceptor();
            interceptor.FireDragEvent(gameObject.GetInstanceID());
        });
        eventTrigger.triggers.Add(drag);

        if (gameObject.TryGetComponent<TMP_InputField>(out var inputField)) {
            inputField.onSubmit.AddListener((data) => {
                this.SetInterceptor();
                interceptor.FireInputFieldSubmit(inputField.gameObject.GetInstanceID(), data);
            });
        }
        
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

        // Toggle
        if (gameObject.TryGetComponent<Toggle>(out Toggle toggle)) {
            toggle.onValueChanged.AddListener((value) => {
                this.SetInterceptor();
                interceptor.FireToggleValueChangedEvent(toggle.gameObject.GetInstanceID(), value);
            });
        }

        // var childText = gameObject.GetComponentInChildren<TMP_Text>();
        // if (childText)
        // {
        //     childText.raycastTarget = false;
        // }
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

        if (interceptor != null) {
        }

        if (data.selectedObject != null) {
            interceptor.FireSelectEvent(data.selectedObject.GetInstanceID());
        }
    }

    public void DeselectHook(BaseEventData data = null) {
        if (data == null) return;
        this.SetInterceptor();

        if (data.selectedObject != null) {
            interceptor.FireDeselectEvent(data.selectedObject.GetInstanceID());
        }
    }

    public void ClickHook(GameObject gameObject)
    {
        this.SetInterceptor();
        this.interceptor.FireClickEvent(gameObject.GetInstanceID());
    }

    /** Sets global interceptor reference. */
    private void SetInterceptor() {
        if (!interceptor) {
            this.interceptor = FindObjectOfType<CanvasUIEventInterceptor>();
        }
    }

    public void ValueChangedHook(float value)
    {
        this.SetInterceptor();
        if (EventSystem.current.currentSelectedGameObject == null) {
            return;
        }
        interceptor.FireValueChangeEvent(EventSystem.current.currentSelectedGameObject.GetInstanceID(), value);
    }
}
