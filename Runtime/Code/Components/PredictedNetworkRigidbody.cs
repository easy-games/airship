using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Transporting;
using GameKit.Dependencies.Utilities;
using UnityEngine;

public class PredictedNetworkRigidbody : NetworkBehaviour {
    //Input fields have been removed.
    public struct ReplicateData : IReplicateData
    {
        public ReplicateData(bool dummy) : this() {}
        private uint _tick;
        public void Dispose() { }
        public uint GetTick() => _tick;
        public void SetTick(uint value) => _tick = value;
    }

    //The reconcile struct is unchanged from our Getting
    //Started example.
    public struct ReconcileData : IReconcileData
    {
        public PredictionRigidbody rigid;

        public ReconcileData(PredictionRigidbody pr)
        {
            rigid = pr;
            _tick = 0;
        }

        private uint _tick;
        public void Dispose() { }
        public uint GetTick() => _tick;
        public void SetTick(uint value) => _tick = value;
    }

    public PredictionRigidbody rigid = new();

    private void Awake()
    {
        rigid = ObjectCaches<PredictionRigidbody>.Retrieve();
        rigid.Initialize(GetComponent<Rigidbody>());
    }

    private void OnDestroy()
    {
        ObjectCaches<PredictionRigidbody>.StoreAndDefault(ref rigid);
    }

    //In this example we do not need to use OnTick, only OnPostTick.
    //Because input is not processed on this object you only
    //need to pass in default for Move, which can safely
    //be done in OnPostTick.
    public override void OnStartNetwork()
    {
        base.TimeManager.OnPostTick += TimeManager_OnPostTick;
    }

    public override void OnStopNetwork()
    {
        base.TimeManager.OnPostTick -= TimeManager_OnPostTick;
    }

    private void TimeManager_OnPostTick()
    {
        Move(default);
        /* The base.IsServer check is not required but does save a little
        * performance by not building the reconcileData if not server. */
        if (IsServerInitialized)
        {
            CreateReconcile();
        }
    }

    public override void CreateReconcile()
    {
        ReconcileData rd = new ReconcileData(rigid);
        Reconciliation(rd);
    }

    [Replicate]
    private void Move(ReplicateData md, ReplicateState state = ReplicateState.Invalid, Channel channel = Channel.Unreliable)
    {
        //If this object is free-moving and uncontrolled then there is no logic.
        //Just let physics do it's thing.	
    }

    //This method is unchanged.
    [Reconcile]
    private void Reconciliation(ReconcileData data, Channel channel = Channel.Unreliable)
    {
        rigid.Reconcile(data.rigid);
    }
}