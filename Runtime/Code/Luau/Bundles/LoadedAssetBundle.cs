using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class LoadedAssetBundle {
	public AssetBundle assetBundle;
	//A fully formed path looks like: "Assets/Game/GameName/Bundles/Client/Resources/TS/TestScripts/TestScript.lua"
	//                                 [           root            ][bundle or alias][    name without ext   ][ext]

	//A full path is required by unity to load the path
	//however we can process different formats to get what the user intended:
	//
	//      1: Start with the bundle alias:   "Client/TS/TestScripts/TestScript.lua"
	//      2: Start with the full bundle:    "Client/Resources/TS/TestScripts/TestScript.lua"

	// public string m_pathRoot; //eg: "Assets/Game/GameName/Bundles"  <- path
	// public string m_name;     //eg: "Client/Resources"  <- actual bundle name in unity asset bundle editor
	// public string m_alias;    //eg: "Client"            <- short name so we dont have to type /resources/ a lot

	public string bundleId;
	public string bundleFolder;
	public bool import;

	// public List<string> m_prefixes = new(); //Because we have a few combinations, we'll do some "StartsWith()" checks to see if they mean us

	/// <summary>
	///
	/// </summary>
	/// <param name="bundleId">Example: "core"</param>
	/// <param name="bundleFolder">Example: "client/resources"</param>
	/// <param name="import"></param>
	public LoadedAssetBundle(string bundleId, string bundleFolder, bool import, AssetBundle assetBundle) {
		this.bundleId = bundleId.ToLower();
		this.bundleFolder = bundleFolder.ToLower();
		this.import = import;
		this.assetBundle = assetBundle;
	}

	public bool PathBelongsToThisAssetBundle(string path)
	{
		if (this.import) {
			return path.StartsWith("imports/" + bundleId + "/" + bundleFolder);
		}

		return path.StartsWith(this.bundleFolder);
	}

	public string FixupPath(string sourcePath)
	{
		Debug.Log("pre fix: " + sourcePath);
		sourcePath = sourcePath.ToLower();

		sourcePath = Path.Combine("assets", "bundles", sourcePath);

		// if (sourcePath.StartsWith(m_pathRoot))
		// {
		// 	return sourcePath;
		// }
		//
		// if (sourcePath.StartsWith(m_name))
		// {
		// 	//Case 2:  "Client/Resources/TS/TestScripts/TestScript.lua"
		// 	//Client/Resources is m_name, the bundle name, so we're okay to use it as-is
		//
		// 	string trimmed = sourcePath.Substring(m_name.Length + 1);
		//
		// 	string bigPath = Path.Combine(m_pathRoot, m_name, trimmed);
		// 	return bigPath;
		// }
		//
		// if (sourcePath.StartsWith(m_alias))
		// {
		// 	//Case 1:"Client/TS/TestScripts/TestScript.lua"
		// 	//'Client' is the alias, needs to be unpacked to its bundle name 'Client/Resources'
		//
		// 	string trimmed = sourcePath.Substring(m_alias.Length + 1);
		//
		// 	string bigPath = Path.Combine(m_pathRoot, m_name, trimmed);
		// 	return bigPath;
		// }

		Debug.Log("post fix: " + sourcePath);
		//Really an error case, we couldn't make a determination
		return sourcePath;
	}
}