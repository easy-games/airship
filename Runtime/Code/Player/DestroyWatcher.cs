using UnityEngine;

[LuauAPI]
public class DestroyWatcher : MonoBehaviour {
	public delegate void DestroyedDelegate();

	public event DestroyedDelegate destroyedEvent;

	public bool IsDestroyed { get; private set; }

	private void OnDestroy() {
		IsDestroyed = true;
		destroyedEvent?.Invoke();
		destroyedEvent = null;
	}
}
