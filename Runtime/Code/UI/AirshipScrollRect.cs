using UnityEngine.EventSystems;
using UnityEngine.UI;

public class AirshipScrollRect : ScrollRect {
    public bool disableScrollWheel;
    public ScrollRect redirectScrollWheelInput;

    public override void OnScroll(PointerEventData data) {
        if (redirectScrollWheelInput != null) {
            redirectScrollWheelInput.OnScroll(data);
            return;
        }
        if (disableScrollWheel) {
            return;
        }
        base.OnScroll(data);
    }
}