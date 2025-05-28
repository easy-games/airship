using System;
using System.Buffers;
using System.IO;
using System.Runtime.InteropServices;
using static Code.Zstd.ZstdNative;

namespace Code.Zstd {
	/// <summary>
	/// Provides methods for compressing streams using the zstd algorithm.
	/// </summary>
	public sealed class ZstdCompressStream : Stream {
		public override bool CanRead => false;
		public override bool CanSeek => false;
		public override bool CanWrite => true;
		public override long Length => _compressedStream.Length;
		public override long Position { get; set; }

		private readonly Stream _compressedStream;
		private readonly bool _leaveOpen;

		private IntPtr _cctx;
		private GCHandle _outHandle;
		private readonly byte[] _bufOut;
		private readonly ulong _bufOutSize;

		private bool _disposed;

		/// <summary>
		/// Constructs a new ZstdCompressionStream.
		/// </summary>
		/// <param name="compressedStream">The stream to which compressed data is written.</param>
		/// <param name="leaveOpen">Optionally leave the <c>compressedStream</c> open after closing (defaults to <c>false</c>).</param>
		public ZstdCompressStream(Stream compressedStream, bool leaveOpen = false) : this(compressedStream, Zstd.DefaultCompressionLevel, leaveOpen) { }

		/// <summary>
		/// Constructs a new ZstdCompressionStream.
		/// </summary>
		/// <param name="compressedStream">The stream to which compressed data is written.</param>
		/// <param name="compressionLevel">The zstd compression level. This should be in the range of <c>Zstd.MinCompressionLevel</c> and <c>Zstd.MaxCompressionLevel</c>. Most use-cases should use <c>Zstd.DefaultCompressionLevel</c>.</param>
		/// <param name="leaveOpen">Optionally leave the <c>compressedStream</c> open after closing (defaults to <c>false</c>).</param>
		public ZstdCompressStream(Stream compressedStream, int compressionLevel, bool leaveOpen = false) {
			_compressedStream = compressedStream;
			_leaveOpen = leaveOpen;

			_cctx = ZSTD_createCCtx();

			_bufOutSize = ZSTD_CStreamOutSize();
			_bufOut = ArrayPool<byte>.Shared.Rent((int)_bufOutSize);
			
			_outHandle = GCHandle.Alloc(_bufOut, GCHandleType.Pinned);
			
			ZSTD_CCtx_setParameter(_cctx, ZSTD_cParameter.ZSTD_c_compressionLevel, compressionLevel);
		}
		
		public override void Flush() {
			var finished = false;
			do {
				var output = new ZSTD_outBuffer {
					dst = _outHandle.AddrOfPinnedObject(),
					size = _bufOutSize,
					pos = 0,
				};
				var remaining = ZSTD_flushStream(_cctx, ref output);
				if (ZSTD_isError(remaining)) {
					throw new ZstdStreamException(remaining);
				}
				_compressedStream.Write(_bufOut, 0, (int)output.pos);
				finished = remaining == 0;
			} while (!finished);
		}

		public override int Read(byte[] buffer, int offset, int count) {
			throw new NotSupportedException();
		}

		public override long Seek(long offset, SeekOrigin origin) {
			throw new NotSupportedException();
		}

		public override void SetLength(long value) {
			throw new NotSupportedException();
		}

		public override void Write(byte[] buffer, int offset, int count) {
			WriteCore(new ReadOnlySpan<byte>(buffer, offset, count));
		}

		public override void Write(ReadOnlySpan<byte> buffer) {
			WriteCore(buffer);
		}

		private unsafe void WriteCore(ReadOnlySpan<byte> buffer) {
			fixed (byte* bufferPtr = &MemoryMarshal.GetReference(buffer)) {
				var input = new ZSTD_inBuffer {
					src = (IntPtr)bufferPtr,
					size = (ulong)buffer.Length,
					pos = 0,
				};

				do {
					var output = new ZSTD_outBuffer {
						dst = _outHandle.AddrOfPinnedObject(),
						size = _bufOutSize,
						pos = 0,
					};
					var ret = ZSTD_compressStream(_cctx, ref output, ref input);
					if (ZSTD_isError(ret)) {
						throw new ZstdStreamException(ret);
					}
					_compressedStream.Write(_bufOut, 0, (int)output.pos);
				} while (input.pos != input.size);
			}
		}

		protected override void Dispose(bool disposing) {
			base.Dispose(disposing);
			if (disposing && !_disposed) {
				var finished = false;
				do {
					var output = new ZSTD_outBuffer {
						dst = _outHandle.AddrOfPinnedObject(),
						size = _bufOutSize,
						pos = 0,
					};
					var remaining = ZSTD_endStream(_cctx, ref output);
					if (ZSTD_isError(remaining)) {
						throw new ZstdStreamException(remaining);
					}
					_compressedStream.Write(_bufOut, 0, (int)output.pos);
					finished = remaining == 0;
				} while (!finished);
				
				_outHandle.Free();
				ArrayPool<byte>.Shared.Return(_bufOut);
			
				if (!_leaveOpen) {
					_compressedStream.Close();
				}
			}

			ZSTD_freeCCtx(_cctx);
			_cctx = IntPtr.Zero;
			
			_disposed = true;
		}
	}
}
