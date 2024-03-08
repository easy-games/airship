#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using Airship;
using Animancer;
using Assets.Code.Misc;
using Code.Bootstrap;
using Code.Http.Internal;
using Code.Http.Public;
using Code.Network;
using Code.Platform.Client;
using Code.Platform.Server;
using Code.Player.Character.API;
using Code.Projectile;
using Code.UI;
using Code.UI.Canvas;
using CsToTs;
using CsToTs.TypeScript;
using Airship.DevConsole;
using ElRaccoone.Tweens.Core;
using FishNet;
using FishNet.Component.ColliderRollback;
using FishNet.Component.Transforming;
using LeTai.TrueShadow;
using Player.Entity;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;
using Button = UnityEngine.UI.Button;
using Image = UnityEngine.UI.Image;
using Slider = UnityEngine.UI.Slider;
using Toggle = UnityEngine.UI.Toggle;

public class TypeGenerator : MonoBehaviour
{
#if AIRSHIP_INTERNAL
    [MenuItem("Airship/TypeScript/Generate Types")]
#endif
    private static void GenerateTypes()
    {
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
            typeof(InstanceFinder),
            typeof(Key),
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
            typeof(AccessoryBuilder),
            typeof(FadeMode),
            typeof(AvatarMask),
            typeof(SkinnedMeshRenderer),
            // typeof(VoxelWorld),
            typeof(DebugUtil),
            typeof(CollisionWatcher),
            typeof(TriggerWatcher),
            typeof(PhysicsExt),
            typeof(SphereCastReturnData),
            typeof(Rigidbody),
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
            typeof(ProjectileTrajectoryRenderer),
            typeof(ProjectileLauncher),
            typeof(AirshipProjectile),
            typeof(ProjectileValidateEvent),
            typeof(TrailRenderer),
            typeof(EntityAnimationEventKey),
            typeof(WindowCore),
            typeof(CharacterMoveModifier),
            typeof(DynamicVariables),
            typeof(ProjectileHitEvent),
            typeof(MaterialColor),
            typeof(MaterialColor.ColorSetting),
            typeof(AirshipObjectPool),
            typeof(MainMenuLoadingScreen),
            typeof(HttpManager),
            typeof(InternalHttpManager),
            typeof(FriendsControllerBackend),
            typeof(MatchmakingControllerBackend),
            typeof(PartyControllerBackend),
            typeof(AirshipInventoryControllerBackend),
            typeof(TransferControllerBackend),
            typeof(UsersControllerBackend),
            typeof(CacheStoreServiceBackend),
            typeof(DataStoreServiceBackend),
            typeof(LeaderboardServiceBackend),
            typeof(PartyServiceBackend),
            typeof(MatchmakingServiceBackend),
            typeof(TransferServiceBackend),
            typeof(AirshipInventoryServiceBackend),
            typeof(UsersServiceBackend),
            typeof(CrossSceneState),
            typeof(Toggle),
            typeof(HorizontalLayoutGroup),
            typeof(VerticalLayoutGroup),
            typeof(ContentSizeFitter),
            typeof(LayoutRebuilder),
            typeof(RectTransformUtility),
            typeof(ScrollRect),
            typeof(NetworkTransform),
            typeof(CameraScreenshotRecorder),
            typeof(Ray),
            typeof(MaterialPropertyBlock),
            typeof(DevConsole),
            typeof(EasyShake),
            typeof(EasyMotion),
            typeof(GroundItemDrop),
            typeof(CloudImage),
            typeof(AccessoryOutfit),
            typeof(LineRenderer),
            typeof(AirshipRedirectDrag),
            typeof(TrueShadow),
            typeof(ScalableBufferManager),
            typeof(AirshipPlatformUtil),
            typeof(CharacterRig),
            typeof(ColliderRollback),
            typeof(AccessoryFace),
            typeof(AvatarAccessoryCollection),
            typeof(ContactPoint),
            typeof(ContactPoint2D),
            typeof(SystemInfo),
            typeof(CanvasScaler),
            typeof(GridLayoutGroup),
            typeof(LayoutElement),
        };

        // Completely ignores these types (both declarations and usages in other types)
        string[] skipTypePatterns =
        {
            @"^System\.*",
            "ILogger",
            "UnityEngine.Vector3Int"
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
            "\\.Collision$",
            "\\.Transform$",
            "\\.DynamicVariablesManager$",
            "\\.HttpGetResponse$",
            "\\.Collider$",
            "\\.VoxelWorld$",
            "\\.NetworkObject$",
            "\\.InputProxy$"
        };

        var options = new TypeScriptOptions
        {
            ShouldGenerateMethod = (info, definition) => true,
            UseInterfaceForClasses = type => true,
            SkipTypePatterns = skipTypePatterns,
            SkipClassDeclarationPatterns = skipClassDeclarationPatterns,
            TypeRenamer = type =>
            {
                // if (type == "Vector3Int") {
                //     return "Vector3";
                // }
                type = type.Replace("*", "");
                if (type.Contains("$1"))
                {
                    print(type);
                    type = type.Substring(0, type.IndexOf("$1"));
                }

                return type;
            }
        };

        var tsDir = TypeScriptDirFinder.FindCorePackageDirectory();
        if (tsDir == null)
        {
            Debug.LogError("Failed to find TypeScript~ directory");
            return;
        }

        var generatedTypesPath = Path.Join(tsDir, "src/Shared/Types/Generated.d.ts");

        var ts = Generator.GenerateTypeScript(options, types);
        var task = File.WriteAllTextAsync(generatedTypesPath, ts);
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