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
            if (!_camera) {
                _camera = Camera.main;
            }
            _canvasObjects.Add(canvasObject);
            CheckDistanceCondition(canvasObject, _camera.transform.position);
        }

        public void SetCamera(Camera cam) {
            this._camera = cam;
        }

        private void Tick() {
            var cameraPosition = _camera.transform.position;
            try {
                for (var i = _canvasObjects.Count - 1; i >= 0; i--) {
                    var canvDistComp = _canvasObjects[i];
                    if (canvDistComp.IsDestroyed()) {
                        _canvasObjects.RemoveAt(i);
                        continue;
                    }
                    
                    CheckDistanceCondition(canvDistComp, cameraPosition);
                }
            } catch (Exception ex) {
                Debug.LogError("Error ticking canvas distance: " + ex);
            }
        }

        private void CheckDistanceCondition(CanvasDistanceCondition canvDistComp, Vector3 cameraPosition) {
            var canvGo = canvDistComp.gameObject;

            var distSqr = Vector3.SqrMagnitude(canvDistComp.transform.position - cameraPosition);
            var shouldBeEnabled = distSqr < canvDistComp.maxDistanceSqrd;
            if (shouldBeEnabled != canvGo.activeSelf) {
                canvGo.SetActive(shouldBeEnabled);
            }
        }
    }
}