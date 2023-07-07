using UnityEngine;

namespace Code.Player.Accessories.Editor {
    internal class AccessoryInputTracker : ScriptableObject {
        [SerializeField] [HideInInspector] private Vector3 positionSer;
        [SerializeField] [HideInInspector] private Vector3 rotationSer;
        [SerializeField] [HideInInspector] private Vector3 scaleSer;

        private Vector3 _position;
        private Vector3 _rotation;
        private Vector3 _scale;

        private bool _positionDirty;
        private bool _rotationDirty;
        private bool _scaleDirty;

        internal Vector3 Position {
            get => positionSer;
            set {
                positionSer = value;
                _position = value;
                _positionDirty = true;
            }
        }

        internal Vector3 Rotation {
            get => rotationSer;
            set {
                rotationSer = value;
                _rotation = value;
                _rotationDirty = true;
            }
        }

        internal Vector3 Scale {
            get => scaleSer;
            set {
                scaleSer = value;
                _scale = value;
                _scaleDirty = true;
            }
        }

        internal void SetWithoutDirty(Vector3 pos, Vector3 rot, Vector3 scl) {
            _position = pos;
            positionSer = pos;
            _rotation = rot;
            rotationSer = rot;
            _scale = scl;
            scaleSer = scl;
        }

        internal bool DidUndoRedoAffectPosition() {
            return _position != positionSer;
        }

        internal bool DidUndoRedoAffectRotation() {
            return _rotation != rotationSer;
        }

        internal bool DidUndoRedoAffectScale() {
            return _scale != scaleSer;
        }

        internal void CheckAfterUndoRedo() {
            if (_position != positionSer) {
                _positionDirty = true;
                _position = positionSer;
            }
            if (_rotation != rotationSer) {
                _rotationDirty = true;
                _rotation = rotationSer;
            }
            if (_scale != scaleSer) {
                _scaleDirty = true;
                _scale = scaleSer;
            }
        }

        internal bool TryUpdatePosition(out Vector3 pos) {
            if (_positionDirty) {
                pos = positionSer;
                _positionDirty = false;
                return true;
            }
            pos = Vector3.zero;
            return false;
        }
        
        internal bool TryUpdateRotation(out Vector3 rot) {
            if (_rotationDirty) {
                rot = rotationSer;
                _rotationDirty = false;
                return true;
            }
            rot = Vector3.zero;
            return false;
        }
        
        internal bool TryUpdateScale(out Vector3 s) {
            if (_scaleDirty) {
                s = scaleSer;
                _scaleDirty = false;
                return true;
            }
            s = Vector3.zero;
            return false;
        }
    }
}
