namespace Code.Components {
   using UnityEngine;

    // [ExecuteAlways]
    public class WorldSpaceCanvasScaler : MonoBehaviour {
        [Header("Camera & Facing")]
        public Camera cam;                         // If null, uses Camera.main
        public bool faceCamera = true;             // Billboard toward camera

        [Header("Screen-Space Target")]
        [Tooltip("How tall (in pixels) the whole RectTransform should appear on screen.")]
        public float targetScreenHeightPixels = 64f;

        // [Tooltip("Clamp the apparent size (pixels). Set to 0 to disable.")]
        // public float minScreenHeightPixels = 0f;
        // public float maxScreenHeightPixels = 0f;

        // [Header("Distance Clamps (optional)")]
        // public float minDistance = 0f;
        // public float maxDistance = 0f;

        [Header("Smoothing")]
        [Tooltip("0 = no smoothing, higher = smoother")]
        public float smoothTime = 0.08f;

        RectTransform _rt;
        Vector3 _vel; // x used; we mirror to y,z

        void OnEnable() {
            _rt = GetComponent<RectTransform>();
            if (!_rt) Debug.LogWarning("WorldSpaceCanvasScaler needs to be on a RectTransform.");
        }

        void LateUpdate()
        {
            if (_rt == null) return;

            Camera c = cam != null ? cam : Camera.main;
            if (c == null) return;

            // ✅ Correct 'behind camera' test
            // If dot < 0, it's behind camera; otherwise proceed.
            if (Vector3.Dot(transform.position - c.transform.position, c.transform.forward) < 0f)
                return;

            // Optional distance clamps
            // float d = Vector3.Distance(c.transform.position, transform.position);
            // if (minDistance > 0f && d < minDistance) d = minDistance;
            // if (maxDistance > 0f && maxDistance >= minDistance && d > maxDistance) d = maxDistance;

            // Desired pixel height with clamps
            float desiredPx = targetScreenHeightPixels;
            // if (minScreenHeightPixels > 0f) desiredPx = Mathf.Max(desiredPx, minScreenHeightPixels);
            // if (maxScreenHeightPixels > 0f && maxScreenHeightPixels >= minScreenHeightPixels)
            //     desiredPx = Mathf.Min(desiredPx, maxScreenHeightPixels);

            // How many pixels per 1 world unit at this position? Sample vertically.
            Vector3 p0 = c.WorldToScreenPoint(transform.position);
            Vector3 p1 = c.WorldToScreenPoint(transform.position + c.transform.up);
            float pixelsPerWorldUnit = Mathf.Abs(p1.y - p0.y);
            if (pixelsPerWorldUnit <= 0.0001) return;

            // Rect height in local units (pre-scale)
            float localHeight = Mathf.Abs(_rt.rect.height);
            if (localHeight <= 0.0001) return;

            // Solve for uniform scale
            float targetScale = desiredPx / (pixelsPerWorldUnit * localHeight);

            // Smooth scale (uniform)
            Vector3 ls = transform.localScale;
            float newScale = smoothTime > 0f ? Mathf.SmoothDamp(ls.x, targetScale, ref _vel.x, smoothTime) : targetScale;
            transform.localScale = new Vector3(newScale, newScale, newScale);

            // Optional for billboards
            if (faceCamera) {
                transform.forward = c.transform.forward;
            }
        }
    }
}