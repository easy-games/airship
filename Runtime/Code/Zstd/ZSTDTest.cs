using System;
using System.Collections;
using System.Diagnostics;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Code.Zstd {
	public class ZSTDTest : MonoBehaviour {
		public ulong size;
		public int seed;
		
		private bool _destroyed = false;
		
		private void Start() {
			StartCoroutine(TestCompression());
		}

		private void OnDestroy() {
			_destroyed = true;
		}

		private IEnumerator TestCompression() {
			while (!_destroyed) {
				var data = new byte[size];
			
				Random.InitState(seed);
				for (var i = 0; i < data.Length; i++) {
					data[i] = (byte)Random.Range(0, 2);
				}

				using var zstd = new Zstd(size);
				zstd.PrewarmForCompression();
				zstd.PrewarmForDecompression();
				
				var compressWatch = Stopwatch.StartNew();
				var compressed = zstd.Compress(data, Zstd.DefaultCompressionLevel);
				compressWatch.Stop();
				
				var decompressWatch = Stopwatch.StartNew();
				var decompressed = zstd.Decompress(compressed);
				decompressWatch.Stop();
				
				print($"DATA SIZE: {data.Length}b | COMPRESSED: {compressed.Length}b | DECOMPRESSED: {decompressed.Length}b");
				print($"COMPRESS TIME: {compressWatch.ElapsedTicks / (TimeSpan.TicksPerMillisecond / 1000)}us | DECOMPRESSED TIME: {decompressWatch.ElapsedTicks / (TimeSpan.TicksPerMillisecond / 1000)}us");
				
				yield return new WaitForSeconds(1);
			}
		}
	}
}
