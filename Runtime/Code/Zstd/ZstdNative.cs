using System;
using System.Runtime.InteropServices;
using NativePlugins;
using UnityEngine;

// ReSharper disable InconsistentNaming

namespace Code.Zstd {
	internal static class ZstdNative {
		internal enum ZSTD_ErrorCode {
			ZSTD_error_no_error = 0,
			ZSTD_error_GENERIC = 1,
			ZSTD_error_prefix_unknown = 10,
			ZSTD_error_version_unsupported = 12,
			ZSTD_error_frameParameter_unsupported = 14,
			ZSTD_error_frameParameter_windowTooLarge = 16,
			ZSTD_error_corruption_detected = 20,
			ZSTD_error_checksum_wrong = 22,
			ZSTD_error_literals_headerWrong = 24,
			ZSTD_error_dictionary_corrupted = 30,
			ZSTD_error_dictionary_wrong = 32,
			ZSTD_error_dictionaryCreation_failed = 34,
			ZSTD_error_parameter_unsupported = 40,
			ZSTD_error_parameter_combination_unsupported = 41,
			ZSTD_error_parameter_outOfBound = 42,
			ZSTD_error_tableLog_tooLarge = 44,
			ZSTD_error_maxSymbolValue_tooLarge = 46,
			ZSTD_error_maxSymbolValue_tooSmall = 48,
			ZSTD_error_cannotProduce_uncompressedBlock = 49,
			ZSTD_error_stabilityCondition_notRespected = 50,
			ZSTD_error_stage_wrong = 60,
			ZSTD_error_init_missing = 62,
			ZSTD_error_memory_allocation = 64,
			ZSTD_error_workSpace_tooSmall = 66,
			ZSTD_error_dstSize_tooSmall = 70,
			ZSTD_error_srcSize_wrong = 72,
			ZSTD_error_dstBuffer_null = 74,
			ZSTD_error_noForwardProgress_destFull = 80,
			ZSTD_error_noForwardProgress_inputEmpty = 82,
			ZSTD_error_frameIndex_tooLarge = 100,
			ZSTD_error_seekableIO = 102,
			ZSTD_error_dstBuffer_wrong = 104,
			ZSTD_error_srcBuffer_wrong = 105,
			ZSTD_error_sequenceProducer_failed = 106,
			ZSTD_error_externalSequences_invalid = 107,
			ZSTD_error_maxCode = 120,
		}

		internal enum ZSTD_cParameter {
			ZSTD_c_compressionLevel = 100,
			ZSTD_c_windowLog = 101,
			ZSTD_c_hashLog = 102,
			ZSTD_c_chainLog = 103,
			ZSTD_c_searchLog = 104,
			ZSTD_c_minMatch = 105,
			ZSTD_c_targetLength = 106,
			ZSTD_c_strategy = 107,
			ZSTD_c_targetCBlockSize = 130,
			ZSTD_c_enableLongDistanceMatching = 160,
			ZSTD_c_ldmHashLog = 161,
			ZSTD_c_ldmMinMatch = 162,
			ZSTD_c_ldmBucketSizeLog = 163,
			ZSTD_c_ldmHashRateLog = 164,
			ZSTD_c_contentSizeFlag = 200,
			ZSTD_c_checksumFlag = 201,
			ZSTD_c_dictIDFlag = 202,
			ZSTD_c_nbWorkers = 400,
			ZSTD_c_jobSize = 401,
			ZSTD_c_overlapLog = 402,
			ZSTD_c_experimentalParam1 = 500,
			ZSTD_c_experimentalParam2 = 10,
			ZSTD_c_experimentalParam3 = 1000,
			ZSTD_c_experimentalParam4 = 1001,
			ZSTD_c_experimentalParam5 = 1002,
			ZSTD_c_experimentalParam7 = 1004,
			ZSTD_c_experimentalParam8 = 1005,
			ZSTD_c_experimentalParam9 = 1006,
			ZSTD_c_experimentalParam10 = 1007,
			ZSTD_c_experimentalParam11 = 1008,
			ZSTD_c_experimentalParam12 = 1009,
			ZSTD_c_experimentalParam13 = 1010,
			ZSTD_c_experimentalParam14 = 1011,
			ZSTD_c_experimentalParam15 = 1012,
			ZSTD_c_experimentalParam16 = 1013,
			ZSTD_c_experimentalParam17 = 1014,
			ZSTD_c_experimentalParam18 = 1015,
			ZSTD_c_experimentalParam19 = 1016,
			ZSTD_c_experimentalParam20 = 1017,
		}

		internal enum ZSTD_dParameter {
			ZSTD_d_windowLogMax = 100,
			ZSTD_d_experimentalParam1 = 1000,
			ZSTD_d_experimentalParam2 = 1001,
			ZSTD_d_experimentalParam3 = 1002,
			ZSTD_d_experimentalParam4 = 1003,
			ZSTD_d_experimentalParam5 = 1004,
			ZSTD_d_experimentalParam6 = 1005,
		}
		
		internal enum ZSTD_ResetDirective {
			ZSTD_reset_session_only = 1,
			ZSTD_reset_parameters = 2,
			ZSTD_reset_session_and_parameters = 3,
		}

		internal enum ZSTD_EndDirective {
			ZSTD_e_continue = 0,
			ZSTD_e_flush = 1,
			ZSTD_e_end = 2,
		}
		
		[StructLayout(LayoutKind.Sequential)]
		internal struct ZSTD_bounds {
			internal ulong error;
			internal int lowerBound;
			internal int upperBound;
		}
		
		[StructLayout(LayoutKind.Sequential)]
		internal struct ZSTD_inBuffer {
			internal IntPtr src;
			internal ulong size;
			internal ulong pos;
		}
		
		[StructLayout(LayoutKind.Sequential)]
		internal struct ZSTD_outBuffer {
			internal IntPtr dst;
			internal ulong size;
			internal ulong pos;
		}
		
#if UNITY_EDITOR && !UNITY_EDITOR_LINUX
		private const string BasePluginsPath = "/Packages/gg.easy.airship/Runtime/Plugins";
#if UNITY_EDITOR_OSX
	private const string LuauLibPath = BasePluginsPath + "/Mac/LuauPlugin.bundle/Contents/MacOS/LuauPlugin";
#elif UNITY_EDITOR_LINUX
	private const string LuauLibPath = BasePluginsPath + "/Linux/libLuauPlugin.so";
#elif UNITY_EDITOR_WIN
		private const string LuauLibPath = BasePluginsPath + "/Windows/x64/LuauPlugin.dll";
#endif
		
		[NativePlugin(LuauLibPath)]
		private static IntPtr _libHandle;
		
		// Delegates:
		internal delegate ulong ZSTD_compress_Delegate(IntPtr dst, ulong dstCapacity, IntPtr src, ulong srcCapacity, int compressionLevel);
		[NativeDelegate] internal static ZSTD_compress_Delegate ZSTD_compress;
		
		internal delegate ulong ZSTD_decompress_Delegate(IntPtr dst, ulong dstCapacity, IntPtr src, ulong compressedSize);
		[NativeDelegate] internal static ZSTD_decompress_Delegate ZSTD_decompress;
		
		internal delegate ulong ZSTD_getFrameContentSize_Delegate(IntPtr src, ulong srcSize);
		[NativeDelegate] internal static ZSTD_getFrameContentSize_Delegate ZSTD_getFrameContentSize;

		internal delegate ulong ZSTD_findFrameCompressedSize_Delegate(IntPtr src, ulong srcSize);
		[NativeDelegate] internal static ZSTD_findFrameCompressedSize_Delegate ZSTD_findFrameCompressedSize;
		
		internal delegate ulong ZSTD_compressBound_Delegate(ulong srcSize);
		[NativeDelegate] internal static ZSTD_compressBound_Delegate ZSTD_compressBound;
		
		private delegate uint ZSTD_isError_native_Delegate(ulong result);
		[NativeDelegate("ZSTD_isError")] private static ZSTD_isError_native_Delegate ZSTD_isError_native;
		
		internal delegate ZSTD_ErrorCode ZSTD_getErrorCode_Delegate(ulong functionResult);
		[NativeDelegate] internal static ZSTD_getErrorCode_Delegate ZSTD_getErrorCode;
		
		private delegate IntPtr ZSTD_getErrorName_native_Delegate(ulong result);
		[NativeDelegate("ZSTD_getErrorName")] private static ZSTD_getErrorName_native_Delegate ZSTD_getErrorName_native;
		
		internal delegate int ZSTD_minCLevel_Delegate();
		[NativeDelegate] internal static ZSTD_minCLevel_Delegate ZSTD_minCLevel;
		
		internal delegate int ZSTD_maxCLevel_Delegate();
		[NativeDelegate] internal static ZSTD_maxCLevel_Delegate ZSTD_maxCLevel;
		
		internal delegate int ZSTD_defaultCLevel_Delegate();
		[NativeDelegate] internal static ZSTD_defaultCLevel_Delegate ZSTD_defaultCLevel;
		
		internal delegate IntPtr ZSTD_createCCtx_Delegate();
		[NativeDelegate] internal static ZSTD_createCCtx_Delegate ZSTD_createCCtx;
		
		internal delegate ulong ZSTD_freeCCtx_Delegate(IntPtr cctx);
		[NativeDelegate] internal static ZSTD_freeCCtx_Delegate ZSTD_freeCCtx;
		
		internal delegate ulong ZSTD_compressCCtx_Delegate(IntPtr cctx, IntPtr dst, ulong dstCapacity, IntPtr src, ulong srcSize, int compressionLevel);
		[NativeDelegate] internal static ZSTD_compressCCtx_Delegate ZSTD_compressCCtx;
		
		internal delegate IntPtr ZSTD_createDCtx_Delegate();
		[NativeDelegate] internal static ZSTD_createDCtx_Delegate ZSTD_createDCtx;
		
		internal delegate ulong ZSTD_freeDCtx_Delegate(IntPtr dctx);
		[NativeDelegate] internal static ZSTD_freeDCtx_Delegate ZSTD_freeDCtx;

		internal delegate ulong ZSTD_decompressDCtx_Delegate(IntPtr dctx, IntPtr dst, ulong dstCapacity, IntPtr src, ulong srcSize);
		[NativeDelegate] internal static ZSTD_decompressDCtx_Delegate ZSTD_decompressDCtx;
		
		internal delegate ZSTD_bounds ZSTD_cParam_getBounds_Delegate(ZSTD_cParameter cParam);
		[NativeDelegate] internal static ZSTD_cParam_getBounds_Delegate ZSTD_cParam_getBounds;
		
		internal delegate ulong ZSTD_CCtx_setParameter_Delegate(IntPtr cctx, ZSTD_cParameter param, int value);
		[NativeDelegate] internal static ZSTD_CCtx_setParameter_Delegate ZSTD_CCtx_setParameter;
		
		internal delegate ulong ZSTD_CCtx_setPledgedSrcSize_Delegate(IntPtr cctx, ulong pledgedSrcSize);
		[NativeDelegate] internal static ZSTD_CCtx_setPledgedSrcSize_Delegate ZSTD_CCtx_setPledgedSrcSize;
		
		internal delegate ulong ZSTD_CCtx_reset_Delegate(IntPtr cctx, ZSTD_ResetDirective reset);
		[NativeDelegate] internal static ZSTD_CCtx_reset_Delegate ZSTD_CCtx_reset;
		
		internal delegate ulong ZSTD_compress2_Delegate(IntPtr cctx, IntPtr dst, ulong dstCapacity, IntPtr src, ulong srcSize);
		[NativeDelegate] internal static ZSTD_compress2_Delegate ZSTD_compress2;
		
		internal delegate ZSTD_bounds ZSTD_dParam_getBounds_Delegate(ZSTD_dParameter dParam);
		[NativeDelegate] internal static ZSTD_dParam_getBounds_Delegate ZSTD_dParam_getBounds;
		
		internal delegate ZSTD_bounds ZSTD_DCtx_setParameter_Delegate(IntPtr dctx, ZSTD_dParameter param, int value);
		[NativeDelegate] internal static ZSTD_DCtx_setParameter_Delegate ZSTD_DCtx_setParameter;
		
		internal delegate ZSTD_bounds ZSTD_DCtx_reset_Delegate(IntPtr dctx, ZSTD_ResetDirective reset);
		[NativeDelegate] internal static ZSTD_DCtx_reset_Delegate ZSTD_DCtx_reset;
		
		internal delegate IntPtr ZSTD_createCStream_Delegate();
		[NativeDelegate] internal static ZSTD_createCStream_Delegate ZSTD_createCStream;
		
		internal delegate ulong ZSTD_freeCStream_Delegate(IntPtr zcs);
		[NativeDelegate] internal static ZSTD_freeCStream_Delegate ZSTD_freeCStream;
		
		internal delegate ulong ZSTD_compressStream2_Delegate(IntPtr cctx, ref ZSTD_outBuffer output, ref ZSTD_inBuffer input, ZSTD_EndDirective endOp);
		[NativeDelegate] internal static ZSTD_compressStream2_Delegate ZSTD_compressStream2;
		
		internal delegate ulong ZSTD_CStreamInSize_Delegate();
		[NativeDelegate] internal static ZSTD_CStreamInSize_Delegate ZSTD_CStreamInSize;
		
		internal delegate ulong ZSTD_CStreamOutSize_Delegate();
		[NativeDelegate] internal static ZSTD_CStreamOutSize_Delegate ZSTD_CStreamOutSize;
		
		internal delegate ulong ZSTD_initCStream_Delegate(IntPtr zcs, int compressionLevel);
		[NativeDelegate] internal static ZSTD_initCStream_Delegate ZSTD_initCStream;
		
		internal delegate ulong ZSTD_compressStream_Delegate(IntPtr zcs, ref ZSTD_outBuffer output, ref ZSTD_inBuffer input);
		[NativeDelegate] internal static ZSTD_compressStream_Delegate ZSTD_compressStream;
		
		internal delegate ulong ZSTD_flushStream_Delegate(IntPtr zcs, ref ZSTD_outBuffer output);
		[NativeDelegate] internal static ZSTD_flushStream_Delegate ZSTD_flushStream;
		
		internal delegate ulong ZSTD_endStream_Delegate(IntPtr zcs, ref ZSTD_outBuffer output);
		[NativeDelegate] internal static ZSTD_endStream_Delegate ZSTD_endStream;
		
		internal delegate IntPtr ZSTD_createDStream_Delegate();
		[NativeDelegate] internal static ZSTD_createDStream_Delegate ZSTD_createDStream;
		
		internal delegate ulong ZSTD_freeDStream_Delegate(IntPtr zds);
		[NativeDelegate] internal static ZSTD_freeDStream_Delegate ZSTD_freeDStream;
		
		internal delegate ulong ZSTD_initDStream_Delegate(IntPtr zds);
		[NativeDelegate] internal static ZSTD_initDStream_Delegate ZSTD_initDStream;
		
		internal delegate ulong ZSTD_decompressStream_Delegate(IntPtr zds, ref ZSTD_outBuffer output, ref ZSTD_inBuffer input);
		[NativeDelegate] internal static ZSTD_decompressStream_Delegate ZSTD_decompressStream;
		
		internal delegate ulong ZSTD_DStreamInSize_Delegate();
		[NativeDelegate] internal static ZSTD_DStreamInSize_Delegate ZSTD_DStreamInSize;
		
		internal delegate ulong ZSTD_DStreamOutSize_Delegate();
		[NativeDelegate] internal static ZSTD_DStreamOutSize_Delegate ZSTD_DStreamOutSize;

		public static uint InitId {
			get;
			private set;
		}

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
		private static void InitPlugin() {
			InitId++;

			NativePluginHandles.RegisterPlugin(typeof(ZstdNative));

			Application.quitting -= DeinitPlugin;
			Application.quitting += DeinitPlugin;
		}

		private static void DeinitPlugin() {
			InitId++;
			_libHandle = IntPtr.Zero;
		}

		public static void TryInitPlugin() {
			if (_libHandle == IntPtr.Zero) {
				InitPlugin();
			}
		}
#else
#if UNITY_IPHONE
		[DllImport("__Internal")]
#else
		[DllImport("LuauPlugin")]
#endif
		internal static extern ulong ZSTD_compress(IntPtr dst, ulong dstCapacity, IntPtr src, ulong srcCapacity, int compressionLevel);
		
#if UNITY_IPHONE
		[DllImport("__Internal")]
#else
		[DllImport("LuauPlugin")]
#endif
		internal static extern ulong ZSTD_decompress(IntPtr dst, ulong dstCapacity, IntPtr src, ulong compressedSize);
		
#if UNITY_IPHONE
		[DllImport("__Internal")]
#else
		[DllImport("LuauPlugin")]
#endif
		internal static extern ulong ZSTD_getFrameContentSize(IntPtr src, ulong srcSize);
		
#if UNITY_IPHONE
		[DllImport("__Internal")]
#else
		[DllImport("LuauPlugin")]
#endif
		internal static extern ulong ZSTD_findFrameCompressedSize(IntPtr src, ulong srcSize);
		
#if UNITY_IPHONE
		[DllImport("__Internal")]
#else
		[DllImport("LuauPlugin")]
#endif
		internal static extern ulong ZSTD_compressBound(ulong srcSize);
		
#if UNITY_IPHONE
		[DllImport("__Internal", EntryPoint = "ZSTD_isError")]
#else
		[DllImport("LuauPlugin", EntryPoint = "ZSTD_isError")]
#endif
		private static extern uint ZSTD_isError_native(ulong result);
		
#if UNITY_IPHONE
		[DllImport("__Internal")]
#else
		[DllImport("LuauPlugin")]
#endif
		internal static extern ZSTD_ErrorCode ZSTD_getErrorCode(ulong functionResult);
		
#if UNITY_IPHONE
		[DllImport("__Internal", EntryPoint = "ZSTD_getErrorName")]
#else
		[DllImport("LuauPlugin", EntryPoint = "ZSTD_getErrorName")]
#endif
		private static extern IntPtr ZSTD_getErrorName_native(ulong result);
		
#if UNITY_IPHONE
		[DllImport("__Internal")]
#else
		[DllImport("LuauPlugin")]
#endif
		internal static extern int ZSTD_minCLevel();
		
#if UNITY_IPHONE
		[DllImport("__Internal")]
#else
		[DllImport("LuauPlugin")]
#endif
		internal static extern int ZSTD_maxCLevel();
		
#if UNITY_IPHONE
		[DllImport("__Internal")]
#else
		[DllImport("LuauPlugin")]
#endif
		internal static extern int ZSTD_defaultCLevel();
		
#if UNITY_IPHONE
		[DllImport("__Internal")]
#else
		[DllImport("LuauPlugin")]
#endif
		internal static extern IntPtr ZSTD_createCCtx();
		
#if UNITY_IPHONE
		[DllImport("__Internal")]
#else
		[DllImport("LuauPlugin")]
#endif
		internal static extern ulong ZSTD_freeCCtx(IntPtr cctx);
		
#if UNITY_IPHONE
		[DllImport("__Internal")]
#else
		[DllImport("LuauPlugin")]
#endif
		internal static extern ulong ZSTD_compressCCtx(IntPtr cctx, IntPtr dst, ulong dstCapacity, IntPtr src, ulong srcSize, int compressionLevel);
		
#if UNITY_IPHONE
		[DllImport("__Internal")]
#else
		[DllImport("LuauPlugin")]
#endif
		internal static extern IntPtr ZSTD_createDCtx();
		
#if UNITY_IPHONE
		[DllImport("__Internal")]
#else
		[DllImport("LuauPlugin")]
#endif
		internal static extern ulong ZSTD_freeDCtx(IntPtr dctx);
		
#if UNITY_IPHONE
		[DllImport("__Internal")]
#else
		[DllImport("LuauPlugin")]
#endif
		internal static extern ulong ZSTD_decompressDCtx(IntPtr dctx, IntPtr dst, ulong dstCapacity, IntPtr src, ulong srcSize);
		
#if UNITY_IPHONE
		[DllImport("__Internal")]
#else
		[DllImport("LuauPlugin")]
#endif
		internal static extern ZSTD_bounds ZSTD_cParam_getBounds(ZSTD_cParameter cParam);
		
#if UNITY_IPHONE
		[DllImport("__Internal")]
#else
		[DllImport("LuauPlugin")]
#endif
		internal static extern ulong ZSTD_CCtx_setParameter(IntPtr cctx, ZSTD_cParameter param, int value);
		
#if UNITY_IPHONE
		[DllImport("__Internal")]
#else
		[DllImport("LuauPlugin")]
#endif
		internal static extern ulong ZSTD_CCtx_setPledgedSrcSize(IntPtr cctx, ulong pledgedSrcSize);
		
#if UNITY_IPHONE
		[DllImport("__Internal")]
#else
		[DllImport("LuauPlugin")]
#endif
		internal static extern ulong ZSTD_CCtx_reset(IntPtr cctx, ZSTD_ResetDirective reset);
		
#if UNITY_IPHONE
		[DllImport("__Internal")]
#else
		[DllImport("LuauPlugin")]
#endif
		internal static extern ulong ZSTD_compress2(IntPtr cctx, IntPtr dst, ulong dstCapacity, IntPtr src, ulong srcSize);
		
#if UNITY_IPHONE
		[DllImport("__Internal")]
#else
		[DllImport("LuauPlugin")]
#endif
		internal static extern ZSTD_bounds ZSTD_dParam_getBounds(ZSTD_dParameter dParam);
		
#if UNITY_IPHONE
		[DllImport("__Internal")]
#else
		[DllImport("LuauPlugin")]
#endif
		internal static extern ZSTD_bounds ZSTD_DCtx_setParameter(IntPtr dctx, ZSTD_dParameter param, int value);
		
#if UNITY_IPHONE
		[DllImport("__Internal")]
#else
		[DllImport("LuauPlugin")]
#endif
		internal static extern ZSTD_bounds ZSTD_DCtx_reset(IntPtr dctx, ZSTD_ResetDirective reset);
		
#if UNITY_IPHONE
		[DllImport("__Internal")]
#else
		[DllImport("LuauPlugin")]
#endif
		internal static extern IntPtr ZSTD_createCStream();
		
#if UNITY_IPHONE
		[DllImport("__Internal")]
#else
		[DllImport("LuauPlugin")]
#endif
		internal static extern ulong ZSTD_freeCStream(IntPtr zcs);
		
#if UNITY_IPHONE
		[DllImport("__Internal")]
#else
		[DllImport("LuauPlugin")]
#endif
		internal static extern ulong ZSTD_compressStream2(IntPtr cctx, ref ZSTD_outBuffer output, ref ZSTD_inBuffer input, ZSTD_EndDirective endOp);
		
#if UNITY_IPHONE
		[DllImport("__Internal")]
#else
		[DllImport("LuauPlugin")]
#endif
		internal static extern ulong ZSTD_CStreamInSize();
		
#if UNITY_IPHONE
		[DllImport("__Internal")]
#else
		[DllImport("LuauPlugin")]
#endif
		internal static extern ulong ZSTD_CStreamOutSize();
		
#if UNITY_IPHONE
		[DllImport("__Internal")]
#else
		[DllImport("LuauPlugin")]
#endif
		internal static extern ulong ZSTD_initCStream(IntPtr zcs, int compressionLevel);
		
#if UNITY_IPHONE
		[DllImport("__Internal")]
#else
		[DllImport("LuauPlugin")]
#endif
		internal static extern ulong ZSTD_compressStream(IntPtr zcs, ref ZSTD_outBuffer output, ref ZSTD_inBuffer input);
		
#if UNITY_IPHONE
		[DllImport("__Internal")]
#else
		[DllImport("LuauPlugin")]
#endif
		internal static extern ulong ZSTD_flushStream(IntPtr zcs, ref ZSTD_outBuffer output);
		
#if UNITY_IPHONE
		[DllImport("__Internal")]
#else
		[DllImport("LuauPlugin")]
#endif
		internal static extern ulong ZSTD_endStream(IntPtr zcs, ref ZSTD_outBuffer output);
		
#if UNITY_IPHONE
		[DllImport("__Internal")]
#else
		[DllImport("LuauPlugin")]
#endif
		internal static extern IntPtr ZSTD_createDStream();
		
#if UNITY_IPHONE
		[DllImport("__Internal")]
#else
		[DllImport("LuauPlugin")]
#endif
		internal static extern ulong ZSTD_freeDStream(IntPtr zds);
		
#if UNITY_IPHONE
		[DllImport("__Internal")]
#else
		[DllImport("LuauPlugin")]
#endif
		internal static extern ulong ZSTD_initDStream(IntPtr zds);
		
#if UNITY_IPHONE
		[DllImport("__Internal")]
#else
		[DllImport("LuauPlugin")]
#endif
		internal static extern ulong ZSTD_decompressStream(IntPtr zds, ref ZSTD_outBuffer output, ref ZSTD_inBuffer input);
		
#if UNITY_IPHONE
		[DllImport("__Internal")]
#else
		[DllImport("LuauPlugin")]
#endif
		internal static extern ulong ZSTD_DStreamInSize();
		
#if UNITY_IPHONE
		[DllImport("__Internal")]
#else
		[DllImport("LuauPlugin")]
#endif
		internal static extern ulong ZSTD_DStreamOutSize();
#endif
		
		internal static bool ZSTD_isError(ulong result) {
			return ZSTD_isError_native(result) != 0;
		}
		
		internal static string ZSTD_getErrorName(ulong result) {
			return Marshal.PtrToStringUTF8(ZSTD_getErrorName_native(result));
		}
	}
}
