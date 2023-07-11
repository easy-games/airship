using System;
using System.Collections.Generic;

namespace Assets.Code.Core
{
	[LuauAPI]
	[Serializable]
	public class KeyValuePairs
	{
		public List<SimpleKeyValuePair> KVPs;

		public Dictionary<string, string> ToDictionary()
		{
			var dict = new Dictionary<string, string>();

			if (KVPs != null)
			{
				foreach (var kv in KVPs)
				{
					dict.Add(kv.Key, kv.Value);
				}
			}

			return dict;
		}
	}
}