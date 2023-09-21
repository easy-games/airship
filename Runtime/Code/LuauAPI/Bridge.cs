using Tayx.Graphy;
using UnityEngine;
using UnityEngine.Scripting;
using UnityEngine.UI;

[LuauAPI][Preserve]
public static class Bridge
{
    public static Vector2 MakeVector2(float x, float y)
    {
        return new Vector2(x, y);
    }

    public static Sprite MakeSprite(Texture2D texture2D)
    {
        return Sprite.Create(texture2D, new Rect(0.0f, 0.0f, texture2D.width, texture2D.height),
            new Vector2(0.5f, 0.5f), 100.0f);
    }

    public static float GetAverageFPS()
    {
        return GraphyManager.Instance.AverageFPS;
    }

    public static float GetCurrentFPS()
    {
        return GraphyManager.Instance.CurrentFPS;
    }

    public static void SetVolume(float volume)
    {
        AudioListener.volume = volume;
    }

    public static float GetVolume()
    {
        return AudioListener.volume;
    }

    public static void SetFullScreen(bool value)
    {
        Screen.fullScreen = value;
    }

    public static bool IsFullScreen()
    {
        return Screen.fullScreen;
    }

    public static void SetParentToSceneRoot(Transform transform)
    {
        transform.SetParent(null);
    }

    public static void CopyToClipboard(string text) {
        GUIUtility.systemCopyBuffer = text;
    }

    public static Vector2 ScreenPointToLocalPointInRectangle(RectTransform rectTransform, Vector2 screenPoint) {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPoint, Camera.main, out var point);
        return point;
    }

    public static void UpdateLayout(Transform xform, bool recursive) {
        UpdateLayout_Internal(xform, recursive);
    }

    private static void UpdateLayout_Internal(Transform xform, bool recursive) {
        if (xform == null || xform.Equals(null)) {
            return;
        }

        // Update children first
        if (recursive) {
            for (int x = 0; x < xform.childCount; ++x) {
                UpdateLayout_Internal(xform.GetChild(x), true);
            }
        }

        // Update any components that might resize UI elements
        foreach (var layout in xform.GetComponents<LayoutGroup>()) {
            layout.CalculateLayoutInputVertical();
            layout.CalculateLayoutInputHorizontal();
        }
        foreach (var fitter in xform.GetComponents<ContentSizeFitter>()) {
            fitter.SetLayoutVertical();
            fitter.SetLayoutHorizontal();
        }
        foreach (var layout in xform.GetComponents<LayoutGroup>()) {
            LayoutRebuilder.ForceRebuildLayoutImmediate(layout.GetComponent<RectTransform>());
        }
    }

    [HideFromTS]
    public static void DisconnectEvent(int eventId) {
        LuauCore.DisconnectEvent(eventId);
    }
}