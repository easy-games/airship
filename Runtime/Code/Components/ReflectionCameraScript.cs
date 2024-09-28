using UnityEditor;
#if UNITY_EDITOR
using UnityEngine;
#endif
//Script that produces a reflection texture based on a plane (like water) and the current scene/editor/game main camera
//Useful for planar water, mirrors, etc

#if UNITY_EDITOR
[ExecuteInEditMode]
#endif
public class ReflectionCameraScript : MonoBehaviour {

    public Camera explicitCameraOverride;
    
    public Transform reflectiveSurface;
    public RenderTexture reflectionTexture;
    public float offset = 0.1f;

    //Because we only have one texture and multiple viewports might be using it
    //In edit mode we have to pick who we follow
    public enum DebugCamera {
        EditorCamera,
        GameCamera
    }
    public DebugCamera editorReflectionMode = DebugCamera.EditorCamera;

#if UNITY_EDITOR
    private void OnEnable() {
        EditorApplication.update += Render;
    }

    private void OnDisable() {
        EditorApplication.update -= Render;
    }
#else
    //Is there a better connection for ingame?
    private void LateUpdate() {
        Render();
    }
#endif    
    
    private void Render() {

        if (!reflectiveSurface || !reflectionTexture)
            return;
        Camera thisCamera = GetComponent<Camera>();
        
        if (!thisCamera)
            return;

        //Start by trying to mirror the main camera
        Camera camToMirror = Camera.main;

#if UNITY_EDITOR
        //In edit mode, pick a sensible camera
        if (editorReflectionMode == DebugCamera.EditorCamera && Application.isEditor && !Application.isPlaying) {
            // Get the viewport camera
            camToMirror = SceneView.lastActiveSceneView.camera;
        }
        UnityEditor.SceneView.RepaintAll();
#endif
        if (explicitCameraOverride) {
            camToMirror = explicitCameraOverride;
        }
        
        //Somehow didnt end up with a camera
        if (camToMirror == null)
            return;

        thisCamera.CopyFrom(camToMirror);

        thisCamera.fieldOfView = camToMirror.fieldOfView;
        thisCamera.aspect = camToMirror.aspect;

        thisCamera.focalLength = camToMirror.focalLength;
        thisCamera.focusDistance = camToMirror.focusDistance;
 
        // Take main camera directions and position in world space
        Vector3 cameraDirectionWorldSpace = camToMirror.transform.forward;
        Vector3 cameraUpWorldSpace = camToMirror.transform.up;
        Vector3 cameraPositionWorldSpace = camToMirror.transform.position;

        // Transform direction and position by reflection plane
        Transform reflectionPlane = reflectiveSurface;
        Vector3 cameraDirectionPlaneSpace = reflectionPlane.InverseTransformDirection(cameraDirectionWorldSpace);
        Vector3 cameraUpPlaneSpace = reflectionPlane.InverseTransformDirection(cameraUpWorldSpace);
        Vector3 cameraPositionPlaneSpace = reflectionPlane.InverseTransformPoint(cameraPositionWorldSpace);

        // Invert direction and position by reflection plane
        cameraDirectionPlaneSpace.y *= -1;
        cameraUpPlaneSpace.y *= -1;
        cameraPositionPlaneSpace.y *= -1;

        // Transform direction and position back to world space from reflection plane local space
        cameraPositionWorldSpace = reflectionPlane.TransformPoint(cameraPositionPlaneSpace);

        // Apply position to the reflection camera
        thisCamera.transform.position = cameraPositionWorldSpace + new Vector3(0, offset,0); //a small offset removes gap

        // Instead of LookAt, we explicitly mirror the camera's rotation
        // Reflect the camera's rotation across the reflection plane
        thisCamera.transform.rotation = ReflectRotation(camToMirror.transform.rotation, reflectionPlane.up);

        // Set the projection matrix with oblique near-plane clipping
        Vector4 reflectionPlaneWorldSpace = new Vector4(
            reflectiveSurface.up.x,
            reflectiveSurface.up.y,
            reflectiveSurface.up.z,
            -Vector3.Dot(reflectiveSurface.up, reflectiveSurface.position)
        );

        // Calculate the camera's projection matrix for oblique clipping
        // This slices the world at the water plane
        thisCamera.projectionMatrix = CalculateObliqueMatrix(thisCamera, reflectionPlaneWorldSpace);
        
        // Render the reflection to the texture
        thisCamera.targetTexture = reflectionTexture;
        thisCamera.Render();
    } 

    // Helper function to calculate the oblique matrix 
    Matrix4x4 CalculateObliqueMatrix(Camera cam, Vector4 plane) {
        Matrix4x4 projection = cam.projectionMatrix;
        Vector4 clipPlaneCameraSpace = cam.worldToCameraMatrix.inverse.transpose * plane;

        Vector4 q = projection.inverse * new Vector4(
            Mathf.Sign(clipPlaneCameraSpace.x),
            Mathf.Sign(clipPlaneCameraSpace.y),
            1.0f,
            1.0f
        );

        Vector4 c = clipPlaneCameraSpace * (2.0f / (Vector4.Dot(clipPlaneCameraSpace, q)));

        projection[2] = c.x - projection[3];
        projection[6] = c.y - projection[7];
        projection[10] = c.z - projection[11];
        projection[14] = c.w - projection[15];

        return projection;
    }

    // Helper function to reflect a quaternion (rotation) across a plane
    Quaternion ReflectRotation(Quaternion originalRotation, Vector3 planeNormal) {
        // Convert the quaternion to a matrix
        Matrix4x4 rotationMatrix = Matrix4x4.Rotate(originalRotation);

        // Reflect the forward and up vectors
        Vector3 forward = rotationMatrix.MultiplyVector(Vector3.forward);
        Vector3 up = rotationMatrix.MultiplyVector(Vector3.up);

        forward = Vector3.Reflect(forward, planeNormal);
        up = Vector3.Reflect(up, planeNormal);

        // Create a new rotation from the reflected vectors
        return Quaternion.LookRotation(forward, up);
    }
}
 