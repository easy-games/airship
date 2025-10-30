using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;

namespace Nobi.UiRoundedCorners {
    [ExecuteInEditMode]								//Required to check the OnEnable function
    [DisallowMultipleComponent]                     //You can only have one of these in every object.
    [RequireComponent(typeof(RectTransform))]
	public class ImageWithRoundedCorners : BaseMeshEffect {
		/// <summary>
		/// Single shared material across all ImageWithRoundedCorners instances
		/// </summary>
		private static Material material;

        public float radius = 40f;
        
		[HideInInspector, SerializeField] private MaskableGraphic image;
		/// <summary>
		/// Cached props to know when we need to update this component
		/// </summary>
		private Vector4 currentProps;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void OnReload() {
			material = null;
		}

		private void OnValidate() {
			if (Application.isPlaying && !RunCore.IsClient()) return;

			Validate();
			Refresh();
		}

		private void OnDestroy() {
			if (Application.isPlaying && !RunCore.IsClient()) return;
			
			image = null;
		}

		protected override void OnTransformParentChanged() {
			base.OnTransformParentChanged();
			if (Application.isPlaying && !RunCore.IsClient()) return;
			
			SetupCanvasShaderChannels();
		}

		private void OnEnable() {
			base.OnEnable();
			if (Application.isPlaying && !RunCore.IsClient()) return;

			SetupCanvasShaderChannels();

            //You can only add either ImageWithRoundedCorners or ImageWithIndependentRoundedCorners
            //It will replace the other component when added into the object.
            var other = GetComponent<ImageWithIndependentRoundedCorners>();
            if (other != null)
            {
                radius = other.r.x;					//When it does, transfer the radius value to this script
                DestroyHelper.Destroy(other);
            }

            Validate();
			Refresh();
		}

		private void SetupCanvasShaderChannels() {
			var canvas = GetComponentInParent<Canvas>();
			if (canvas != null && (canvas.additionalShaderChannels & AdditionalCanvasShaderChannels.TexCoord1) == 0) {
				// TexCoord1 required for sending UV1 to shaders
				canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1;
			}
		}

		private void OnRectTransformDimensionsChange() {
			if (Application.isPlaying && !RunCore.IsClient()) return;

			if (enabled && material != null) {
				Refresh();
			}
		}

		public void Validate() {
			if (material == null) {
#if UNITY_EDITOR
				var premadeMaterial =
					AssetDatabase.LoadAssetAtPath<Material>(
						"Packages/gg.easy.airship/ThirdParty/UiRoundedCorners/RoundedCorners.mat");
				if (premadeMaterial != null) material = premadeMaterial;
#endif

				// If we're not using the premade material (which we should be) then create a new material
				// instance (this will dirty scene).
				if (material == null) {
					var shader = Shader.Find("UI/RoundedCorners/RoundedCorners");
					if (shader == null) return;

					material = new Material(shader) {
						// hideFlags = HideFlags.DontSave
					};
				}
			}

			if (image == null) {
				TryGetComponent(out image);
			}

			if (image != null) {
				image.material = material;
			}
		}
		
		/// <summary>
		/// This runs whenever this UI mesh is regenerated. Insert the rounded corner info into UV channels 
		/// </summary>
		/// <param name="mesh"></param>
		public override void ModifyMesh(VertexHelper vh) {
			if (!IsActive()) return;

			var rect = ((RectTransform)transform).rect;
			var vert = new UIVertex();
			var height = rect.height;
			var width = rect.width;

			for (int i = 0; i < vh.currentVertCount; i++) {
				vh.PopulateUIVertex(ref vert, i);
				vert.uv1 = new Vector4(width, height, radius * 2);
				vh.SetUIVertex(vert, i);
			}
		}

		public void Refresh() {
			var rect = ((RectTransform)transform).rect;

            //Multiply radius value by 2 to make the radius value appear consistent with ImageWithIndependentRoundedCorners script.
            //Right now, the ImageWithIndependentRoundedCorners appears to have double the radius than this.
            if (material) {
	            var newVec = new Vector4(rect.width, rect.height, radius * 2, 0);
	            var existing = currentProps;
	            if ((existing - newVec).magnitude > 0.1f) {
		            currentProps = newVec;
		            graphic.SetVerticesDirty();
		            graphic.SetMaterialDirty();
	            }
            }
		}
	}
}