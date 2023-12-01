using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

[LuauAPI]
public class InputProxy : MonoBehaviour {
	private struct KeyCodeAddRemove {
		public readonly KeyCode KeyCode;
		public readonly bool Add;
		public KeyCodeAddRemove(KeyCode keyCode, bool add) {
			KeyCode = keyCode;
			Add = add;
		}
	}
	
	[SerializeField] private MobileJoystick mobileJoystick;

	private Vector3 _lastMousePos = Vector3.zero;
	private readonly List<KeyCode> _keyCodes = new();
	private readonly List<KeyCodeAddRemove> _keyCodesAddRemove = new();
	private bool _firingKeyEvent = false;
	
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
	
	// public delegate void MouseDeltaDelegate(object mouseDelta);
	// public event MouseDeltaDelegate mouseDeltaEvent;
	
	// public delegate void TouchDelegate(object touchIndex, object position, object phase);
	// public event TouchDelegate touchEvent;
	// public event TouchDelegate touchTapEvent;
	//
	// public delegate void MobileJoystickDelegate(object position, object phase);
	// public event MobileJoystickDelegate mobileJoystickEvent;
	//
	// public delegate void SchemeDelegate(object scheme);
	// public event SchemeDelegate schemeChangedEvent;
	
	#endregion
	
	#region LUA-EXPOSED UTILITY METHODS

	public bool IsMobileJoystickVisible() {
		return mobileJoystick.JoystickVisible;
	}

	public void SetMobileJoystickVisible(bool visible) {
		mobileJoystick.JoystickVisible = visible;
	}

	public bool IsLeftMouseButtonDown() {
		return Input.GetMouseButton(0) || Input.GetMouseButtonDown(0);
	}

	public bool IsRightMouseButtonDown() {
		return Input.GetMouseButton(1) || Input.GetMouseButtonDown(1);
	}

	public bool IsMiddleMouseButtonDown() {
		return Input.GetMouseButton(2) || Input.GetMouseButtonDown(2);
	}
	
	public Vector3 GetMouseLocation() {
		return Input.mousePosition;
		// var pos = Mouse.current?.position.ReadValue() ?? Vector2.zero;
		// return new Vector3(pos.x, pos.y, 0);
	}

	public Vector3 GetMouseDelta() {
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

	public void RegisterKeyCode(int keyCodeInt) {
		var keyCode = (KeyCode)keyCodeInt;
		if (_firingKeyEvent) {
			_keyCodesAddRemove.Add(new KeyCodeAddRemove(keyCode, true));
		} else if (!_keyCodes.Contains(keyCode)) {
			_keyCodes.Add(keyCode);
		}
	}

	public void UnregisterKeyCode(int keyCodeInt) {
		var keyCode = (KeyCode)keyCodeInt;
		if (_firingKeyEvent) {
			_keyCodesAddRemove.Add(new KeyCodeAddRemove(keyCode, false));
		} else {
			_keyCodes.Remove(keyCode);
		}
	}
	
	#endregion
	
	#region COMPONENT SETUP

	private void OnEnable() {
		UserInputService.SetInputProxy(this);
	}

	private void Update() {
		// Button down:
		if (Input.GetMouseButtonDown(0)) {
			leftMouseButtonPressEvent?.Invoke(true);
		}
		if (Input.GetMouseButtonDown(1)) {
			rightMouseButtonPressEvent?.Invoke(true);
		}
		if (Input.GetMouseButtonDown(2)) {
			middleMouseButtonPressEvent?.Invoke(true);
		}

		// Button up:
		if (Input.GetMouseButtonUp(0)) {
			leftMouseButtonPressEvent?.Invoke(false);
		}
		if (Input.GetMouseButtonUp(1)) {
			rightMouseButtonPressEvent?.Invoke(false);
		}
		if (Input.GetMouseButtonUp(2)) {
			middleMouseButtonPressEvent?.Invoke(false);
		}

		// Mouse scroll:
		var scrollDelta = Input.mouseScrollDelta.y;
		if (scrollDelta != 0) {
			mouseScrollEvent?.Invoke(scrollDelta);
		}

		// Mouse move:
		var mousePos = Input.mousePosition;
		if (mousePos != _lastMousePos) {
			_lastMousePos = mousePos;
			mouseMoveEvent?.Invoke(mousePos);
		}

		// Keys:
		_firingKeyEvent = true;
		foreach (var keyCode in _keyCodes) {
			if (Input.GetKeyDown(keyCode)) {
				keyPressEvent?.Invoke((int)keyCode, true);
			}
			if (Input.GetKeyUp(keyCode)) {
				keyPressEvent?.Invoke((int)keyCode, false);
			}
		}
		_firingKeyEvent = false;
		
		// Add/remove key registrations that were triggered during a key press event:
		if (_keyCodesAddRemove.Count > 0) {
			foreach (var addRemove in _keyCodesAddRemove) {
				if (addRemove.Add) {
					if (!_keyCodes.Contains(addRemove.KeyCode)) {
						_keyCodes.Add(addRemove.KeyCode);
					}
				} else {
					_keyCodes.Remove(addRemove.KeyCode);
				}
			}
			_keyCodesAddRemove.Clear();
		}
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
