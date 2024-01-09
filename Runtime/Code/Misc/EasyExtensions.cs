using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class EasyExtensions {
    
    //GAME OBJECT
    public static void DestroyAllChildren(this GameObject parent){
        foreach (Transform child in parent.transform) GameObject.Destroy(child.gameObject);
    }
    
    public static void EnableAllComponentsInChildren<T>(this GameObject obj) where T: Behaviour{
        obj.ToggleAllComponentsInChildren<T>(true);
    }
    
    public static void DisableAllComponentsInChildren<T>(this GameObject obj) where T: Behaviour{
        obj.ToggleAllComponentsInChildren<T>(false);
    }
	
    public static void HideAllRenderers(this GameObject obj){
        obj.ToggleAllRenderersInChildren<Renderer>(false);
    }
    
    public static void ShowAllRenderers(this GameObject obj){
        obj.ToggleAllRenderersInChildren<Renderer>(true);
    }
    
    public static void ToggleAllComponentsInChildren<T>(this GameObject obj, bool show) where T: Behaviour{
        foreach(var component in obj.GetComponentsInChildren<T>()){
            component.enabled = show;
        }
    }
    
    public static void ToggleAllRenderersInChildren<T>(this GameObject obj, bool show) where T: Renderer{
        foreach(var component in obj.GetComponentsInChildren<T>()){
            component.enabled = show;
        }
    }
	
    //TEXTURES and SPRITES
    public static Sprite CreateSprite(this Texture2D texture) {
        return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f));
    }

    public static Sprite CreateSprite(this Texture2D texture, Rect renderRect, Vector2 pivot, float pixelsPerUnit = 100) {
        return Sprite.Create(texture, renderRect, pivot, pixelsPerUnit);
    }
    
    //TRANSFORM
    public static void ClearLocalTransform(this Transform transform) {
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;
    }
    
    //RECT TRANSFORM
    public static Rect RectTransformToScreenSpace(this RectTransform transform){
        Vector2 size = Vector2.Scale(transform.rect.size, transform.lossyScale);
        return new Rect((Vector2)transform.position - (size * 0.5f), size);
    }
    
    //COLLIDER
    public static T GetRootComponent<T>(this Collider collider) {
        var root = collider.attachedRigidbody != null ? collider.attachedRigidbody.gameObject : collider.gameObject;
        return root.GetComponent<T>();
    }
    
    //QUATERNION
    public static Vector4 ConvertToVector4(this Quaternion quaternion) {
        return new Vector4(quaternion.x, quaternion.y, quaternion.z, quaternion.w);
    }

    public static Quaternion ConvertToQuaternion(this Vector4 vec4) {
        return new Quaternion(vec4.x, vec4.y, vec4.z, vec4.w);
    }
    
    //Vectors
    public static int ConvertTileToLinear(this Vector2Int value, int stride) {
        return value.y * stride + value.x;
    }
    
    public static Vector2Int ConvertToLinearToTile(this int value, int stride) {
        return new Vector2Int(value % stride, Mathf.FloorToInt(value / (float)stride));
    }
}
