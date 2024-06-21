using System;
using UnityEngine;
using UnityEngine.EventSystems;

[LuauAPI]
public class CanvasUIEventInterceptor : MonoBehaviour {

	/** Generic pointer event. */
	public event Action<object, object, object> PointerEvent;

	/** Generic hover event. */
	public event Action<object, object, object> HoverEvent;
	
	/** Params: InstanceId */
	public event Action<object> SubmitEvent;

	/** Params: InstanceId, string value */
	public event Action<object, object> InputFieldSubmitEvent;
	
	/** Params: InstanceId */
	public event Action<object> SelectEvent;
	
	/** Params: InstanceId */
	public event Action<object> DeselectEvent;

	public event Action<object> ClickEvent;
	
	public event Action<object, object> ValueChangeEvent;

	public event Action<object, object> ToggleValueChangeEvent;

	public event Action<object, object> BeginDragEvent;
	public event Action<object, object> EndDragEvent;
	public event Action<object, object> DropEvent;
	public event Action<object, object> DragEvent;

	public event Action<object, object> ScreenSizeChangeEvent;

	/** Fires a pointer event for instance that corresponds to `instanceId`. Includes pointer button and direction. (up or down) */
	public void FirePointerEvent(int instanceId, int direction, int button) {
		PointerEvent?.Invoke(instanceId, direction, button);
	}
	
	/** Fires a pointer event for instance that corresponds to `instanceId`. Includes pointer button and direction. (up or down) */
	public void FireHoverEvent(int instanceId, int hoverState, PointerEventData data) {
		HoverEvent?.Invoke(instanceId, hoverState, data);
	}

	public void FireSubmitEvent(int instanceId) {
		SubmitEvent?.Invoke(instanceId);
	}

	public void FireInputFieldSubmit(int instanceId, string value) {
		InputFieldSubmitEvent?.Invoke(instanceId, value);
	}
	
	public void FireSelectEvent(int instanceId) {
		SelectEvent?.Invoke(instanceId);
	}
	
	public void FireDeselectEvent(int instanceId) {
		DeselectEvent?.Invoke(instanceId);
	}

	public void FireBeginDragEvent(int instanceId, PointerEventData data) {
		BeginDragEvent?.Invoke(instanceId, data);
	}

	public void FireEndDragEvent(int instanceId, PointerEventData data) {
		EndDragEvent?.Invoke(instanceId, data);
	}

	public void FireDropEvent(int instanceId, PointerEventData data) {
		DropEvent?.Invoke(instanceId, data);
	}

	public void FireDragEvent(int instanceId, PointerEventData data) {
		DragEvent?.Invoke(instanceId, data);
	}

	public void FireClickEvent(int instanceId)
	{
		ClickEvent?.Invoke(instanceId);
	}

	public void FireValueChangeEvent(int instanceId, float value)
	{
		ValueChangeEvent?.Invoke(instanceId, value);
	}

	public void FireToggleValueChangedEvent(int instanceId, bool value) {
		ToggleValueChangeEvent?.Invoke(instanceId, value);
	}

	public void FireScreenSizeEvent(int width, int height){
		ScreenSizeChangeEvent?.Invoke(width, height);
	}
}
