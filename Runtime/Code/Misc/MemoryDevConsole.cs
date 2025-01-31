using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class MemoryDevConsole : MonoBehaviour {
	[Min(0.25f)]
	[Tooltip("Seconds between data refreshes")]
	public float refreshRate = 1;

	public LuauContext context = LuauContext.Game;

	public GameObject logInstance;

	private float _lastRefreshTime;
	private Dictionary<LuauContext, List<LuauPlugin.LuauMemoryCategoryDumpItem>> _dumps;
		
	private static string FormatBytes(ulong bytes) {
		if (bytes < 1024) {
			return $"{bytes} B";
		}

		if (bytes < 1024L * 1024L) {
			return $"{(bytes / 1024f):F2} KB";
		}

		if (bytes < 1024 * 1024L * 1024L) {
			return $"{(bytes / (1024f * 1024f)):F2} MB";
		}

		return $"{(bytes / (1024f * 1024f * 1024f)):F2} GB";
	}

	private void Start() {
		_lastRefreshTime = Time.unscaledTime;
		_dumps = new Dictionary<LuauContext, List<LuauPlugin.LuauMemoryCategoryDumpItem>> {
			[LuauContext.Game] = new(),
			[LuauContext.Protected] = new(),
		};
	}

	private void Update() {
		var now = Time.unscaledTime;
		if (now - _lastRefreshTime < refreshRate) {
			return;
		}

		_lastRefreshTime = now;

		Refresh();
	}

	private List<TMP_InputField> _logItems = new();
	private void Refresh() {
		LuauPlugin.LuauGetMemoryCategoryDump(context, _dumps[context]);
		
		var sortedDump = new List<LuauPlugin.LuauMemoryCategoryDumpItem>(_dumps[context]);
		sortedDump.Sort(((itemA, itemB) => itemA.Bytes == itemB.Bytes ? 0 : itemA.Bytes < itemB.Bytes ? 1 : -1));

		var i = 0;
		foreach (var item in sortedDump) {
			TMP_InputField instance;
			if (i < _logItems.Count) {
				instance = _logItems[i];
			} else {
				instance = Instantiate(logInstance, transform).GetComponent<TMP_InputField>();
				_logItems.Add(instance);
			}
			instance.text = $"{item.ShortName}: {FormatBytes(item.Bytes)}";
			i++;
		}

		while (_logItems.Count > i) {
			Destroy(_logItems[^1].gameObject);
			_logItems.RemoveAt(_logItems.Count - 1);
		}
	}
}
