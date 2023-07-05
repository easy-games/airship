using System.Collections;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;

namespace ReferenceBuilder {
	[CreateAssetMenu(fileName = "ReferenceBuilderAsset", menuName = "EasyGG/ReferenceBuilder/ReferenceBuilderAsset",
		order = 1)]
	public class ReferenceBuilderAsset : ScriptableObject {
		public string referenceId = "PackageID";
		[ItemCanBeNull]
		public List<KeyValueReference<List<KeyValueReference<Object>>>> bundles = new ();
	}
}