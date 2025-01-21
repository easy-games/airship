using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

[LuauAPI]
public class InputBridge : Singleton<InputBridge> {
	// [SerializeField] private MobileJoystick mobileJoystick;

	private Vector2 _mouseLockedPos = Vector2Int.zero;
	private readonly List<IDisposable> _disposables = new();
	
	#region LUA-EXPOSED EVENTS
	
	public delegate void KeyDelegate(object key, object down);
	public event KeyDelegate keyPressEvent;
	
	public delegate void MouseButtonDelegate(object down);
	public event MouseButtonDelegate leftMouseButtonPressEvent;
	public event MouseButtonDelegate rightMouseButtonPressEvent;
	public event MouseButtonDelegate middleMouseButtonPressEvent;
	public event MouseButtonDelegate backMouseButtonPressEvent;
	public event MouseButtonDelegate forwardMouseButtonPressEvent;
	
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

	// public bool IsMobileJoystickVisible() {
	// 	return mobileJoystick.JoystickVisible;
	// }
	//
	// public void SetMobileJoystickVisible(bool visible) {
	// 	mobileJoystick.JoystickVisible = visible;
	// }

	public bool IsLeftMouseButtonDown() {
		return Mouse.current?.leftButton.isPressed ?? false;
	}

	public bool IsRightMouseButtonDown() {
		return Mouse.current?.rightButton.isPressed ?? false;
	}

	public bool IsMiddleMouseButtonDown() {
		return Mouse.current?.middleButton.isPressed ?? false;
	}
	
	public bool IsForwardMouseButtonDown() {
		return Mouse.current?.forwardButton.isPressed ?? false;
	}

	public bool IsBackMouseButtonDown() {
		return Mouse.current?.backButton.isPressed ?? false;
	}

	public Vector2 GetMousePosition() {
		return Mouse.current?.position.value ?? Touchscreen.current?.position.value ?? Vector2.zero;
	}

	public Vector2 GetMouseDelta() {
		return Mouse.current?.delta.value ?? Touchscreen.current?.delta.value ?? Vector2.zero;
	}

	public void WarpCursorPosition(Vector2 pos) {
		Mouse.current.WarpCursorPosition(pos);
	}

	public void SetMouseLocked(bool mouseLocked) {
		if (Mouse.current == null) return;
		
		var wasLocked = Cursor.lockState == CursorLockMode.Locked;
		if (mouseLocked == wasLocked) return;
		
		if (mouseLocked) {
			_mouseLockedPos = Mouse.current.position.value;
			_mouseLockedPos = Vector2.Max(Vector2.zero, Vector2.Min(_mouseLockedPos, new Vector2(Screen.width, Screen.height)));
		}
		
		Cursor.lockState = mouseLocked ? CursorLockMode.Locked : CursorLockMode.None;
		
		if (!mouseLocked) {
			Mouse.current.WarpCursorPosition(_mouseLockedPos);
		}
	}

	public bool IsMouseLocked() {
		return Cursor.lockState == CursorLockMode.Locked;
	}

	public bool IsCursorVisible() {
		return Cursor.visible;
	}

	public void SetCursorVisible(bool val) {
		Cursor.visible = val;
	}

	public bool IsKeyDown(Key key) {
		return Keyboard.current?[key].isPressed ?? false;
	}

	public string GetScheme() {
		// return _playerInput.currentControlScheme;
		return "MouseKeyboard";
	}

	public bool IsPointerOverUI() {
		var eventDataCurrentPos = new PointerEventData(EventSystem.current);

		var pos = GetMousePosition();
		eventDataCurrentPos.position = pos;

		var results = new List<RaycastResult>();
		EventSystem.current.RaycastAll(eventDataCurrentPos, results);

		return results.Count > 0;
	}
	
	#endregion
	
	#region COMPONENT SETUP

	private void OnEnable() {
		// Keyboard
		if (Keyboard.current != null) {
			var keyboardInput = new KeyboardInput();
			
			keyboardInput.KeyDown += key => keyPressEvent?.Invoke((int)key, true);
			keyboardInput.KeyUp += key => keyPressEvent?.Invoke((int)key, false);
			
			_disposables.Add(keyboardInput);
		}

		// Mouse
		if (Mouse.current != null) {
			var mouseInput = new MouseInput();
			
			mouseInput.OnButton += (button, down) => {
				switch (button) {
					case MouseButton.Left:
						leftMouseButtonPressEvent?.Invoke(down);
						break;
					case MouseButton.Middle:
						middleMouseButtonPressEvent?.Invoke(down);
						break;
					case MouseButton.Right:
						rightMouseButtonPressEvent?.Invoke(down);
						break;
					case MouseButton.Back:
						backMouseButtonPressEvent?.Invoke(down);
						break;
					case MouseButton.Forward:
						forwardMouseButtonPressEvent?.Invoke(down);
						break;
				}
			};
			
			mouseInput.OnMove += position => {
				mouseMoveEvent?.Invoke(position);
			};
			
			mouseInput.OnScroll += scroll => {
				mouseScrollEvent?.Invoke(scroll);
			};
			
			_disposables.Add(mouseInput);
		}
	}

	private void OnDisable() {
		foreach (var disposable in _disposables) {
			disposable.Dispose();
		}
	}

	#endregion
}
