using System;
using UnityEngine;

public abstract class BundleLoadingScreen : MonoBehaviour {
    [NonSerialized] public bool showContinueButton = false;

    public abstract void SetProgress(string text, float percent);

    public virtual void SetTotalDownloadSize(long sizeBytes) {

    }

    public virtual void SetError(string msg) {

    }
}