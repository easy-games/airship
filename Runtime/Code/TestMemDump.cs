using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Code {
	public class TestMemDump : MonoBehaviour {
		public float updateInterval = 1;

		private float _lastUpdate;

		private Dictionary<LuauContext, List<LuauPlugin.LuauMemoryCategoryDumpItem>> _dumps;

		private void Start() {
			_lastUpdate = Time.unscaledTime;
			_dumps = new Dictionary<LuauContext, List<LuauPlugin.LuauMemoryCategoryDumpItem>> {
				[LuauContext.Game] = new(),
				[LuauContext.Protected] = new(),
			};
		}

		private void Update() {
			var now = Time.unscaledTime;
			if (now - _lastUpdate > updateInterval) {
				_lastUpdate = now;
				Dump();
			}
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

		private void Dump() {
			var dump = _dumps[LuauContext.Game];
			LuauPlugin.LuauGetMemoryCategoryDump(LuauContext.Game, dump);

			var sortedDump = new List<LuauPlugin.LuauMemoryCategoryDumpItem>(dump);
			sortedDump.Sort(((itemA, itemB) => itemA.Bytes == itemB.Bytes ? 0 : itemA.Bytes < itemB.Bytes ? 1 : -1));

			var sb = new StringBuilder();
			sb.AppendLine("DUMP:");
			foreach (var dumpItem in sortedDump) {
				sb.AppendLine($" - {dumpItem.ShortName}: {FormatBytes(dumpItem.Bytes)}");
			}
			Debug.Log(sb.ToString());
		}
	}
}
