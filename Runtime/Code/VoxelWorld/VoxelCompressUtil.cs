using System.IO;
using System.IO.Compression;

public static class VoxelCompressUtil {
	/// <summary>
	/// Compress the given stream to a byte array.
	/// </summary>
	public static byte[] CompressToByteArray(Stream stream) {
		using var compressedStream = new MemoryStream();
		using var compressor = new DeflateStream(compressedStream, CompressionMode.Compress);
		
		stream.Flush();
		stream.Seek(0, SeekOrigin.Begin);
		stream.CopyTo(compressor);
		
		compressor.Close();
		
		return compressedStream.ToArray();
	}

	/// <summary>
	/// Decompress the compressed byte array into a new MemoryStream. The returned
	/// stream must be disposed once done.
	/// </summary>
	public static MemoryStream DecompressToMemoryStream(byte[] data) {
		var decompressedStream = new MemoryStream();
		
		using var compressedStream = new MemoryStream(data);
		using var decompressor = new DeflateStream(compressedStream, CompressionMode.Decompress);
		
		decompressor.CopyTo(decompressedStream);
		decompressor.Close();
		decompressedStream.Seek(0, SeekOrigin.Begin);

		return decompressedStream;
	}
}
