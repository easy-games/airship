# Terminology

**Asset** - A source file on disk, typically located in the Project’s Assets folder. This file is imported to a game-ready representation of your Asset internally which can contain multiple Objects.

**Object** - A single Unity serializable unit. All Unity Objects derive from [UnityEngine.Object](https://docs.unity3d.com/ScriptReference/Object.html).  An imported Asset is made up of one or more Objects. For example a ScriptableObject Asset has a single Object, while a Prefab contains a heirarchy of GameObjects, Components and other Unity Objects.

**SubAsset** - An additional Asset that is stored inside an Asset file.  See [AssetDatabase.AddObjectToAsset](https://docs.unity3d.com/ScriptReference/AssetDatabase.AddObjectToAsset.html) and [AssetBundle.LoadAssetWithSubAssets](https://docs.unity3d.com/ScriptReference/AssetBundle.LoadAssetWithSubAssets.html).

**Includes** - The set of Objects from which an Asset is constructed.

**References** - The unique set of Objects that are needed (referenced) by the Includes of an Asset, but not included in the Asset.  For example a Material object inside a Material Asset can reference a Shader object inside a Shader Asset.
