using System;
using UnityEngine;
using UnityEngine.EventSystems;

[LuauAPI]
public class CanvasUIEventInterceptor : MonoBehaviour {

	/** Generic pointer event. */
	public event Action<int, int, int> PointerEvent;

	/** Generic hover event. */
	public event Action<int, int, PointerEventData> HoverEvent;
	
	/** Params: InstanceId */
	public event Action<int> SubmitEvent;

	/** Params: InstanceId, string value */
	public event Action<int, string> InputFieldSubmitEvent;
	
	/** Params: InstanceId */
	public event Action<int> SelectEvent;
	
	/** Params: InstanceId */
	public event Action<int> DeselectEvent;

	public event Action<int> ClickEvent;
	
	public event Action<int, float> ValueChangeEvent;

	public event Action<int, bool> ToggleValueChangeEvent;

	public event Action<int, PointerEventData> BeginDragEvent;
	public event Action<int, PointerEventData> EndDragEvent;
	public event Action<int, PointerEventData> DropEvent;
	public event Action<int, PointerEventData> DragEvent;

	public event Action<int, int> ScreenSizeChangeEvent;

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
