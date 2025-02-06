using System;
using System.Collections.Generic;
using Code.Player.Character.Net;
using Mirror;
using RSG.Promises;
using UnityEngine;

namespace Code.Player.Character.NetworkedMovement
{
    [RequireComponent(typeof(NetworkIdentity))]
    public abstract class AirshipMovementManager<MovementSystem, State, Input> : NetworkBehaviour
        where State : StateSnapshot where Input : InputCommand where MovementSystem : NetworkedMovement<State, Input>

    {
        // Inspector settings
        public MovementSystem movementSystem;
        public int maxServerCommandBuffer = 4;
        public int interpolationBufferSize = 3;
        public int clientInputRate = 30;
        public int serverSnapshotRate = 20;
        public bool serverAuth = false;

        // Command number tracking for server and client
        private int serverLastProcessedCommandNumber = 0;
        private int clientCommandNumber = 0;

        // interpolation implementation fields.
        private State[] interpolationBuffer;
        private double lastReceivedSnapshotTime = 0;

        // Send rate tracking fields
        private double lastClientSend = 0;
        private double lastServerSend = 0;

        // Server processing for commands
        private Input lastProcessedCommand;
        private LinkedList<Input> serverCommandBuffer = new LinkedList<Input>();

        // Client processing for input prediction
        private Queue<Input> clientInputHistory = new Queue<Input>();
        private Queue<State> clientPredictedHistory = new Queue<State>();
        private int clientLastConfirmedCommand = 0;
        private int clientLastStoredCommandNumber = 0;

        // Networking actions to be used by subclass;
        protected Action<State> OnClientReceiveSnapshot;
        protected Action<State> OnServerReceiveSnapshot;
        protected Action<Input> OnServerReceiveInput;

        public abstract void SendClientInputToServer(Input input);
        public abstract void SendClientSnapshotToServer(State snapshot);
        public abstract void SendServerSnapshotToClients(State snapshot);

        private void Awake()
        {
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

        // Late update runs after the physics world has been updated. We use this
        // to create our snapshots.
        private void LateUpdate()
        {
            // We are a client and we are not authoritative. Enqueue history snapshots
            // for input prediction processing.
            if (isClient && isOwned && serverAuth)
            {
                // Ensure that we only store the command once since it represents a single
                // tick and we may run LateUpdate more than once per fixed update.
                if (this.clientCommandNumber != this.clientLastStoredCommandNumber)
                {
                    var state = this.movementSystem.GetCurrentState(clientCommandNumber, NetworkTime.time);
                    Debug.Log("Processing command " + state.lastProcessedCommand + " resulted in " + state);
                    this.clientPredictedHistory.Enqueue(state);
                    this.clientLastStoredCommandNumber = state.lastProcessedCommand;
                }
            }

            // We are a client and we are authoritative.
            if (isClient && isOwned && !serverAuth)
            {
                // Report snapshot from movement system to server.
                if (this.lastClientSend < NetworkTime.time - (1f / this.clientInputRate))
                {
                    var state = this.movementSystem.GetCurrentState(clientCommandNumber, NetworkTime.time);
                    this.lastClientSend = NetworkTime.time;
                    this.SendClientSnapshotToServer(state);
                }
            }


            // We are the server and we are authoritative.
            if (isServer && serverAuth)
            {
                // TODO: in the future, lag compensation may need to be recorded here.
                // Depends on if we are able to use the default rigidbody lag compensation from mirror

                if (this.lastServerSend < NetworkTime.time - (1f / this.serverSnapshotRate))
                {
                    // get snapshot to send to clients
                    var state = this.movementSystem.GetCurrentState(this.lastProcessedCommand.commandNumber,
                        NetworkTime.time);
                    Debug.Log("Processing commands up to " + state.lastProcessedCommand + " resulted in " + state);

                    this.lastServerSend = NetworkTime.time;
                    this.SendServerSnapshotToClients(state);
                }
            }

            // We are the server, but the client is authoritative. Simply forward to observers.
            if (isServer && !serverAuth)
            {
                if (this.lastServerSend < NetworkTime.time - (1f / this.serverSnapshotRate))
                {
                    // read current state
                    var state = this.movementSystem.GetCurrentState(serverLastProcessedCommandNumber,
                        NetworkTime.time);
                    this.lastServerSend = NetworkTime.time;
                    this.SendServerSnapshotToClients(state);
                }
            }
        }

        /**
         * Handles triggering interpolation in the movement system for observers
         */
        private void Interpolate()
        {
            //Don't process unless we have received data
            if (this.interpolationBuffer[0] == null || this.interpolationBuffer[1] == null)
            {
                print("Not enough observer states");
                return;
            }

            var renderBuffer = (float)(this.interpolationBufferSize - 1) / this.serverSnapshotRate;
            var clientTime = (float)NetworkTime.time - renderBuffer;
            State prevState = null;
            State nextState = null;

            //Find the states to lerp to based on our current time
            for (int i = 0; i < this.interpolationBuffer.Length; i++)
            {
                var state = this.interpolationBuffer[i];
                if (state == null) break;

                // print("At time: " + clientTime + " Checkign state: " + i + " time: " + state.time);
                if (state.time < clientTime)
                {
                    prevState = state;
                }
                else if (state.time > clientTime)
                {
                    nextState = state;
                    break;
                }
            }

            // No state in the past
            if (prevState == null || nextState == null)
            {
                print("no prev or next state");
                return;
            }

            // How far along are we in this interp?
            var timeDelta = (clientTime - prevState.time) / (nextState.time - prevState.time);
            this.movementSystem.Interpolate((float)timeDelta, prevState, nextState);
        }

        private void FixedUpdate()
        {
            // We are the client and we are authoritative. Report our state to the server.
            if (isClient && isOwned && !serverAuth)
            {
                clientCommandNumber++;

                // Read input from movement system
                var command = this.movementSystem.GetCommand(clientCommandNumber, NetworkTime.time);
                // Tick movement system
                this.movementSystem.Tick(command, false);
            }

            // We are the server, and we are the authority. Expect commands to come from owning client.
            // Process those commands and distribute and authoritative answer.
            if (isServer && serverAuth)
            {
                // Attempt to get a new command out of the buffer.
                var commandEntry = this.serverCommandBuffer.First;

                // Todo: we could use the last command for a few frames if we run out of buffer.
                // We would only want to do this for a short time though since it's not a real client
                // command and will make people run forward forever etc. if they crash

                if (commandEntry != null)
                {
                    var command = this.serverCommandBuffer.First.Value;
                    this.serverCommandBuffer.RemoveFirst();
                    this.lastProcessedCommand = command;

                    // tick commands and todo: store history if lag compensating
                    this.movementSystem.Tick(command, false);
                }
            }

            // We are the client and the server is authoritative. Send our input commands and handle prediction
            if (isClient && isOwned && serverAuth)
            {
                clientCommandNumber++;

                // read input from movement system
                var input = this.movementSystem.GetCommand(this.clientCommandNumber, NetworkTime.time);

                // enqueue command to history if predicting
                this.clientInputHistory.Enqueue(input);

                // tick movement to get new state
                this.movementSystem.Tick(input, false);

                if (this.lastClientSend < NetworkTime.time /*- (1f / this.clientInputRate)*/)
                {
                    this.lastClientSend = NetworkTime.time;
                    this.SendClientInputToServer(input);
                }
            }
        }

        /**
         * Uses a server snapshot to reconcile client prediction with the actual authoritative
         * server history.
         */
        private void ReconcileInputHistory(State state)
        {
            // Clear out input history that has been processed by the server. Use <=
            while (this.clientInputHistory.TryPeek(out Input input) &&
                   input.commandNumber <= state.lastProcessedCommand)
            {
                this.clientInputHistory.Dequeue();
            }

            // Clear out all non-authoritative state up to our new current authoritative state. Use <=
            State clientPredictedState = null;
            while (this.clientPredictedHistory.TryPeek(out State predictedState) &&
                   predictedState.lastProcessedCommand <= state.lastProcessedCommand)
            {
                clientPredictedState = this.clientPredictedHistory.Dequeue();
            }

            // Check if our predicted state matches up with our new authoritative state.
            if (clientPredictedState != null && clientPredictedState.CompareWithMargin(0f, state))
            {
                // If it does, we can just return since our predictions have all been correct so far.
                return;
            }

            // If we got here, a resim is required since our predicted state was inaccurate.
            Debug.Log("Resim required for command " + state.lastProcessedCommand + ". Predicted state: " +
                      clientPredictedState + " Server state: " + state);

            // Set the movement state to be based on the last received state from the server.
            this.movementSystem.SetCurrentState(state);

            // Clear prediction history since we need to rebuild it.
            this.clientPredictedHistory.Clear();

            // Re-predict the input that has not yet been confirmed based on the received state.
            this.clientInputHistory.Each((command) =>
            {
                // Ok this is actually a problem because we are resiming but storing state
                // from before the physics is applied... We will need to fix this somehow
                this.movementSystem.Tick(command, true);
                var state = this.movementSystem.GetCurrentState(command.commandNumber, command.time);
                Debug.Log(("Resimed command " + command + " resulted in " + state));
                this.clientPredictedHistory.Enqueue(state);
            });
        }

        private void ClientReceiveSnapshot(State state)
        {
            if (lastReceivedSnapshotTime > state.time)
            {
                print("Ignoring out of order snapshot");
                return;
            }

            lastReceivedSnapshotTime = state.time;

            // Debug.Log("Client receive snapshot" + state);
            // The client is a non-authoritative owner and should update
            // their local state with the authoritative state from the server.
            if (isOwned && serverAuth)
            {
                this.ReconcileInputHistory(state);
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
                // Shift the buffer and put in the new snapshot.
                for (int i = 0; i < this.interpolationBuffer.Length - 1; i++)
                {
                    interpolationBuffer[i] = interpolationBuffer[i + 1];
                }

                interpolationBuffer[this.interpolationBuffer.Length - 1] = state;
            }
        }

        private void ServerReceiveInputCommand(Input command)
        {
            // This should only occur if the server is authoritative.
            if (!serverAuth) return;

            if (this.serverCommandBuffer.Last != null && this.serverCommandBuffer.Last.Value != null &&
                this.serverCommandBuffer.Last.Value.commandNumber > command.commandNumber)
            {
                // Command was received out of order. Ignore it for now.
                Debug.Log("Received out of order command");
                return;
            }

            // Reject commands if our buffer is full.
            // todo: We could also do something like sv_maxunlag where we process more than
            // one command in a tick to catch up and make room in the buffer.
            if (this.serverCommandBuffer.Count > this.maxServerCommandBuffer)
            {
                return;
            }

            // Queue the command for processing
            this.serverCommandBuffer.AddLast(command);
        }

        private void ServerReceiveSnapshot(State snapshot)
        {
            // Debug.Log("Server receive snapshot" + snapshot.lastProcessedCommand + " data: " + snapshot.ToString());
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