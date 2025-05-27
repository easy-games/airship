using System;
using System.Buffers;
using System.IO;
using System.Runtime.InteropServices;
using static Code.Zstd.ZstdNative;

namespace Code.Zstd {
	public sealed class ZstdCompressStream : Stream {
		public override bool CanRead => _compressedStream.CanRead;
		public override bool CanSeek => _compressedStream.CanSeek;
		public override bool CanWrite => _compressedStream.CanWrite;
		public override long Length => _compressedStream.Length;
		public override long Position { get; set; }

		private readonly Stream _compressedStream;
		private readonly bool _leaveOpen;

		private IntPtr _cctx;
		private GCHandle _outHandle;
		private readonly byte[] _bufOut;
		private readonly ulong _bufOutSize;

		public ZstdCompressStream(Stream compressedStream, bool leaveOpen = false) {
			_compressedStream = compressedStream;
			_leaveOpen = leaveOpen;

			_cctx = ZSTD_createCCtx();

			_bufOutSize = ZSTD_CStreamOutSize();
			_bufOut = ArrayPool<byte>.Shared.Rent((int)_bufOutSize);
			
			_outHandle = GCHandle.Alloc(_bufOut, GCHandleType.Pinned);
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
					var remaining = ZSTD_compressStream(_cctx, ref output, ref input);
					if (ZSTD_isError(remaining)) {
						throw new ZstdStreamException(remaining);
					}
					_compressedStream.Write(_bufOut, 0, (int)output.pos);
				} while (input.pos != input.size);
			}
		}

		private bool _disposed = false;
		protected override void Dispose(bool disposing) {
			base.Dispose(disposing);
			if (disposing && !_disposed) {
				_disposed = true;
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
		}
	}
}
