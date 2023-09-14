using UnityEngine;

public abstract class BundleLoadingScreen : MonoBehaviour {
    public abstract void SetProgress(string text, float percent);
}