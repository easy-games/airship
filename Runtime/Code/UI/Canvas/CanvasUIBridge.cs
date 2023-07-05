using UnityEngine;
using UnityEngine.Scripting;
using UnityEngine.UI;

[LuauAPI]
[Preserve]
public static class CanvasUIBridge {
    
    
    /** Initializes a canvas. Enforces `RenderMode.ScreenSpaceOverlay`. */
    public static void InitializeCanvas(Canvas canvas, bool pixelPerfect) {
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.pixelPerfect = pixelPerfect;
    }

    /** Hides a visible canvas. */
    public static void HideCanvas(Canvas canvas) {
        canvas.enabled = false;
    }
    
    /** Creates a `Vector2` given an `x` and `y` component. */
    public static Vector2 CreateVector2(float x, float y) {
        return new Vector2(x, y);
    }

    /** Creates a `Rect` given `x`, `y`, `w`, and `h` components. */
    public static Rect CreateRect(float x, float y, float w, float h) {
        return new Rect(x, y, w, h);
    }

    /** Sets the sprite for a given `GameObject` with an `Image` component. */
    public static void SetSprite(GameObject goWithImage, string spritePath) {
        var image = goWithImage.GetComponent<Image>();
        var texture = AssetBridge.LoadAssetIfExistsInternal<Texture2D>(spritePath);
        image.sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
    }

}
