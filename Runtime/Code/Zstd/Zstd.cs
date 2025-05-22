using System;
using System.Runtime.InteropServices;

namespace Code.Zstd {
	/// <summary>
	/// Zstandard (ZSTD) is a compression algorithm.
	/// </summary>
	/// <see href="https://facebook.github.io/zstd/"/>
	public class Zstd : IDisposable {
		// Compression & decompression will try to utilize the stack if certain buffers can fit
		// within the given byte size here:
		private const ulong MaxStackSize = 1024;

		/// Minimum compression level.
		public static readonly int MinCompressionLevel = ZSTDMinCompressionLevel();
	
		/// Maximum compression level.
		public static readonly int MaxCompressionLevel = ZSTDMaxCompressionLevel();
	
		/// Default compression level.
		public static readonly int DefaultCompressionLevel = ZSTDDefaultCompressionLevel();

		private readonly ZstdContext _ctx;
		
		public Zstd(ulong scratchBufferSize) {
			_ctx = new ZstdContext(scratchBufferSize);
		}

		public void PrewarmForCompression() {
			_ = _ctx.Cctx;
		}

		public void PrewarmForDecompression() {
			_ = _ctx.Dctx;
		}
		
		/// <summary>
		/// Compress the bytes. The compression level can be between <c>Zstd.MinCompressionLevel</c>
		/// and <c>Zstd.MaxCompressionLevel</c>. Most use-cases should use <c>Zstd.DefaultCompressionLevel</c>.
		/// </summary>
		public byte[] Compress(byte[] data, int compressionLevel) {
			return CompressData(data, compressionLevel, _ctx);
		}
		
		/// <summary>
		/// Decompress the bytes.
		/// </summary>
		public byte[] Decompress(byte[] data) {
			return DecompressData(data, _ctx);
		}

		public void Dispose() {
			_ctx.Dispose();
		}

		/// <summary>
		/// Compress the bytes. The compression level can be between <c>Zstd.MinCompressionLevel</c>
		/// and <c>Zstd.MaxCompressionLevel</c>. Most use-cases should use <c>Zstd.DefaultCompressionLevel</c>.
		/// </summary>
		public static byte[] CompressData(byte[] data, int compressionLevel, ZstdContext ctx = null) {
			var bound = ZSTDCompressBound((ulong)data.Length);
			if (IsError(bound)) {
				throw new ZstdException(bound);
			}
			if (bound <= MaxStackSize) {
				return CompressWithStack(data, bound, compressionLevel, ctx);
			}
			return CompressWithHeap(data, bound, compressionLevel, ctx);
		}

		/// <summary>
		/// Decompress the bytes.
		/// </summary>
		public static byte[] DecompressData(byte[] data, ZstdContext ctx = null) {
			var dataHandle = GCHandle.Alloc(data, GCHandleType.Pinned);
			var rSize = ZSTDGetFrameContentSize(dataHandle.AddrOfPinnedObject(), (ulong)data.Length);
			if (IsError(rSize)) {
				dataHandle.Free();
				throw new ZstdException(rSize);
			}
			byte[] decompressedData;
			if (rSize <= MaxStackSize) {
				decompressedData = DecompressWithStack(data, dataHandle, rSize, ctx);
			} else {
				decompressedData = DecompressWithHeap(data, dataHandle, rSize, ctx);
			}
			dataHandle.Free();
			return decompressedData;
		}

		private static unsafe byte[] DecompressWithStack(byte[] data, GCHandle dataHandle, ulong rSize, ZstdContext ctx) {
			var decompressedData = stackalloc byte[(int)rSize];
			ulong decompressedSize;
			if (ctx != null) {
				decompressedSize = ZSTDDecompressDCTX(ctx.Dctx, new IntPtr(decompressedData), rSize, dataHandle.AddrOfPinnedObject(), (ulong)data.Length);
			} else {
				decompressedSize = ZSTDDecompress(new IntPtr(decompressedData), rSize, dataHandle.AddrOfPinnedObject(), (ulong)data.Length);
			}
			if (IsError(decompressedSize)) {
				dataHandle.Free();
				throw new ZstdException(decompressedSize);
			}
			var decompressedBuffer = new byte[decompressedSize];
			fixed (byte* decompressedBuf = decompressedBuffer) {
				Buffer.MemoryCopy(decompressedData, decompressedBuf, decompressedSize, decompressedSize);
			}
			return decompressedBuffer;
		}

		private static byte[] DecompressWithHeap(byte[] data, GCHandle dataHandle, ulong rSize, ZstdContext ctx) {
			var allocDst = ctx == null || rSize > (ulong)ctx.ScratchBuffer.Length;
			var decompressedData = allocDst ? new byte[rSize] : ctx.ScratchBuffer;
			var dstHandle = GCHandle.Alloc(decompressedData, GCHandleType.Pinned);
			ulong decompressedSize;
			if (ctx != null) {
				decompressedSize = ZSTDDecompressDCTX(ctx.Dctx, dstHandle.AddrOfPinnedObject(), rSize, dataHandle.AddrOfPinnedObject(), (ulong)data.Length);
			} else {
				decompressedSize = ZSTDDecompress(dstHandle.AddrOfPinnedObject(), rSize, dataHandle.AddrOfPinnedObject(), (ulong)data.Length);
			}
			dstHandle.Free();
			if (IsError(decompressedSize)) {
				dataHandle.Free();
				throw new ZstdException(decompressedSize);
			}
			Array.Resize(ref decompressedData, (int)decompressedSize);
			return decompressedData;
		}

		private static unsafe byte[] CompressWithStack(byte[] data, ulong bound, int compressionLevel, ZstdContext ctx) {
			var dst = stackalloc byte[(int)bound];
			var srcHandle = GCHandle.Alloc(data, GCHandleType.Pinned);
			ulong compressedSize;
			if (ctx != null) {
				compressedSize = ZSTDCompressCCTX(ctx.Cctx, new IntPtr(dst), bound, srcHandle.AddrOfPinnedObject(), (ulong)data.Length, compressionLevel);
			} else {
				compressedSize = ZSTDCompress(new IntPtr(dst), bound, srcHandle.AddrOfPinnedObject(), (ulong)data.Length, compressionLevel);
			}
			srcHandle.Free();
			if (IsError(compressedSize)) {
				throw new ZstdException(compressedSize);
			}
			var compressedBuffer = new byte[compressedSize];
			fixed (byte* compressedBuf = compressedBuffer) {
				Buffer.MemoryCopy(dst, compressedBuf, compressedSize, compressedSize);
			}
			return compressedBuffer;
		}

		private static byte[] CompressWithHeap(byte[] data, ulong bound, int compressionLevel, ZstdContext ctx) {
			var allocDst = ctx == null || bound > (ulong)ctx.ScratchBuffer.Length;
			var dst = allocDst ? new byte[bound] : ctx.ScratchBuffer;
			var dstHandle = GCHandle.Alloc(dst, GCHandleType.Pinned);
			var srcHandle = GCHandle.Alloc(data, GCHandleType.Pinned);
			ulong compressedSize;
			if (ctx != null) {
				compressedSize = ZSTDCompressCCTX(ctx.Cctx, dstHandle.AddrOfPinnedObject(), bound, srcHandle.AddrOfPinnedObject(), (ulong)data.Length, compressionLevel);
			} else {
				compressedSize = ZSTDCompress(dstHandle.AddrOfPinnedObject(), bound, srcHandle.AddrOfPinnedObject(), (ulong)data.Length, compressionLevel);
			}
			dstHandle.Free();
			srcHandle.Free();
			if (IsError(compressedSize)) {
				throw new ZstdException(compressedSize);
			}
			Array.Resize(ref dst, (int)compressedSize);
			return dst;
		}

		private static bool IsError(ulong code) {
			return ZSTDIsError(code) != 0;
		}

		internal static string GetErrorName(ulong code) {
			var errNamePtr = ZSTDGetErrorName(code);
			return Marshal.PtrToStringUTF8(errNamePtr);
		}
	
		#region Extern
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
		private static extern ulong ZSTDCompressCCTX(IntPtr cctx, IntPtr dst, ulong dstSize, IntPtr src, ulong srcSize, int compressionLevel);
	
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
		private static extern ulong ZSTDDecompress(IntPtr dst, ulong dstSize, IntPtr src, ulong srcSize);
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
		[DllImport("LuauPlugin")]
#endif
		private static extern ulong ZSTDDecompressDCTX(IntPtr dctx, IntPtr dst, ulong dstSize, IntPtr src, ulong srcSize);
	
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
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
		[DllImport("LuauPlugin")]
#endif
		private static extern uint ZSTDIsError(ulong code);
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
		[DllImport("LuauPlugin")]
#endif
		private static extern IntPtr ZSTDGetErrorName(ulong code);
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
		[DllImport("LuauPlugin")]
#endif
		internal static extern IntPtr ZSTDCreateCCTX();
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
		[DllImport("LuauPlugin")]
#endif
		internal static extern IntPtr ZSTDCreateDCTX();
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
		[DllImport("LuauPlugin")]
#endif
		internal static extern void ZSTDFreeCCTX(IntPtr cctx);
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
		[DllImport("LuauPlugin")]
#endif
		internal static extern void ZSTDFreeDCTX(IntPtr dctx);
		
		#endregion
	}

	public class ZstdContext : IDisposable {
		internal byte[] ScratchBuffer;
		
		private IntPtr _cctx = IntPtr.Zero;
		private IntPtr _dctx = IntPtr.Zero;

		internal IntPtr Cctx {
			get {
				if (_cctx == IntPtr.Zero) {
					_cctx = Zstd.ZSTDCreateCCTX();
				}
				return _cctx;
			}
		}

		internal IntPtr Dctx {
			get {
				if (_dctx == IntPtr.Zero) {
					_dctx = Zstd.ZSTDCreateDCTX();
				}
				return _dctx;
			}
		}
		
		public ZstdContext(ulong scratchBufferSize) {
			ScratchBuffer = new byte[scratchBufferSize];
		}

		public void Dispose() {
			if (Cctx != IntPtr.Zero) {
				Zstd.ZSTDFreeCCTX(Cctx);
			}
			if (Dctx != IntPtr.Zero) {
				Zstd.ZSTDFreeDCTX(Dctx);
			}
		}
	}

	public class ZstdException : Exception {
		public ZstdException(string message) : base(message) { }

		public ZstdException(ulong code) : base(Zstd.GetErrorName(code)) { }
	}
}
