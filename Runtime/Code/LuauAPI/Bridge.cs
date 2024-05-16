using System.Collections;
using Adrenak.UniMic;
using Airship.DevConsole;
using Luau;
using Proyecto26.Helper;
using Tayx.Graphy;
using UnityEngine;
using UnityEngine.SceneManagement;
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

    public static MaterialPropertyBlock MakeMaterialPropertyBlock() {
        return new MaterialPropertyBlock();
    }

    public static Mesh MakeMesh() {
        return new Mesh();
    }
    
    public static float GetAverageFPS()
    {
        return GraphyManager.Instance.AverageFPS;
    }

    public static float GetCurrentFPS()
    {
        return GraphyManager.Instance.CurrentFPS;
    }

    public static float GetMonoRam() {
        return GraphyManager.Instance.MonoRam;
    }

    public static float GetReservedRam() {
        return GraphyManager.Instance.ReservedRam;
    }

    public static float GetAllocatedRam() {
        return GraphyManager.Instance.AllocatedRam;
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
        RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPoint, null, out var point);
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

    public static string RemoveRichText(string input) {
        input = RemoveRichTextDynamicTag(input, "color");

        input = RemoveRichTextTag(input, "b");
        input = RemoveRichTextTag(input, "i");


        // TMP
        input = RemoveRichTextDynamicTag(input, "align");
        input = RemoveRichTextDynamicTag(input, "size");
        input = RemoveRichTextDynamicTag(input, "cspace");
        input = RemoveRichTextDynamicTag(input, "font");
        input = RemoveRichTextDynamicTag(input, "indent");
        input = RemoveRichTextDynamicTag(input, "line-height");
        input = RemoveRichTextDynamicTag(input, "line-indent");
        input = RemoveRichTextDynamicTag(input, "link");
        input = RemoveRichTextDynamicTag(input, "margin");
        input = RemoveRichTextDynamicTag(input, "margin-left");
        input = RemoveRichTextDynamicTag(input, "margin-right");
        input = RemoveRichTextDynamicTag(input, "mark");
        input = RemoveRichTextDynamicTag(input, "mspace");
        input = RemoveRichTextDynamicTag(input, "noparse");
        input = RemoveRichTextDynamicTag(input, "nobr");
        input = RemoveRichTextDynamicTag(input, "page");
        input = RemoveRichTextDynamicTag(input, "pos");
        input = RemoveRichTextDynamicTag(input, "space");
        input = RemoveRichTextDynamicTag(input, "sprite index");
        input = RemoveRichTextDynamicTag(input, "sprite name");
        input = RemoveRichTextDynamicTag(input, "sprite");
        input = RemoveRichTextDynamicTag(input, "style");
        input = RemoveRichTextDynamicTag(input, "voffset");
        input = RemoveRichTextDynamicTag(input, "width");

        input = RemoveRichTextTag(input, "u");
        input = RemoveRichTextTag(input, "s");
        input = RemoveRichTextTag(input, "sup");
        input = RemoveRichTextTag(input, "sub");
        input = RemoveRichTextTag(input, "allcaps");
        input = RemoveRichTextTag(input, "smallcaps");
        input = RemoveRichTextTag(input, "uppercase");
        // TMP end

        return input;
    }

    private static string RemoveRichTextDynamicTag (string input, string tag) {
        int index = -1;
        while (true)
        {
            index = input.IndexOf($"<{tag}=");
            //Debug.Log($"{{{index}}} - <noparse>{input}");
            if (index != -1)
            {
                int endIndex = input.Substring(index, input.Length - index).IndexOf('>');
                if (endIndex > 0)
                    input = input.Remove(index, endIndex + 1);
                continue;
            }
            input = RemoveRichTextTag(input, tag, false);
            return input;
        }
    }
    private static string RemoveRichTextTag (string input, string tag, bool isStart = true) {
        while (true)
        {
            int index = input.IndexOf(isStart ? $"<{tag}>" : $"</{tag}>");
            if (index != -1)
            {
                input = input.Remove(index, 2 + tag.Length + (!isStart).GetHashCode());
                continue;
            }
            if (isStart)
                input = RemoveRichTextTag(input, tag, false);
            return input;
        }
    }

    public static Scene GetActiveScene() {
        return SceneManager.GetActiveScene();
    }

    [LuauAPI(LuauContext.Protected)]
    public static void LoadScene(string sceneName, bool restartLuau) {
        SystemRoot.Instance.StartCoroutine(StartLoadScene(sceneName, restartLuau));
    }

    [LuauAPI(LuauContext.Protected)]
    private static IEnumerator StartLoadScene(string sceneName, bool restartLuau) {
        yield return null;
        if (restartLuau) {
            LuauCore.ResetContext(LuauContext.Game);
            LuauCore.ResetContext(LuauContext.Protected);
        }

        SceneManager.LoadScene(sceneName);
    }

    public static Scene GetScene(string sceneName) {
        return SceneManager.GetSceneByName(sceneName);
    }

    public static void OpenDevConsole() {
        DevConsole.OpenConsole();
    }

    [LuauAPI(LuauContext.Protected)]
    public static string[] GetMicDevices() {
        return Mic.Instance.Devices.ToArray();
    }

    [LuauAPI(LuauContext.Protected)]
    public static void SetMicDeviceIndex(int i) {
        Mic.Instance.SetDeviceIndex(i);
    }

    [LuauAPI(LuauContext.Protected)]
    public static int GetCurrentMicDeviceIndex() {
        return Mic.Instance.CurrentDeviceIndex;
    }

    [LuauAPI(LuauContext.Protected)]
    public static void StartMicRecording(int frequency, int sampleLength) {
        Mic.Instance.StartRecording(frequency, sampleLength);
    }

    [LuauAPI(LuauContext.Protected)]
    public static void StopMicRecording() {
        Mic.Instance.StopRecording();
    }

    [LuauAPI(LuauContext.Protected)]
    public static bool IsMicRecording() {
        return Mic.Instance.IsRecording;
    }

}