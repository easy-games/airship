using System;
using System.Buffers;
using System.Runtime.InteropServices;

using static Code.Zstd.ZstdNative;

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
		public static readonly int MinCompressionLevel = ZSTD_minCLevel();
	
		/// Maximum compression level.
		public static readonly int MaxCompressionLevel = ZSTD_maxCLevel();
	
		/// Default compression level.
		public static readonly int DefaultCompressionLevel = ZSTD_defaultCLevel();

		private readonly ZstdContext _ctx;
		
		public Zstd(ulong scratchBufferSize) {
			_ctx = new ZstdContext(scratchBufferSize);
		}
		
		/// <summary>
		/// Compress the data. The compression level can be between <c>Zstd.MinCompressionLevel</c>
		/// and <c>Zstd.MaxCompressionLevel</c>. Most use-cases should use <c>Zstd.DefaultCompressionLevel</c>.
		/// </summary>
		public byte[] Compress(byte[] data, int compressionLevel) {
			return CompressData(data, compressionLevel, _ctx);
		}
		
		/// <summary>
		/// Compress the data using the default compression level.
		/// </summary>
		public byte[] Compress(byte[] data) {
			return CompressData(data, DefaultCompressionLevel, _ctx);
		}
		
		/// <summary>
		/// Decompress the data.
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
			var bound = ZSTD_compressBound((ulong)data.Length);
			if (ZSTD_isError(bound)) {
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
			var rSize = ZSTD_getFrameContentSize(dataHandle.AddrOfPinnedObject(), (ulong)data.Length);
			if (ZSTD_isError(rSize)) {
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
				decompressedSize = ZSTD_decompressDCtx(ctx.Dctx, new IntPtr(decompressedData), rSize, dataHandle.AddrOfPinnedObject(), (ulong)data.Length);
			} else {
				decompressedSize = ZSTD_decompress(new IntPtr(decompressedData), rSize, dataHandle.AddrOfPinnedObject(), (ulong)data.Length);
			}
			if (ZSTD_isError(decompressedSize)) {
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
				decompressedSize = ZSTD_decompressDCtx(ctx.Dctx, dstHandle.AddrOfPinnedObject(), rSize, dataHandle.AddrOfPinnedObject(), (ulong)data.Length);
			} else {
				decompressedSize = ZSTD_decompress(dstHandle.AddrOfPinnedObject(), rSize, dataHandle.AddrOfPinnedObject(), (ulong)data.Length);
			}
			dstHandle.Free();
			if (ZSTD_isError(decompressedSize)) {
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
				compressedSize = ZSTD_compressCCtx(ctx.Cctx, new IntPtr(dst), bound, srcHandle.AddrOfPinnedObject(), (ulong)data.Length, compressionLevel);
			} else {
				compressedSize = ZSTD_compress(new IntPtr(dst), bound, srcHandle.AddrOfPinnedObject(), (ulong)data.Length, compressionLevel);
			}
			srcHandle.Free();
			if (ZSTD_isError(compressedSize)) {
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
				compressedSize = ZSTD_compressCCtx(ctx.Cctx, dstHandle.AddrOfPinnedObject(), bound, srcHandle.AddrOfPinnedObject(), (ulong)data.Length, compressionLevel);
			} else {
				compressedSize = ZSTD_compress(dstHandle.AddrOfPinnedObject(), bound, srcHandle.AddrOfPinnedObject(), (ulong)data.Length, compressionLevel);
			}
			dstHandle.Free();
			srcHandle.Free();
			if (ZSTD_isError(compressedSize)) {
				throw new ZstdException(compressedSize);
			}
			Array.Resize(ref dst, (int)compressedSize);
			return dst;
		}
	}

	public class ZstdContext : IDisposable {
		internal readonly byte[] ScratchBuffer;
		
		internal readonly IntPtr Cctx;
		internal readonly IntPtr Dctx;
		
		public ZstdContext(ulong scratchBufferSize) {
			ScratchBuffer = ArrayPool<byte>.Shared.Rent((int)scratchBufferSize);
			Cctx = ZSTD_createCCtx();
			Dctx = ZSTD_createDCtx();
		}

		public void Dispose() {
			ZSTD_freeCCtx(Cctx);
			ZSTD_freeDCtx(Dctx);
			ArrayPool<byte>.Shared.Return(ScratchBuffer);
		}
	}

	public class ZstdException : Exception {
		public ZstdException(ulong code) : base(ZSTD_getErrorName(code)) { }
	}

	public class ZstdStreamException : Exception {
		public ZstdStreamException(string message) : base(message) { }
		public ZstdStreamException(ulong code) : this(ZSTD_getErrorName(code)) { }
	}
}
