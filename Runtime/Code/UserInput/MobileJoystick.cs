using System.Collections;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.EventSystems;

public enum MobileJoystickPhase {
	Began,
	Moved,
	Ended,
}

public class MobileJoystick : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler {
	private const float JoystickHandleCenterEpsilon = 1e-4f;
	
	[SerializeField] private RectTransform handle;
	[SerializeField] private float handleRange = 1f;
	[SerializeField] private float deadZone;
	[SerializeField] private float tweenSensitivity = 0.1f;

	public delegate void Changed(Vector2 position, MobileJoystickPhase phase);
	public event Changed OnChanged;
	
	private RectTransform _background;
	private CanvasGroup _canvasGroup;
	private Canvas _canvas;

	private Vector2 _input;
	private int _tweenId;

	private bool _visible;
	private bool _dragging;

	public bool JoystickVisible {
		get => _visible;
		set {
			_visible = value;
			_canvasGroup.alpha = value ? 1 : 0;
			_canvasGroup.interactable = value;
			_canvasGroup.blocksRaycasts = value;
			if (!value) {
				if (_dragging) {
					_dragging = false;
					_input = Vector2.zero;
					OnChanged?.Invoke(Vector2.zero, MobileJoystickPhase.Ended);
				}
			}
		}
	}
	
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static Vector2 ApplyDeadZoneToInput(Vector2 input, float deadZone) {
		var magnitude = input.magnitude;
		return magnitude > deadZone ? (magnitude > 1f ? input.normalized : input) : Vector2.zero;
	}

	private void Start() {
		_background = GetComponent<RectTransform>();
		_canvasGroup = GetComponent<CanvasGroup>();
		_canvas = GetComponentInParent<Canvas>();
		JoystickVisible = false;
	}

	private IEnumerator TweenBackToCenter(Vector2 startPosition) {
		var id = ++_tweenId;
		var position = startPosition;
		while (id == _tweenId) {
			var radius = _background.sizeDelta / 2;
			position = Vector2.Lerp(position, Vector2.zero, Time.deltaTime * tweenSensitivity);
			if (position.sqrMagnitude < JoystickHandleCenterEpsilon || !_visible) {
				handle.anchoredPosition = Vector2.zero;
				break;
			}
			handle.anchoredPosition = position * radius * handleRange;
			yield return null;
		}
	}

	private void HandleDrag(Vector2 dragPosition, MobileJoystickPhase phase) {
		var position = RectTransformUtility.WorldToScreenPoint(null, _background.position);
		var radius = _background.sizeDelta / 2;
		_input = (dragPosition - position) / (radius * _canvas.scaleFactor);
		_input = ApplyDeadZoneToInput(_input, deadZone);
		handle.anchoredPosition = _input * radius * handleRange;
		OnChanged?.Invoke(_input, phase);
	}

	public void OnBeginDrag(PointerEventData eventData) {
		_tweenId++;
		_dragging = true;
		HandleDrag(eventData.position, MobileJoystickPhase.Began);
	}

	public void OnEndDrag(PointerEventData eventData) {
		StartCoroutine(TweenBackToCenter(_input));
		_input = Vector2.zero;
		_dragging = false;
		OnChanged?.Invoke(Vector2.zero, MobileJoystickPhase.Ended);
	}

	public void OnDrag(PointerEventData eventData) {
		if (!_dragging) return;
		HandleDrag(eventData.position, MobileJoystickPhase.Moved);
	}
}
