using System;
using System.Collections.Generic;
using FishNet.Object;
using UnityEngine;
using UnityEngine.EventSystems;

[LuauAPI]
public class InputProxy : MonoBehaviour {
	[SerializeField] private MobileJoystick mobileJoystick;
	
	#region LUA-EXPOSED EVENTS
	
	public delegate void KeyDelegate(object key, object down);
	public event KeyDelegate keyPressEvent;
	
	public delegate void MouseButtonDelegate(object down);
	public event MouseButtonDelegate leftMouseButtonPressEvent;
	public event MouseButtonDelegate rightMouseButtonPressEvent;
	public event MouseButtonDelegate middleMouseButtonPressEvent;
	
	public delegate void MouseScrollDelegate(object scrollAmount);
	public event MouseScrollDelegate mouseScrollEvent;
	
	public delegate void MouseMoveDelegate(object mouseLocation);
	public event MouseMoveDelegate mouseMoveEvent;
	
	public delegate void MouseDeltaDelegate(object mouseDelta);
	public event MouseDeltaDelegate mouseDeltaEvent;
	
	public delegate void TouchDelegate(object touchIndex, object position, object phase);
	public event TouchDelegate touchEvent;
	public event TouchDelegate touchTapEvent;

	public delegate void MobileJoystickDelegate(object position, object phase);
	public event MobileJoystickDelegate mobileJoystickEvent;

	public delegate void SchemeDelegate(object scheme);
	public event SchemeDelegate schemeChangedEvent;
	
	#endregion
	
	#region LUA-EXPOSED UTILITY METHODS

	public bool IsMobileJoystickVisible() {
		return mobileJoystick.JoystickVisible;
	}

	public void SetMobileJoystickVisible(bool visible) {
		mobileJoystick.JoystickVisible = visible;
	}

	public bool IsLeftMouseButtonDown()
	{
		return Input.GetMouseButton(0) || Input.GetMouseButtonDown(0);
	}

	public bool IsRightMouseButtonDown()
	{
		return Input.GetMouseButton(1) || Input.GetMouseButtonDown(1);
	}

	public bool IsMiddleMouseButtonDown()
	{
		return Input.GetMouseButton(2) || Input.GetMouseButtonDown(2);
	}
	
	public Vector3 GetMouseLocation()
	{
		return Input.mousePosition;
		// var pos = Mouse.current?.position.ReadValue() ?? Vector2.zero;
		// return new Vector3(pos.x, pos.y, 0);
	}
	
	public Vector3 GetMouseDelta()
	{
		var dx = Input.GetAxis("Mouse X");
		var dy = Input.GetAxis("Mouse Y");
		return new Vector3(dx, dy, 0);
	}

	public void SetMouseLocation(Vector3 position) {
		// Mouse.current?.WarpCursorPosition(position);
	}

	public void SetMouseLocked(bool mouseLocked) {
		Cursor.lockState = mouseLocked ? CursorLockMode.Locked : CursorLockMode.None;
	}

	public bool IsMouseLocked() {
		return Cursor.lockState == CursorLockMode.Locked;
	}

	public string GetScheme() {
		// return _playerInput.currentControlScheme;
		return "MouseKeyboard";
	}

	public bool IsPointerOverUI() {
		var eventDataCurrentPos = new PointerEventData(EventSystem.current);

		// switch (_playerInput.currentControlScheme) {
		// 	case "MouseKeyboard":
		// 		eventDataCurrentPos.position = Mouse.current.position.ReadValue();
		// 		break;
		// 	case "Touch":
		// 		eventDataCurrentPos.position = Touchscreen.current.position.ReadValue();
		// 		break;
		// }

		var posV3 = GetMouseLocation();
		var pos = new Vector2(posV3.x, posV3.y);
		eventDataCurrentPos.position = pos;

		var results = new List<RaycastResult>();
		EventSystem.current.RaycastAll(eventDataCurrentPos, results);

		return results.Count > 0;
	}
	
	#endregion
	
	#region COMPONENT SETUP

	private void OnEnable()
	{
		UserInputService.SetInputProxy(this);
	}

	/*
	private void Awake() {
		_playerInput = GetComponent<PlayerInput>();
		_playerInput.enabled = true;
	}
	
	private void OnEnable() {
		UserInputService.SetInputProxy(this);
		EnhancedTouchSupport.Enable();
		mobileJoystick.OnChanged += OnMobileJoystickChanged;
	}

	private void OnDisable() {
		EnhancedTouchSupport.Disable();
		if (mobileJoystick != null) {
			mobileJoystick.OnChanged -= OnMobileJoystickChanged;
		}
	}
	*/

	#endregion
	
	#region UNITY EVENTS

	/*
	public void OnMobileJoystickChanged(Vector2 position, MobileJoystickPhase phase) {
		mobileJoystickEvent?.Invoke(new Vector3(position.x, 0, position.y), (int) phase);
	}

	public void OnKeyPress(InputAction.CallbackContext context) {
		// if (context.performed == true) return;
		var keyControl = (KeyControl) context.control;
		keyPressEvent?.Invoke((int) keyControl.keyCode, keyControl.isPressed);
	}

	public void OnLeftMouseButton(InputAction.CallbackContext context) {
		var buttonControl = (ButtonControl) context.control;
		leftMouseButtonPressEvent?.Invoke(buttonControl.isPressed);
	}

	public void OnRightMouseButton(InputAction.CallbackContext context) {
		var buttonControl = (ButtonControl) context.control;
		rightMouseButtonPressEvent?.Invoke(buttonControl.isPressed);
	}

	public void OnMiddleMouseButton(InputAction.CallbackContext context) {
		var buttonControl = (ButtonControl) context.control;
		middleMouseButtonPressEvent?.Invoke(buttonControl.isPressed);
	}

	public void OnMouseScroll(InputAction.CallbackContext context)
	{
		var deltaScroll = context.ReadValue<Vector2>().y;
		if (deltaScroll == 0)
		{
			return;
		}
		mouseScrollEvent?.Invoke(deltaScroll);
	}

	public void OnMouseMove(InputAction.CallbackContext context) {
		var location = context.ReadValue<Vector2>();
		mouseMoveEvent?.Invoke(new Vector3(location.x, location.y, 0));
	}

	public void OnMouseDelta(InputAction.CallbackContext context) {
		var delta = context.ReadValue<Vector2>();
		mouseDeltaEvent?.Invoke(new Vector3(delta.x, delta.y, 0));
	}

	public void OnTouchPrimary(InputAction.CallbackContext context) {
		var touchControl = (TouchControl) context.control;
		var position = touchControl.position.ReadValue();
		touchEvent?.Invoke(0, new Vector3(position.x, position.y, 0), (int) touchControl.phase.ReadValue());
	}

	public void OnTouchSecondary(InputAction.CallbackContext context) {
		var touchControl = (TouchControl) context.control;
		var position = touchControl.position.ReadValue();
		touchEvent?.Invoke(1, new Vector3(position.x, position.y, 0), (int) touchControl.phase.ReadValue());
	}

	public void OnTouchTapPrimary(InputAction.CallbackContext context) {
		var position = Touchscreen.current.primaryTouch.position.ReadValue();
		touchTapEvent?.Invoke(0, new Vector3(position.x, position.y, 0), (int) context.phase);
	}

	public void OnTouchTapSecondary(InputAction.CallbackContext context) {
		var position = Touchscreen.current.touches[1].position.ReadValue();
		touchTapEvent?.Invoke(1, new Vector3(position.x, position.y, 0), (int) context.phase);
	}

	public void OnControlsChanged(PlayerInput playerInput) {
		schemeChangedEvent?.Invoke(playerInput.currentControlScheme);
	}
	*/
	
	#endregion
}
