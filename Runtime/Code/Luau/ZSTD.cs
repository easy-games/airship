using System;
using System.Runtime.InteropServices;
using System.Threading;

public static class ZSTD {
	private const ulong MaxStackSize = 1024;
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern ulong ZSTDCompressBound(ulong uncompressedBufferSize);
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern ulong ZSTDCompress(IntPtr dst, ulong dstSize, IntPtr src, ulong srcSize, int compressionLevel);
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern ulong ZSTDGetFrameContentSize(IntPtr src, ulong srcSize);
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern ulong ZSTDDecompress(IntPtr dst, ulong dstSize, IntPtr src, ulong compressedSize);
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern int ZSTDMinCompressionLevel();
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern int ZSTDMaxCompressionLevel();
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern int ZSTDDefaultCompressionLevel();

	public static readonly int MinCompressionLevel = ZSTDMinCompressionLevel();
	public static readonly int MaxCompressionLevel = ZSTDMaxCompressionLevel();
	public static readonly int DefaultCompressionLevel = ZSTDDefaultCompressionLevel();
	
	public static byte[] Compress(byte[] data, int compressionLevel) {
		var bound = ZSTDCompressBound((ulong)data.Length);
		if (bound <= MaxStackSize) {
			return CompressWithStack(data, bound, compressionLevel);
		}
		return CompressWithHeap(data, bound, compressionLevel);
	}

	public static byte[] Decompress(byte[] data) {
		var dataHandle = GCHandle.Alloc(data, GCHandleType.Pinned);
		var rSize = ZSTDGetFrameContentSize(dataHandle.AddrOfPinnedObject(), (ulong)data.Length);
		byte[] decompressedData;
		if (rSize <= MaxStackSize) {
			decompressedData = DecompressWithStack(data, dataHandle, rSize);
		} else {
			decompressedData = DecompressWithHeap(data, dataHandle, rSize);
		}
		dataHandle.Free();
		return decompressedData;
	}

	private static unsafe byte[] DecompressWithStack(byte[] data, GCHandle dataHandle, ulong rSize) {
		var decompressedData = stackalloc byte[(int)rSize];
		var decompressedSize = ZSTDDecompress(new IntPtr(decompressedData), rSize, dataHandle.AddrOfPinnedObject(), (ulong)data.Length);
		var decompressedBuffer = new byte[decompressedSize];
		fixed (byte* decompressedBuf = decompressedBuffer) {
			Buffer.MemoryCopy(decompressedData, decompressedBuf, decompressedSize, decompressedSize);
		}
		return decompressedBuffer;
	}

	private static byte[] DecompressWithHeap(byte[] data, GCHandle dataHandle, ulong rSize) {
		var decompressedData = new byte[rSize];
		var dstHandle = GCHandle.Alloc(decompressedData, GCHandleType.Pinned);
		var decompressedSize = ZSTDDecompress(dstHandle.AddrOfPinnedObject(), rSize, dataHandle.AddrOfPinnedObject(), (ulong)data.Length);
		dstHandle.Free();
		var dstSegment = new ArraySegment<byte>(decompressedData);
		return dstSegment.Slice(0, (int)decompressedSize).Array;
	}

	private static unsafe byte[] CompressWithStack(byte[] data, ulong bound, int compressionLevel) {
		var dst = stackalloc byte[(int)bound];
		var srcHandle = GCHandle.Alloc(data, GCHandleType.Pinned);
		var compressedSize = ZSTDCompress(new IntPtr(dst), bound, srcHandle.AddrOfPinnedObject(), (ulong)data.Length, compressionLevel);
		srcHandle.Free();
		var compressedBuffer = new byte[compressedSize];
		fixed (byte* compressedBuf = compressedBuffer) {
			Buffer.MemoryCopy(dst, compressedBuf, compressedSize, compressedSize);
		}
		return compressedBuffer;
	}

	private static byte[] CompressWithHeap(byte[] data, ulong bound, int compressionLevel) {
		var dst = new byte[bound];
		var dstHandle = GCHandle.Alloc(dst, GCHandleType.Pinned);
		var srcHandle = GCHandle.Alloc(data, GCHandleType.Pinned);
		var compressedSize = ZSTDCompress(dstHandle.AddrOfPinnedObject(), bound, srcHandle.AddrOfPinnedObject(), (ulong)data.Length, compressionLevel);
		dstHandle.Free();
		srcHandle.Free();
		var dstSegment = new ArraySegment<byte>(dst);
		return dstSegment.Slice(0, (int)compressedSize).Array;
	}
}
