using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ScrollRectNoDrag : ScrollRect {
    public override void OnBeginDrag(PointerEventData eventData) {
        if (Application.isMobilePlatform) return;
    }
    public override void OnDrag(PointerEventData eventData) { }
    public override void OnEndDrag(PointerEventData eventData) { }
}