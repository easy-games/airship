using System.Collections.Generic;
using System.IO;
using Code.Bootstrap;
using UnityEngine;

public class LoadedAssetBundle {
	public AssetBundle assetBundle;

	public AirshipPackage airshipPackage;
	public string bundleId;
	public ushort netCollectionId;

	/// <summary>
	/// Example: "shared/resources"
	/// </summary>
	public string assetBundleFile;
	public LoadedAssetBundle(AirshipPackage airshipPackage, string bundleFolder, AssetBundle assetBundle, ushort netCollectionId) {
		this.airshipPackage = airshipPackage;
		this.bundleId = this.airshipPackage.id.ToLower();
		this.assetBundleFile = bundleFolder.ToLower();
		this.assetBundle = assetBundle;
		this.netCollectionId = netCollectionId;
	}
}