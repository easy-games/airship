using FishNet.Component.Prediction;
using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Transporting;
using UnityEngine;

/*
* 
* See TransformPrediction.cs for more detailed notes.
* 
*/

namespace FishNet.PredictionV2
{
    /* THIS CLASS IS CURRENTLY USED FOR TESTING AND IS NOT CONSIDERED
     * AN EXAMPLE TO FOLLOW. */
    /* THIS CLASS IS CURRENTLY USED FOR TESTING AND IS NOT CONSIDERED
     * AN EXAMPLE TO FOLLOW. */
    /* THIS CLASS IS CURRENTLY USED FOR TESTING AND IS NOT CONSIDERED
     * AN EXAMPLE TO FOLLOW. */
    /* THIS CLASS IS CURRENTLY USED FOR TESTING AND IS NOT CONSIDERED
     * AN EXAMPLE TO FOLLOW. */

    public class RigidbodyPredictionV2 : NetworkBehaviour
    {
        public struct MoveData : IReplicateData
        {
            public bool Jump;
            public float Horizontal;
            public float Vertical;
            public Vector3 OtherImpulseForces;
            public MoveData(bool jump, float horizontal, float vertical, Vector3 otherImpulseForces)
            {
                Jump = jump;
                Horizontal = horizontal;
                Vertical = vertical;
                OtherImpulseForces = otherImpulseForces;
                _tick = 0;
            }

            private uint _tick;
            public void Dispose() { }
            public uint GetTick() => _tick;
            public void SetTick(uint value) => _tick = value;
        }

        public struct ReconcileData : IReconcileData
        {
            public RigidbodyState RigidbodyState;
            public PredictionRigidbody PredictionRigidbody;
            private uint _tick;

            public ReconcileData(PredictionRigidbody pr) : this()
            {
                PredictionRigidbody = pr;
                RigidbodyState = new RigidbodyState(PredictionRigidbody.Rigidbody);
            }

            public void Dispose() { }
            public uint GetTick() => _tick;
            public void SetTick(uint value) => _tick = value;
        }

        [SerializeField]
        private float _jumpForce = 15f;
        [SerializeField]
        private float _moveRate = 15f;

        public PredictionRigidbody PredictionRigidbody = new();
        private bool _jump;

        private void Update()
        {
            if (base.IsOwner)
            {
                if (Input.GetKeyDown(KeyCode.Space))
                    _jump = true;
            }
        }

        public override void OnStartNetwork()
        {
            PredictionRigidbody.Initialize(GetComponent<Rigidbody>());
            base.TimeManager.OnTick += TimeManager_OnTick;
            base.TimeManager.OnPostTick += TimeManager_OnPostTick;
        }

        public override void OnStopNetwork()
        {

            base.TimeManager.OnTick -= TimeManager_OnTick;
            base.TimeManager.OnPostTick -= TimeManager_OnPostTick;
        }


        private void TimeManager_OnTick()
        {
            Move(BuildMoveData());
        }

        private void TimeManager_OnPostTick()
        {
            CreateReconcile();
        }

        private MoveData BuildMoveData()
        {
            if (!IsOwner && Owner.IsValid)
                return default;

            float horizontal = Input.GetAxisRaw("Horizontal");
            float vertical = Input.GetAxisRaw("Vertical");
            MoveData md = new MoveData(_jump, horizontal, vertical, Vector3.zero);
            _jump = false;


            if (base.IsServerInitialized && base.IsOwner)
            {
                _moveCountRemaining--;
                if (_moveCountRemaining > 25)
                {
                    md.Horizontal = (1f * _moveDirection);
                }
                if (_moveCountRemaining == 0)
                {
                    md.Jump = true;
                }
                else if (_moveCountRemaining < -40)
                {
                    _moveCountRemaining = RESET_MOVE_COUNT;
                    _moveDirection *= -1f;
                }
            }


            return md;
        }

        private int _moveCountRemaining = STARTING_MOVE_COUNT;
        private const int STARTING_MOVE_COUNT = 150;
        private const int RESET_MOVE_COUNT = 100;
        private float _moveDirection = 1f;

        public static float JumpedTime;
        private int _replayedCreated = 0;
        private int _totalRun = 0;
        private MoveData _lastMd;
        [Replicate]
        private void Move(MoveData md, ReplicateState state = ReplicateState.Invalid, Channel channel = Channel.Unreliable)
        {
            if (!base.IsOwner)
            {
                _totalRun++;
                if (md.Horizontal != 0f)
                    _replayedCreated++;

                if (!IsServerInitialized)
                {
                    if (state.IsCreated())
                    {
                        _lastMd = md;
                    }
                    else
                    {
                        //PredictionRigidbody.Velocity(Vector3.zero);
                        //_lastMd.Horizontal *= 0.9f;
                        //if (!_lastMd.Equals(default))
                        //    md.Horizontal = _lastMd.Horizontal;
                    }
                }
            }

            if (base.IsOwner && base.IsServerStarted)
            {
                float nowY = transform.position.y;
                if (nowY > _lastY)
                {
                    if (_yLogsRemaining > 0)
                    {
                        _yLogsRemaining--;
                        Debug.Log($"Y change. Diff {(nowY - _lastY).ToString("0.000")}. prev {_lastY.ToString("0.000")}, next {nowY.ToString("0.000")}");
                    }
                }
                else if (nowY == -1f)
                {
                    _yLogsRemaining = 4;
                }
                _lastY = nowY;
            }
            //PredictionRigidbody.Velocity(new Vector3(md.Horizontal * _moveRate, PredictionRigidbody.Rigidbody.velocity.y, 0f));
            if (md.Jump)
            {
                //if (!IsOwner)
                    Debug.Log($"JUMPING {md.GetTick()}");
                if (!IsOwner)
                    AdaptiveLocalTransformTickSmoother.JumpedTime = Time.unscaledTime;
              //  PredictionRigidbody.AddForce(new Vector3(0f, _jumpForce, 0f), ForceMode.Impulse);
            }
            //Vector3 forces = new Vector3(md.Horizontal, 0f, md.Vertical) * _moveRate;
            //forces += Physics.gravity * 3f;

            //PredictionRigidbody.AddForce(forces);
            PredictionRigidbody.Velocity(new Vector3(md.Horizontal, 0f, 0f) * _moveRate / 4);
            PredictionRigidbody.Simulate();
        }
        private float _lastY = -10f;
        private int _yLogsRemaining = 4;

        public override void CreateReconcile()
        {
            /* The base.IsServer check is not required but does save a little
            * performance by not building the reconcileData if not server. */
            if (IsServerStarted)
            {
                ReconcileData rd = new ReconcileData(PredictionRigidbody);
                Reconciliation(rd);
            }
        }

        [Reconcile]
        private void Reconciliation(ReconcileData rd, Channel channel = Channel.Unreliable)
        {
            if (!base.IsOwner)
            {
                //Debug.Log($"ReplayedCreated {_replayedCreated}. TotalRun {_totalRun}");
                _totalRun = 0;
                _replayedCreated = 0;
            }
            PredictionRigidbody.Rigidbody.SetState(rd.RigidbodyState);
            PredictionRigidbody.Reconcile(rd.PredictionRigidbody);
        }

    }

}