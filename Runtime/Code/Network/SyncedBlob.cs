using System;
using Assets.Luau;
using Mirror;
using UnityEngine.Serialization;

namespace Code.Network
{
    [LuauAPI]
    public class SyncedBlob: NetworkBehaviour
    {
        public event Action<object, object> OnChanged;
        
        [NonSerialized]
        [SyncVar(hook = nameof(OnNetworkChanged))] public BinaryBlob blob = new BinaryBlob();

        void OnNetworkChanged(BinaryBlob oldBolb, BinaryBlob newBlob)
        {
            this.OnChanged?.Invoke(oldBolb, newBlob);
        }

        public void SetBlob(BinaryBlob blob)
        {
            this.blob = blob;
        }
    }
}