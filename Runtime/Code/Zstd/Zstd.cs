using System;
using System.Buffers;
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
			return Compress(new ReadOnlySpan<byte>(data), compressionLevel);
		}
		
		/// <summary>
		/// Compress the data. The compression level can be between <c>Zstd.MinCompressionLevel</c>
		/// and <c>Zstd.MaxCompressionLevel</c>. Most use-cases should use <c>Zstd.DefaultCompressionLevel</c>.
		/// </summary>
		public byte[] Compress(byte[] data, int start, int length, int compressionLevel) {
			return Compress(new ReadOnlySpan<byte>(data, start, length), compressionLevel);
		}
		
		/// <summary>
		/// Compress the data using the default compression level.
		/// </summary>
		public byte[] Compress(byte[] data) {
			return Compress(new ReadOnlySpan<byte>(data));
		}
		
		/// <summary>
		/// Compress the data using the default compression level.
		/// </summary>
		public byte[] Compress(byte[] data, int start, int length) {
			return Compress(new ReadOnlySpan<byte>(data, start, length));
		}

		/// <summary>
		/// Compress the data using the default compression level.
		/// </summary>
		public byte[] Compress(ReadOnlySpan<byte> data) {
			return Compress(data, DefaultCompressionLevel);
		}

		/// <summary>
		/// Compress the data. The compression level can be between <c>Zstd.MinCompressionLevel</c>
		/// and <c>Zstd.MaxCompressionLevel</c>. Most use-cases should use <c>Zstd.DefaultCompressionLevel</c>.
		/// </summary>
		public byte[] Compress(ReadOnlySpan<byte> data, int compressionLevel) {
			return CompressData(data, compressionLevel, _ctx);
		}
		
		/// <summary>
		/// Decompress the data.
		/// </summary>
		public byte[] Decompress(byte[] data) {
			return Decompress(new ReadOnlySpan<byte>(data));
		}
		
		/// <summary>
		/// Decompress the data.
		/// </summary>
		public byte[] Decompress(byte[] data, int start, int length) {
			return Decompress(new ReadOnlySpan<byte>(data, start, length));
		}
		
		/// <summary>
		/// Decompress the data.
		/// </summary>
		public byte[] Decompress(ReadOnlySpan<byte> data) {
			return DecompressData(data, _ctx);
		}

		public void Dispose() {
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing) {
			_ctx.Dispose();
		}

		~Zstd() {
			Dispose(false);
		}

		/// <summary>
		/// Compress the bytes. The compression level can be between <c>Zstd.MinCompressionLevel</c>
		/// and <c>Zstd.MaxCompressionLevel</c>. Most use-cases should use <c>Zstd.DefaultCompressionLevel</c>.
		/// </summary>
		public static byte[] CompressData(ReadOnlySpan<byte> data, int compressionLevel, ZstdContext ctx = null) {
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
		public static unsafe byte[] DecompressData(ReadOnlySpan<byte> data, ZstdContext ctx = null) {
			ulong rSize;
			fixed (byte* src = data) {
				rSize = ZSTD_getFrameContentSize(new IntPtr(src), (ulong)data.Length);
			}
			if (ZSTD_isError(rSize)) {
				throw new ZstdException(rSize);
			}
			byte[] decompressedData;
			if (rSize <= MaxStackSize) {
				decompressedData = DecompressWithStack(data, rSize, ctx);
			} else {
				decompressedData = DecompressWithHeap(data, rSize, ctx);
			}
			return decompressedData;
		}

		private static unsafe byte[] DecompressWithStack(ReadOnlySpan<byte> data, ulong rSize, ZstdContext ctx) {
			var decompressedData = stackalloc byte[(int)rSize];
			ulong decompressedSize;
			fixed (byte* src = data) {
				if (ctx != null) {
					decompressedSize = ZSTD_decompressDCtx(ctx.Dctx, new IntPtr(decompressedData), rSize, new IntPtr(src), (ulong)data.Length);
				} else {
					decompressedSize = ZSTD_decompress(new IntPtr(decompressedData), rSize, new IntPtr(src), (ulong)data.Length);
				}
			}
			if (ZSTD_isError(decompressedSize)) {
				throw new ZstdException(decompressedSize);
			}
			var decompressedBuffer = new byte[decompressedSize];
			fixed (byte* decompressedBuf = decompressedBuffer) {
				Buffer.MemoryCopy(decompressedData, decompressedBuf, decompressedSize, decompressedSize);
			}
			return decompressedBuffer;
		}

		private static unsafe byte[] DecompressWithHeap(ReadOnlySpan<byte> data, ulong rSize, ZstdContext ctx) {
			var allocDst = ctx == null || rSize > (ulong)ctx.ScratchBuffer.Length;
			var decompressedData = allocDst ? new byte[rSize] : ctx.ScratchBuffer;
			ulong decompressedSize;
			fixed (byte* src = data) {
				fixed (byte* dst = decompressedData) {
					if (ctx != null) {
						decompressedSize = ZSTD_decompressDCtx(ctx.Dctx, new IntPtr(dst), rSize, new IntPtr(src), (ulong)data.Length);
					} else {
						decompressedSize = ZSTD_decompress(new IntPtr(dst), rSize, new IntPtr(src), (ulong)data.Length);
					}
				}
			}
			if (ZSTD_isError(decompressedSize)) {
				throw new ZstdException(decompressedSize);
			}
			Array.Resize(ref decompressedData, (int)decompressedSize);
			return decompressedData;
		}

		private static unsafe byte[] CompressWithStack(ReadOnlySpan<byte> data, ulong bound, int compressionLevel, ZstdContext ctx) {
			var dst = stackalloc byte[(int)bound];
			ulong compressedSize;
			fixed (byte* src = data) {
				if (ctx != null) {
					compressedSize = ZSTD_compressCCtx(ctx.Cctx, new IntPtr(dst), bound, new IntPtr(src), (ulong)data.Length, compressionLevel);
				} else {
					compressedSize = ZSTD_compress(new IntPtr(dst), bound, new IntPtr(src), (ulong)data.Length, compressionLevel);
				}
			}
			if (ZSTD_isError(compressedSize)) {
				throw new ZstdException(compressedSize);
			}
			var compressedBuffer = new byte[compressedSize];
			fixed (byte* compressedBuf = compressedBuffer) {
				Buffer.MemoryCopy(dst, compressedBuf, compressedSize, compressedSize);
			}
			return compressedBuffer;
		}

		private static unsafe byte[] CompressWithHeap(ReadOnlySpan<byte> data, ulong bound, int compressionLevel, ZstdContext ctx) {
			var allocDst = ctx == null || bound > (ulong)ctx.ScratchBuffer.Length;
			var dstBuf = allocDst ? new byte[bound] : ctx.ScratchBuffer;
			ulong compressedSize;
			fixed (byte* src = data) {
				fixed (byte* dst = dstBuf) {
					if (ctx != null) {
						compressedSize = ZSTD_compressCCtx(ctx.Cctx, new IntPtr(dst), bound, new IntPtr(src), (ulong)data.Length, compressionLevel);
					}
					else {
						compressedSize = ZSTD_compress(new IntPtr(dst), bound, new IntPtr(src), (ulong)data.Length, compressionLevel);
					}
				}
			}
			if (ZSTD_isError(compressedSize)) {
				throw new ZstdException(compressedSize);
			}
			Array.Resize(ref dstBuf, (int)compressedSize);
			return dstBuf;
		}
	}

	public sealed class ZstdContext : IDisposable {
		internal readonly byte[] ScratchBuffer;
		
		internal readonly IntPtr Cctx;
		internal readonly IntPtr Dctx;

		private bool _disposed;
		
		public ZstdContext(ulong scratchBufferSize) {
			ScratchBuffer = ArrayPool<byte>.Shared.Rent((int)scratchBufferSize);
			Cctx = ZSTD_createCCtx();
			Dctx = ZSTD_createDCtx();
		}

		~ZstdContext() {
			Dispose(false);
		}

		public void Dispose() {
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		private void Dispose(bool disposing) {
			if (_disposed) return;
			_disposed = true;
			
			if (disposing) {
				ArrayPool<byte>.Shared.Return(ScratchBuffer);
			}
			
			ZSTD_freeCCtx(Cctx);
			ZSTD_freeDCtx(Dctx);
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
