using System;
using UnityEngine;

[LuauAPI]
public class CanvasUIEventInterceptor : MonoBehaviour {

	/** Generic pointer event. */
	public event Action<object, object, object> PointerEvent;

	/** Generic hover event. */
	public event Action<object, object> HoverEvent;
	
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

	/** Fires a pointer event for instance that corresponds to `instanceId`. Includes pointer button and direction. (up or down) */
	public void FirePointerEvent(int instanceId, int direction, int button) {
		PointerEvent?.Invoke(instanceId, direction, button);
	}
	
	/** Fires a pointer event for instance that corresponds to `instanceId`. Includes pointer button and direction. (up or down) */
	public void FireHoverEvent(int instanceId, int hoverState) {
		HoverEvent?.Invoke(instanceId, hoverState);
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

	public void FireClickEvent(int instanceId)
	{
		ClickEvent?.Invoke(instanceId);
	}

	public void FireValueChangeEvent(int instanceId, float value)
	{
		ValueChangeEvent?.Invoke(instanceId, value);
	}
}
