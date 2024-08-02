using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

[CreateAssetMenu(fileName = "NetworkPrefabCollection", menuName = "Airship/Create Network Prefab Collection", order = 100)]
public class NetworkPrefabCollection : ScriptableObject {
    public List<Object> networkPrefabs = new();
}