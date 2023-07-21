using FishNet.Broadcast;
using UnityEngine;
using UnityEngine.Serialization;

namespace Code.Components.DynamicVariables
{
    public struct DynamicVariablesUpdateStringBroadcast : IBroadcast
    {
        public string collectionKey;
        public string key;
        public string valueString;
    }

    public struct DynamicVariablesUpdateNumberBroadcast : IBroadcast
    {
        public string collectionKey;
        public string key;
        public float valueNumber;
    }

    public struct DynamicVariablesUpdateVector3Broadcast : IBroadcast
    {
        public string collectionKey;
        public string key;
        public Vector3 valueVector3;
    }
}