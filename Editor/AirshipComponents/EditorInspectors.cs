using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

internal static class EditorInspectors {
    private static readonly IList allInspectors = null;
	private static readonly FieldInfo inspectorElementEditorField;
	
	private static IEnumerable<EditorWindow> AllInspectors {
		get {
			foreach (var inspector in allInspectors) {
				yield return inspector as EditorWindow;
			}
		}
	}

	static EditorInspectors()
	{
		inspectorElementEditorField = typeof(InspectorElement).GetField("m_Editor", BindingFlags.Instance | BindingFlags.NonPublic);
		var unityEditorAssembly = typeof(EditorWindow).Assembly;
		var inspectorType = unityEditorAssembly.GetType("UnityEditor.InspectorWindow");

		if(inspectorType is null)
		{
			allInspectors = new List<EditorWindow>(GetAllInspectorWindowsFallback());
			return;
		}

		void FetchAllInspectors(out IList allInspectors)
		{
			var allInspectorsField = inspectorType.GetField("m_AllInspectors", BindingFlags.Static | BindingFlags.NonPublic);
			if(allInspectorsField is null)
			{
				allInspectors = new List<EditorWindow>(GetAllInspectorWindowsFallback());
				return;
			}

			allInspectors = allInspectorsField.GetValue(null) as IList;
		}
		
		FetchAllInspectors(out allInspectors);
	}

	internal static IEnumerable<(UnityEditor.Editor, IMGUIContainer)> GetComponentHeaderElementsFromEditorWindowOf(UnityEditor.Editor gameObjectEditor)
	{
		var inspector = GetGameObjectEditorWindow(gameObjectEditor);
		if(inspector == null)
		{
			return Enumerable.Empty<(UnityEditor.Editor, IMGUIContainer)>();
		}

		return GetAllComponentHeaders(inspector);
	}
	
	private static EditorWindow GetGameObjectEditorWindow(UnityEditor.Editor gameObjectEditor) {
		return AllInspectors.FirstOrDefault(window => HasGameObjectEditor(window.rootVisualElement, gameObjectEditor));
	}

	private static IEnumerable<(UnityEditor.Editor, IMGUIContainer)> GetAllComponentHeaders(EditorWindow inspector)
	{
		return GetAllComponentHeaders(inspector.rootVisualElement);
	}

	private static bool HasGameObjectEditor(VisualElement parentElement, UnityEditor.Editor gameObjectEditor)
	{
		if (parentElement is null) return false;
		if (parentElement.GetType().Name != "EditorElement")
			return parentElement.Children().Any(child => HasGameObjectEditor(child, gameObjectEditor));
		
		foreach(var child in parentElement.Children()) {
			if (child is not InspectorElement inspectorElement) continue;
			
			var editor = inspectorElementEditorField.GetValue(inspectorElement) as UnityEditor.Editor;
			if(editor == gameObjectEditor) return true;
			if(editor != null && editor.target as GameObject != null) return false;
		}
		
		return parentElement.Children().Any(child => HasGameObjectEditor(child, gameObjectEditor));
	}

	private static IEnumerable<(UnityEditor.Editor, IMGUIContainer)> GetAllComponentHeaders(VisualElement parentElement)
	{
		if (parentElement is null)
		{
			yield break;
		}

		if (parentElement.GetType().Name == "EditorElement")
		{
			IMGUIContainer header = null;
			foreach(var child in parentElement.Children())
			{
				if(TryGetComponentHeader(child, out var headerOrNull))
				{
					header = headerOrNull;
				}
				else if(header is null)
				{
					continue;
				}

				if (child is not InspectorElement inspectorElement) continue;
				var editor = inspectorElementEditorField.GetValue(inspectorElement) as UnityEditor.Editor;

				if(editor != null && editor.target as Component != null)
				{
					yield return (editor, header);
				}

				break;
			}
		}

		foreach(var child in parentElement.Children())
		{
			foreach(var childHeader in GetAllComponentHeaders(child))
			{
				yield return childHeader;
			}
		}
	}
	
	private static bool TryGetComponentHeader(VisualElement visualElement, out IMGUIContainer header)
	{
		if (visualElement is IMGUIContainer imguiContainer && visualElement.name.EndsWith("Header", StringComparison.Ordinal))
		{
			header = imguiContainer;
			return true;
		}

		header = default;
		return false;
	}

	private static IEnumerable<EditorWindow> GetAllInspectorWindowsFallback() {
		return Resources.FindObjectsOfTypeAll<EditorWindow>()
			.Where(window => window.GetType().Name == "InspectorWindow");
	}
}
