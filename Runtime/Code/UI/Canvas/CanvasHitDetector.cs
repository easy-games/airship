using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Code.UI.Canvas
{
    [LuauAPI]
    public class CanvasHitDetector : MonoBehaviour {
        public bool IsPointerOverUI()
        {
            PointerEventData eventDataCurrentPosition = new PointerEventData(EventSystem.current);
            eventDataCurrentPosition.position = new Vector2(Input.mousePosition.x, Input.mousePosition.y);
            List<RaycastResult> results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventDataCurrentPosition, results);

            int counter = 0;
            foreach (var result in results) {
                if (result.gameObject.layer == LayerMask.NameToLayer("UI")) {
                    counter++;
                }
            }

            return counter > 0;
        }

        public bool IsPointerOverTarget(GameObject target)
        {
            PointerEventData eventDataCurrentPosition = new PointerEventData(EventSystem.current);
            eventDataCurrentPosition.position = new Vector2(Input.mousePosition.x, Input.mousePosition.y);
            List<RaycastResult> results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventDataCurrentPosition, results);

            foreach (var result in results) {
                if (result.gameObject == target) {
                    return true;
                }
            }

            return false;
        }
    }
}