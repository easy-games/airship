#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Airship;
using CsToTs;

using UnityEngine;
using UnityEngine.Experimental.Rendering;
using Object = UnityEngine.Object;
using Assets.Code.Projectiles;
using Assets.Code.Alignment;
using Assets.Code.Misc;

using Animancer;
using Code.UI.Canvas;
using ElRaccoone.Tweens.Core;
using FishNet;
using TMPro;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEngine.Profiling;
using UnityEngine.UIElements;
using Button = UnityEngine.UI.Button;
using Image = UnityEngine.UI.Image;
using Code.Projectile;
using CsToTs.TypeScript;
using VoxelWorldStuff;
using Assets.Code.Core;
using Code.Network;
using Code.PoolManager;
using Player.Entity;
using Toggle = UnityEngine.UI.Toggle;

public class TypeGenerator : MonoBehaviour
{

	[MenuItem("Airship/🏗️ TypeScript/Generate Types", priority = 305)]
	static void GenerateTypes()
	{
		print("Generating types...");

		List<Type> types = new() {
			typeof(RaycastHit),
			typeof(Physics),
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
			typeof(AlignmentManager),
			typeof(VoxelWorld),
			typeof(DebugUtil),
			typeof(CollisionWatcher),
			typeof(TriggerWatcher),
			typeof(PhysicsExt),
			typeof(SphereCastReturnData),
			typeof(Rigidbody),
			typeof(ServerConsole),
			typeof(Image),
			typeof(Application),
			typeof(ClientNetworkConnector),
			typeof(ParticleSystem),
			typeof(ParticleSystem.EmitParams),
			typeof(ParticleSystemRenderer),
			typeof(Profiler),
			typeof(TMP_InputField),
			typeof(Rigidbody2D),
			typeof(UnityEngine.UI.Slider),
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
			typeof(EasyProjectile),
			typeof(ProjectileValidateEvent),
			typeof(TrailRenderer),
			typeof(EntityAnimationEventData),
			typeof(EntityAnimationEventKey),
			typeof(WindowCore),
			typeof(EasyCoreAPI),
			typeof(CoreUserData),
			typeof(GameCoordinatorMessageHook),
			typeof(MoveModifier),
			typeof(DynamicVariables),
			typeof(ProjectileHitEvent),
			typeof(MaterialColor),
			typeof(MaterialColor.ColorSetting),
			typeof(AirshipObjectPool),
			typeof(MainMenuLoadingScreen),
			typeof(HttpManager),
			typeof(InternalHttpManager),
			typeof(CrossSceneState),
			typeof(Toggle)
		};

		// Completely ignores these types (both declarations and usages in other types)
		string[] skipTypePatterns = new[] {
			@"^System\.*",
			"ILogger",
			"UnityEngine.Vector3Int"
		};

		// Skips class declaration but still parses use in parameters.
		string[] skipClassDeclarationPatterns = new[] {
			"UnityEngine.Vector3",
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
            "\\.Button$",
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
            "\\.DynamicVariablesManager$"
        };

		var options = new TypeScriptOptions
		{
			ShouldGenerateMethod = (info, definition) => true,
			UseInterfaceForClasses = (type => true),
			SkipTypePatterns = skipTypePatterns,
			SkipClassDeclarationPatterns = skipClassDeclarationPatterns,
			TypeRenamer = (type =>
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
			})
		};

		var tsDir = TypeScriptDirFinder.FindTypeScriptDirectory();
		if (tsDir == null)
		{
			Debug.LogError("Failed to find TypeScript~ directory");
			return;
		}

		var generatedTypesPath = Path.Join(tsDir, "src/Shared/Types/Generated.d.ts");

		var ts = CsToTs.Generator.GenerateTypeScript(options, types);
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