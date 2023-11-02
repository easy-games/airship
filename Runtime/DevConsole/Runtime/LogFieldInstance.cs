using TMPro;
using UnityEngine;

public class LogFieldInstance : MonoBehaviour {
    private void Start() {
        GetComponentInChildren<TMP_SelectionCaret>().raycastTarget = false;
    }
}