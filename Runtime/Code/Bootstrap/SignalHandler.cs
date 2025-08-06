using System.Collections;
using System.Runtime.InteropServices;
using Code.Util;
using UnityEngine;

namespace Code.Bootstrap {
    public class SignalHandler : MonoBehaviour {
        public ServerBootstrap serverBootstrap;
        public UnityMainThreadDispatcher unityMainThread;

#if UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void SignalDelegate();

        [DllImport("signalhandler", CallingConvention = CallingConvention.Cdecl)]
        private static extern void RegisterSigTermHandler(SignalDelegate callback);

        private static SignalDelegate cachedDelegate;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init()
        {
            cachedDelegate = OnSigTerm;
            RegisterSigTermHandler(cachedDelegate);
            Debug.Log("Unix SIGTERM handler registered");
        }

        // This is called from a POSIX signal context
        [AOT.MonoPInvokeCallback(typeof(SignalDelegate))]
        private static void OnSigTerm()
        {
            Debug.Log("SIGTERM signal received from native");

            // We can't call Unity APIs directly from signal thread,
            // So we need to dispatch back to main thread
            _hasSigterm = true;
        }

        private static bool _hasSigterm;

        private void Update()
        {
            if (_hasSigterm)
            {
                _hasSigterm = false;
                unityMainThread.Enqueue(HandleSigterm());
            }
        }

        private IEnumerator HandleSigterm()
        {
            Debug.Log("SIGTERM received. Performing cleanup.");
            serverBootstrap.InvokeOnProcessExit();
            yield return null;
            Application.Quit();
        }
#endif
    }
}
