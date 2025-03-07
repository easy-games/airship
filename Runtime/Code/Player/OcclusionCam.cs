using System;
using UnityEngine;
using UnityEngine.Serialization;

[LuauAPI]
public class OcclusionCam : MonoBehaviour
{
    [Header("References")]
    public Camera targetCamera;

    [Header("Variables")]
    public float minAllowedDistance = .5f;
    public bool clampYToHeadHeight = false;

    private float _fov;
    private Vector2Int _res;
    private float _projectionX;
    private float _projectionY;

    public void Init(Camera camera)
    {
        targetCamera = camera;
        OnScreenSizeChanged();
    }

    private void Update()
    {
        if (!targetCamera)
        {
            return;
        }
        if (Screen.width != _res.x || Screen.height != _res.y || Math.Abs(_fov - targetCamera.fieldOfView) > 0.001f)
        {
            OnScreenSizeChanged();
        }
    }

    private void OnScreenSizeChanged()
    {
        _res.x = Screen.width;
        _res.y = Screen.height;
        _fov = targetCamera.fieldOfView;
        var fov = _fov * Mathf.Deg2Rad;
        var aspectRatio = _res.x / _res.y;
        _projectionY = Mathf.Tan(fov / 2f) * 2f;
        _projectionX = _projectionY * aspectRatio;
    }

    // Called from TS/Lua side
    // Returns the ending distance from the target
    public void BumpForOcclusion(Vector3 attachToPos, int mask) {
        var t = transform;
        var camPos = t.position;
        var distance = Vector3.Distance(camPos, attachToPos);
        // If cam is too far above attach pos snap up
        if (this.clampYToHeadHeight && attachToPos.y - camPos.y > 0.4f) {
            distance /= (attachToPos.y - camPos.y) / 0.4f;
        }
        var adjusted = false;
        var diff = camPos - attachToPos;
        var boxHalfExtents = new Vector3(_projectionX * 0.05f, _projectionY * 0.05f, 0f);
        if (!Physics.BoxCast(attachToPos,
                boxHalfExtents, diff, out var hitInfo, t.rotation, distance,
                mask, QueryTriggerInteraction.Ignore)) {
            t.position = attachToPos + diff.normalized * distance;
            return;
        }
		
        t.position = attachToPos + diff.normalized * hitInfo.distance;
    }
}
