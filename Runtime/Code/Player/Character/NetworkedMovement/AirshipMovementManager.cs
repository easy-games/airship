using System;
using System.Collections.Generic;
using System.Linq;
using Code.Network.Simulation;
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
        #region Inspector Settings

        // Inspector settings
        public MovementSystem movementSystem;

        // Size of the command buffer on the server
        // TODO: we might want to make this a function of command rate
        [Tooltip(
            "Amount of commands the server will store for later processing. Increasing this value will help players with a poor network connection, but will result in delayed inputs.")]
        [Range(1, 10)]
        public int maxServerCommandBuffer = 4;

        // Todo: consider making a negative number drop that many frames per tick in order to correct instead of processing them.
        [Tooltip(
            "Amount of extra commands that can be processed per tick to catch up when there is a backup of commands in the command buffer. Increasing this value will help players with a poor network connection, but will result in irregular movement for observers.")]
        [Range(1, 10)]
        public int maxServerCommandCatchup = 1;

        // Amount of times in a row the server will fill in a missing command
        public int maxServerCommandPrediction = 2;

        // The size of the interpolation buffer for observers. Higher means smoother in bad network conditions, but more delayed visuals
        public int interpolationBufferSize = 3;

        // The number of times per second the client sends it's input commands to the server
        public int clientInputRate = 30;

        // The number of times per second the server sends snapshots of client positions to observers
        public int serverSnapshotRate = 20;

        // Determines if the server has authority over the character
        public bool serverAuth = false;

        #endregion

        #region Internal State

        // Command number tracking for server and client
        private int serverLastProcessedCommandNumber = 0;
        private int clientCommandNumber = 0;

        // interpolation implementation fields.
        private State[] interpolationBuffer;
        private double lastReceivedSnapshotTime = 0;

        // Send rate tracking fieldsP 
        private double lastClientSend = 0;
        private double lastServerSend = 0;

        // Server processing for commands
        private Input lastProcessedCommand;

        // This may advance faster than last processed command if we predicted command inputs.
        // The data in lastProcessedCommand will be the command used for ticking even if the number
        // does not match.
        private int lastProcessedCommandNumber;
        private int serverPredictedCommandCount = 0;
        private SortedList<int, Input> serverCommandBuffer = new SortedList<int, Input>();

        // Client processing for input prediction
        private SortedList<double, Input> clientInputHistory = new SortedList<double, Input>();
        private SortedList<double, State> clientPredictedHistory = new SortedList<double, State>();
        private int clientLastConfirmedCommand = 0;
        
        // Fields for managing re-simulations
        // We store a max of 1 second of history
        // Note: The server does not store input history. We don't perform simulation rollbacks on the
        // server because the server is the authority (so it never rolls back) or just an observer (so it
        // doesn't process inputs).
        private History<Input> inputHistory;
        private History<State> stateHistory;
        
        #endregion

        #region Event Definitions
        
        // Networking actions to be used by subclass;
        protected Action<State> OnClientReceiveSnapshot;
        protected Action<State> OnServerReceiveSnapshot;
        protected Action<Input[]> OnServerReceiveInput;

        // Functions to be implemented by subclass that perform networking actions
        public abstract void SendClientInputToServer(Input[] input);
        public abstract void SendClientSnapshotToServer(State snapshot);
        public abstract void SendServerSnapshotToClients(State snapshot);
        
        #endregion

        #region Lifecycle Functions
        private void Awake()
        {
            AirshipSimulationManager.instance.ActivateSimulationManager();
            AirshipSimulationManager.OnPerformTick += this.OnPerformTick;
            AirshipSimulationManager.OnSetSnapshot += this.OnSetSnapshot;
            AirshipSimulationManager.OnCaptureSnapshot += this.OnCaptureSnapshot;

            this.interpolationBuffer = new State[this.interpolationBufferSize + 1];
            this.inputHistory = new((int)Math.Ceiling(1f / Time.fixedDeltaTime));
            this.stateHistory = new((int)Math.Ceiling(1f / Time.fixedDeltaTime));

            this.OnClientReceiveSnapshot += ClientReceiveSnapshot;
            this.OnServerReceiveSnapshot += ServerReceiveSnapshot;
            this.OnServerReceiveInput += ServerReceiveInputCommand;
        }

        public void Start()
        {
            // We are a shared client and server
            if (isClient && isServer)
            {
                this.movementSystem.OnSetMode(MovementMode.Authority);
            }
            // We are an authoritative client
            else if (isClient && isOwned && !serverAuth)
            {
                this.movementSystem.OnSetMode(MovementMode.Authority);
            }
            // We are a non-authoritative client
            else if (isClient && isOwned && serverAuth)
            {
                this.movementSystem.OnSetMode(MovementMode.Input);
            }
            // We are an observing client
            else if (isClient && !isOwned)
            {
                this.movementSystem.OnSetMode(MovementMode.Observer);
            }
            // We are an authoritative server
            else if (isServer && serverAuth)
            {
                this.movementSystem.OnSetMode(MovementMode.Authority);
            }
            // We are a non-authoritative server
            else if (isServer && !serverAuth)
            {
                this.movementSystem.OnSetMode(MovementMode.Observer);
            }
            else
            {
                Debug.LogWarning("Unable to determine networked movement mode. Did we miss a case? " + isServer + " " +
                                 isClient + " " + isOwned + " " + serverAuth);
            }
        }

        public void OnDestroy()
        {
            AirshipSimulationManager.OnPerformTick -= this.OnPerformTick;
            AirshipSimulationManager.OnSetSnapshot -= this.OnSetSnapshot;
            AirshipSimulationManager.OnCaptureSnapshot -= this.OnCaptureSnapshot;
        }
        
        #endregion

        #region Top Level Event Functions

        private void OnPerformTick(double time, bool replay)
        {
            // We are the client and we are authoritative. Report our state to the server.
            if (isClient && isOwned && !serverAuth)
            {
                this.AuthClientTick(time, replay);
            }

            // We are the server, and we are the authority. Expect commands to come from owning client.
            // Process those commands and distribute and authoritative answer.
            if (isServer && serverAuth)
            {
                this.AuthServerTick(time, replay);
            }

            // We are the client and the server is authoritative. Send our input commands and handle prediction
            if (isClient && isOwned && serverAuth)
            {
                this.NonAuthClientTick(time, replay);
            }

            // We are the server and we are not authoritative.
            if (isServer && !serverAuth)
            {
                this.NonAuthServerTick(time, replay);
            }
        }

        private void OnCaptureSnapshot(double time, bool replay)
        {
            // We are the client and we are authoritative. Report our state to the server.
            if (isClient && isOwned && !serverAuth)
            {
                this.AuthClientCaptureSnapshot(time, replay);
            }

            // We are the server, and we are the authority. Expect commands to come from owning client.
            // Process those commands and distribute and authoritative answer.
            if (isServer && serverAuth)
            {
                this.AuthServerCaptureSnapshot(time, replay);
            }

            // We are the server, but the client is authoritative. Simply forward to observers.
            if (isServer && !serverAuth)
            {
                this.NonAuthServerCaptureSnapshot(time, replay);
            }

            // We are the client and the server is authoritative. Send our input commands and handle prediction
            if (isClient && isOwned && serverAuth)
            {
                this.NonAuthClientCaptureSnapshot(time, replay);
            }
        }

        private void OnSetSnapshot(double time)
        {
            // We are the client and we are authoritative.
            if (isClient && isOwned && !serverAuth)
            {
                this.AuthClientSetSnapshot(time);
            }

            // We are the server, and we are the authority.
            if (isServer && serverAuth)
            {
                this.AuthServerSetSnapshot(time);
            }

            // We are the server, but the client is authoritative.
            if (isServer && !serverAuth)
            {
                this.NonAuthServerSetSnapshot(time);
            }

            // We are the client and the server is authoritative.
            if (isClient && isOwned && serverAuth)
            {
                this.NonAuthClientSetSnapshot(time);
            }
        }

        private void LateUpdate()
        {
            // Unowned clients should interpolate the observed character
            if (isClient && !isOwned)
            {
                this.Interpolate();
            }
        }

        #endregion

        #region Authoritative Server Functions

        public void AuthServerTick(double time, bool replay)
        {
            var commandsProcessed = 0;
            do
            {
                commandsProcessed++;
                if (commandsProcessed > 1)
                {
                    Debug.Log("Processing additional tick for catchup.");
                }

                // Attempt to get a new command out of the buffer.
                var commandEntry = this.serverCommandBuffer.Count > 0 ? this.serverCommandBuffer.Values[0] : null;

                // If we have a new command to process
                if (commandEntry != null)
                {
                    // Get the command to process
                    var command = this.serverCommandBuffer.Values[0];
                    // Get the expected next command number
                    var expectedNextCommandNumber = this.lastProcessedCommandNumber + 1;

                    // Check if that command is in sequence. If we have a gap of commands, we will fill it with the last
                    // processed command up to the maxServerCommandPrediction size.
                    if (this.lastProcessedCommand != null && command.commandNumber != expectedNextCommandNumber &&
                        this.serverPredictedCommandCount < this.maxServerCommandPrediction)
                    {
                        Debug.LogWarning("Missing command " + expectedNextCommandNumber +
                                         " in the command buffer. Next command was: " + command.commandNumber +
                                         ". Predicted " +
                                         (this.serverPredictedCommandCount + 1) + " command(s) so far.");
                        this.lastProcessedCommandNumber = expectedNextCommandNumber;
                        command = this.lastProcessedCommand;
                        this.serverPredictedCommandCount++;
                    }
                    // We have a valid command that is in sequence, or we reached our max fill. Remove the next
                    // valid command from the buffer and process it.
                    else
                    {
                        Debug.Log("Ticking next command in sequence: " + command.commandNumber);
                        this.serverPredictedCommandCount = 0;
                        this.serverCommandBuffer.RemoveAt(0);
                        this.lastProcessedCommand = command;
                        this.lastProcessedCommandNumber = command.commandNumber;
                    }

                    // tick commands and todo: store history if lag compensating
                    // todo: we may need to make sure that tick is always called and pass an empty command
                    this.movementSystem.Tick(command, false);
                }
                else
                {
                    Debug.LogWarning("No commands left. Last command processed: " + this.lastProcessedCommand);
                    this.movementSystem.Tick(null, false);
                }
            } while (this.serverCommandBuffer.Count >
                     Math.Ceiling((1f / this.clientInputRate / Time.fixedDeltaTime) * 1.5) &&
                     commandsProcessed < 1 + this.maxServerCommandCatchup);
            // ^ we process up to maxServerCommandCatchup commands per tick if our buffer has more than 1.5 snapshots worth of additional commands.
        }

        public void AuthServerCaptureSnapshot(double time, bool replay)
        {
            // get snapshot to send to clients
            var state = this.movementSystem.GetCurrentState(this.lastProcessedCommandNumber,
                time);
            this.stateHistory.Add(time, state);
            Debug.Log("Processing commands up to " + this.lastProcessedCommandNumber + " resulted in " + state);

            // TODO: in the future, lag compensation may need to be recorded here.
            // Depends on if we are able to use the default rigidbody lag compensation from mirror

            // Send snapshot to clients if we need to.
            if (this.lastServerSend <= NetworkTime.time - (1f / this.serverSnapshotRate))
            {
                this.lastServerSend = NetworkTime.time;
                this.SendServerSnapshotToClients(state);
            }
        }

        public void AuthServerSetSnapshot(double time)
        {
        }

        #endregion

        #region Non-Authoritative Client Functions

        public void NonAuthClientTick(double time, bool replay)
        {
            // Update our command number and process the next tick.
            clientCommandNumber++;
            var input = this.movementSystem.GetCommand(this.clientCommandNumber, time);
            var added = this.clientInputHistory.TryAdd(time, input);
            if (!added)
            {
                Debug.LogWarning(
                    "Attempted to add a command at a time that already contained a command. Using the existing command for that time instead.");
                this.clientInputHistory.TryGetValue(time, out input);
            }

            // Todo: switch to use only input history so we can get rid of clientInputHistory which does the same thing.
            // will need to add .Values to History class
            input = this.inputHistory.Add(time, input);
            this.movementSystem.Tick(input, false);


            var sendIntervalSec = (1f / this.clientInputRate);

            // Clear old inputs that haven't been confirmed by the server if they are older than 2 * send interval.
            // If they are older than that, it means we've already sent that command twice.
            while (this.clientInputHistory.Values.Count > 0 &&
                   NetworkTime.time - this.clientInputHistory.Values[0].time > sendIntervalSec * 2)
            {
                this.clientInputHistory.RemoveAt(0);
            }

            if (this.lastClientSend <= NetworkTime.time - sendIntervalSec)
            {
                this.lastClientSend = NetworkTime.time;
                this.clientInputHistory.Values.Each((c) =>
                {
                    Debug.Log(("Client sending command: " + c.commandNumber));
                });

                // We will sometimes resend unconfirmed commands. The server should ignore these if
                // it has them already.
                this.SendClientInputToServer(this.clientInputHistory.Values.ToArray());
            }
        }

        public void NonAuthClientCaptureSnapshot(double time, bool replay)
        {
            // Store the current physics state for prediction
            var state = this.movementSystem.GetCurrentState(clientCommandNumber, time);
            this.stateHistory.Add(time, state);
            Debug.Log("Processing command " + state.lastProcessedCommand + " resulted in " + state);
            this.clientPredictedHistory.TryAdd(time, state);
        }

        public void NonAuthClientSetSnapshot(double time)
        {
        }

        #endregion

        #region Non-Authoritative Server Functions

        public void NonAuthServerTick(double time, bool replay)
        {
            // Non auth server ticks have no functionality
        }

        public void NonAuthServerCaptureSnapshot(double time, bool replay)
        {
            if (this.lastServerSend <= NetworkTime.time - (1f / this.serverSnapshotRate))
            {
                // read current state
                var state = this.movementSystem.GetCurrentState(serverLastProcessedCommandNumber,
                    time);
                this.stateHistory.Add(time, state);
                this.lastServerSend = NetworkTime.time;
                this.SendServerSnapshotToClients(state);
            }
        }

        public void NonAuthServerSetSnapshot(double time)
        {
            Debug.LogWarning("Attempted to set snapshot on the server. This is not implemented.");
            // TODO: I'm not sure why we would need this, but we could very well implement it if we have
            // a server snapshot history for this component. Consider doing that in the future.
        }

        #endregion

        #region Authoritative Client Functions

        public void AuthClientTick(double time, bool replay)
        {
            // Update our command number and process the next tick
            clientCommandNumber++;
            // Read input from movement system
            var command = this.movementSystem.GetCommand(clientCommandNumber, time);
            this.inputHistory.Add(time, command);
            // Tick movement system
            this.movementSystem.Tick(command, false);
        }

        public void AuthClientCaptureSnapshot(double time, bool replay)
        {
            // Report snapshot from movement system to server.
            if (this.lastClientSend <= NetworkTime.time - (1f / this.clientInputRate))
            {
                var state = this.movementSystem.GetCurrentState(clientCommandNumber, time);
                this.stateHistory.Add(time, state);
                this.lastClientSend = NetworkTime.time;
                this.SendClientSnapshotToServer(state);
            }
        }

        public void AuthClientSetSnapshot(double time)
        {
        }

        #endregion

        #region Utility Functions

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

            // var timePerSnapshot = 1f / this.serverSnapshotRate;
            // var ticksPerSnapshot = timePerSnapshot / Time.fixedDeltaTime;
            // var tickBuffer = (this.interpolationBufferSize - 1) * ticksPerSnapshot;
            // var clientTick = AirshipSimulationManager.instance.tick - tickBuffer;

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

            // var ticksBetweenSnapshots = (nextState.tick - prevState.tick);
            // var tickProgress = (clientTick - prevState.tick) / ticksBetweenSnapshots;
            // var currentTickProgress = (Time.time - Time.fixedTime) / Time.fixedDeltaTime;
            // var timeDelta = tickProgress + (currentTickProgress / ticksBetweenSnapshots);

            this.movementSystem.Interpolate((float)timeDelta, prevState, nextState);
        }

        /**
        * Uses a server snapshot to reconcile client prediction with the actual authoritative
        * server history.
        */
        private void ReconcileInputHistory(State state)
        {
            // Clear out input history that has been processed by the server. Use <=
            while (this.clientInputHistory.Values.Count > 0 &&
                   this.clientInputHistory.Values[0].commandNumber <= state.lastProcessedCommand)
            {
                this.clientInputHistory.RemoveAt(0);
            }

            // Clear out all non-authoritative state up to our new current authoritative state. Use <=
            State clientPredictedState = null;
            while (this.clientPredictedHistory.Values.Count > 0 &&
                   this.clientPredictedHistory.Values[0].lastProcessedCommand < state.lastProcessedCommand)
            {
                clientPredictedState = this.clientPredictedHistory.Values[0];
                this.clientPredictedHistory.RemoveAt(0);
            }

            // We leave the last processed command state in the queue just in case we get two packets
            // with the same lastProcessedCommand. This could happen if the server doesn't receive commands
            // from us for a while because of network issues.
            clientPredictedState = this.clientPredictedHistory.Values.Count > 0
                ? this.clientPredictedHistory.Values[0]
                : null;

            // Check if our predicted state matches up with our new authoritative state.
            if (clientPredictedState != null && clientPredictedState.CompareWithMargin(0f, state))
            {
                // If it does, we can just return since our predictions have all been correct so far.
                return;
            }

            // TODO: unusual resims on position?

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
                this.movementSystem.Tick(command.Value, true);
                var state = this.movementSystem.GetCurrentState(command.Value.commandNumber, command.Value.time);
                Debug.Log(("Resimed command " + command + " resulted in " + state));
                this.clientPredictedHistory.Add(command.Value.time, state);
            });
        }

        #endregion

        #region Networking

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
                // TODO: we can't resim directly on network recieve. If we get back the command we haven't captured state for
                // yet, we get a null history entry which triggers a resim...
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

        private void ServerReceiveInputCommand(Input[] commands)
        {
            foreach (var command in commands)
            {
                Debug.Log("Server received command " + command.commandNumber);
                // This should only occur if the server is authoritative.
                if (!serverAuth) continue;

                if (this.serverCommandBuffer.TryGetValue(command.commandNumber, out Input existingInput))
                {
                    Debug.Log("Received duplicate command number from client. Ignoring");
                    continue;
                }

                // Reject commands if our buffer is full.
                var maxCommandBuffer = Math.Ceiling((1f / this.clientInputRate / Time.fixedDeltaTime) * 2);
                if (this.serverCommandBuffer.Count > maxCommandBuffer)
                {
                    Debug.Log("Dropping command " + command.commandNumber + " due to exceeding command buffer size.");
                    continue;
                }

                if (this.lastProcessedCommandNumber >= command.commandNumber)
                {
                    Debug.Log(("Command " + command.commandNumber + " arrived too late to be processed. Ignoring."));
                    continue;
                }

                // Queue the command for processing
                this.serverCommandBuffer.Add(command.commandNumber, command);
            }
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

            // TODO: consider interpolating on the server as well...
            // I think client authed characters are currently freezing between snapshots
            // which would make things like server hit registration be a bit behind.
            // Consider implementing interpolation in the fixed update of the server using
            // lag compensation history so that we have some reasonable in-between frames
            // Or maybe just only do that when lag compensating? It doesn't matter to observers
            // since they do their own interpolation of the snapshots forwarded to them by the server.
        }

        #endregion
    }
}