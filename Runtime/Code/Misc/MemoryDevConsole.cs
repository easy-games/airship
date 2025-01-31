using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MemoryDevConsole : MonoBehaviour {
	[Min(0.25f)]
	[Tooltip("Seconds between data refreshes")]
	public float refreshRate = 1;

	public GameObject logInstance;
	public ScrollRect scrollRect;
	public RectTransform contentFrame;

	[SerializeField] private GameObject sortedButton;
	
	[SerializeField] private Color sortedButtonBackgroundColorActive;
	[SerializeField] private Color sortedButtonBackgroundColorInactive;
	[SerializeField] private Color sortedButtonTextColorActive;
	[SerializeField] private Color sortedButtonTextColorInactive;

	private LuauContext _context = LuauContext.Game;
	private MemoryEnvironment _environment = MemoryEnvironment.Client;
	private bool _sorted;
	
	private float _lastRefreshTime;
	private Dictionary<LuauContext, List<LuauPlugin.LuauMemoryCategoryDumpItem>> _dumps;

	private readonly List<TMP_InputField> _logItems = new();

	public enum MemoryEnvironment {
		Client,
		Server,
	}
		
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
		_lastRefreshTime = 0;
		_dumps = new Dictionary<LuauContext, List<LuauPlugin.LuauMemoryCategoryDumpItem>> {
			[LuauContext.Game] = new(),
			[LuauContext.Protected] = new(),
		};
		UpdateSortedButtonAppearance();
	}

	private void OnEnable() {
		LuauCore.onResetInstance += OnLuauContextReset;
	}

	private void OnDisable() {
		LuauCore.onResetInstance -= OnLuauContextReset;
	}

	private void OnLuauContextReset(LuauContext ctx) {
		_dumps[ctx].Clear();
	}

	public void OnContextDropdownSelected(int n) {
		var newContext = n switch {
			0 => LuauContext.Game,
			1 => LuauContext.Protected,
			_ => LuauContext.Game,
		};

		if (newContext != _context) {
			_context = newContext;
			_lastRefreshTime = 0;
		}
	}

	public void OnEnvironmentDropdownSelected(int n) {
		var newEnvironment = n switch {
			0 => MemoryEnvironment.Client,
			1 => MemoryEnvironment.Server,
			_ => MemoryEnvironment.Client,
		};

		if (newEnvironment != _environment) {
			_environment = newEnvironment;
			_lastRefreshTime = 0;
		}
	}

	private void UpdateSortedButtonAppearance() {
		var image = sortedButton.GetComponent<Image>();
		image.color = _sorted ? sortedButtonBackgroundColorActive : sortedButtonBackgroundColorInactive;

		var text = sortedButton.GetComponentInChildren<TMP_Text>();
		text.color = _sorted ? sortedButtonTextColorActive : sortedButtonTextColorInactive;
	}

	public void OnSortedButtonClicked() {
		_sorted = !_sorted;
		_lastRefreshTime = 0;
		UpdateSortedButtonAppearance();
	}

	private void Update() {
		var now = Time.unscaledTime;
		if (now - _lastRefreshTime < refreshRate) {
			return;
		}

		_lastRefreshTime = now;

		Refresh();
	}

	private void Refresh() {
		var dump = _dumps[_context];
		LuauPlugin.LuauGetMemoryCategoryDump(_context, dump);

		if (_sorted) {
			var sortedDump = new List<LuauPlugin.LuauMemoryCategoryDumpItem>(_dumps[_context]);
			sortedDump.Sort(((itemA, itemB) => itemA.Bytes == itemB.Bytes ? 0 : itemA.Bytes < itemB.Bytes ? 1 : -1));
			dump = sortedDump;
		}

		var i = 0;
		foreach (var item in dump) {
			TMP_InputField instance;
			if (i < _logItems.Count) {
				instance = _logItems[i];
			} else {
				var go = Instantiate(logInstance, contentFrame);
				go.GetComponent<LogFieldInstance>().redirectTarget = scrollRect;
				instance = go.GetComponent<TMP_InputField>();
				_logItems.Add(instance);
			}
			instance.text = $"{item.ShortName}: <b><color=\"green\">{FormatBytes(item.Bytes)}</color></b>";
			i++;
		}

		while (_logItems.Count > i) {
			Destroy(_logItems[^1].gameObject);
			_logItems.RemoveAt(_logItems.Count - 1);
		}
	}
}
