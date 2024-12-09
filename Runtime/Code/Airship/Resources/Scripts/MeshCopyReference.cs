using System;
using JetBrains.Annotations;
using UnityEngine;

namespace Code.Airship.Resources.Scripts {
    /// MeshCopyReference is where to get the mesh data from (an asset, or a child game object)
    [System.Serializable]
    public class MeshCopyReference {
        [SerializeField] public string name;
        [SerializeField] public bool enabled = true;
        [SerializeField] public Transform transform = null;
        [SerializeField] public bool maskThisMesh = false;
        [SerializeField][CanBeNull] public ActiveAccessory activeAccessory;

        [NonSerialized] public MeshCopy[] meshCopies = null;

        public override string ToString() {
            return "Transform: " + transform.name;
        }

        public MeshCopyReference(Transform obj) {
            this.name = obj.name;
            this.transform = obj;
        }

        public MeshCopyReference(ActiveAccessory activeAccessory) {
            this.activeAccessory = activeAccessory;
            this.transform = activeAccessory.rootTransform;
        }

        public MeshCopyReference ManualClone() {
            MeshCopyReference output = new MeshCopyReference(this.transform) {
                enabled = this.enabled
            };

            if (this.meshCopies != null) {
                output.meshCopies = new MeshCopy[this.meshCopies.Length];
                for (int i = 0; i < this.meshCopies.Length; i++) {
                    output.meshCopies[i] = this.meshCopies[i].ManualClone();
                }
            }

            return output;
        }

        public void LoadMeshCopies() {
            if (this.activeAccessory != null) {
                this.meshCopies = MeshCopy.LoadActiveAccessory(activeAccessory).ToArray();
            } else {
                this.meshCopies = MeshCopy.LoadSlow(this.transform).ToArray();
            }
        }
    }
}