using System;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

namespace LeTai.TrueShadow
{
[AddComponentMenu("")]
[ExecuteAlways]
public partial class ShadowRenderer : MonoBehaviour, ILayoutIgnorer, IMaterialModifier, IMeshModifier
{
    public bool ignoreLayout => true;

#pragma warning disable CS0103
    static bool needRedraw = false;
#pragma warning restore CS0103

    [Conditional("UNITY_EDITOR")]
    internal static void QueueRedraw()
    {
        needRedraw = true;
    }

    internal CanvasRenderer CanvasRenderer { get; private set; }

    TrueShadow    shadow;
    RectTransform rt;
    RawImage      graphic;
    Texture       shadowTexture;

    public static void Initialize(TrueShadow shadow, ref ShadowRenderer renderer)
    {
        if (renderer && renderer.shadow == shadow)
        {
            renderer.gameObject.SetActive(true);
            return;
        }

        GameObject obj;
        var        objectName = $"{shadow.gameObject.name}'s Shadow";
        HideFlags hideFlags =
#if LETAI_TRUESHADOW_DEBUG
                DebugSettings.Instance.showObjects
                    ? HideFlags.DontSave
                    : HideFlags.HideAndDontSave
#else
                HideFlags.HideAndDontSave
#endif
            ;

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            obj = UnityEditor.EditorUtility.CreateGameObjectWithHideFlags(objectName, hideFlags);
        }
        else
        {
#endif
            obj = new GameObject(objectName) {
                hideFlags = hideFlags
            };
#if UNITY_EDITOR
        }
#endif

#if LETAI_TRUESHADOW_DEBUG && UNITY_EDITOR
        UnityEditor.SceneVisibilityManager.instance.DisablePicking(obj, true);
#endif


        RectTransform rt;
        RawImage      graphic;
// All these seem unnecessary
// #if UNITY_EDITOR
//         UnityEditor.Undo.RegisterCreatedObjectUndo(obj, "");
//         rt = UnityEditor.Undo.AddComponent<RectTransform>(obj);
//         UnityEditor.Undo.AddComponent<CanvasRenderer>(obj);
//         graphic  = UnityEditor.Undo.AddComponent<RawImage>(obj);
//         renderer = UnityEditor.Undo.AddComponent<ShadowRenderer>(obj);
//         UnityEditor.Undo.RecordObject(obj, "");
// #else
        rt = obj.AddComponent<RectTransform>();
        obj.AddComponent<CanvasRenderer>();
        graphic  = obj.AddComponent<RawImage>();
        renderer = obj.AddComponent<ShadowRenderer>();
// #endif
#if UNITY_EDITOR
        // Despite the object should not participate in undoing,
        // not registering it cause some kind of broken state in the Undo system.
        // in 2021.3.18, a crash will happen regardless of any form of registering. 2021.3.28 seem to have fixed this
        // low patch versions of 22 and 23 also crash, but only without this call
        // higher patch versions will throw a warning without this call
        UnityEditor.Undo.RecordObject(obj, "");
#endif

        shadow.SetHierachyDirty();

        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.zero;

        graphic.raycastTarget = false;
        graphic.color         = shadow.Color;

        renderer.shadow  = shadow;
        renderer.rt      = rt;
        renderer.graphic = graphic;

        renderer.UpdateMaterial();

        renderer.CanvasRenderer = obj.GetComponent<CanvasRenderer>();
        renderer.CanvasRenderer.SetColor(shadow.IgnoreCasterColor ? Color.white : shadow.CanvasRenderer.GetColor());
        renderer.CanvasRenderer.SetAlpha(shadow.CanvasRenderer.GetAlpha());

        renderer.ReLayout();
    }

    public void UpdateMaterial()
    {
        if (!graphic)
            return;

        if (shadow.Graphic is MaskableGraphic mg)
            graphic.maskable = mg.maskable;

        if (CanvasUpdateRegistry.IsRebuildingGraphics())
        {
            this.NextFrames(() => graphic.material = shadow.GetShadowRenderingMaterial());
#if UNITY_EDITOR
            UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
#endif
        }
        else
            graphic.material = shadow.GetShadowRenderingMaterial();
    }

    internal void ReLayout()
    {
        if (!isActiveAndEnabled)
            return;

        var casterRt = shadow.RectTransform;
        if (!casterRt)
        {
            CanvasRenderer.SetAlpha(0);
            return;
        }

        if (!shadowTexture)
        {
            CanvasRenderer.SetAlpha(0);
            return;
        }

        if (!shadow.SpriteMesh)
        {
            CanvasRenderer.SetAlpha(0);
            return;
        }

        var nudgeSize = !(shadow.DisableFitCompensation || shadow.Graphic is Text);
#if TMP_PRESENT
        nudgeSize = nudgeSize && !(shadow.Graphic is TMPro.TextMeshProUGUI || shadow.Graphic is TMPro.TMP_SubMeshUI);
#endif

        var container   = shadow.ShadowContainer;
        var canvasScale = container?.Snapshot?.canvasScale ?? graphic.canvas.scaleFactor;

        var casterMeshBounds = shadow.SpriteMesh.bounds;

        var misalignRatio = container == null
                                ? Vector2.one
                                : (Vector2)casterMeshBounds.size * canvasScale / (Vector2)container.ImprintSize;

        var shadowTexSize = new Vector2(shadowTexture.width, shadowTexture.height);
        shadowTexSize *= misalignRatio;
        shadowTexSize /= canvasScale;
        if (nudgeSize)
        {
            if (shadow.Inset)
                shadowTexSize += Vector2.one / canvasScale;
            else
                shadowTexSize -= Vector2.one / canvasScale;
        }

        if (shadowTexSize.x < 1e-3f || shadowTexSize.y < 1e-3f)
        {
            CanvasRenderer.SetAlpha(0);
            return;
        }

        rt.sizeDelta = shadowTexSize;


        float paddingLS = container?.Padding ?? Mathf.CeilToInt(shadow.Size * canvasScale);
        paddingLS /= canvasScale;
        if (nudgeSize)
        {
            if (shadow.Inset)
                paddingLS += .5f / canvasScale;
            else
                paddingLS -= .5f / canvasScale;
        }

        // pivot should be relative to the un-blurred part of the texture, not the whole mesh
        var casterPivotLS = -(Vector2)casterMeshBounds.min;
        rt.pivot = (casterPivotLS + paddingLS * misalignRatio) / shadowTexSize;


        var canvasRelativeOffset = container?.Snapshot?.canvasRelativeOffset / canvasScale ?? shadow.Offset;
        var offset = shadow.ShadowAsSibling
                         ? shadow.Offset.WithZ(0)
                         : canvasRelativeOffset.WithZ(0);
        rt.localPosition = shadow.ShadowAsSibling
                               ? casterRt.localPosition + offset
                               : offset;

        rt.localRotation = shadow.ShadowAsSibling ? casterRt.localRotation : Quaternion.identity;
        rt.localScale    = shadow.ShadowAsSibling ? casterRt.localScale : Vector3.one;


        var color = shadow.Color;
        if (shadow.UseCasterAlpha)
            color.a *= shadow.Graphic.color.a;
        graphic.color = color;

        CanvasRenderer.SetColor(shadow.IgnoreCasterColor ? Color.white : shadow.CanvasRenderer.GetColor());
        CanvasRenderer.SetAlpha(shadow.CanvasRenderer.GetAlpha());

        graphic.Rebuild(CanvasUpdate.PreRender);
    }

    public void SetTexture(Texture texture)
    {
        shadowTexture = texture;
        CanvasRenderer.SetTexture(texture);
        graphic.texture = texture;
    }

    public void SetMaterialDirty()
    {
        graphic.SetMaterialDirty();
    }

    public void ModifyMesh(VertexHelper vertexHelper)
    {
        if (!shadow)
            return;

        shadow.ModifyShadowRendererMesh(vertexHelper);
    }

    public void ModifyMesh(Mesh mesh)
    {
        Debug.Assert(true, "This should only be called on old unsupported Unity version");
    }

    protected virtual void LateUpdate()
    {
        // Destroy events are not consistently called for some reason, have to poll
        if (!shadow)
            Dispose();

        if (willBeDestroyed || !gameObject) return;

#if UNITY_EDITOR
        if (!Application.isPlaying && needRedraw)
            graphic.SetAllDirty();
#endif
    }

    bool willBeDestroyed;

    protected virtual void OnDestroy()
    {
        willBeDestroyed = true;
    }

    public void Dispose()
    {
        if (willBeDestroyed) return;

        if (shadow && shadow.ShadowAsSibling)
        {
            // Destroy does not happen immediately. Want out of hierarchy.
            gameObject.SetActive(false);
            transform.SetParent(null);
        }

#if UNITY_EDITOR
        // This look redundant but is necessary!
        if (!Application.isPlaying && !UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
            DestroyImmediate(gameObject);
        else if (Application.isPlaying)
            Destroy(gameObject);
#else
            Destroy(gameObject);
#endif
    }

#if LETAI_TRUESHADOW_DEBUG && UNITY_EDITOR
    void OnValidate()
    {
        if (shadow)
            shadow.SetLayoutDirty();
    }
#endif
}
}
