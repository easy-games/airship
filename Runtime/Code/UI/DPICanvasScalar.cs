using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

// https://github.com/LightBuzz/Unity-Canvas-Scaler

namespace LightBuzz.UI
{
    [ExecuteInEditMode]
    [RequireComponent(typeof(Canvas))]
    [RequireComponent(typeof(CanvasScaler))]
    [AddComponentMenu("Airship/DPI Canvas Scalar")]
    public class DPICanvasScalar : UIBehaviour
    {
        [Header("Target DPI")]
        [SerializeField]
        [Range(1.0f, 400.0f)]
        [Tooltip("The desired target DPI.")]
        private float _targetDPI = 100.0f;

        private Canvas _canvas;
        private CanvasScaler _scaler;

        private float _previousScaleFactor = 1.0f;

        private float _previousReferencePixelsPerUnit = 100.0f;

        public float TargetDPI
        {
            get => _targetDPI;
            set => _targetDPI = value;
        }

        protected DPICanvasScalar()
        {
        }

        protected override void OnEnable()
        {
            base.OnEnable();

#if UNITY_EDITOR
            AssemblyReloadEvents.afterAssemblyReload += OnAfterAssemblyReload;
#endif
            _canvas = GetComponent<Canvas>();
            _scaler = GetComponent<CanvasScaler>();

            _previousScaleFactor = _scaler.scaleFactor;
            _previousReferencePixelsPerUnit = _scaler.referencePixelsPerUnit;
        }

        protected override void OnDisable()
        {
            SetScaleFactor(1.0f);
            SetReferencePixelsPerUnit(100.0f);

#if UNITY_EDITOR
            AssemblyReloadEvents.afterAssemblyReload -= OnAfterAssemblyReload;
#endif

            base.OnDisable();
        }

        protected virtual void Update()
        {
            Handle();
        }

        protected void Handle()
        {
            if (_canvas == null)
            {
                Debug.LogError("Canvas should not be null.");
                return;
            }
            if (!_canvas.isRootCanvas)
            {
                Debug.LogError("Canvas should be root.");
                return;
            }

            RenderMode mode = _canvas.renderMode;

            float dpi = Screen.dpi != 0 ?
                Screen.dpi :
                _scaler.fallbackScreenDPI;

            float scale = mode == RenderMode.WorldSpace ?
                _scaler.dynamicPixelsPerUnit :
                dpi / _targetDPI;

            float ppu = mode == RenderMode.WorldSpace ?
                _scaler.referencePixelsPerUnit :
                _scaler.referencePixelsPerUnit * _targetDPI / _scaler.defaultSpriteDPI;

            float factor = Mathf.Sqrt(scale);
            scale /= factor;

            SetReferencePixelsPerUnit(ppu);
            SetScaleFactor(scale);
        }

        protected void SetScaleFactor(float value)
        {
            if (Math.Abs(value - _previousScaleFactor) < 0.01) return;

            _canvas.scaleFactor = value;
            _previousScaleFactor = value;
            print("Set scale factor=" + value);
        }

        protected void SetReferencePixelsPerUnit(float value)
        {
            if (Math.Abs(value - _previousReferencePixelsPerUnit) < 0.01) return;

            _canvas.referencePixelsPerUnit = value;
            _previousReferencePixelsPerUnit = value;
            print("Set reference pixels per unit=" + value);
        }

#if UNITY_EDITOR
        public void OnAfterAssemblyReload()
        {
            SetScaleFactor(1.0f);
            SetReferencePixelsPerUnit(100.0f);
            Handle();
        }
#endif
    }
}