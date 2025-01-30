using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Code {
	public class TestMemDump : MonoBehaviour {
		public float updateInterval = 1;

		private float _lastUpdate;

		private readonly Dictionary<LuauContext, List<LuauPlugin.LuauMemoryCategoryDumpItem>> _dumps;

		private void Start() {
			_lastUpdate = Time.unscaledTime;
			_dumps[LuauContext.Game] = new List<LuauPlugin.LuauMemoryCategoryDumpItem>();
			_dumps[LuauContext.Protected] = new List<LuauPlugin.LuauMemoryCategoryDumpItem>();
		}

		private void Update() {
			var now = Time.unscaledTime;
			if (now - _lastUpdate > updateInterval) {
				_lastUpdate = now;
				Dump();
			}
		}

		private void Dump() {
			var dump = _dumps[LuauContext.Game];
			LuauPlugin.LuauGetMemoryCategoryDump(LuauContext.Game, dump);

			var sb = new StringBuilder();
			sb.AppendLine("DUMP:");
			foreach (var dumpItem in dump) {
				sb.AppendLine($" - {dumpItem.Name}: {dumpItem.Bytes} bytes");
			}
			Debug.Log(sb.ToString());
		}
	}
}