// DISABLED FOR NOW

/*
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using UnityEditor;
using UnityEngine;

namespace Editor.Util {
	public static class LuauReflectionGenerator {
		private const string FileName = "airship_luau_reflection.xml";
		
		private static readonly HashSet<string> Assemblies = new() {
			"UnityEngine",
			"UnityEngine.AIModule",
			"UnityEngine.ARModule",
			"UnityEngine.AccessibilityModule",
			"UnityEngine.AnimationModule",
			"UnityEngine.AssetBundleModule",
			"UnityEngine.AudioModule",
			"UnityEngine.ClothModule",
			"UnityEngine.CoreModule",
			"UnityEngine.GameCenterModule",
			"UnityEngine.GridModule",
			"UnityEngine.HierarchyCoreModule",
			"UnityEngine.IMGUIModule",
			"UnityEngine.ImageConversionModule",
			"UnityEngine.InputModule",
			"UnityEngine.InputForUIModule",
			"UnityEngine.InputLegacyModule",
			"UnityEngine.JSONSerializeModule",
			"UnityEngine.LocalizationModule",
			"UnityEngine.MarshallingModule",
			"UnityEngine.MultiplayerModule",
			"UnityEngine.ParticleSystemModule",
			"UnityEngine.PhysicsModule",
			"UnityEngine.Physics2DModule",
			"UnityEngine.ProfilerModule",
			"UnityEngine.PropertiesModule",
			"UnityEngine.ScreenCaptureModule",
			"UnityEngine.SharedInternalsModule",
			"UnityEngine.SpriteMaskModule",
			"UnityEngine.SpriteShapeModule",
			"UnityEngine.StreamingModule",
			"UnityEngine.SubstanceModule",
			"UnityEngine.SubsystemsModule",
			"UnityEngine.TLSModule",
			"UnityEngine.TerrainModule",
			"UnityEngine.TerrainPhysicsModule",
			"UnityEngine.TextCoreFontEngineModule",
			"UnityEngine.TextCoreTextEngineModule",
			"UnityEngine.TextRenderingModule",
			"UnityEngine.TilemapModule",
			"UnityEngine.UIModule",
			"UnityEngine.UIElementsModule",
			"UnityEngine.UmbraModule",
			"UnityEngine.UnityAnalyticsModule",
			"UnityEngine.UnityAnalyticsCommonModule",
			"UnityEngine.UnityConnectModule",
			"UnityEngine.UnityCurlModule",
			"UnityEngine.UnityTestProtocolModule",
			"UnityEngine.UnityWebRequestModule",
			"UnityEngine.UnityWebRequestAssetBundleModule",
			"UnityEngine.UnityWebRequestAudioModule",
			"UnityEngine.UnityWebRequestTextureModule",
			"UnityEngine.UnityWebRequestWWWModule",
			"UnityEngine.VFXModule",
			"UnityEngine.VRModule",
			"UnityEngine.VehiclesModule",
			"UnityEngine.VideoModule",
			"UnityEngine.VirtualTexturingModule",
			"UnityEngine.WindModule",
			"UnityEngine.XRModule",
			"Agones",
			"Airship.Bundles",
			"Airship.Core",
			"Airship.CoroutineExtensions",
			"Airship.DependencyInterfaces",
			"Airship.DevConsole",
			"Airship.DynamicVariables",
			"Airship.GameConfig",
			"Airship.LuauCore",
			"Airship.PoolManager",
			"Airship.RemoteConsole",
			"Airship.TextureLoader",
			"Airship.Util",
			"Airship.VoxelWorld",
			"AloneSoft.VeryAnimation",
			"AppleAuth",
			"Cdm.Authentication",
			"com.rlabrecque.steamworks.net",
			"CsToTs",
			"Easy.Airship",
			"LeTai.TrueShadow",
			"LuauAPI",
			"Mirror.Authenticators",
			"Mirror.CompilerSymbols",
			"Mirror.Components",
			"Mirror",
			"Mirror.Examples",
			"Mirror.Transports",
			"NativeGallery.Runtime",
			"nl.elraccoone.tweens",
			"Nobi.UiRoundedCorners",
			"PPv2URPConverters",
			"RestClient",
			"SimpleWebTransport",
			"SocketIO",
			"StandaloneFileBrowser",
			"Tayx.Graphy",
			"Telepathy",
			"Unity.AI.Navigation",
			"Unity.AI.Navigation.Updater",
			"Unity.Animation.Rigging",
			"Unity.Animation.Rigging.DocCodeExamples",
			"Unity.Burst.CodeGen",
			"Unity.Burst",
			"Unity.Collections.BurstCompatibilityGen",
			"Unity.Collections.CodeGen",
			"Unity.Collections",
			"Unity.Collections.DocCodeSamples",
			"Unity.InputSystem",
			"Unity.InputSystem.ForUI",
			"Unity.InputSystem.TestFramework",
			"Unity.Mathematics",
			"Unity.MemoryProfiler",
			"Unity.Multiplayer.Playmode.Common.Runtime",
			"Unity.Multiplayer.Playmode",
			"Unity.Rendering.LightTransport.Runtime",
			"Unity.TextMeshPro",
			"Unity.Timeline",
			"Unity.VisualEffectGraph.Runtime",
			"Unity.VisualScripting.Core",
			"Unity.VisualScripting.Flow",
			"Unity.VisualScripting.State",
			"UnityEngine.TestRunner",
			"UnityEngine.UI",
			"nunit.framework",
			"ReportGeneratorMerged",
			"Mirror.BouncyCastle.Cryptography",
			"Ookii.Dialogs",
			"log4net",
			"Newtonsoft.Json",
			"Handlebars",
			"Accessibility",
		};
			
		[MenuItem("Airship/Luau/Generate Reflection Data")]
		private static void GenerateReflectionData() {
			var filepath = Path.Join(Path.GetTempPath(), FileName);
			using var fs = new FileStream(filepath, FileMode.Create);

			var writerSettings = new XmlWriterSettings {
				Indent = true,
				Encoding = Encoding.UTF8,
				OmitXmlDeclaration = true,
				NewLineChars = "\n",
			};

			using var writer = XmlWriter.Create(fs, writerSettings);

			writer.WriteStartElement("reflection");
			
			foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) {
				var fullName = assembly.FullName;
				
				var commaIdx = fullName.IndexOf(',');
				if (commaIdx == -1) {
					continue;
				}

				var name = fullName[..commaIdx];
				if (!Assemblies.Contains(name)) {
					continue;
				}
				
				var types = assembly.GetTypes();
				foreach (var t in types) {
					if (!t.IsClass) continue;
					
					writer.WriteStartElement("cls");
					writer.WriteAttributeString("n", t.Name);
					
					var baseType = t.BaseType;
					if (baseType != null && baseType != typeof(object)) {
						writer.WriteAttributeString("base", baseType.Name);
					}
						
					var methods = t.GetMethods();
					foreach (var method in methods) {
						if (method.DeclaringType != t || !method.IsPublic || method.IsSpecialName) continue;
						writer.WriteStartElement("method");
						writer.WriteAttributeString("n", method.Name);
						writer.WriteAttributeString("ret", method.ReturnType.Name);
						if (method.IsStatic) {
							writer.WriteAttributeString("static", "true");
						}
						var parameters = method.GetParameters();
						foreach (var parameter in parameters) {
							writer.WriteStartElement("param");
							writer.WriteAttributeString("t", parameter.ParameterType.Name);
							writer.WriteAttributeString("n", parameter.Name);
							writer.WriteAttributeString("pos", parameter.Position.ToString());
							if (parameter.IsOptional) {
								writer.WriteAttributeString("opt", parameter.IsOptional.ToString());
							}
							writer.WriteEndElement();
						}
						writer.WriteEndElement();
					}

					var fields = t.GetFields();
					foreach (var field in fields) {
						if (field.DeclaringType != t || !field.IsPublic) continue;
						writer.WriteStartElement("field");
						writer.WriteAttributeString("n", field.Name);
						writer.WriteAttributeString("t", field.FieldType.Name);
						if (field.IsStatic) {
							writer.WriteAttributeString("static", "true");
						}
						writer.WriteEndElement();
					}

					var properties = t.GetProperties();
					foreach (var property in properties) {
						if (property.DeclaringType != t) continue;
						writer.WriteStartElement("property");
						writer.WriteAttributeString("n", property.Name);
						writer.WriteAttributeString("t", property.PropertyType.Name);
						if (property.IsStatic()) {
							writer.WriteAttributeString("static", "true");
						}
						writer.WriteEndElement();
					}
					
					writer.WriteEndElement();
				}
			}
			
			writer.WriteEndElement();
			
			Debug.Log($"Luau Reflection File: <a href=\"{filepath}\">{filepath}</a>");
		}
	}
}
*/
