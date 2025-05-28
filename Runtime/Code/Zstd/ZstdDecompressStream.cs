using System;
using System.Buffers;
using System.IO;
using System.Runtime.InteropServices;
using static Code.Zstd.ZstdNative;

namespace Code.Zstd {
	public sealed class ZstdDecompressStream : Stream {
		public override bool CanRead => true;
		public override bool CanSeek => false;
		public override bool CanWrite => false;
		public override long Length => _compressedStream.Length;
		public override long Position { get; set; }

		private readonly Stream _compressedStream;
		private readonly bool _leaveOpen;

		private IntPtr _dctx;
		private GCHandle _outHandle;
		private GCHandle _inHandle;
		private readonly byte[] _bufOut;
		private readonly byte[] _bufIn;
		private readonly ulong _bufOutSize;
		private readonly ulong _bufInSize;

		private readonly MemoryStream _bufOutStream;

		private int _currentBufOutOffset;
		private int _currentBufIn;
		
		private bool _disposed;

		public ZstdDecompressStream(Stream compressedStream, bool leaveOpen = false) {
			_compressedStream = compressedStream;
			_leaveOpen = leaveOpen;

			_dctx = ZSTD_createDCtx();
			
			_bufOutSize = ZSTD_DStreamOutSize();
			_bufInSize = ZSTD_DStreamInSize();
			
			_bufOut = ArrayPool<byte>.Shared.Rent((int)_bufOutSize);
			_bufIn = ArrayPool<byte>.Shared.Rent((int)_bufInSize);
			
			_outHandle = GCHandle.Alloc(_bufOut, GCHandleType.Pinned);
			_inHandle = GCHandle.Alloc(_bufIn, GCHandleType.Pinned);

			_bufOutStream = new MemoryStream((int)ZSTD_DStreamOutSize());
		}
		
		public override void Flush() {
			throw new NotSupportedException();
		}

		public override int Read(byte[] buffer, int offset, int count) {
			return ReadCore(new Span<byte>(buffer, offset, count));
		}

		public override int Read(Span<byte> buffer) {
			return ReadCore(buffer);
		}

		private int ReadCore(Span<byte> buffer) {
			var read = 0;
			var lastRet = 0ul;
			var streamEmpty = false;
			var bufferOffset = 0;

			if (buffer.IsEmpty) {
				throw new ZstdStreamException("Empty buffer");
			}

			while (true) {
				if (_bufOutStream.Length > 0) {
					var len = Math.Min(buffer.Length, (int)(_bufOutStream.Length - _currentBufOutOffset));
					_bufOutStream.Seek(_currentBufOutOffset, SeekOrigin.Begin);
					var bufOutRead = _bufOutStream.Read(buffer.Slice(bufferOffset, len));
					if (bufOutRead != len) {
						throw new ZstdStreamException("Failed to read full amount into buffer");
					}
					_currentBufOutOffset += len;
					bufferOffset += len;
					read += len;
					
					if (_currentBufOutOffset == _bufOutStream.Length) {
						_currentBufOutOffset = 0;
						_bufOutStream.Seek(0, SeekOrigin.Begin);
						_bufOutStream.SetLength(0);
					}

					if (len == buffer.Length) {
						break;
					}
				}

				if (_bufOutStream.Length == 0) {
					if (_currentBufIn == 0) {
						var bytesFromStream = ReadInCompressedChunkFromStream();
						streamEmpty = bytesFromStream == 0;
					}

					if (!streamEmpty) {
						lastRet = Decompress();
					}
				}

				if (streamEmpty && _bufOutStream.Length == 0) {
					break;
				}
			}

			if (streamEmpty && _bufOutStream.Length == 0 && lastRet != 0) {
				throw new ZstdStreamException($"EOF before end of stream: {lastRet}");
			}

			return read;
		}

		private ulong Decompress() {
			var lastRet = 0ul;
			
			var input = new ZSTD_inBuffer {
				src = _inHandle.AddrOfPinnedObject(),
				size = (ulong)_currentBufIn,
				pos = 0,
			};
			
			while (input.pos < input.size) {
				var output = new ZSTD_outBuffer {
					dst = _outHandle.AddrOfPinnedObject(),
					size = _bufOutSize,
					pos = 0,
				};
				
				lastRet = ZSTD_decompressStream(_dctx, ref output, ref input);
				if (ZSTD_isError(lastRet)) {
					throw new ZstdStreamException(lastRet);
				}

				_bufOutStream.Write(_bufOut, 0, (int)output.pos);
			}
			
			_currentBufIn = 0;

			return lastRet;
		}

		private int ReadInCompressedChunkFromStream() {
			var streamRead = 0;
			if (_currentBufIn < (int)_bufInSize) {
				streamRead = _compressedStream.Read(_bufIn, _currentBufIn, (int)_bufInSize - _currentBufIn);
				_currentBufIn += streamRead;
			}
			return streamRead;
		}

		public override long Seek(long offset, SeekOrigin origin) {
			throw new NotSupportedException();
		}

		public override void SetLength(long value) {
			throw new NotSupportedException();
		}

		public override void Write(byte[] buffer, int offset, int count) {
			throw new NotSupportedException();
		}

		protected override void Dispose(bool disposing) {
			base.Dispose(disposing);
			if (disposing && !_disposed) {
				_outHandle.Free();
				_inHandle.Free();
				ArrayPool<byte>.Shared.Return(_bufOut);
				ArrayPool<byte>.Shared.Return(_bufIn);
				if (!_leaveOpen) {
					_compressedStream.Close();
				}
				_bufOutStream.Dispose();
			}

			ZSTD_freeDCtx(_dctx);
			_dctx = IntPtr.Zero;
			
			_disposed = true;
		}
	}
}
