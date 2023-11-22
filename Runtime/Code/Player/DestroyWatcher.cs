using System;
using UnityEngine;
using UnityEngine.Profiling;

[LuauAPI]
public class DestroyWatcher : MonoBehaviour {
	public delegate void DestroyedDelegate();

	public event DestroyedDelegate destroyedEvent;
	public event DestroyedDelegate disabledEvent;

	public bool IsDestroyed { get; private set; }

	private void OnDestroy() {
		IsDestroyed = true;
		destroyedEvent?.Invoke();
		destroyedEvent = null;
		disabledEvent = null;
	}

	private void OnDisable() {
		Profiler.BeginSample($"DestroyWatcher.OnDisable ({gameObject.name})");
		disabledEvent?.Invoke();
		Profiler.EndSample();
	}
}
