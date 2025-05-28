using System;
using System.IO;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Code.Zstd {
	public class ZstdTest : MonoBehaviour {
		public int dataSize;
		public int dataSeed;

		private void Start() {
			RunTest();
		}
		
		private void RunTest() {
			var data = new byte[dataSize];
			
			Random.InitState(dataSeed);
			
			for (var i = 0; i < data.Length; i++) {
				data[i] = (byte)Random.Range(0, 2);
			}

			byte[] compressed;
			byte[] decompressed;
			{
				print("Compressing...");
				using var compressedStream = new MemoryStream();
				using var compressor = new ZstdCompressStream(compressedStream);
				compressor.Write(data, 0, data.Length);
				compressor.Close();

				compressed = compressedStream.ToArray();
				print($"Compressed size: {compressed.Length}");
			}

			{
				print("Decompressing...");
				var decompressedStream = new MemoryStream();
				var compressedStream = new MemoryStream(compressed);
				using var decompressor = new ZstdDecompressStream(compressedStream);
				decompressor.CopyTo(decompressedStream);
				decompressor.Close();
				decompressedStream.Seek(0, SeekOrigin.Begin);
				
				decompressed = decompressedStream.ToArray();
				print($"Decompressed size: {decompressed.Length} (expecting {data.Length})");
			}
			
			// Check equal:
			if (decompressed.Length != data.Length) {
				Debug.LogError("Decompressed length does not match original length");
				return;
			}

			for (var i = 0; i < data.Length; i++) {
				if (decompressed[i] != data[i]) {
					Debug.LogError("Decompressed != original");
					return;
				}
			}
			
			print("Decompressed matches original!");
		}
	}
}
