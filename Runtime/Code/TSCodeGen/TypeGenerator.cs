#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using Agones.Model;
using Airship;
using Assets.Code.Misc;
using Code.Bootstrap;
using Code.Http.Internal;
using Code.Http.Public;
using Code.UI;
using Code.UI.Canvas;
using CsToTs;
using Airship.DevConsole;
using Code.Managers;
using Code.Network;
using Code.Network.Simulation;
using Code.Player;
using Code.RemoteConsole;
using Code.VoiceChat;
using ElRaccoone.Tweens;
using ElRaccoone.Tweens.Core;
using LeTai.TrueShadow;
using Nobi.UiRoundedCorners;
using SFB;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Networking;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;
using Button = UnityEngine.UI.Button;
using Image = UnityEngine.UI.Image;
using Slider = UnityEngine.UI.Slider;
using Toggle = UnityEngine.UI.Toggle;
using UnityEngine.Tilemaps;
using Mirror;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.VFX;
using Code.Player.Accessories;
using Code.Player.Character.MovementSystems.Character;
using Code.Player.Character.NetworkedMovement;
using Unity.Mathematics;
using UnityEngine.Splines;

public class TypeGenerator : MonoBehaviour
{
#if AIRSHIP_INTERNAL
    [MenuItem("Airship/TypeScript/Generate Types", false, 2000)]
#endif
    private static void GenerateTypes() {
        print("Generating types...");

        List<Type> types = new()
        {
            typeof(RaycastHit),
            typeof(Physics),
            typeof(Physics2D),
            typeof(GameObject),
            typeof(MonoBehaviour),
            typeof(Debug),
            typeof(Sprite),
            typeof(DefaultFormat),
            typeof(TextureCreationFlags),
            typeof(TextAsset),
            typeof(Resources),
            typeof(AgonesCore),
            typeof(Object),
            typeof(RunCore),
            typeof(NetworkCore),
            typeof(Camera),
            typeof(Input),
            typeof(TouchPhase),
            typeof(Button),
            typeof(RectTransform),
            typeof(MeshRenderer),
            typeof(MeshFilter),
            typeof(TextMeshProUGUI),
            typeof(Animation),
            typeof(Animator),
            typeof(ClientSceneListener),
            typeof(CoreLoadingScreen),
            typeof(TextField),
            typeof(HumanBodyBones),
            typeof(GameConfig),
            typeof(RenderSettings),
            typeof(ServerBootstrap),
            typeof(SceneManager),
            typeof(AvatarMask),
            typeof(SkinnedMeshRenderer),
            typeof(GizmoUtils),
            typeof(CollisionWatcher),
            typeof(TriggerWatcher),
            typeof(PhysicsExt),
            typeof(SphereCastReturnData),
            typeof(Rigidbody),
            typeof(CharacterJoint),
            typeof(ServerConsole),
            typeof(Image),
            typeof(RawImage),
            typeof(Application),
            typeof(ClientNetworkConnector),
            typeof(ParticleSystem),
            typeof(ParticleSystem.EmitParams),
            typeof(ParticleSystemRenderer),
            typeof(Profiler),
            typeof(TMP_InputField),
            typeof(Rigidbody2D),
            typeof(Slider),
            typeof(Dropdown),
            typeof(TMP_Dropdown),
            typeof(CanvasHitDetector),
            typeof(AudioSource),
            typeof(AudioClip),
            typeof(Tween<>),
            typeof(Bridge),
            typeof(CanvasGroup),
            typeof(AutoShutdownBridge),
            typeof(ScreenCapture),
            typeof(VoxelBlocks),
            typeof(CharacterController),
            typeof(TrailRenderer),
            typeof(WindowCore),
            typeof(MaterialColorURP),
            typeof(MaterialColorURP.ColorSetting),
            typeof(MainMenuLoadingScreen),
            typeof(HttpManager),
            typeof(InternalHttpManager),
            typeof(UdpPingTool),
            typeof(CrossSceneState),
            typeof(Toggle),
            typeof(HorizontalLayoutGroup),
            typeof(VerticalLayoutGroup),
            typeof(ContentSizeFitter),
            typeof(AspectRatioFitter),
            typeof(LayoutRebuilder),
            typeof(RectTransformUtility),
            typeof(ScrollRect),
            typeof(Ray),
            typeof(MaterialPropertyBlock),
            typeof(DevConsole),
            typeof(EasyShake),
            typeof(EasyMotion),
            typeof(CloudImage),
            typeof(AccessoryOutfit),
            typeof(LineRenderer),
            typeof(AirshipRedirectScroll),
            typeof(TrueShadow),
            typeof(ScalableBufferManager),
            typeof(AirshipPlatformUtil),
            typeof(CharacterRig),
            typeof(AccessoryFace),
            typeof(AvatarAccessoryCollection),
            typeof(ContactPoint), 
            typeof(ContactPoint2D),
            typeof(SystemInfo),
            typeof(CanvasScaler),
            typeof(GridLayoutGroup),
            typeof(LayoutElement),
            typeof(Screen),
            typeof(Gizmos),
            typeof(DeviceBridge),
            typeof(Mask),
            typeof(ImageWithRoundedCorners),
            typeof(ImageWithIndependentRoundedCorners),
            typeof(NavMeshAgent),
            typeof(NavMesh),
            typeof(NavMeshObstacle),
            typeof(ScrollView),
            typeof(AirshipLongPress),
            typeof(DecalProjector),
            typeof(Time),
            typeof(FrameTimingManager),
            //Collider 2D Types
            typeof(BoxCollider2D),
            typeof(CircleCollider2D),
            typeof(CapsuleCollider2D),
            typeof(PolygonCollider2D),
            typeof(CustomCollider2D),
            typeof(EdgeCollider2D),
            typeof(CompositeCollider2D),
            typeof(TilemapCollider2D),
            typeof(TilemapCollider2D),
            typeof(CircleCollider2D),
            typeof(CircleCollider2D),
            typeof(CircleCollider2D),
            typeof(CircleCollider2D),
            //Collider 3D Types
            typeof(SphereCollider),
            typeof(BoxCollider),
            typeof(CapsuleCollider),
            typeof(MeshCollider),
            typeof(WheelCollider),
            typeof(TerrainCollider),
            typeof(NavMeshHit),
            typeof(Graphics),
            typeof(AirshipUniVoiceNetwork),
            typeof(StandaloneFileBrowser),
            typeof(MaterialColorURP),
            typeof(UnityWebRequestTexture),
            typeof(DownloadHandlerTexture),
            typeof(UIOutline),
            typeof(EventTrigger),
            typeof(EasyShake),
            typeof(TreeInstance),
            typeof(Terrain),
            typeof(GameServer),
            typeof(GraphicRaycaster),
            typeof(DepthOfField),
            typeof(Volume),
            typeof(DepthOfFieldMode),
            typeof(LensDistortion),
            typeof(Vignette),
            typeof(ShaderWarmup),
            typeof(ShaderVariantCollection),
            typeof(OcclusionCam),
            typeof(QualitySettings),
            typeof(GraphicsSettings),
            typeof(UniversalRenderPipelineAsset),
            typeof(WheelFrictionCurve),
            typeof(WheelHit),

            // Mirror
            typeof(NetworkServer),
            typeof(NetworkClient),
            typeof(NetworkIdentity),
            typeof(NetworkTransform),
            typeof(NetworkTransformReliable),
            typeof(NetworkTransformUnreliable),
            typeof(NetworkAnimator),
            typeof(NetworkConnection),
            typeof(NetworkConnectionToClient),
            typeof(NetworkConnectionToServer),
            typeof(NetworkTime),
            typeof(PredictedRigidbody),
            typeof(PredictedState),

            // Tweens
            typeof(NativeTween),
            typeof(Tween<>),
            typeof(TweenComponent<,>),
            typeof(ConstantForce),
            typeof(ConstantForce2D),
            typeof(FixedJoint),
            typeof(Grid),
            typeof(UIScrollRectEventBubbler),
            typeof(VisualEffect),
            
            // Splines
            typeof(BezierCurve),
            typeof(BezierKnot),
            typeof(CurveUtility),
            typeof(Spline),
            typeof(SplineAnimate),
            typeof(SplineContainer),
            typeof(SplineUtility),
            typeof(float3),

            // Airship
            typeof(AnimationEventData),
            typeof(VoxelWorld),
            typeof(CanvasDistanceCondition),
            typeof(CanvasDistanceManager),
            // typeof(AccessorySkin),
            typeof(AccessoryComponent),
            typeof(ActiveAccessory),
            typeof(VisualGraphComponent),
            typeof(CharacterMovementSettings),
            typeof(CharacterAnimationSyncData),
            typeof(AccessoryAddMode),
            typeof(RectMask2D),
            
            // Steam
            typeof(AirshipSteamFriendInfo),
            
            // Editor stuff
            typeof(EditorPrefs),
        };

        // TwoBoneIKConstraint ik;
        // ik.data.hint = null;

        // Completely ignores these types (both declarations and usages in other types)
        string[] skipTypePatterns =
        {
            @"^System\.*",
            "ILogger",
            // "UnityEngine.Vector3Int"
        };

        // Skips class declaration but still parses use in parameters.
        string[] skipClassDeclarationPatterns =
        {
            "^UnityEngine.Physics$",
            "UnityEngine.Vector2",
            "UnityEngine.Vector3",
            "UnityEngine.Vector4",
            "UnityEngine.Matrix4x4",
            "UnityEngine.Quaternion",
            // "Object",
            "^UnityEngine.Object$",
            "ListCache",
            // "InputControl",
            // "Vector2Control",
            // "AxisControl",
            // "TouchPhase",
            "\\.VisualElement$",
            "\\.ValueAnimation",
            "\\.StyleEnum$",
            "\\.ValueAnimation$",
            "\\.EventCallback$",
            //"\\.Button$",
            "\\.Clickable$",
            "\\.IStyle$",
            "\\.Attributes$",
            "\\.UICore$",
            "\\.Ray$",
            "\\.GameObject$",
            "\\.Component$",
            "\\.VoxelRaycastResult$",
            "\\.Color$",
            "\\.CanvasUI",
            "\\.EventSystem$",
            "\\.LayerMask$",
            // "\\.Collision$",
            "\\.Transform$",
            "\\.DynamicVariablesManager$",
            "\\.HttpGetResponse$",
            "\\.Collider$",
            "\\.NetworkObject$",
            "\\.InputProxy$",
            "\\.NavMesh$",
            "\\.SceneManager$",
            "\\.TwoBoneIKConstraint$",
            "\\.MultiAimIKConstraint$",
            "\\.Random$",
            "\\.NetworkTransform$",
            "\\.NetworkBehaviour$",
            "\\.NetworkIdentity$",
            "\\.NetworkTime$",
            "UnityEngine.TextCore.Text.Character",
            "\\.VolumeProfile$",
            "\\.Clothing$",
            "\\.ClothingManager$",
            "\\.MonoBehaviour$",
            "\\.InternalCameraScreenshotRecorder$",
            "\\.OcclusionCam$",
            "\\.AirshipUniVoiceNetwork$",
            "\\.ServerConsole$",
        };
        
        var options = new TypeScriptOptions
        {
            ShouldGenerateMethod = (info, definition) => true,
            UseInterfaceForClasses = type => true,
            SkipTypePatterns = skipTypePatterns,
            SkipClassDeclarationPatterns = skipClassDeclarationPatterns,
            TypeRenamer = type =>
            {
                if (type == "Vector3Int") {
                    return "Vector3";
                }
                type = type.Replace("*", "");
                if (type.Contains("$1"))
                {
                    print(type);
                    type = type.Substring(0, type.IndexOf("$1"));
                }

                return type;
            }
        };

        var tsDir = "Assets/AirshipPackages/@Easy/Core/Shared/Types/Generated.d.ts";
        // if (tsDir == null)
        // {
        //     Debug.LogError("Failed to find TypeScript~ directory");
        //     return;
        // }

        var ts = Generator.GenerateTypeScript(options, types);
        var task = File.WriteAllTextAsync(tsDir, ts);
        print("Saving generated types...");

        try
        {
            task.Wait();
            print("Finished saving Generated.d.ts!");
        }
        catch (AggregateException e)
        {
            Debug.LogException(e);
        }
    }
}
#endif