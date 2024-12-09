using System;
using Airship;
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

        [SerializeField] public SkinnedMeshRenderer skinnedMeshRenderer;

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

        public MeshCopyReference(SkinnedMeshRenderer skinnedMeshRenderer) {
            this.skinnedMeshRenderer = skinnedMeshRenderer;
            this.transform = skinnedMeshRenderer.transform;
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

        public void LoadMeshCopiesByAccessory() {
            this.meshCopies = MeshCopy.LoadActiveAccessory(activeAccessory).ToArray();
            // this.meshCopies = MeshCopy.LoadSlow(this.transform).ToArray();
        }

        public void LoadMeshCopiesAsBaseMesh() {
            this.meshCopies = new MeshCopy[1];
            this.meshCopies[0] = new MeshCopy(this.skinnedMeshRenderer.sharedMesh, this.skinnedMeshRenderer.sharedMaterials, this.transform, this.skinnedMeshRenderer.bones, this.skinnedMeshRenderer.rootBone);

            if (this.transform.TryGetComponent<MaterialColorURP>(out var matColor)) {
                this.meshCopies[0].ExtractMaterialColor(matColor);
            }
        }
    }
}