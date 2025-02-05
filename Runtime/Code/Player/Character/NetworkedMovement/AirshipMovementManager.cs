using System;
using Code.Player.Character.MovementSystem.NetworkedMovement;
using Code.Player.Character.Net;
using Code.Player.Character.NetworkedMovement.BasicTest;
using Mirror;
using UnityEngine;

namespace Code.Player.Character.NetworkedMovement
{

    [RequireComponent(typeof(NetworkIdentity))]
    public abstract class AirshipMovementManager<MovementSystem, State, Input>: NetworkBehaviour where State : StateSnapshot where Input : InputCommand where MovementSystem : NetworkedMovement<State, Input>
       
    {
        // Inspector settings
        public MovementSystem movementSystem;
        public int interpolationBufferSize = 3;
        public int clientInputRate = 20;
        public int serverSnapshotRate = 20;
        
        // todo: prediction
        private bool serverAuth = false;

        // Command number tracking for server and client
        private int serverLastProcessedCommandNumber = 0;
        private int clientCommandNumber = 0;

        // interpolation implementation fields.
        private State[] interpolationBuffer;
        private double lastReceivedSnapshotTime = 0;
        
        // Send rate tracking fields
        private double lastClientSend = 0;
        private double lastServerSend = 0;
        
        // Networking actions to be used by subclass;
        protected Action<State> OnClientReceiveSnapshot;
        protected Action<State> OnServerReceiveSnapshot;
        protected Action<Input> OnServerReceiveInput;
        
        public abstract void SendClientInputToServer(Input input);
        public abstract void SendClientSnapshotToServer(State snapshot);
        public abstract void SendServerSnapshotToClients(State snapshot);

        private void Awake()
        {
            // prediction = gameObject.GetComponent<AirshipInputPrediction>();
            // if (prediction != null) serverAuth = true;

            this.interpolationBuffer = new State[this.interpolationBufferSize + 1];
                
            this.OnClientReceiveSnapshot += ClientReceiveSnapshot;
            this.OnServerReceiveSnapshot += ServerReceiveSnapshot;
            this.OnServerReceiveInput += ServerReceiveInputCommand;
        }

        private void Update()
        {
            // Unowned clients should interpolate the observed character
            if (isClient && !isOwned)
            {
                this.Interpolate();
            }
        }

        /**
         * Handles triggering interpolation in the movement system for observers
         */
        private void Interpolate()
        {
            //Don't process unless we have received data
            if(this.interpolationBuffer[0] == null || this.interpolationBuffer[1] == null){
                print("Not enough observer states");
                return;
            }

            var renderBuffer = (float) (this.interpolationBufferSize - 1) / this.serverSnapshotRate;
            var clientTime = (float)NetworkTime.time - renderBuffer;
            State prevState = null;
            State nextState = null;
           
            //Find the states to lerp to based on our current time
            for(int i = 0; i < this.interpolationBuffer.Length; i++) {
                var state = this.interpolationBuffer[i];
                if (state == null) break;

                print("At time: " + clientTime + " Checkign state: " + i + " time: " + state.time);
                if(state.time < clientTime) {
                    prevState = state;
                } else if (state.time > clientTime) {
                    nextState = state;
                    break;
                }
            }

            // No state in the past
            if(prevState == null || nextState == null){
                print("no prev or next state");
                return;
            }

            // How far along are we in this interp?
            var timeDelta = (clientTime - prevState.time) / (nextState.time - prevState.time);
            this.movementSystem.Interpolate((float) timeDelta, prevState, nextState);
        }

        private void FixedUpdate()
        {
            // We are the client and we are authoritative. Report our state to the server.
            if (isClient && isOwned && !serverAuth)
            {
                // Read input from movement system
                var command = this.movementSystem.GetCommand(clientCommandNumber);
                // Tick movement system
                this.movementSystem.Tick(command, false);
                // Report snapshot from movement system to server.
                var state = this.movementSystem.GetCurrentState(clientCommandNumber, NetworkTime.time);
                Debug.Log("client state:" + state);
                clientCommandNumber++;

                if (this.lastClientSend < NetworkTime.time - (1f / this.clientInputRate))
                {
                    this.lastClientSend = NetworkTime.time;
                    this.SendClientSnapshotToServer(state);
                }
            }
            
            // We are the server, and we are the authority. Expect commands to come from owning client.
            // Process those commands and distribute and authoritative answer.
            if (isServer && serverAuth)
            {
               // read pending commands
               // tick commands and store history if lag compensating
               // send snapshot to client
               // send snapshot to observers
            }
           
            // We are the server, but the client is authoritative. Simply update to match and forward to observers.
            if (isServer && !serverAuth)
            {
               // read current state
               var state = this.movementSystem.GetCurrentState(serverLastProcessedCommandNumber, NetworkTime.time);
               // send snapshot to observers

               if (this.lastServerSend < NetworkTime.time - (1f / this.serverSnapshotRate))
               {
                   this.lastServerSend = NetworkTime.time;
                   this.SendServerSnapshotToClients(state);
               }
            }

            // We are the client, but the server is athoritative. Send our input commands and handle prediction
            if (isClient && isOwned && serverAuth) {
                // process snapshots from server? might need to go in update instead?
                // read input from movement system
                // enqueue command to history if predicting
                // tick movement to get new state
                
                
                // var command = movementSystem.GetCommand();
                // if (serverAuth)
                // {
                //     prediction.history.Enqueue(command);
                // }
                // movementSystem.Tick(command, false);
            }
           
        }
        
        private void ClientReceiveSnapshot(State state)
        {
            Debug.Log("Client receive snapshot" + state);
            // The client is a non-authoritative owner and should update
            // their local state with the authoritative state from the server.
            if (isOwned && serverAuth)
            {
                // Queue as base state for new predictions
                // Perhaps trigger a resim immediately?
                return;
            }

            // This client is an authoritative owner and should ignore the snapshot,
            // it is only meant for observers (todo, ignore owner in this case?)
            if (isOwned && !serverAuth)
            {
                return;
            }
            
            // This client is an observer and should store and interpolate the snapshot.
            if (!isOwned)
            {
                if (lastReceivedSnapshotTime > state.time)
                {
                    print("Ignoring out of order snapshot");
                    return;
                }
                lastReceivedSnapshotTime = state.time;

                // Shift the buffer and put in the new snapshot.
                for(int i=0; i < this.interpolationBuffer.Length - 1; i++)
                {
                    interpolationBuffer[i] = interpolationBuffer[i+1];
                }
                interpolationBuffer[this.interpolationBuffer.Length - 1] = state;
            }
        }

        private void ServerReceiveInputCommand(Input command)
        {
            // This should only occur if the server is authoritative.
            if (!serverAuth) return;
            
            // Queue the command for processing on the next tick
        }
        
        private void ServerReceiveSnapshot(State snapshot)
        {
            Debug.Log("Server receive snapshot" + snapshot.lastProcessedCommand + " data: " + snapshot.ToString());
            // This should only occur if the server is not authoritative.
            if (serverAuth) return;
            
            // The server is receiving an update from the authoritative client owner.
            // Update our state so we can distribute it to all observers.
            if (serverLastProcessedCommandNumber >= snapshot.lastProcessedCommand)
            {
                // We already have newer state, so this should be ignored.
                return;
            }

            this.serverLastProcessedCommandNumber = snapshot.lastProcessedCommand;
            this.movementSystem.SetCurrentState(snapshot);
        }
    }
}