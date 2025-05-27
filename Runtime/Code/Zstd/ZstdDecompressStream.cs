using System;
using System.Buffers;
using System.IO;
using System.Runtime.InteropServices;
using static Code.Zstd.ZstdNative;

namespace Code.Zstd {
	public sealed class ZstdDecompressStream : Stream {
		public override bool CanRead => _compressedStream.CanRead;
		public override bool CanSeek => _compressedStream.CanSeek;
		public override bool CanWrite => _compressedStream.CanWrite;
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

		private int _currentBufOut;
		private int _currentBufOutOffset = 0;
		private int _currentBufIn = 0;

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
		}
		
		public override void Flush() {
			throw new System.NotImplementedException();
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

			if (buffer.IsEmpty) {
				throw new ZstdStreamException("Empty buffer");
			}

			while (true) {
				if (_currentBufOut > 0) {
					var len = Math.Min(buffer.Length, _currentBufOut - _currentBufOutOffset);
					new Span<byte>(_bufOut, _currentBufOutOffset, len).CopyTo(buffer[..len]);
					_currentBufOutOffset += len;
					read += len;
					
					if (_currentBufOutOffset == _currentBufOut) {
						_currentBufOut = 0;
						_currentBufOutOffset = 0;
					}

					if (len == buffer.Length) {
						break;
					}
				}

				if (_currentBufOut == 0) {
					if (_currentBufIn == 0) {
						var bytesFromStream = ReadInCompressedChunkFromStream();
						streamEmpty = bytesFromStream == 0;
					}

					if (!streamEmpty) {
						lastRet = Decompress();
					}
				}

				if (streamEmpty && _currentBufOut == 0) {
					break;
				}
			}

			if (streamEmpty && _currentBufOut == 0 && lastRet != 0) {
				throw new ZstdStreamException($"EOF before end of stream: {lastRet}");
			}

			return read;
		}

		private ulong Decompress() {
			var lastRet = 0ul;
			var decompressedBytes = 0;
			
			var input = new ZSTD_inBuffer {
				src = _inHandle.AddrOfPinnedObject(),
				size = (ulong)_currentBufIn,
				pos = 0,
			};
				
			while (input.pos < input.size) {
				var output = new ZSTD_outBuffer {
					dst = IntPtr.Add(_outHandle.AddrOfPinnedObject(), _currentBufOut),
					size = _bufOutSize,
					pos = 0,
				};
					
				lastRet = ZSTD_decompressStream(_dctx, ref output, ref input);
				if (ZSTD_isError(lastRet)) {
					throw new ZstdStreamException(lastRet);
				}

				decompressedBytes = (int)output.pos;
			}
			
			_currentBufOut += decompressedBytes;
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

		private bool _disposed = false;
		protected override void Dispose(bool disposing) {
			base.Dispose(disposing);
			if (disposing && !_disposed) {
				_disposed = true;
				_outHandle.Free();
				_inHandle.Free();
				ArrayPool<byte>.Shared.Return(_bufOut);
				ArrayPool<byte>.Shared.Return(_bufIn);
				if (!_leaveOpen) {
					_compressedStream.Close();
				}
			}

			ZSTD_freeDCtx(_dctx);
			_dctx = IntPtr.Zero;
		}
	}
}
