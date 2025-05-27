using System;
using System.IO;
using System.Runtime.InteropServices;
using static Code.Zstd.ZstdNative;

namespace Code.Zstd {
	public class ZstdCompressStream : Stream {
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

			_bufOutSize = ZSTD_CStreamOutSize();
			
			_bufOut = new byte[_bufOutSize];
			
			_outHandle = GCHandle.Alloc(_bufOut, GCHandleType.Pinned);

			_cctx = ZSTD_createCCtx();
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
				_compressedStream.Write(_bufOut, 0, (int)output.pos);
				finished = remaining == 0;
			} while (!finished);
		}

		public override int Read(byte[] buffer, int offset, int count) {
			throw new System.NotImplementedException();
		}

		public override long Seek(long offset, SeekOrigin origin) {
			throw new System.NotImplementedException();
		}

		public override void SetLength(long value) {
			throw new System.NotImplementedException();
		}

		public override void Write(byte[] buffer, int offset, int count) {
			var srcHandle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
			
			var input = new ZSTD_inBuffer {
				src = IntPtr.Add(srcHandle.AddrOfPinnedObject(), offset * sizeof(byte)),
				size = (ulong)count,
				pos = 0,
			};
			
			var finished = false;
			do {
				var output = new ZSTD_outBuffer {
					dst = _outHandle.AddrOfPinnedObject(),
					size = _bufOutSize,
					pos = 0,
				};
				var remaining = ZSTD_compressStream(_cctx, ref output, ref input);
				_compressedStream.Write(_bufOut, 0, (int)output.pos);
				finished = input.pos == input.size;
			} while (!finished);
			
			srcHandle.Free();
		}

		public override void Close() {
			var finished = false;
			do {
				var output = new ZSTD_outBuffer {
					dst = _outHandle.AddrOfPinnedObject(),
					size = _bufOutSize,
					pos = 0,
				};
				var remaining = ZSTD_endStream(_cctx, ref output);
				_compressedStream.Write(_bufOut, 0, (int)output.pos);
				finished = remaining == 0;
			} while (!finished);
			
			if (!_leaveOpen) {
				_compressedStream.Close();
			}
		}

		protected override void Dispose(bool disposing) {
			base.Dispose(disposing);
			if (disposing) {
				_outHandle.Free();
			}

			ZSTD_freeCCtx(_cctx);
			_cctx = IntPtr.Zero;
		}
	}
}
