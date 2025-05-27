using System;
using System.IO;
using System.Runtime.InteropServices;
using static Code.Zstd.ZstdNative;

namespace Code.Zstd {
	public class ZstdDecompressStream : Stream {
		public override bool CanRead => _compressedStream.CanRead;
		public override bool CanSeek => _compressedStream.CanSeek;
		public override bool CanWrite => _compressedStream.CanWrite;
		public override long Length => _compressedStream.Length;
		public override long Position { get; set; }

		private readonly Stream _compressedStream;
		private readonly bool _leaveOpen;

		private IntPtr _dctx;
		private GCHandle _inHandle;
		private readonly byte[] _bufIn;
		private readonly ulong _bufInSize;

		public ZstdDecompressStream(Stream compressedStream, bool leaveOpen = false) {
			_compressedStream = compressedStream;
			_leaveOpen = leaveOpen;

			_bufInSize = ZSTD_DStreamInSize();
			
			_bufIn = new byte[_bufInSize];
			
			_inHandle = GCHandle.Alloc(_bufIn, GCHandleType.Pinned);

			_dctx = ZSTD_createDCtx();
		}
		
		public override void Flush() {
			throw new System.NotImplementedException();
		}

		public override int Read(byte[] buffer, int offset, int count) {
			var dstHandle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
			
			var readTotal = 0;
			var read = 0;
			var lastRet = 0ul;
			var isEmpty = true;
			while ((read = _compressedStream.Read(_bufIn, 0, Math.Min(count - readTotal, (int)_bufInSize))) != 0) {
				readTotal += read;
				isEmpty = false;

				var input = new ZSTD_inBuffer {
					src = _inHandle.AddrOfPinnedObject(),
					size = (ulong)read,
					pos = 0,
				};

				while (input.pos < input.size) {
					// TODO: I think this is all wrong. I think it needs to use a larger buffer, b/c the decompressed data is going to be larger.
					var output = new ZSTD_outBuffer {
						dst = IntPtr.Add(dstHandle.AddrOfPinnedObject(), sizeof(byte) * offset),
						size = (ulong)read,
						pos = 0,
					};
					var ret = ZSTD_decompressStream(_dctx, ref output, ref input);
					lastRet = ret;
				}
				
				if (readTotal >= count) {
					break;
				}
			}

			dstHandle.Free();

			if (isEmpty) {
				// TODO: Throw "input is empty" error
			}

			if (lastRet != 0) {
				// TODO: Throw EOF error
			}

			return readTotal;
		}

		public override long Seek(long offset, SeekOrigin origin) {
			throw new System.NotImplementedException();
		}

		public override void SetLength(long value) {
			throw new System.NotImplementedException();
		}

		public override void Write(byte[] buffer, int offset, int count) {
			throw new System.NotImplementedException();
		}

		public override void Close() {
			throw new System.NotImplementedException();
			
			if (!_leaveOpen) {
				_compressedStream.Close();
			}
		}

		protected override void Dispose(bool disposing) {
			base.Dispose(disposing);
			if (disposing) {
				_inHandle.Free();
			}

			ZSTD_freeDCtx(_dctx);
			_dctx = IntPtr.Zero;
		}
	}
}
