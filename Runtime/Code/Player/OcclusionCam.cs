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
    public float BumpForOcclusion(Vector3 attachToPos, int mask)
    {
        var thisTransform = transform;
        var camPos = thisTransform.position;

        var distance = Vector3.Distance(camPos, attachToPos);
        var adjusted = false;

        var diff = camPos - attachToPos;
        var boxHalfExtents = new Vector3(_projectionX * 0.05f, _projectionY * 0.05f, 0f);

        //Cast from target attached position to camera position 
        if (!Physics.BoxCast(attachToPos,
                boxHalfExtents, diff, out var attachedHitInfo, thisTransform.rotation, distance,
                mask, QueryTriggerInteraction.Ignore))
        {
            return distance;
        }

        //Cast from camera to target attached pos
        if (Physics.BoxCast(camPos,
                boxHalfExtents, diff * -1, out var cameraHitInfo, thisTransform.rotation, distance,
                mask, QueryTriggerInteraction.Ignore))
        {
            //Determine if we should use the adjusted position or look from the outside of it
            //Use distance to hit to determine which side of the obstrution we should be at 
            if (cameraHitInfo.distance > attachedHitInfo.distance &&
                cameraHitInfo.distance > minAllowedDistance &&
                cameraHitInfo.colliderInstanceID == attachedHitInfo.colliderInstanceID)
            {
                //Closer to camera and camera position is useable
                return distance;
            }
        }
        //print("cameraHitDistance: " + cameraHitInfo.distance + " attachedHitDistance: " + attachedHitInfo.distance);

        //Move in from of obstruction
        distance = Mathf.Max(this.minAllowedDistance, attachedHitInfo.distance + .01f);
        thisTransform.position = attachToPos + diff.normalized * distance;
        return distance;

    }
}
