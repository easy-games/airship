using System.IO;
using System.IO.Compression;

public static class VoxelCompressUtil {
	public static byte[] CompressToByteArray(Stream stream) {
		using var compressedStream = new MemoryStream();
		using var compressor = new DeflateStream(compressedStream, CompressionMode.Compress);
		
		stream.Flush();
		stream.Seek(0, SeekOrigin.Begin);
		stream.CopyTo(compressor);
		
		compressor.Close();
		
		return compressedStream.ToArray();
	}

	public static MemoryStream DecompressToMemoryStream(byte[] data) {
		using var compressedStream = new MemoryStream(data);
		var decompressedStream = new MemoryStream();
		using var decompressor = new DeflateStream(compressedStream, CompressionMode.Decompress);
		decompressor.CopyTo(decompressedStream);
		decompressor.Close();

		decompressedStream.Seek(0, SeekOrigin.Begin);

		return decompressedStream;
	}
}
