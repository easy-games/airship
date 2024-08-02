using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[LuauAPI]
public class CanvasUIEvents : MonoBehaviour {

    /** Event interceptor instance. */
    public CanvasUIEventInterceptor interceptor;

    /** Last hovered GameObject. */
    private GameObject _lastHovered = null;

    private HashSet<int> _registeredEvents = new();

    public void RegisterEvents(GameObject gameObject) {
        var instanceId = gameObject.GetInstanceID();
        
        if (_registeredEvents.Contains(instanceId)) {
            return;
        }

        _registeredEvents.Add(instanceId);
        
        if (!gameObject.TryGetComponent<EventTrigger>(out EventTrigger eventTrigger)) {
            eventTrigger = gameObject.AddComponent<EventTrigger>();
        }

        if (!gameObject.TryGetComponent<DestroyWatcher>(out var destroyWatcher)) {
            destroyWatcher = gameObject.AddComponent<DestroyWatcher>();
        }

        destroyWatcher.disabledEvent += () => {
            if (interceptor) {
                interceptor.FireDeselectEvent(instanceId);
            }
        };

        destroyWatcher.destroyedEvent += () => {
            if (interceptor) {
                interceptor.FireDeselectEvent(instanceId);
            }
        };

        // Pointer enter
        EventTrigger.Entry pointerEnter = new EventTrigger.Entry();
        pointerEnter.eventID = EventTriggerType.PointerEnter;
        pointerEnter.callback.AddListener((d) =>
        {
            PointerEventData data = (PointerEventData)d;
            PointerEnterHook(gameObject, data);
        });
        eventTrigger.triggers.Add(pointerEnter);

        // Pointer Exit
        EventTrigger.Entry pointerExit = new EventTrigger.Entry();
        pointerExit.eventID = EventTriggerType.PointerExit;
        pointerExit.callback.AddListener((d) =>
        {
            PointerEventData data = (PointerEventData)d;
            PointerExitHook(gameObject, data);
        });
        eventTrigger.triggers.Add(pointerExit);

        // Pointer down
        EventTrigger.Entry pointerDown = new EventTrigger.Entry();
        pointerDown.eventID = EventTriggerType.PointerDown;
        pointerDown.callback.AddListener((data) =>
        {
            PointerDownHook(data);
        });
        eventTrigger.triggers.Add(pointerDown);

        // Pointer up
        EventTrigger.Entry pointerUp = new EventTrigger.Entry();
        pointerUp.eventID = EventTriggerType.PointerUp;
        pointerUp.callback.AddListener((data) =>
        {
            PointerUpHook(data);
        });
        eventTrigger.triggers.Add(pointerUp);

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
            interceptor.FireBeginDragEvent(instanceId, data);
        });
        eventTrigger.triggers.Add(beginDrag);

        // End Drag
        EventTrigger.Entry endDrag = new EventTrigger.Entry();
        endDrag.eventID = EventTriggerType.EndDrag;
        endDrag.callback.AddListener((d) => {
            PointerEventData data = (PointerEventData)d;
            this.SetInterceptor();
            interceptor.FireEndDragEvent(instanceId, data);
        });
        eventTrigger.triggers.Add(endDrag);

        // Drop
        EventTrigger.Entry drop = new EventTrigger.Entry();
        drop.eventID = EventTriggerType.Drop;
        drop.callback.AddListener((d) => {
            PointerEventData data = (PointerEventData)d;
            // this.SetInterceptor();
            interceptor.FireDropEvent(instanceId, data);
        });
        eventTrigger.triggers.Add(drop);

        // Drag
        EventTrigger.Entry drag = new EventTrigger.Entry();
        drag.eventID = EventTriggerType.Drag;
        drag.callback.AddListener((d) => {
            PointerEventData data = (PointerEventData)d;
            this.SetInterceptor();
            interceptor.FireDragEvent(instanceId, data);
        });
        eventTrigger.triggers.Add(drag);


        if (gameObject.TryGetComponent<TMP_InputField>(out var inputField)) {
            inputField.onSubmit.AddListener((data) => {
                this.SetInterceptor();
                interceptor.FireInputFieldSubmit(inputField.gameObject.GetInstanceID(), data);
            });
            inputField.onValueChanged.AddListener((data) => {
                this.SetInterceptor();
                interceptor.FireValueChangeEvent(inputField.gameObject.GetInstanceID(), 0);
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
                ValueChangedHook(slider.gameObject, value);
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

    private Vector2Int screenSize = Vector2Int.zero;

    private void FixedUpdate(){
        if(screenSize.x != Screen.width  || screenSize.y != Screen.height){
            screenSize.x = Screen.width;
            screenSize.y = Screen.height;
            SetInterceptor();
            interceptor.FireScreenSizeEvent(screenSize.x, screenSize.y);
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
    public void PointerEnterHook(GameObject go, PointerEventData data) {
        this.SetInterceptor();
        interceptor.FireHoverEvent(go.GetInstanceID(), 0, data);
    }
    
    /**
     * `PointerExit` handler.
     * Captured event is routed to the interceptor and dispatched to TS.
     */
    public void PointerExitHook(GameObject go, PointerEventData data) {
        this.SetInterceptor();
        interceptor.FireHoverEvent(go.GetInstanceID(), 1, data);
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
            if (interceptor != null) {
                interceptor.FireDeselectEvent(data.selectedObject.GetInstanceID());
            }
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

    public void ValueChangedHook(GameObject gameObject, float value)
    {
        this.SetInterceptor();
        interceptor.FireValueChangeEvent(gameObject.GetInstanceID(), value);
    }
}
