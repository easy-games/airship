using UnityEngine;

namespace Code.Zstd {
	public class ZSTDTest : MonoBehaviour {
		private void Start() {
			var data = new byte[1024 * 1024];
		
			Random.InitState(235439468);
			for (var i = 0; i < data.Length; i++) {
				data[i] = (byte)Random.Range(0, 256);
			}
		
			var compressed = Zstd.Compress(data, Zstd.DefaultCompressionLevel);
			var decompressed = Zstd.Decompress(compressed);
			print($"DATA SIZE: {data.Length}b | COMPRESSED: {compressed.Length}b | DECOMPRESSED: {decompressed.Length}b");
		}
	}
}
