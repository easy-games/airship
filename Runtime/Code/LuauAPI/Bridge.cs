using Tayx.Graphy;
using UnityEngine;
using UnityEngine.Scripting;

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
}