using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

namespace Code.Managers {
    [LuauAPI]
    public class CanvasDistanceManager : Singleton<CanvasDistanceManager> {
        private Camera _camera;
        private List<CanvasDistanceCondition> _canvasObjects = new();
        
        private void Start() {
            _camera = Camera.main;
            
            InvokeRepeating(nameof(Tick), 0f, 0.1f);
        }

        public void Register(CanvasDistanceCondition canvasObject) {
            _canvasObjects.Add(canvasObject);
        }

        public void SetCamera(Camera cam) {
            this._camera = cam;
        }

        private void Tick() {
            var cameraPosition = _camera.transform.position;
            for (var i = _canvasObjects.Count - 1; i >= 0; i--) {
                var canvDistComp = _canvasObjects[i];
                if (canvDistComp.IsDestroyed()) {
                    _canvasObjects.RemoveAt(i);
                    continue;
                }

                var canvGo = canvDistComp.gameObject;

                var distSqr = Vector3.SqrMagnitude(canvDistComp.transform.position - cameraPosition);
                var shouldBeEnabled = distSqr < canvDistComp.maxDistanceSqrd;
                if (shouldBeEnabled != canvGo.activeSelf) {
                    canvGo.SetActive(shouldBeEnabled);
                }
            }
        }
    }
}