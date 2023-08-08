using System.Collections.Generic;
using System.IO;
using Code.Bootstrap;
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

	public AirshipPackage airshipPackage;
	public string bundleId;
	public string assetBundleFile;
	public LoadedAssetBundle(AirshipPackage airshipPackage, string bundleFolder, AssetBundle assetBundle) {
		this.airshipPackage = airshipPackage;
		this.bundleId = this.airshipPackage.id.ToLower();
		this.assetBundleFile = bundleFolder.ToLower();
		this.assetBundle = assetBundle;
	}

	public bool PathBelongsToThisAssetBundle(string path)
	{
		if (this.airshipPackage.packageType == AirshipPackageType.Package) {
			return path.StartsWith("imports/" + bundleId + "/" + assetBundleFile);
		}

		return path.StartsWith(this.assetBundleFile);
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