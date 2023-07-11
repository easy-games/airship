using UnityEngine;

[LuauAPI]
public class WindowProxy : MonoBehaviour
{
    public delegate void WindowFocusAction(object hasFocus);
    public event WindowFocusAction windowFocus;

    private void OnApplicationFocus(bool hasFocus)
    {
        windowFocus?.Invoke((object)hasFocus);
    }

    public bool HasFocus()
    {
        return Application.isFocused;
    }

    private void OnEnable()
    {
        WindowCore.SetWindowProxy(this);
    }
}
