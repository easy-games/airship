using FishNet.Component.Prediction;
using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Transporting;
using UnityEngine;

public class PredictedNetworkRigidbody : NetworkBehaviour {
    //Input fields have been removed.
    public struct MoveData : IReplicateData
    {
        //b does nothing, but parameterless ctors are not supported
        //in the current C# version with Unity.
        public MoveData(bool b = false)
        {
            _tick = 0;
        }

        private uint _tick;
        public void Dispose() { }
        public uint GetTick() => _tick;
        public void SetTick(uint value) => _tick = value;
    }

    //The reconcile struct is unchanged from our Getting
    //Started example.
    public struct ReconcileData : IReconcileData
    {
        public RigidbodyState RigidbodyState;

        public ReconcileData(PredictionRigidbody pr)
        {
            RigidbodyState = new RigidbodyState(pr.Rigidbody);
            _tick = 0;
        }

        private uint _tick;
        public void Dispose() { }
        public uint GetTick() => _tick;
        public void SetTick(uint value) => _tick = value;
    }

    public PredictionRigidbody PredictionRigidbody = new();

    private void Awake()
    {
        PredictionRigidbody.Initialize(GetComponent<Rigidbody>());
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

    [Replicate]
    private void Move(MoveData md, ReplicateState state = ReplicateState.Invalid, Channel channel = Channel.Unreliable)
    {
        //If this object is free-moving and uncontrolled then there is no logic.
        //Just let physics do it's thing.	
    }

    //This method is unchanged.
    [Reconcile]
    private void Reconciliation(ReconcileData rd, Channel channel = Channel.Unreliable)
    {
        Rigidbody rb = PredictionRigidbody.Rigidbody;
        rb.SetState(rd.RigidbodyState);
    }

    public override void CreateReconcile()
    {
        ReconcileData rd = new ReconcileData(PredictionRigidbody);
        Reconciliation(rd);
    }
}