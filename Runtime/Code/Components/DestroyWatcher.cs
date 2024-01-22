using System;
using UnityEngine;
using UnityEngine.Profiling;

[LuauAPI]
public class DestroyWatcher : MonoBehaviour {
	public delegate void DestroyedDelegate();

	public event DestroyedDelegate destroyedEvent;
	public event DestroyedDelegate disabledEvent;

	public bool IsDestroyed { get; private set; }

	private bool _isQuitting;

	private void OnDestroy() {
		if (_isQuitting) return;
		IsDestroyed = true;
		destroyedEvent?.Invoke();
		destroyedEvent = null;
		disabledEvent = null;
	}

	private void OnDisable() {
		if (_isQuitting) return;
		disabledEvent?.Invoke();
	}

	private void OnApplicationQuit() {
		_isQuitting = true;
	}
}
